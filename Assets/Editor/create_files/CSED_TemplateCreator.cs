/* ================================================
 * 命名規則付きの作成メニューと、名前・ヘッダー・コード本文の生成を提供する。
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-20 | 初回作成
 * 2026-09-23 | 処理説明と日本語コメントを追加
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
    /// <summary>
    /// 命名規則付きの作成メニューと、名前・ヘッダー・コード本文の生成を提供する。
    /// </summary>
    internal static class CSED_TemplateCreator
    {
        private const string Menu = "Assets/Create/template/";

        /// <summary>
        /// MonoBehaviourを継承するCS_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CS_MonoBehaviour", false, -1000)]
        private static void Mono() => Begin("Mono", "CS_", ".cs");
        /// <summary>
        /// 通常のC#クラスを作るCS_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CS_Class", false, -999)]
        private static void Plain() => Begin("Plain", "CS_", ".cs");
        /// <summary>
        /// ScriptableObjectを継承するCSO_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CSO_ScriptableObject", false, -998)]
        private static void SO() => Begin("SO", "CSO_", ".cs");
        /// <summary>
        /// VolumeComponentを継承するCSV_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CSV_VolumeComponent", false, -997)]
        private static void Volume() => Begin("Volume", "CSV_", ".cs");
        /// <summary>
        /// EditorWindowを継承するCSED_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CSED_EditorWindow", false, -996)]
        private static void EditorScript() => Begin("Editor", "CSED_", ".cs");
        /// <summary>
        /// 列挙型を定義するCSE_スクリプトの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Scripts/CSE_Enum", false, -995)]
        private static void EnumScript() => Begin("Enum", "CSE_", ".cs");
        /// <summary>
        /// HDRP/Litを使うMT_Materialの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Materials/MT_Material", false, -980)]
        private static void Material() => Begin("Material", "MT_", ".mat");
        /// <summary>
        /// 選択中のMaterialを親にするMTV_Material Variantの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Materials/MTV_MaterialVariant (From Selected Material)", false, -979)]
        private static void Variant() => Begin("Variant", "MTV_", ".mat", Selection.activeObject as Material);
        /// <summary>
        /// Material選択時だけVariant作成メニューを有効にする。
        /// </summary>
        [MenuItem(Menu + "Materials/MTV_MaterialVariant (From Selected Material)", true)]
        private static bool CanVariant() => Selection.activeObject is Material;
        /// <summary>
        /// 作成可能なScriptableObject型を一覧表示し、選ばれた型のDB_アセット作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Data/DB_ScriptableObject (Select Type)", false, -978)]
        private static void Database()
        {
            var menu = new GenericMenu();
            // インスタンス化できるデータ型に絞り、Editor用の作業型を候補から外す。
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
        /// <summary>
        /// 頂点シェーダー用のVS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/VS_Vertex", false, -970)]
        private static void Vertex() => Begin("Hlsl", "VS_", ".hlsl");
        /// <summary>
        /// ピクセルシェーダー用のPS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/PS_Pixel", false, -969)]
        private static void Pixel() => Begin("Hlsl", "PS_", ".hlsl");
        /// <summary>
        /// ジオメトリシェーダー用のGS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/GS_Geometry", false, -968)]
        private static void Geometry() => Begin("Hlsl", "GS_", ".hlsl");
        /// <summary>
        /// ハルシェーダー用のHS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/HS_Hull", false, -967)]
        private static void Hull() => Begin("Hlsl", "HS_", ".hlsl");
        /// <summary>
        /// ドメインシェーダー用のDS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/DS_Domain", false, -966)]
        private static void Domain() => Begin("Hlsl", "DS_", ".hlsl");
        /// <summary>
        /// コンピュート処理用のCS_HLSLファイル作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/HLSL/CS_Compute", false, -965)]
        private static void Compute() => Begin("Hlsl", "CS_", ".hlsl");
        /// <summary>
        /// HDRP向けの最小構成を持つSH_Shaderの作成を開始する。
        /// </summary>
        [MenuItem(Menu + "Shaders/SH_Shader", false, -960)]
        private static void ShaderFile() => Begin("Shader", "SH_", ".shader");
        /// <summary>
        /// Shader Graphパッケージの作成機能を呼び出す。対応する内部APIがなければ案内を表示する。
        /// </summary>
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
        /// <summary>
        /// 選択GameObjectを元にPrefabを作る。未選択なら空のGameObjectから作成する。
        /// </summary>
        [MenuItem(Menu + "Objects/Prefab", false, -950)]
        private static void Prefab() => Begin("Prefab", "", ".prefab", Selection.activeGameObject);
        /// <summary>
        /// 空のSceneアセットの名前入力を開始する。
        /// </summary>
        [MenuItem(Menu + "Objects/Scene", false, -949)]
        private static void Scene() => Begin("Scene", "", ".unity");

        /// <summary>
        /// 作成条件を名前確定コールバックへ渡し、Projectウィンドウで名前入力を始める。
        /// </summary>
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

        /// <summary>
        /// 既存の接頭辞を外して名前を整形し、接頭辞を付け直す。空名と数字始まりには代替文字を補う。
        /// </summary>
        internal static string NormalizeName(string name, string prefix)
        {
            if (prefix.Length > 0 && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(prefix.Length);
            var parts = Regex.Split(name, prefix.Length == 0 ? @"[^\p{L}\p{Nd}]+" : @"[^\p{L}\p{Nd}_]+").Where(p => p.Length > 0);
            name = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            if (string.IsNullOrEmpty(name)) name = "NewFile";
            if (char.IsDigit(name[0])) name = "Object" + name;
            return prefix + name;
        }

        /// <summary>
        /// ブランチ名の最初のスラッシュより前を制作者とし、当日の日付を含むヘッダーを生成する。取得できない場合は未設定とする。
        /// </summary>
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
            // ブランチ由来の文字でコメントが閉じたり、余分な行が挿入されたりしないようにする。
            author = author.Replace("*/", "").Replace("\r", "").Replace("\n", "");
            return "/* ================================================\n * \n * ================================================\n * 制作者：" + author
                + "\n * ------------------------------------------------\n * " + DateTime.Now.ToString("yyyy-MM-dd")
                + " | 初回作成\n * ================================================ */\n\n";
        }

        /// <summary>
        /// 種別に応じたコード本文へ、複数行のsummary記入欄を追加する。
        /// </summary>
        internal static string Body(string kind, string name)
        {
            string body = BodyContent(kind, name);
            if (kind == "Hlsl" || kind == "Shader") return Summary + body;
            // 属性がある場合も、summaryは属性より前に置く。
            return new Regex(@"(?m)^(?=\[|public (?:class|enum) )").Replace(body,
                "/// <summary>\n/// \n/// </summary>\n", 1);
        }

        internal const string Summary = "/// <summary>\n/// \n/// </summary>\n";

        /// <summary>
        /// ヘッダーと説明欄を追加する対象が、シェーダー系ソースの拡張子か判定する。
        /// </summary>
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

        /// <summary>
        /// 指定された種別のコード本文を生成する。未対応の種別は例外にする。
        /// </summary>
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
