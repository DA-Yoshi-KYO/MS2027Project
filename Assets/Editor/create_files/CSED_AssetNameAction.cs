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
    internal sealed class CSED_AssetNameAction : EndNameEditAction
    {
        public EndNameEditAction Original;
        public string Prefix;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string extension = Path.GetExtension(pathName);
            string name = CSED_TemplateCreator.NormalizeName(Path.GetFileNameWithoutExtension(pathName), Prefix);
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
                if (temporaryTemplate != null && File.Exists(temporaryTemplate)) File.Delete(temporaryTemplate);
                if (Original != null) Original.CleanUp();
            }
        }

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

        public override void Cancelled(int instanceId, string pathName, string resourceFile)
        {
            try { if (Original != null) Original.Cancelled(instanceId, pathName, resourceFile); }
            finally { if (Original != null) Original.CleanUp(); }
        }
    }
}
