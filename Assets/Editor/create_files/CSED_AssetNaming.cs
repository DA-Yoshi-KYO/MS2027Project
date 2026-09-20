using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace MS2027.EditorTools
{
    // 作成前の名前入力だけを扱う。標準メニューや既存ファイルには変更を加えない。
    [InitializeOnLoad]
    internal static class CSED_AssetNaming
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type Browser = typeof(ProjectWindowUtil).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly FieldInfo LastBrowser = Browser?.GetField("s_LastInteractedProjectBrowser", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static bool checkedProjectGUI;

        static CSED_AssetNaming()
        {
            EditorApplication.update += Update;
            EditorApplication.projectWindowItemOnGUI += BeforeNameField;
        }

        private static void Update()
        {
            checkedProjectGUI = false;
            PrepareActiveCreation();
        }

        private static void BeforeNameField(string guid, Rect rect)
        {
            // 名前欄を描画する前に一度だけ準備する。表示アイテムごとの監視は行わない。
            if (checkedProjectGUI) return;
            checkedProjectGUI = true;
            PrepareActiveCreation();
        }

        private static void PrepareActiveCreation()
        {
            // editingTextFieldがtrueになるのを待つと、名前欄の初期表示に間に合わない。
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var browser = LastBrowser?.GetValue(null) as EditorWindow;
            if (browser == null) return;
            try
            {
                Prepare(browser, Read(browser, "m_ListAreaState"));
                Prepare(browser, Read(browser, "m_AssetTreeState"));
                Prepare(browser, Read(browser, "m_FolderTreeState"));
            }
            catch (Exception e)
            {
                EditorApplication.update -= Update;
                EditorApplication.projectWindowItemOnGUI -= BeforeNameField;
                Debug.LogError("作成前のアセット命名を設定できません: " + e.Message);
            }
        }

        private static void Prepare(EditorWindow browser, object state)
        {
            object utility = Read(state, "m_CreateAssetUtility") ?? Read(state, "createAssetUtility");
            var action = Read(utility, "endAction") as EndNameEditAction;
            if (action == null || action is CSED_AssetNameAction || action is CSED_TemplateNameAction) return;
            string extension = Read(utility, "extension") as string;
            object overlay = Read(state, "m_RenameOverlay") ?? Read(state, "renameOverlay");
            if (overlay == null || !(bool)overlay.GetType().GetMethod("IsRenaming", Members).Invoke(overlay, null)) return;
            var nameProperty = overlay.GetType().GetProperty("name", Members);
            var actionField = Field(utility.GetType(), "m_EndAction");
            if (nameProperty?.GetSetMethod(true) == null || actionField == null) return;

            int id = (int)Read(utility, "instanceID");
            var asset = EditorUtility.EntityIdToObject(id);
            string oldName = (string)nameProperty.GetValue(overlay);
            string prefix = Prefix(extension, oldName, asset, action.GetType().Name);
            string newName = prefix.Length > 0 ? prefix : CSED_TemplateCreator.NormalizeName(oldName, "");
            var wrapper = ScriptableObject.CreateInstance<CSED_AssetNameAction>();
            wrapper.Original = action;
            wrapper.Prefix = prefix;
            actionField.SetValue(utility, wrapper);
            nameProperty.SetValue(overlay, newName);
            Field(overlay.GetType(), "m_OriginalName")?.SetValue(overlay, newName);
            // Unityが保持するテキスト編集バッファも初期表示に合わせる。
            const BindingFlags staticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            object textEditor = typeof(EditorGUI).GetField("s_RecycledEditor", staticMembers)?.GetValue(null)
                ?? typeof(EditorGUI).GetProperty("s_RecycledEditor", staticMembers)?.GetValue(null);
            if (textEditor is TextEditor editor)
            {
                editor.text = newName;
                editor.SelectAll();
            }
            browser.Repaint();
        }

        internal static string Prefix(string extension, string name, UnityEngine.Object asset, string actionName)
        {
            switch ((extension ?? "").ToLowerInvariant())
            {
                case ".cs": return "CS_";
                case ".mat": return asset is Material material && material.parent != null || actionName.IndexOf("Variant", StringComparison.OrdinalIgnoreCase) >= 0 ? "MTV_" : "MT_";
                case ".shader": return "SH_";
                case ".shadergraph": return "SHG_";
                case ".compute": return "CS_";
                case ".raytrace": return "RS_";
                case ".hlsl":
                    foreach (string stage in new[] { "VS_", "PS_", "GS_", "HS_", "DS_", "CS_", "RS_" })
                        if (name.StartsWith(stage, StringComparison.OrdinalIgnoreCase)) return stage;
                    return "S_";
                default: return asset is ScriptableObject ? "DB_" : "";
            }
        }

        private static object Read(object value, string name)
        {
            if (value == null) return null;
            return Field(value.GetType(), name)?.GetValue(value) ?? value.GetType().GetProperty(name, Members)?.GetValue(value);
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Members | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }
    }
}
