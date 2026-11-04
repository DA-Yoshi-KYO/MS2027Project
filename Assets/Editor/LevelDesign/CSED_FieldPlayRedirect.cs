using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/*
 * Field(フィールド)を編集している時に再生すると、MultiplayerDebugBoot から再生するクラス
 * ・Field_Common.prefab のプレハブモード、または CreateField を開いている時だけ切り替える
 * ・CreateField に Apply していない Field の変更があれば、再生前に Apply するか聞く
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */
[InitializeOnLoad]
public static class CSED_FieldPlayRedirect
{
    private const string _enabledKey = "MS2027.FieldPlayRedirect.Enabled";
    private const string _menuPath = "Tools/Field/再生時にマルチ用のシーンで始める";
    private const string _fieldPrefabPath = "Assets/Programmer/Prefab/Field/Field_Common.prefab";
    private const string _createFieldPath = "Assets/Programmer/Scene/Test/Field/CreateField.unity";
    private const string _bootScenePath = "Assets/Programmer/Scene/Test/NetworkTest/MultiplayerDebugBoot.unity";

    private static bool isEnabled
    {
        get => EditorPrefs.GetBool(_enabledKey, false);
        set => EditorPrefs.SetBool(_enabledKey, value);
    }

    static CSED_FieldPlayRedirect()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(_menuPath)]
    private static void Toggle() => isEnabled = !isEnabled;

    [MenuItem(_menuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(_menuPath, isEnabled);
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }
        if (state != PlayModeStateChange.ExitingEditMode) return;

        EditorSceneManager.playModeStartScene = null;
        if (!isEnabled) return;

        // Field_Common.prefab のプレハブモード
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool isFieldPrefabMode = stage != null && stage.assetPath == _fieldPrefabPath;
        if (isFieldPrefabMode && stage.scene.isDirty)
            Debug.LogWarning("Field_Common.prefab に保存していない変更があります。MainScene には反映されません");

        // CreateField
        bool isCreateField = !isFieldPrefabMode && EditorSceneManager.GetActiveScene().path == _createFieldPath;
        if (!isFieldPrefabMode && !isCreateField) return;
        if (isCreateField && !ConfirmApply())
        {
            EditorApplication.isPlaying = false;   // キャンセルされたら再生しない
            return;
        }

        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(_bootScenePath);
    }

    // Apply していない変更があれば、Apply するか聞く。再生を続けるなら true
    private static bool ConfirmApply()
    {
        var field = EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .FirstOrDefault(go => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == _fieldPrefabPath);
        if (field == null || !HasFieldChanges(field)) return true;

        int choice = EditorUtility.DisplayDialogComplex("Field の変更",
            "CreateField の Field に、プレハブへ Apply していない変更があります。\nApply しないと MainScene には反映されません。",
            "Apply して再生", "キャンセル", "そのまま再生");
        if (choice == 1) return false;
        if (choice == 0) PrefabUtility.ApplyPrefabInstance(field, InteractionMode.UserAction);
        return true;
    }

    // Netcode が付ける値と、位置・名前などの「いつもある上書き」は数えない
    private static bool HasFieldChanges(GameObject field)
    {
        bool hasPropertyChange = PrefabUtility.GetPropertyModifications(field)
            .Any(m => m.propertyPath != "GlobalObjectIdHash" && !PrefabUtility.IsDefaultOverride(m));
        return hasPropertyChange
            || PrefabUtility.GetAddedGameObjects(field).Count > 0
            || PrefabUtility.GetAddedComponents(field).Count > 0
            || PrefabUtility.GetRemovedComponents(field).Count > 0
            || PrefabUtility.GetRemovedGameObjects(field).Count > 0;
    }
}
