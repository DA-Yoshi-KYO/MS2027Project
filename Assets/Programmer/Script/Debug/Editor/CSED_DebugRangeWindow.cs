/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-31 | 初回作成
 * ================================================ */

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// CS_DebugRangeDrawerの表示する要素を、タブごとにまとめて切り替えるウィンドウ
/// </summary>
// ========================================
/*
 * メモ
 * ・Tools/DebugRangeDrawer から開く
 * ・タブは 警察 / 悪人 / プレイヤー / 防犯装置 の4つで、中に要素ごとのチェックボックスを置く
 * ・値はSerializedObject経由で書き換える(Inspectorで切り替えるのと同じ。再生を止めると元に戻る)
 * ・シーン中のCS_DebugRangeDrawerは、ウィンドウを開いた時・フォーカスした時・再生の開始/終了時だけ探す
 */
// ========================================
public class CSED_DebugRangeWindow : EditorWindow
{
    /// <summary>
    /// 1つの要素(CS_DebugRangeDrawerのbool変数名と、表示名)
    /// </summary>
    private struct Element
    {
        public string propertyName;
        public string label;

        public Element(string propertyName, string label)
        {
            this.propertyName = propertyName;
            this.label = label;
        }
    }

    // タブの名前
    private static readonly string[] _tabNames = { "警察", "悪人", "プレイヤー", "防犯装置" };

    // タブごとの要素(_tabNamesと同じ順番)
    private static readonly Element[][] _tabElements =
    {
        new[]
        {
            new Element("_showPolicePatrolView", "巡回視界"),
            new Element("_showPoliceAlertView", "発見視界"),
            new Element("_showPoliceNotice", "背後で気付く距離"),
            new Element("_showPoliceAttack", "攻撃範囲"),
        },
        new[]
        {
            new Element("_showVillainEngage", "臨戦態勢範囲"),
            new Element("_showVillainCounter", "反撃の索敵範囲"),
            new Element("_showVillainLeash", "追跡限界"),
            new Element("_showVillainHit", "攻撃判定"),
        },
        new[]
        {
            new Element("_showPlayerAttack", "通常攻撃の判定(各段)"),
            new Element("_showPlayerSpecial", "必殺技の判定"),
        },
        new[]
        {
            new Element("_showSecurityAirView", "ドローンの高さの視界"),
            new Element("_showSecurityGroundView", "真下の地面の視界"),
        },
    };

    // 表示全体のON/OFF(F3と同じもの)の変数名
    private const string _visiblePropertyName = "_isVisible";

    // 切り替える対象
    private CS_DebugRangeDrawer _drawer = null;

    // _drawerの値を読み書きするためのSerializedObject
    private SerializedObject _serializedDrawer = null;

    // 選んでいるタブ
    private int _selectedTab = 0;

    /// <summary>
    /// Toolsメニューからウィンドウを開くメソッド
    /// </summary>
    [MenuItem("Tools/DebugRangeDrawer")]
    private static void Open()
    {
        GetWindow<CSED_DebugRangeWindow>("DebugRangeDrawer");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        FindDrawer();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnFocus()
    {
        FindDrawer();
    }

    private void OnInspectorUpdate()
    {
        // F3での切り替えやInspectorでの変更を反映するため、定期的に描き直す
        Repaint();
    }

    private void OnGUI()
    {
        // シーンを切り替えた等で、対象が消えていたら探し直す
        if (_drawer == null) FindDrawer();

        if (_drawer == null)
        {
            EditorGUILayout.HelpBox("CS_DebugRangeDrawerがシーンにありません", MessageType.Info);
            if (GUILayout.Button("探し直す")) FindDrawer();
            return;
        }

        _serializedDrawer.Update();

        DrawHeader();
        EditorGUILayout.Space();

        _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
        DrawTab(_tabElements[_selectedTab]);

        _serializedDrawer.ApplyModifiedProperties();
    }

    /// <summary>
    /// 再生の開始・終了時に、対象を探し直すメソッド(再生の前後で別のオブジェクトになるため)
    /// </summary>
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode) return;

        FindDrawer();
        Repaint();
    }

    /// <summary>
    /// シーン中のCS_DebugRangeDrawerを探すメソッド
    /// </summary>
    private void FindDrawer()
    {
        _drawer = FindFirstObjectByType<CS_DebugRangeDrawer>();
        _serializedDrawer = _drawer != null ? new SerializedObject(_drawer) : null;
    }

    /// <summary>
    /// 対象と、表示全体のON/OFF、全部ON/OFFのボタンを描画するメソッド
    /// </summary>
    private void DrawHeader()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("対象", _drawer, typeof(CS_DebugRangeDrawer), true);
        }

        SerializedProperty visible = _serializedDrawer.FindProperty(_visiblePropertyName);
        visible.boolValue = EditorGUILayout.ToggleLeft("表示する(F3でも切り替え)", visible.boolValue);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("全部ON")) SetAll(true);
            if (GUILayout.Button("全部OFF")) SetAll(false);
        }
    }

    /// <summary>
    /// タブの中身(要素ごとのチェックボックスと、タブ内をまとめて切り替えるボタン)を描画するメソッド
    /// </summary>
    /// <param name="elements">タブの要素</param>
    private void DrawTab(Element[] elements)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("このタブを全部ON")) SetElements(elements, true);
            if (GUILayout.Button("このタブを全部OFF")) SetElements(elements, false);
        }

        foreach (Element element in elements)
        {
            SerializedProperty property = _serializedDrawer.FindProperty(element.propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"{element.propertyName} が見つかりません", MessageType.Warning);
                continue;
            }

            property.boolValue = EditorGUILayout.ToggleLeft(element.label, property.boolValue);
        }
    }

    /// <summary>
    /// 全タブの要素をまとめて切り替えるメソッド
    /// </summary>
    /// <param name="isOn">ONにするか</param>
    private void SetAll(bool isOn)
    {
        foreach (Element[] elements in _tabElements) SetElements(elements, isOn);
    }

    /// <summary>
    /// 指定した要素をまとめて切り替えるメソッド
    /// </summary>
    /// <param name="elements">切り替える要素</param>
    /// <param name="isOn">ONにするか</param>
    private void SetElements(Element[] elements, bool isOn)
    {
        foreach (Element element in elements)
        {
            SerializedProperty property = _serializedDrawer.FindProperty(element.propertyName);
            if (property != null) property.boolValue = isOn;
        }
    }
}
#endif
