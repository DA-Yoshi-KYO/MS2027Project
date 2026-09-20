/* ================================================
 * Script SurveillanceのAnalyzerをIDE用プロジェクトへ登録するクラス
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-20 | 既存csprojへのAnalyzer自動登録に対応
 *            | CS0169のIDE側警告抑制を解除
 * ================================================ */

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2027.EditorTools
{
    // 生成されるIDE用プロジェクトだけにAnalyzerを登録し、Unityのコンパイラーには渡さない。
    [InitializeOnLoad]
    internal sealed class CSED_SurveillanceProject : AssetPostprocessor
    {
        private const string _analyzerPath =
            "Assets/Editor/script_surveillance/MS2027.ScriptSurveillance.dll";

        /// <summary>
        /// Unity起動・再コンパイル時に、既存のcsprojへAnalyzer設定を反映する。
        /// </summary>
        static CSED_SurveillanceProject()
        {
            EditorApplication.delayCall += UpdateExistingProjects;
        }

        /// <summary>
        /// Script Surveillanceの解析設定を手動で再適用する。
        /// </summary>
        [MenuItem("Tools/Script Surveillance/コードエディターの解析を設定")]
        private static void Configure()
        {
            UpdateExistingProjects();

            Debug.Log(
                "Script Surveillance: 解析を設定しました。コードエディターでUnityのソリューションを開き直してください。"
            );
        }

        /// <summary>
        /// プロジェクト直下の既存csprojを確認し、必要なAnalyzer設定を書き込む。
        /// </summary>
        private static void UpdateExistingProjects()
        {
            string root = Path.GetDirectoryName(Application.dataPath);

            // Analyzer本体が存在しない場合は設定を変更しない。
            if (!File.Exists(Path.Combine(root, _analyzerPath)))
                return;

            foreach (string path in Directory.GetFiles(root, "*.csproj"))
            {
                try
                {
                    string content = File.ReadAllText(path);
                    string updated = OnGeneratedCSProject(path, content);

                    // 内容が変化した場合だけ書き戻す。
                    if (updated != content)
                        File.WriteAllText(path, updated);
                }
                catch (Exception exception)
                    when (
                        exception is IOException ||
                        exception is UnauthorizedAccessException ||
                        exception is System.Xml.XmlException
                    )
                {
                    Debug.LogWarning(
                        "Script Surveillance: " +
                        Path.GetFileName(path) +
                        " の設定に失敗: " +
                        exception.Message
                    );
                }
            }
        }

        /// <summary>
        /// Unityが生成したcsprojへScript Surveillance用のAnalyzer設定を追加する。
        /// </summary>
        private static string OnGeneratedCSProject(string path, string content)
        {
            var document =
                XDocument.Parse(content, LoadOptions.PreserveWhitespace);

            XElement root = document.Root;
            if (root == null)
                return content;

            XNamespace ns = root.Name.Namespace;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // Packagesのみのプロジェクトは対象外。独自asmdefのプロジェクトにも対応する。
            bool hasAssets = root
                .Descendants(ns + "Compile")
                .Any(element =>
                {
                    string include =
                        ((string)element.Attribute("Include") ?? "")
                        .Replace('\\', '/');

                    return include.StartsWith(
                               "Assets/",
                               StringComparison.OrdinalIgnoreCase
                           )
                           ||
                           include.StartsWith(
                               projectRoot.Replace('\\', '/') + "/Assets/",
                               StringComparison.OrdinalIgnoreCase
                           );
                });

            if (!hasAssets)
                return content;

            // Unityが抑制しているCS0169をIDE側だけ復元し、未使用privateフィールドも検出する。
            bool changed = false;

            foreach (XElement noWarn in root.Descendants(ns + "NoWarn"))
            {
                string[] codes =
                    noWarn.Value.Split(
                        new[] { ';', ',', ' ' },
                        StringSplitOptions.RemoveEmptyEntries
                    );

                string[] kept =
                    codes
                        .Where(code =>
                            code != "0169" &&
                            code != "169" &&
                            !code.Equals(
                                "CS0169",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .ToArray();

                if (kept.Length == codes.Length)
                    continue;

                noWarn.Value = string.Join(";", kept);
                changed = true;
            }

            string analyzer =
                Path.Combine(projectRoot, _analyzerPath)
                    .Replace('\\', '/');

            // 既に登録済みの場合はAnalyzerを二重追加しない。
            if (
                root.Descendants(ns + "Analyzer")
                    .Any(element =>
                        string.Equals(
                            ((string)element.Attribute("Include") ?? "")
                                .Replace('\\', '/'),
                            analyzer,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
            )
            {
                return changed
                    ? document.ToString(SaveOptions.DisableFormatting)
                    : content;
            }

            // IDE側のRoslyn AnalyzerとしてScript Surveillanceを登録する。
            root.Add(
                new XElement(
                    ns + "ItemGroup",
                    new XElement(
                        ns + "Analyzer",
                        new XAttribute("Include", analyzer)
                    )
                )
            );

            // Visual Studio上でリアルタイム解析を有効にする。
            root.Add(
                new XElement(
                    ns + "PropertyGroup",
                    new XElement(
                        ns + "RunAnalyzersDuringLiveAnalysis",
                        "true"
                    )
                )
            );

            return document.ToString(SaveOptions.DisableFormatting);
        }
    }
}