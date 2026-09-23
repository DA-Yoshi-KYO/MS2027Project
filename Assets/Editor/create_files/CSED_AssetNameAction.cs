/* ================================================
 * 標準アセット作成を包み、入力名の重複回避とコードヘッダーの追加を行う。
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-23 | 処理説明と日本語コメントを追加
 * ================================================ */
using System.IO;
using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace MS2027.EditorTools
{
    /// <summary>
    /// 標準アセット作成を包み、入力名の重複回避とコードヘッダーの追加を行う。
    /// </summary>
    internal sealed class CSED_AssetNameAction : EndNameEditAction
    {
        // 元の作成処理を保持し、保存とキャンセル通知を委譲する。
        public EndNameEditAction Original;
        public string Prefix;

        /// <summary>
        /// 入力名を保持して重複を回避し、コードにはヘッダーを追加して元の作成処理へ渡す。最後に一時ファイルとコールバックを片付ける。
        /// </summary>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string extension = Path.GetExtension(pathName);
            // 接頭辞は入力開始時の候補のみ。確定時は入力された名前をそのまま使う。
            string name = Path.GetFileNameWithoutExtension(pathName);
            string path = Path.GetDirectoryName(pathName).Replace('\\', '/') + "/" + name + extension;
            // 作成前に確定するので、作成後のMoveAssetや追加のインポートは不要。
            for (int i = 1; File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"); i++)
                path = Path.GetDirectoryName(pathName).Replace('\\', '/') + "/" + name + i + extension;
            string temporaryTemplate = null;
            try
            {
                if (CSED_TemplateCreator.IsShaderSource(extension) || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    // 標準テンプレートは変更せず、コメント付きの一時コピーを最初の作成に渡す。
                    if (Original.GetType().Name == "DoCreateScriptAsset" && File.Exists(resourceFile))
                    {
                        temporaryTemplate = Path.Combine(Path.GetTempPath(), "MS2027Shader_" + Guid.NewGuid().ToString("N") + ".txt");
                        File.WriteAllText(temporaryTemplate, AddDocumentation(File.ReadAllText(resourceFile), extension), new UTF8Encoding(true));
                        resourceFile = temporaryTemplate;
                    }
                    else
                    {
                        // ファイルを使わない作成処理では、内部で保持している本文へ直接追加する。
                        var content = Original.GetType().GetField("filecontent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (content?.FieldType == typeof(string))
                            content.SetValue(Original, AddDocumentation((string)content.GetValue(Original), extension));
                        else
                            Debug.LogWarning("このコード作成機能にはヘッダーを挿入できません: " + Original.GetType().FullName);
                    }
                }
                Original.Action(instanceId, path, resourceFile);
            }
            finally
            {
                // 元の処理が例外になった場合も、一時テンプレートを残さない。
                if (temporaryTemplate != null && File.Exists(temporaryTemplate)) File.Delete(temporaryTemplate);
                if (Original != null) Original.CleanUp();
            }
        }

        /// <summary>
        /// 制作者・作成日のヘッダーを追加する。C#にsummaryがなければ、最初の属性または型宣言の前へ挿入する。
        /// </summary>
        private static string AddDocumentation(string source, string extension)
        {
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (!source.Contains("/// <summary>"))
                    source = new Regex(@"(?m)^(?=\s*(?:\[|public (?:class|enum|struct|interface) ))")
                        .Replace(source, CSED_TemplateCreator.Summary, 1);
                return CSED_TemplateCreator.Header() + source;
            }
            return CSED_TemplateCreator.Header() + CSED_TemplateCreator.Summary + source;
        }

        /// <summary>
        /// 名前入力の中止を元の処理へ通知し、元コールバックの終了処理を実行する。
        /// </summary>
        public override void Cancelled(int instanceId, string pathName, string resourceFile)
        {
            try { if (Original != null) Original.Cancelled(instanceId, pathName, resourceFile); }
            finally { if (Original != null) Original.CleanUp(); }
        }
    }
}
