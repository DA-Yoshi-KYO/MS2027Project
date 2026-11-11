/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CSE_SeIdをカテゴリ別の階層メニューで表示する。
/// </summary>
[CustomPropertyDrawer(typeof(CS_SeIDPopupAttribute))]
public class CS_SeIDPopupDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        if (property.propertyType !=
            SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(
                position,
                property,
                label
            );

            return;
        }

        EditorGUI.BeginProperty(
            position,
            label,
            property
        );

        position = EditorGUI.PrefixLabel(
            position,
            label
        );

        string buttonText = GetCurrentDisplayName(
            property
        );

        if (EditorGUI.DropdownButton(
            position,
            new GUIContent(buttonText),
            FocusType.Keyboard))
        {
            ShowMenu(property);
        }

        EditorGUI.EndProperty();
    }

    private static void ShowMenu(
        SerializedProperty property)
    {
        var menu = new GenericMenu();

        Array enumValues =
            Enum.GetValues(typeof(CSE_SeId));

        foreach (CSE_SeId seId in enumValues)
        {
            CSE_SeId capturedId = seId;

            string category =
                GetCategory(capturedId);

            string displayName =
                ObjectNames.NicifyVariableName(
                    capturedId.ToString()
                );

            string menuPath =
                $"{category}/{displayName}";

            bool isSelected =
                property.intValue ==
                (int)capturedId;

            menu.AddItem(
                new GUIContent(menuPath),
                isSelected,
                () =>
                {
                    property.serializedObject.Update();

                    property.intValue =
                        (int)capturedId;

                    property.serializedObject
                        .ApplyModifiedProperties();
                }
            );
        }

        menu.ShowAsContext();
    }

    private static string GetCurrentDisplayName(
        SerializedProperty property)
    {
        if (property.hasMultipleDifferentValues)
            return "Mixed Values";

        CSE_SeId currentId =
            (CSE_SeId)property.intValue;

        string category =
            GetCategory(currentId);

        string displayName =
            ObjectNames.NicifyVariableName(
                currentId.ToString()
            );

        return $"{category}/{displayName}";
    }

    private static string GetCategory(
      CSE_SeId seId)
    {
        int value = (int)seId;

        return value switch
        {
            0 => "None",

            >= 100 and < 200
                => "System",

            >= 200 and < 300
                => "Player",

            >= 300 and < 400
                => "Police",

            >= 400 and < 500
                => "Security",

            >= 500 and < 600
                => "Villain",

            >= 600 and < 700
                => "Raid Boss",

            >= 700 and < 800
                => "Item",

            >= 800 and < 900
                => "Event & Notification",

            _ => "Other",
        };
    }
}

#endif