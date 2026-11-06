using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/*
 * 巡回(CS_VillainPatrolRoute)のInspector
 * 巡回エディタで選んでいる悪人の設定(回り方・速さ・向き・隊列)と、選んでいるポイントでやることを編集する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・悪人・ポイントの選択はSceneビュー(CSED_VillainPatrolSceneGUI)と共有する(CSED_VillainPatrolEditorState)
 *   ここの悪人・ポイントのボタンを押しても選べる
 * ・やることは上から順に実行する(ドラッグで並べ替え)。何も無ければ止まらずに次へ向かう
 * ・値はパターンのアセット(CSO_VillainPatrolPattern)をSerializedObjectで書き換える(Undoできる)
 */
// ========================================

[CustomEditor(typeof(CS_VillainPatrolRoute))]
public class CSED_VillainPatrolRouteEditor : Editor
{
    private const float _addButtonWidth = 90f;

    private SerializedObject _patternObject;
    private ReorderableList _actionList;
    private int _listRoute = -1;   // _actionListが表示しているルートとポイント(選択が変わったら作り直す)
    private int _listPoint = -1;

    private void OnEnable()
    {
        CSED_VillainPatrolEditorState.onChanged += Repaint;
    }

    private void OnDisable()
    {
        CSED_VillainPatrolEditorState.onChanged -= Repaint;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_pattern"), new GUIContent("巡回パターン"));
        serializedObject.ApplyModifiedProperties();

        CS_VillainPatrolRoute route = (CS_VillainPatrolRoute)target;
        CSO_VillainPatrolPattern pattern = route.pattern;
        if (pattern == null)
        {
            EditorGUILayout.HelpBox("Sceneビューの巡回のツールバーで「悪人を置く」を押して、Sceneビューをクリックすると作られます。", MessageType.Info);
            return;
        }

        if (_patternObject == null || _patternObject.targetObject != pattern) _patternObject = new SerializedObject(pattern);
        _patternObject.Update();

        SerializedProperty routes = _patternObject.FindProperty("_routes");
        DrawRouteButtons(routes);

        int routeIndex = CSED_VillainPatrolEditorState.selectedRoute;
        if (routeIndex < 0 || routeIndex >= routes.arraySize)
        {
            EditorGUILayout.HelpBox("Sceneビューで悪人・ポイント・線をクリックするか、上のボタンで悪人を選ぶと設定できます。", MessageType.None);
            _patternObject.ApplyModifiedProperties();
            return;
        }

        SerializedProperty selectedRoute = routes.GetArrayElementAtIndex(routeIndex);
        DrawRouteSettings(routes, selectedRoute, routeIndex);
        DrawPoints(selectedRoute, routeIndex);

        if (_patternObject.ApplyModifiedProperties())
        {
            CSED_VillainPatrolEditorState.MarkPatternDirty(route);
            SceneView.RepaintAll();
        }
    }

    // 消した時など、選択を変えてすぐに描き直す時に、ここまでの変更を反映してからGUIを抜ける
    private void ApplyAndExit(int routeIndex, int pointIndex)
    {
        _patternObject.ApplyModifiedProperties();
        CSED_VillainPatrolEditorState.MarkPatternDirty((CS_VillainPatrolRoute)target);
        CSED_VillainPatrolEditorState.Select(routeIndex, pointIndex);
        GUIUtility.ExitGUI();
    }

    // 悪人(ルート)を選ぶボタン
    private static void DrawRouteButtons(SerializedProperty routes)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("悪人", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < routes.arraySize; i++)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = routes.GetArrayElementAtIndex(i).FindPropertyRelative("_color").colorValue;
            bool isSelected = i == CSED_VillainPatrolEditorState.selectedRoute;
            if (GUILayout.Toggle(isSelected, CSED_VillainPatrolLabels.GetRouteName(i), EditorStyles.miniButton) && !isSelected)
            {
                CSED_VillainPatrolEditorState.Select(i, -1);
            }
            GUI.backgroundColor = previous;
        }
        EditorGUILayout.EndHorizontal();
    }

    // 悪人ごとの設定(回り方・速さ・最初の向き・隊列)
    private void DrawRouteSettings(SerializedProperty routes, SerializedProperty route, int routeIndex)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"{CSED_VillainPatrolLabels.GetRouteName(routeIndex)}の設定", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(route.FindPropertyRelative("_color"), new GUIContent("色"));

        SerializedProperty loopMode = route.FindPropertyRelative("_loopMode");
        int pointCount = route.FindPropertyRelative("_points").arraySize;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("回り方");
        if (GUILayout.Toggle(loopMode.enumValueIndex == (int)CSE_VillainPatrolLoopMode.Loop, "周回", EditorStyles.miniButtonLeft))
        {
            loopMode.enumValueIndex = (int)CSE_VillainPatrolLoopMode.Loop;
        }
        if (GUILayout.Toggle(loopMode.enumValueIndex == (int)CSE_VillainPatrolLoopMode.PingPong, "往復", EditorStyles.miniButtonRight))
        {
            loopMode.enumValueIndex = (int)CSE_VillainPatrolLoopMode.PingPong;
        }
        EditorGUILayout.EndHorizontal();
        int last = Mathf.Max(pointCount, 2);
        EditorGUILayout.LabelField(" ", loopMode.enumValueIndex == (int)CSE_VillainPatrolLoopMode.Loop
            ? $"1→…→{last}→1 を繰り返します"
            : $"1→…→{last}→…→1 と折り返します", EditorStyles.miniLabel);

        SerializedProperty speed = route.FindPropertyRelative("_speed");
        EditorGUILayout.PropertyField(speed, new GUIContent("速さ(m/秒)"));
        speed.floatValue = Mathf.Max(CSO_VillainPatrolPattern.minSpeed, speed.floatValue);
        EditorGUILayout.PropertyField(route.FindPropertyRelative("_startAngle"), new GUIContent("最初の向き(°)", "湧いた時の向き。スポーン位置の向きからの角度(＋で右回り)"));

        SerializedProperty formation = route.FindPropertyRelative("_formation");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("隊列", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(formation.FindPropertyRelative("_followerCount"), new GUIContent("付いてくる人数"));
        SerializedProperty shape = formation.FindPropertyRelative("_shape");
        shape.enumValueIndex = EditorGUILayout.Popup("形", shape.enumValueIndex, CSED_VillainPatrolLabels.formationShapeNames);
        EditorGUILayout.PropertyField(formation.FindPropertyRelative("_spacing"), new GUIContent("間隔(m)"));

        EditorGUILayout.Space(4f);
        if (GUILayout.Button($"{CSED_VillainPatrolLabels.GetRouteName(routeIndex)}を消す"))
        {
            routes.DeleteArrayElementAtIndex(routeIndex);
            ApplyAndExit(-1, -1);
        }
    }

    // ポイントを選ぶボタンと、選んでいるポイントでやること
    private void DrawPoints(SerializedProperty route, int routeIndex)
    {
        SerializedProperty points = route.FindPropertyRelative("_points");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ポイント", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < points.arraySize; i++)
        {
            bool isSelected = i == CSED_VillainPatrolEditorState.selectedPoint;
            if (GUILayout.Toggle(isSelected, (i + 1).ToString(), EditorStyles.miniButton, GUILayout.Width(28f)) && !isSelected)
            {
                CSED_VillainPatrolEditorState.Select(routeIndex, i);
            }
        }
        EditorGUILayout.EndHorizontal();

        int pointIndex = CSED_VillainPatrolEditorState.selectedPoint;
        if (pointIndex < 0 || pointIndex >= points.arraySize)
        {
            EditorGUILayout.HelpBox("ポイントを選ぶと、そこでやることを書けます。", MessageType.None);
            return;
        }

        SerializedProperty actions = points.GetArrayElementAtIndex(pointIndex).FindPropertyRelative("_actions");
        if (_actionList == null || _listRoute != routeIndex || _listPoint != pointIndex || _actionList.serializedProperty.serializedObject != _patternObject)
        {
            _actionList = CreateActionList(actions, pointIndex);
            _listRoute = routeIndex;
            _listPoint = pointIndex;
        }
        else
        {
            _actionList.serializedProperty = actions;
        }

        EditorGUILayout.Space();
        _actionList.DoLayoutList();
        EditorGUILayout.LabelField("上から順に実行します。何も無ければ、止まらずに次へ向かいます。", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        DrawAddButton(actions, "＋停止", CSE_VillainPatrolActionType.Stop);
        DrawAddButton(actions, "＋見回し", CSE_VillainPatrolActionType.Look);
        DrawAddButton(actions, "＋向きを変える", CSE_VillainPatrolActionType.Turn);
        EditorGUILayout.EndHorizontal();

        // ポイント1(スタート)は悪人の位置なので消さない(悪人ごと消す)
        if (pointIndex > 0 && GUILayout.Button($"ポイント{pointIndex + 1}を消す"))
        {
            points.DeleteArrayElementAtIndex(pointIndex);
            ApplyAndExit(routeIndex, -1);
        }
    }

    private ReorderableList CreateActionList(SerializedProperty actions, int pointIndex)
    {
        ReorderableList list = new ReorderableList(_patternObject, actions, true, true, false, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, pointIndex == 0 ? "ポイント1(スタート)でやること" : $"ポイント{pointIndex + 1}でやること");
        list.drawElementCallback = (rect, index, isActive, isFocused) => DrawAction(list.serializedProperty.GetArrayElementAtIndex(index), rect);
        list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        return list;
    }

    // やること1つ分(種類ごとの項目)
    private static void DrawAction(SerializedProperty action, Rect rect)
    {
        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        CSE_VillainPatrolActionType type = (CSE_VillainPatrolActionType)action.FindPropertyRelative("_type").enumValueIndex;
        SerializedProperty seconds = action.FindPropertyRelative("_seconds");
        SerializedProperty angle = action.FindPropertyRelative("_angle");

        Rect name = new Rect(rect.x, rect.y, 90f, rect.height);
        EditorGUI.LabelField(name, CSED_VillainPatrolLabels.GetActionName(type), EditorStyles.boldLabel);
        Rect body = new Rect(name.xMax, rect.y, rect.width - name.width, rect.height);

        switch (type)
        {
            case CSE_VillainPatrolActionType.Stop:
                seconds.floatValue = Mathf.Clamp(DrawNumber(body, seconds.floatValue, "秒"), 0f, 60f);
                break;
            case CSE_VillainPatrolActionType.Look:
                float half = body.width * 0.5f;
                EditorGUI.LabelField(new Rect(body.x, body.y, 30f, body.height), "左右");
                angle.floatValue = Mathf.Clamp(DrawNumber(new Rect(body.x + 30f, body.y, half - 30f, body.height), angle.floatValue, "°を"), 0f, 180f);
                seconds.floatValue = Mathf.Clamp(DrawNumber(new Rect(body.x + half, body.y, half, body.height), seconds.floatValue, "秒"), 0.5f, 30f);
                break;
            case CSE_VillainPatrolActionType.Turn:
                angle.floatValue = Mathf.Clamp(DrawNumber(body, angle.floatValue, "°(＋で右回り)"), -180f, 180f);
                break;
        }
    }

    private static float DrawNumber(Rect rect, float value, string unit)
    {
        const float fieldWidth = 50f;
        float result = EditorGUI.FloatField(new Rect(rect.x, rect.y, fieldWidth, rect.height), value);
        EditorGUI.LabelField(new Rect(rect.x + fieldWidth + 4f, rect.y, rect.width - fieldWidth - 4f, rect.height), unit);
        return result;
    }

    private static void DrawAddButton(SerializedProperty actions, string label, CSE_VillainPatrolActionType type)
    {
        if (!GUILayout.Button(label, GUILayout.MinWidth(_addButtonWidth))) return;

        CSO_VillainPatrolPattern.Action defaults = CSO_VillainPatrolPattern.Action.CreateDefault(type);
        int index = actions.arraySize;
        actions.InsertArrayElementAtIndex(index);
        SerializedProperty added = actions.GetArrayElementAtIndex(index);
        added.FindPropertyRelative("_type").enumValueIndex = (int)type;
        added.FindPropertyRelative("_seconds").floatValue = defaults.seconds;
        added.FindPropertyRelative("_angle").floatValue = defaults.angle;
    }
}
