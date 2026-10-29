/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-29 | 初回作成
 * ================================================ */

#if UNITY_EDITOR

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// SE Databaseの各Element名を、
/// Element 0ではなく設定されたSE IDで表示するEditor。
/// </summary>
[CustomEditor(typeof(CSO_SeDataBase))]
public sealed class CSO_SeDatabaseEditor : Editor
{
    private SerializedProperty _seListProperty;
    private ReorderableList _seReorderableList;

    private void OnEnable()
    {
        _seListProperty =
            serializedObject.FindProperty("_seList");

        if (_seListProperty == null)
        {
            Debug.LogError(
                "[CSO_SeDatabaseEditor] " +
                "CSO_SeDatabaseに_seListが見つかりません。",
                target
            );

            return;
        }

        CreateReorderableList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (_seReorderableList != null)
        {
            _seReorderableList.DoLayoutList();
        }
        else
        {
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CreateReorderableList()
    {
        _seReorderableList = new ReorderableList(
            serializedObject,
            _seListProperty,
            true,  // 並べ替え可能
            true,  // ヘッダー表示
            true,  // 追加ボタン表示
            true   // 削除ボタン表示
        );

        _seReorderableList.drawHeaderCallback =
            DrawHeader;

        _seReorderableList.drawElementCallback =
            DrawElement;

        _seReorderableList.elementHeightCallback =
            GetElementHeight;

        _seReorderableList.onAddCallback =
            AddElement;
    }

    private static void DrawHeader(Rect rect)
    {
        EditorGUI.LabelField(
            rect,
            "SEデータ一覧"
        );
    }

    private void DrawElement(
        Rect rect,
        int index,
        bool isActive,
        bool isFocused)
    {
        SerializedProperty element =
            _seListProperty.GetArrayElementAtIndex(index);

        SerializedProperty idProperty =
            element.FindPropertyRelative("_id");

        string elementName =
            GetElementDisplayName(
                idProperty,
                index
            );

        rect.y += 2f;

        Rect foldoutRect = new Rect(
            rect.x,
            rect.y,
            rect.width,
            EditorGUIUtility.singleLineHeight
        );

        element.isExpanded = EditorGUI.Foldout(
            foldoutRect,
            element.isExpanded,
            elementName,
            true
        );

        if (!element.isExpanded)
            return;

        EditorGUI.indentLevel++;

        Rect contentRect = new Rect(
            rect.x,
            foldoutRect.yMax +
            EditorGUIUtility.standardVerticalSpacing,
            rect.width,
            EditorGUI.GetPropertyHeight(
                element,
                true
            )
        );

        EditorGUI.PropertyField(
            contentRect,
            element,
            GUIContent.none,
            true
        );

        EditorGUI.indentLevel--;
    }

    private float GetElementHeight(int index)
    {
        SerializedProperty element =
            _seListProperty.GetArrayElementAtIndex(index);

        float height =
            EditorGUIUtility.singleLineHeight + 6f;

        if (element.isExpanded)
        {
            height +=
                EditorGUI.GetPropertyHeight(
                    element,
                    true
                );

            height +=
                EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private static string GetElementDisplayName(
        SerializedProperty idProperty,
        int index)
    {
        if (idProperty == null)
        {
            return $"SE {index}";
        }

        CSE_SeId seId =
            (CSE_SeId)idProperty.intValue;

        if (seId == CSE_SeId.None)
        {
            return $"未設定 SE {index}";
        }

        string category =
            GetCategory(seId);

        string idName =
            ObjectNames.NicifyVariableName(
                seId.ToString()
            );

        return $"{category} / {idName}";
    }

    private static string GetCategory(
        CSE_SeId seId)
    {
        int value = (int)seId;

        return value switch
        {
            0 => "未設定",

            >= 100 and < 200
                => "システム",

            >= 200 and < 300
                => "プレイヤー",

            >= 300 and < 400
                => "警察",

            >= 400 and < 500
                => "防犯・監視装置",

            >= 500 and < 600
                => "悪人",

            >= 600 and < 700
                => "レイドボス",

            >= 700 and < 800
                => "アイテム",

            >= 800 and < 900
                => "イベント・通知",

            _ => "その他",
        };
    }

    private void AddElement(
        ReorderableList list)
    {
        int newIndex =
            _seListProperty.arraySize;

        _seListProperty.InsertArrayElementAtIndex(
            newIndex
        );

        SerializedProperty newElement =
            _seListProperty.GetArrayElementAtIndex(
                newIndex
            );

        ResetNewElement(newElement);

        newElement.isExpanded = true;
        list.index = newIndex;
    }

    /// <summary>
    /// 配列追加時に、前のElementの値がコピーされるのを防ぐ。
    /// </summary>
    private static void ResetNewElement(
        SerializedProperty element)
    {
        SerializedProperty idProperty =
            element.FindPropertyRelative("_id");

        SerializedProperty clipsProperty =
            element.FindPropertyRelative("_clips");

        if (idProperty != null)
        {
            idProperty.intValue =
                (int)CSE_SeId.None;
        }

        if (clipsProperty != null &&
            clipsProperty.isArray)
        {
            clipsProperty.arraySize = 0;
        }
    }
}

#endif