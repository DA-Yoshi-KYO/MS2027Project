using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * CS_PathFollowerのエディタ拡張(パスの編集・表示・プレビュー)
 * シーンビューで点のドラッグ・追加・削除、点ごとの値の確認、再生しなくても動きのプレビューができる
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・シーンビュー(CS_PathFollowerを選んでいる時)
 *   点の球をクリックで選択(オレンジの球)→ 矢印をドラッグで移動
 *   Shift + クリックで、クリックした場所(物に当たればその面、無ければ最後の点の高さの面)に新しい点を足す
 *   左上のボタンで「選んでいる点を削除」「プレビュー」
 *   各点の上に、待ち時間と次の点までの速さを表示する
 * ・プレビュー: パスの上を動く印(黄色の球)を、実際の動き(待ち時間・速さ・動き方)どおりに動かして見せる
 *   SAポイント本体は動かさない(シーンを汚さない)。選択を外す・もう一度押すと止まる
 * ・点の追加・移動・削除は全てUndoできる
 * ・曲線は、CS_PathFollower自身がOnDrawGizmosで描く(点を選んでいる時にも見えるように)
 */
// ========================================

[CustomEditor(typeof(CS_PathFollower))]
public class CSED_PathFollowerEditor : Editor
{
    private const float _pointHandleSize = 0.12f;     // 点の球の大きさ(画面上の割合)
    private const float _defaultSpeed = 4f;           // 最初の点の速さ(m/秒)

    private int _selectedIndex = -1;                  // 選んでいる点の番号
    private bool _isPreviewing;
    private double _previewStartTime;

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _isPreviewing = false;
    }

    // プレビュー中は、シーンビューを描き直し続ける
    private void OnEditorUpdate()
    {
        if (_isPreviewing) SceneView.RepaintAll();
    }

    // ========================================
    // インスペクター
    // ========================================

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CS_PathFollower follower = (CS_PathFollower)target;
        List<CS_PathFollowerPoint> points = follower.GetPoints();
        follower.RefreshForEditor();   // 点を読み直す(再生していない間)

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("パス", EditorStyles.boldLabel);
        DrawSummary(follower, points);
        DrawPointTable(follower, points);

        EditorGUILayout.Space();
        DrawButtons(follower, points);
        EditorGUILayout.HelpBox("シーンビューで Shift + クリック: 点を追加 / 点の球をクリックして選び、矢印で移動", MessageType.None);
    }

    private void DrawSummary(CS_PathFollower follower, List<CS_PathFollowerPoint> points)
    {
        if (follower.saPoint == null && follower.GetComponentInChildren<CS_SAPoint>(true) == null)
        {
            EditorGUILayout.HelpBox("動かすSAポイント(CS_SAPoint)が子にありません", MessageType.Warning);
        }
        if (points.Count < 2)
        {
            EditorGUILayout.HelpBox("点(CS_PathFollowerPoint)が2つ以上必要です。シーンビューで Shift + クリックして追加してください", MessageType.Warning);
            return;
        }

        string cycle = follower.mode == CSE_PathFollowMode.Once ? "最後の点まで" : "1周(往復)";
        EditorGUILayout.LabelField($"点 {points.Count} 個 / 曲線の長さ {follower.totalLength:0.0} m / {cycle} {follower.cycleDuration:0.0} 秒");
    }

    // 点ごとの値の一覧(待ち時間・速さ・動き方のカーブが変えられる。区間の長さ・かかる時間も確認できる)
    private void DrawPointTable(CS_PathFollower follower, List<CS_PathFollowerPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            SerializedObject pointObject = new SerializedObject(points[i]);
            SerializedProperty wait = pointObject.FindProperty("_waitTime");
            SerializedProperty speed = pointObject.FindProperty("_speedToNext");

            EditorGUILayout.BeginHorizontal();
            bool isSelected = i == _selectedIndex;
            if (GUILayout.Toggle(isSelected, $"P{i}", EditorStyles.miniButton, GUILayout.Width(36f)) && !isSelected)
            {
                _selectedIndex = i;
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 36f;
            EditorGUILayout.PropertyField(wait, new GUIContent("待ち"), GUILayout.MinWidth(70f));
            EditorGUILayout.PropertyField(speed, new GUIContent("速さ"), GUILayout.MinWidth(70f));
            EditorGUIUtility.labelWidth = 0f;
            if (EditorGUI.EndChangeCheck()) pointObject.ApplyModifiedProperties();   // Undoにも入る

            // この点から次の点までの区間(最後の点は、周回の時だけ最初の点へ戻る区間がある)
            string segment = i < follower.segmentCount ? $"→ {follower.GetSegmentLength(i):0.0}m / {follower.GetSegmentDuration(i):0.0}s" : "(終点)";
            EditorGUILayout.LabelField(segment, GUILayout.Width(110f));
            EditorGUILayout.EndHorizontal();

            DrawEaseFields(follower, points, i, pointObject);
        }

        if (follower.mode == CSE_PathFollowMode.PingPong)
        {
            EditorGUILayout.HelpBox("帰りの速さは、出発する点(次の点)の値を使います。カーブ・帰りの選び方は、区間の始点の点の値です", MessageType.None);
        }
    }

    // 点の動き方のカーブ(行き・帰りの選び方)。次の区間が無い点(一方向・往復の最後の点)には出さない
    // 帰りは、往復の時だけ出す。Customを選んだ時だけ、帰りのカーブが出る
    private void DrawEaseFields(CS_PathFollower follower, List<CS_PathFollowerPoint> points, int index, SerializedObject pointObject)
    {
        bool isLast = index == points.Count - 1;
        bool hasSegment = !isLast || follower.mode == CSE_PathFollowMode.Loop;
        if (!hasSegment) return;

        SerializedProperty goCurve = pointObject.FindProperty("_goCurve");
        SerializedProperty backMode = pointObject.FindProperty("_backMode");
        SerializedProperty backCurve = pointObject.FindProperty("_backCurve");

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(goCurve, new GUIContent("行きのカーブ"));

        bool showBack = !isLast && follower.mode == CSE_PathFollowMode.PingPong;
        if (showBack)
        {
            EditorGUILayout.PropertyField(backMode, new GUIContent("帰りの動き"));
            if (backMode.enumValueIndex == (int)CSE_PathFollowEaseMode.Custom)
            {
                EditorGUILayout.PropertyField(backCurve, new GUIContent("帰りのカーブ"));
            }
        }
        if (EditorGUI.EndChangeCheck()) pointObject.ApplyModifiedProperties();   // Undoにも入る
        EditorGUI.indentLevel--;
    }

    private void DrawButtons(CS_PathFollower follower, List<CS_PathFollowerPoint> points)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("選んでいる点を削除")) DeleteSelectedPoint(points);
        if (GUILayout.Button(_isPreviewing ? "プレビュー停止" : "プレビュー")) TogglePreview();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(points.Count == 0 || follower.saPoint == null))
        {
            if (GUILayout.Button("動かす物を最初の点へ合わせる")) MoveSAPointToFirstPoint(follower, points);
        }
    }

    // ========================================
    // シーンビュー
    // ========================================

    private void OnSceneGUI()
    {
        CS_PathFollower follower = (CS_PathFollower)target;
        List<CS_PathFollowerPoint> points = follower.GetPoints();
        if (_selectedIndex >= points.Count) _selectedIndex = points.Count - 1;

        HandleAddPoint(follower, points);
        DrawPointHandles(points);
        DrawPreview(follower);
        DrawSceneButtons(points);
    }

    // Shift + クリックで点を足す
    private void HandleAddPoint(CS_PathFollower follower, List<CS_PathFollowerPoint> points)
    {
        Event current = Event.current;

        // シーンビューのクリックで、選択が変わらないようにする
        if (current.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (current.type != EventType.MouseDown || current.button != 0 || !current.shift || current.alt || current.control) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        Vector3 position;
        if (Physics.Raycast(ray, out RaycastHit hit)) position = hit.point;
        else
        {
            // 物に当たらなければ、最後の点(無ければこのオブジェクト)の高さの水平な面
            float height = points.Count > 0 ? points[points.Count - 1].transform.position.y : follower.transform.position.y;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            if (!plane.Raycast(ray, out float distance)) return;
            position = ray.GetPoint(distance);
        }

        AddPoint(follower, points, position);
        current.Use();
    }

    // 点の球と、選んでいる点の移動用の矢印・各点の値のラベルを描く
    private void DrawPointHandles(List<CS_PathFollowerPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 position = points[i].transform.position;
            float size = HandleUtility.GetHandleSize(position) * _pointHandleSize;
            bool isSelected = i == _selectedIndex;

            Handles.color = isSelected ? new Color(1f, 0.6f, 0.1f) : new Color(0.3f, 0.9f, 1f);
            if (Handles.Button(position, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
            {
                _selectedIndex = i;
                Repaint();
            }

            string speed = i < points.Count - 1 || ((CS_PathFollower)target).mode != CSE_PathFollowMode.Once ? $" → {points[i].speedToNext:0.#}m/s" : "";
            string label = $"P{i}  待ち {points[i].waitTime:0.#}s{speed}";
            Handles.Label(position + Vector3.up * size * 2.5f, label, EditorStyles.whiteBoldLabel);

            if (!isSelected) continue;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(points[i].transform, "パスの点を移動");
                points[i].transform.position = moved;
            }
        }
    }

    // プレビュー: 実際の動きどおりに動く印を描く
    private void DrawPreview(CS_PathFollower follower)
    {
        if (!_isPreviewing) return;

        follower.RefreshForEditor();
        if (follower.pointCount < 2) return;

        double elapsed = EditorApplication.timeSinceStartup - _previewStartTime;
        Vector3 position = follower.transform.TransformPoint(follower.EvaluateAt(elapsed));

        float size = HandleUtility.GetHandleSize(position) * 0.2f;
        Handles.color = Color.yellow;
        Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
        Handles.Label(position + Vector3.up * size * 2f, $"{elapsed % 1000.0:0.0}s", EditorStyles.whiteBoldLabel);
    }

    // シーンビューの左上のボタン
    private void DrawSceneButtons(List<CS_PathFollowerPoint> points)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 160f, 60f));
        if (GUILayout.Button(_isPreviewing ? "プレビュー停止" : "プレビュー")) TogglePreview();
        if (GUILayout.Button("選んでいる点を削除")) DeleteSelectedPoint(points);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    // ========================================
    // 操作
    // ========================================

    // 最後の点の後ろに点を足す(最後の点と同じ親・同じ速さ。最初の点なら、このオブジェクトの子)
    private void AddPoint(CS_PathFollower follower, List<CS_PathFollowerPoint> points, Vector3 position)
    {
        Transform parent = points.Count > 0 ? points[points.Count - 1].transform.parent : follower.transform;
        float speed = points.Count > 0 ? points[points.Count - 1].speedToNext : _defaultSpeed;

        GameObject pointObject = new GameObject($"Point_{points.Count}");
        Undo.RegisterCreatedObjectUndo(pointObject, "パスの点を追加");
        Undo.SetTransformParent(pointObject.transform, parent, "パスの点を追加");
        pointObject.transform.position = position;

        CS_PathFollowerPoint point = Undo.AddComponent<CS_PathFollowerPoint>(pointObject);
        point.Setup(0f, speed);

        _selectedIndex = points.Count;
        EditorUtility.SetDirty(point);
    }

    private void DeleteSelectedPoint(List<CS_PathFollowerPoint> points)
    {
        if (_selectedIndex < 0 || _selectedIndex >= points.Count) return;

        Undo.DestroyObjectImmediate(points[_selectedIndex].gameObject);
        _selectedIndex = Mathf.Min(_selectedIndex, points.Count - 2);
        SceneView.RepaintAll();
        Repaint();
    }

    private void MoveSAPointToFirstPoint(CS_PathFollower follower, List<CS_PathFollowerPoint> points)
    {
        if (points.Count == 0 || follower.saPoint == null) return;

        Undo.RecordObject(follower.saPoint.transform, "動かす物を最初の点へ");
        follower.saPoint.transform.position = points[0].transform.position;
    }

    private void TogglePreview()
    {
        _isPreviewing = !_isPreviewing;
        _previewStartTime = EditorApplication.timeSinceStartup;
        SceneView.RepaintAll();
    }
}
