using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/*
 * エリア分け(CS_CrimeAreaLayout)のInspectorとSceneビューの操作
 * Sceneビューで、扇形の境目をつまんで回せるようにし、Inspectorにエリアごとのスポーン位置の数を出す
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・境目の丸(扇形の端、始まりの向きの線の先)をドラッグすると、中心の周りに回る(Undoできる)
 *   中心を動かす時は、普段どおりオブジェクトを動かす
 * ・扇形の塗り・エリア名・スポーン位置の色は、CS_CrimeAreaLayout.DrawGizmosが描く
 */
// ========================================

[CustomEditor(typeof(CS_CrimeAreaLayout))]
public class CSED_CrimeAreaLayoutEditor : Editor
{
    private const float _handleDistanceRate = 1f;      // 境目の丸を置く位置(扇形の半径に対する割合。1で扇形の端)
    private const float _handleSizeRate = 0.25f;       // 境目の丸の大きさ(見た目の大きさに対する割合)

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CS_CrimeAreaLayout layout = (CS_CrimeAreaLayout)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("エリアごとのスポーン位置", EditorStyles.boldLabel);

        Dictionary<CS_CrimeAreaLayout.Sector, int> counts = new Dictionary<CS_CrimeAreaLayout.Sector, int>();
        foreach (CS_VillainSpawnPoint point in CS_CrimeAreaLayout.GetSpawnPointsForEditor())
        {
            if (point == null) continue;

            CS_CrimeAreaLayout.Sector sector = layout.GetSector(point.transform.position);
            if (sector == null) continue;
            counts[sector] = counts.TryGetValue(sector, out int count) ? count + 1 : 1;
        }

        foreach (CS_CrimeAreaLayout.Sector sector in layout.sectors)
        {
            string name = sector.area != null ? sector.area.areaName : "(エリア未設定)";
            float start = sector.startAngle;
            float end = Mathf.Repeat(layout.GetEndAngle(sector), 360f);
            counts.TryGetValue(sector, out int count);
            EditorGUILayout.LabelField($"{name}", $"{start:0}°〜{end:0}° / スポーン位置 {count}か所");
        }
        EditorGUILayout.HelpBox("Sceneビューで、境目の丸をドラッグすると扇形を回せます。向きは北(Z+)が0°、時計回り(東が90°)です。", MessageType.None);
    }

    // 境目の丸をドラッグして、扇形の始まりの向きを変える
    private void OnSceneGUI()
    {
        CS_CrimeAreaLayout layout = (CS_CrimeAreaLayout)target;
        Vector3 center = layout.transform.position;
        Plane ground = new Plane(Vector3.up, center);

        foreach (CS_CrimeAreaLayout.Sector sector in layout.sectors)
        {
            Vector3 handle = center + CS_CrimeAreaLayout.GetDirection(sector.startAngle) * (layout.displayRadius * _handleDistanceRate);
            float size = HandleUtility.GetHandleSize(handle) * _handleSizeRate;

            Handles.color = sector.color;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(handle, size, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck()) continue;

            // 地面の上で、中心から見た向きを新しい始まりの向きにする
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Vector3 point = ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : moved;

            Undo.RecordObject(layout, "エリア分けの境目を動かす");
            sector.startAngle = layout.GetAngle(point);
            EditorUtility.SetDirty(layout);
        }
    }
}
