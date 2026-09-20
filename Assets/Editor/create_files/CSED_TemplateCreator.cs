/* ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * ================================================ */
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2027.EditorTools
{
    internal static class CSED_TemplateCreator
    {
        private const string Menu = "Assets/Create/template/";

        [MenuItem(Menu + "Scripts/CS_MonoBehaviour", false, -1000)]
        private static void Mono() => Begin("Mono", "CS_", ".cs");
        [MenuItem(Menu + "Scripts/CS_Class", false, -999)]
        private static void Plain() => Begin("Plain", "CS_", ".cs");
        [MenuItem(Menu + "Scripts/CSO_ScriptableObject", false, -998)]
        private static void SO() => Begin("SO", "CSO_", ".cs");
        [MenuItem(Menu + "Scripts/CSV_VolumeComponent", false, -997)]
        private static void Volume() => Begin("Volume", "CSV_", ".cs");
        [MenuItem(Menu + "Scripts/CSED_EditorWindow", false, -996)]
        private static void EditorScript() => Begin("Editor", "CSED_", ".cs");
        [MenuItem(Menu + "Scripts/CSE_Enum", false, -995)]
        private static void EnumScript() => Begin("Enum", "CSE_", ".cs");
        [MenuItem(Menu + "Materials/MT_Material", false, -980)]
        private static void Material() => Begin("Material", "MT_", ".mat");
        [MenuItem(Menu + "Materials/MTV_MaterialVariant (From Selected Material)", false, -979)]
        private static void Variant() => Begin("Variant", "MTV_", ".mat", Selection.activeObject as Material);
        [MenuItem(Menu + "Materials/MTV_MaterialVariant (From Selected Material)", true)]
        private static bool CanVariant() => Selection.activeObject is Material;
        [MenuItem(Menu + "Data/DB_ScriptableObject (Select Type)", false, -978)]
        private static void Database()
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !typeof(EditorWindow).IsAssignableFrom(t)
                    && !typeof(UnityEditor.Editor).IsAssignableFrom(t) && !typeof(EndNameEditAction).IsAssignableFrom(t)
                    && (t.Name.StartsWith("CSO_", StringComparison.Ordinal) || t.GetCustomAttribute<CreateAssetMenuAttribute>() != null))
                .OrderBy(t => t.FullName).ToArray();
            foreach (var type in types)
            {
                var captured = type;
                menu.AddItem(new GUIContent(type.FullName.Replace('.', '/')), false,
                    () => Begin("Database", "DB_", ".asset", null, captured.AssemblyQualifiedName));
            }
            if (types.Length == 0) menu.AddDisabledItem(new GUIContent("Create and compile a CSO_ script first"));
            menu.ShowAsContext();
        }
        [MenuItem(Menu + "Shaders/HLSL/VS_Vertex", false, -970)]
        private static void Vertex() => Begin("Hlsl", "VS_", ".hlsl");
        [MenuItem(Menu + "Shaders/HLSL/PS_Pixel", false, -969)]
        private static void Pixel() => Begin("Hlsl", "PS_", ".hlsl");
        [MenuItem(Menu + "Shaders/HLSL/GS_Geometry", false, -968)]
        private static void Geometry() => Begin("Hlsl", "GS_", ".hlsl");
        [MenuItem(Menu + "Shaders/HLSL/HS_Hull", false, -967)]
        private static void Hull() => Begin("Hlsl", "HS_", ".hlsl");
        [MenuItem(Menu + "Shaders/HLSL/DS_Domain", false, -966)]
        private static void Domain() => Begin("Hlsl", "DS_", ".hlsl");
        [MenuItem(Menu + "Shaders/HLSL/CS_Compute", false, -965)]
        private static void Compute() => Begin("Hlsl", "CS_", ".hlsl");
        [MenuItem(Menu + "Shaders/SH_Shader", false, -960)]
        private static void ShaderFile() => Begin("Shader", "SH_", ".shader");
        [MenuItem(Menu + "Shaders/SHG_ShaderGraph", false, -959)]
        private static void Graph()
        {
            // パッケージ標準の作成処理を利用し、Graphの内部形式を独自生成しない。
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.ShaderGraph.CreateShaderGraph"))
                .FirstOrDefault(t => t != null);
            var method = type?.GetMethod("CreateFromTemplate", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Action<string>), typeof(string), typeof(string) }, null);
            if (method == null)
            {
                EditorUtility.DisplayDialog("template", "対応するShader Graph作成機能が見つかりません。", "OK");
                return;
            }
            method.Invoke(null, new object[] { null, null, "SHG_" });
        }
        [MenuItem(Menu + "Objects/Prefab", false, -950)]
        private static void Prefab() => Begin("Prefab", "", ".prefab", Selection.activeGameObject);
        [MenuItem(Menu + "Objects/Scene", false, -949)]
        private static void Scene() => Begin("Scene", "", ".unity");

        internal static void Begin(string kind, string prefix, string extension, UnityEngine.Object source = null, string type = null)
        {
            var action = ScriptableObject.CreateInstance<CSED_TemplateNameAction>();
            action.Kind = kind;
            action.Prefix = prefix;
            action.Extension = extension;
            action.Source = source;
            action.TypeName = type;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action,
                prefix.Length > 0 ? prefix + extension : "New" + kind + extension, null, null);
        }

        internal static string NormalizeName(string name, string prefix)
        {
            if (prefix.Length > 0 && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(prefix.Length);
            var parts = Regex.Split(name, prefix.Length == 0 ? @"[^\p{L}\p{Nd}]+" : @"[^\p{L}\p{Nd}_]+").Where(p => p.Length > 0);
            name = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            if (string.IsNullOrEmpty(name)) name = "NewFile";
            if (char.IsDigit(name[0])) name = "Object" + name;
            return prefix + name;
        }

        internal static string Header()
        {
            string author = "未設定";
            try
            {
                // Gitプロセスを起動せず、現在のHEADだけを読む（worktreeにも対応）。
                var root = new DirectoryInfo(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
                while (root != null)
                {
                    string git = Path.Combine(root.FullName, ".git");
                    if (File.Exists(git))
                    {
                        string pointer = File.ReadAllText(git).Trim();
                        if (!pointer.StartsWith("gitdir: ", StringComparison.Ordinal)) break;
                        git = Path.GetFullPath(Path.Combine(root.FullName, pointer.Substring(8)));
                    }
                    if (Directory.Exists(git))
                    {
                        string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                        const string reference = "ref: refs/heads/";
                        if (head.StartsWith(reference, StringComparison.Ordinal))
                        {
                            string branch = head.Substring(reference.Length);
                            int slash = branch.IndexOf('/');
                            if (slash > 0) author = branch.Substring(0, slash);
                        }
                        break;
                    }
                    root = root.Parent;
                }
            }
            catch (Exception e) { Debug.LogWarning("template: Git制作者を取得できませんでした。" + e.Message); }
            author = author.Replace("*/", "").Replace("\r", "").Replace("\n", "");
            return "/* ================================================\n * \n * ================================================\n * 制作者：" + author
                + "\n * ------------------------------------------------\n * " + DateTime.Now.ToString("yyyy-MM-dd")
                + " | 初回作成\n * ================================================ */\n\n";
        }

        internal static string Body(string kind, string name)
        {
            string body = BodyContent(kind, name);
            if (kind == "Hlsl" || kind == "Shader") return Summary + body;
            // 属性がある場合も、summaryは属性より前に置く。
            return new Regex(@"(?m)^(?=\[|public (?:class|enum) )").Replace(body,
                "/// <summary>\n/// \n/// </summary>\n", 1);
        }

        internal const string Summary = "/// <summary>\n/// \n/// </summary>\n";

        internal static bool IsShaderSource(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".shader":
                case ".hlsl":
                case ".compute":
                case ".raytrace":
                case ".cginc": return true;
                default: return false;
            }
        }

        private static string BodyContent(string kind, string name)
        {
            switch (kind)
            {
                case "Mono": return "using UnityEngine;\n\npublic class " + name + " : MonoBehaviour\n{\n}\n";
                case "Plain": return "public class " + name + "\n{\n}\n";
                case "SO": return "using UnityEngine;\n\n[CreateAssetMenu(fileName = \"DB_" + name.Substring(name.IndexOf('_') + 1) + "\", menuName = \"Data/" + name + "\")]\npublic class " + name + " : ScriptableObject\n{\n}\n";
                case "PlayableBehaviour": return "using UnityEngine.Playables;\n\npublic class " + name + " : PlayableBehaviour\n{\n}\n";
                case "PlayableAsset": return "using UnityEngine;\nusing UnityEngine.Playables;\n\npublic class " + name + " : PlayableAsset\n{\n    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)\n    {\n        return Playable.Create(graph);\n    }\n}\n";
                case "ScenePipeline": return "#if UNITY_EDITOR\nusing UnityEditor.SceneTemplate;\n\npublic class " + name + " : SceneTemplatePipelineAdapter\n{\n}\n#endif\n";
                case "Volume": return "using System;\nusing UnityEngine.Rendering;\n\n[Serializable]\n[VolumeComponentMenu(\"Custom/" + name + "\")]\npublic class " + name + " : VolumeComponent\n{\n}\n";
                case "Editor": return "#if UNITY_EDITOR\nusing UnityEditor;\nusing UnityEngine;\n\npublic class " + name + " : EditorWindow\n{\n    [MenuItem(\"Tools/" + name + "\")]\n    private static void Open()\n    {\n        GetWindow<" + name + ">(\"" + name + "\");\n    }\n\n    private void OnGUI()\n    {\n    }\n}\n#endif\n";
                case "Enum": return "public enum " + name + "\n{\n    None = 0,\n}\n";
                case "Hlsl": return "#ifndef " + name.ToUpperInvariant() + "_INCLUDED\n#define " + name.ToUpperInvariant() + "_INCLUDED\n\n#endif\n";
                case "Shader": return "Shader \"Custom/" + name + "\"\n{\n    Properties\n    {\n        _Color (\"Color\", Color) = (1, 1, 1, 1)\n    }\n    SubShader\n    {\n        Tags { \"RenderPipeline\" = \"HDRenderPipeline\" \"RenderType\" = \"Opaque\" }\n        Pass\n        {\n            Tags { \"LightMode\" = \"ForwardOnly\" }\n            HLSLPROGRAM\n            #pragma vertex Vert\n            #pragma fragment Frag\n            #include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl\"\n            #include \"Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl\"\n            float4 _Color;\n            struct Attributes { float3 positionOS : POSITION; };\n            struct Varyings { float4 positionCS : SV_POSITION; };\n            Varyings Vert(Attributes input)\n            {\n                Varyings output;\n                output.positionCS = TransformObjectToHClip(input.positionOS);\n                return output;\n            }\n            float4 Frag(Varyings input) : SV_Target { return _Color; }\n            ENDHLSL\n        }\n    }\n}\n";
                default: throw new ArgumentException("未対応のテンプレート: " + kind);
            }
        }
    }

}
