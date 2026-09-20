using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2027.EditorTools
{
    // 生成されるIDE用プロジェクトだけに登録し、Unityのコンパイラーには渡さない。
    [InitializeOnLoad]
    internal sealed class CSED_SurveillanceProject : AssetPostprocessor
    {
        private const string _analyzerPath = "Assets/Editor/script_surveillance/MS2027.ScriptSurveillance.dll";

        static CSED_SurveillanceProject()
        {
            EditorApplication.delayCall += UpdateExistingProjects;
        }

        [MenuItem("Tools/Script Surveillance/コードエディターの解析を設定")]
        private static void Configure()
        {
            UpdateExistingProjects();
            Debug.Log("Script Surveillance: 解析を設定しました。コードエディターでUnityのソリューションを開き直してください。");
        }

        private static void UpdateExistingProjects()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            if (!File.Exists(Path.Combine(root, _analyzerPath))) return;
            foreach (string path in Directory.GetFiles(root, "*.csproj"))
            {
                try
                {
                    string content = File.ReadAllText(path);
                    string updated = OnGeneratedCSProject(path, content);
                    if (updated != content) File.WriteAllText(path, updated);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                    || exception is System.Xml.XmlException)
                {
                    Debug.LogWarning("Script Surveillance: " + Path.GetFileName(path) + " の設定に失敗: " + exception.Message);
                }
            }
        }

        private static string OnGeneratedCSProject(string path, string content)
        {
            var document = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            XElement root = document.Root;
            if (root == null) return content;
            XNamespace ns = root.Name.Namespace;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            // Packagesのみのプロジェクトは対象外。独自asmdefのプロジェクトにも対応。
            bool hasAssets = root.Descendants(ns + "Compile").Any(element =>
            {
                string include = ((string)element.Attribute("Include") ?? "").Replace('\\', '/');
                return include.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || include.StartsWith(projectRoot.Replace('\\', '/') + "/Assets/", StringComparison.OrdinalIgnoreCase);
            });
            if (!hasAssets) return content;
            // Unityが既定で抑制するCS0169をIDE側だけ復元し、未使用privateフィールドも検出する。
            bool changed = false;
            foreach (XElement noWarn in root.Descendants(ns + "NoWarn"))
            {
                string[] codes = noWarn.Value.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] kept = codes.Where(code => code != "0169" && code != "169"
                    && !code.Equals("CS0169", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (kept.Length == codes.Length) continue;
                noWarn.Value = string.Join(";", kept);
                changed = true;
            }
            string analyzer = Path.Combine(projectRoot, _analyzerPath).Replace('\\', '/');
            if (root.Descendants(ns + "Analyzer").Any(element =>
                string.Equals(((string)element.Attribute("Include") ?? "").Replace('\\', '/'), analyzer,
                    StringComparison.OrdinalIgnoreCase)))
                return changed ? document.ToString(SaveOptions.DisableFormatting) : content;
            root.Add(new XElement(ns + "ItemGroup", new XElement(ns + "Analyzer", new XAttribute("Include", analyzer))));
            root.Add(new XElement(ns + "PropertyGroup",
                new XElement(ns + "RunAnalyzersDuringLiveAnalysis", "true")));
            return document.ToString(SaveOptions.DisableFormatting);
        }
    }
}
