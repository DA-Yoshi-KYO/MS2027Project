/* ================================================
 * シェーダーを参照するMaterialのプロパティを初期値へ戻す。
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-10-03 | 初回作成・全項目と指定項目のリセットに対応
 * ================================================ */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Projectの右クリックメニューからShader Reloadを実行する。
/// </summary>
internal static class CSED_ShaderReload
{
    private const string Menu = "Assets/sheder_reload/";

    /// <summary>参照Materialの全プロパティをシェーダーの初期値へ戻す。</summary>
    [MenuItem(Menu + "defaultリセット", false, -2000)]
    private static void ResetDefault()
    {
        List<Shader> shaders = FindShaders();
        if (shaders.Count == 0) { ShowMissingShader(); return; }
        Reset(shaders.ToDictionary(shader => shader,
            shader => Enumerable.Range(0, shader.GetPropertyCount())
                .Select(shader.GetPropertyName).ToArray()));
    }

    /// <summary>公開プロパティを選択するウィンドウを開く。</summary>
    [MenuItem(Menu + "指定リセット", false, -1999)]
    private static void ResetSelected()
    {
        List<Shader> shaders = FindShaders();
        if (shaders.Count == 0) { ShowMissingShader(); return; }
        CSED_ShaderReloadWindow.Open(shaders);
    }

    /// <summary>Shader・Shader Graph・HLSL選択時だけメニューを有効にする。</summary>
    [MenuItem(Menu + "defaultリセット", true)]
    [MenuItem(Menu + "指定リセット", true)]
    private static bool CanReset()
    {
        string extension = Path.GetExtension(AssetDatabase.GetAssetPath(Selection.activeObject));
        return Selection.activeObject is Shader ||
            extension.Equals(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".hlsl", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 選択したアセットのShaderを取得する。HLSLは依存関係から参照元を検索する。
    /// </summary>
    private static List<Shader> FindShaders()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        var result = new List<Shader>();
        if (!Path.GetExtension(selectedPath).Equals(".hlsl", StringComparison.OrdinalIgnoreCase))
        {
            AddShaders(selectedPath, result);
            return result;
        }

        // HLSL単体にはMaterialの公開項目がないため、Shader側の定義を使う。
        var paths = new HashSet<string>(AssetDatabase.FindAssets("t:Shader")
            .Select(AssetDatabase.GUIDToAssetPath));
        foreach (string guid in AssetDatabase.FindAssets("t:ShaderGraph"))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (string path in paths)
        {
            if (AssetDatabase.GetDependencies(path, true).Contains(selectedPath))
                AddShaders(path, result);
        }
        return result;
    }

    /// <summary>Shader Graph内のサブアセットも含め、Shaderを重複なく取得する。</summary>
    private static void AddShaders(string path, List<Shader> result)
    {
        foreach (Shader shader in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Shader>())
            if (!result.Contains(shader)) result.Add(shader);
    }

    /// <summary>参照元が取得できない場合は、何も変更せず理由を表示する。</summary>
    private static void ShowMissingShader()
    {
        EditorUtility.DisplayDialog("sheder_reload",
            "参照元のShaderが見つかりませんでした。HLSLの場合は、そのファイルを使用する.shaderまたはShader Graphから実行してください。", "OK");
    }

    /// <summary>
    /// Assets内の参照Materialへ指定プロパティをコピーし、Undoと保存を行う。
    /// </summary>
    internal static void Reset(Dictionary<Shader, string[]> targets)
    {
        if (!targets.Any(pair => pair.Value.Length > 0)) return;
        var materials = new List<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // モデル内の埋め込みMaterialなど、直接保存できないものは対象外。
            if (!Path.GetExtension(path).Equals(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && targets.TryGetValue(material.shader, out string[] names) && names.Length > 0)
                materials.Add(material);
        }
        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog("sheder_reload", "Assets内に対象のMaterialがありません。", "OK");
            return;
        }

        var defaults = new Dictionary<Shader, Material>();
        int changed = 0;
        int skipped = 0;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("sheder_reload");
        try
        {
            foreach (Material material in materials)
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (!AssetDatabase.IsOpenForEdit(path)) { skipped++; continue; }
                if (!defaults.TryGetValue(material.shader, out Material source))
                {
                    // 新規MaterialにはShaderのデフォルトテクスチャも反映される。
                    source = new Material(material.shader) { hideFlags = HideFlags.HideAndDontSave };
                    defaults.Add(material.shader, source);
                }
                bool recorded = false;
                foreach (string name in targets[material.shader])
                {
                    int index = material.shader.FindPropertyIndex(name);
                    if (index < 0 || !material.HasProperty(name)) continue;
                    // Material Variantの親がロックした値は変更しない。
                    if (material.IsPropertyLockedByAncestor(Shader.PropertyToID(name))) { skipped++; continue; }
                    if (!recorded) { Undo.RecordObject(material, "sheder_reload"); recorded = true; }
                    CopyProperty(source, material, name, material.shader.GetPropertyType(index));
                }
                if (!recorded) continue;
                // ToggleなどのPropertyDrawerが管理するキーワードを値に合わせる。
                MaterialEditor.ApplyMaterialPropertyDrawers(material);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                changed++;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("sheder_reload", "処理中にエラーが発生しました。Consoleを確認してください。変更済みの値はUndoで戻せます。", "OK");
            return;
        }
        finally
        {
            foreach (Material material in defaults.Values) UnityEngine.Object.DestroyImmediate(material);
            Undo.CollapseUndoOperations(undoGroup);
        }
        EditorUtility.DisplayDialog("sheder_reload",
            $"{changed}個のMaterialをリセットしました。\n変更はUndoで取り消せます。" +
            (skipped > 0 ? $"\n編集不可のMaterialまたは親ロック項目を{skipped}件スキップしました。" : ""), "OK");
    }

    /// <summary>プロパティ型に合わせて初期値を転記する。</summary>
    private static void CopyProperty(Material source, Material target, string name, ShaderPropertyType type)
    {
        switch (type)
        {
            case ShaderPropertyType.Color: target.SetColor(name, source.GetColor(name)); break;
            case ShaderPropertyType.Vector: target.SetVector(name, source.GetVector(name)); break;
            case ShaderPropertyType.Int: target.SetInteger(name, source.GetInteger(name)); break;
            case ShaderPropertyType.Float:
            case ShaderPropertyType.Range: target.SetFloat(name, source.GetFloat(name)); break;
            case ShaderPropertyType.Texture:
                target.SetTexture(name, source.GetTexture(name));
                target.SetTextureScale(name, source.GetTextureScale(name));
                target.SetTextureOffset(name, source.GetTextureOffset(name));
                break;
        }
    }
}

/// <summary>公開プロパティのチェック状態を保持する指定リセットウィンドウ。</summary>
internal sealed class CSED_ShaderReloadWindow : EditorWindow
{
    [Serializable]
    private sealed class PropertyRow
    {
        public Shader shader;
        public string name;
        public string label;
        public bool selected;
    }

    [SerializeField] private List<PropertyRow> rows = new List<PropertyRow>();
    private Vector2 scroll;

    /// <summary>ShaderがInspectorへ公開しているプロパティを一覧にする。</summary>
    internal static void Open(List<Shader> shaders)
    {
        var window = GetWindow<CSED_ShaderReloadWindow>("sheder_reload");
        window.minSize = new Vector2(480, 360);
        window.rows.Clear();
        foreach (Shader shader in shaders)
        {
            for (int index = 0; index < shader.GetPropertyCount(); index++)
            {
                if ((shader.GetPropertyFlags(index) & ShaderPropertyFlags.HideInInspector) != 0) continue;
                window.rows.Add(new PropertyRow { shader = shader, name = shader.GetPropertyName(index),
                    label = shader.GetPropertyDescription(index) });
            }
        }
        window.Show();
    }

    /// <summary>項目名の右側にチェックを表示し、選択項目だけを実行対象にする。</summary>
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("チェックした項目を、参照する全MaterialでShaderの初期値に戻します。\n対象：Assets内の.mat。テクスチャはTiling / Offsetも戻します。", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("全て選択")) rows.ForEach(row => row.selected = true);
            if (GUILayout.Button("全て解除")) rows.ForEach(row => row.selected = false);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Shader previous = null;
        foreach (PropertyRow row in rows)
        {
            if (row.shader == null) continue;
            if (row.shader != previous)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(row.shader.name, EditorStyles.boldLabel);
                previous = row.shader;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent($"{row.label}  ({row.name})", row.name));
                row.selected = EditorGUILayout.Toggle(row.selected, GUILayout.Width(20));
            }
        }
        EditorGUILayout.EndScrollView();
        if (rows.Count == 0) EditorGUILayout.HelpBox("公開プロパティがありません。", MessageType.Info);
        using (new EditorGUI.DisabledScope(!rows.Any(row => row.selected && row.shader != null)))
        {
            if (GUILayout.Button("実行", GUILayout.Height(30)))
            {
                var targets = rows.Where(row => row.selected && row.shader != null)
                    .GroupBy(row => row.shader).ToDictionary(group => group.Key,
                        group => group.Select(row => row.name).ToArray());
                CSED_ShaderReload.Reset(targets);
            }
        }
    }
}
#endif
