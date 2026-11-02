/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-11-02 | 子オブジェクトの巡回ポイントも表示するようにした
 * 2026-11-02 | オン/オフの設定ではなく、グループかその子オブジェクトを選択している時だけ表示するようにした
 * ================================================ */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CS_PoliceSquadの巡回ルートを、Sceneビューに表示するエディタ専用クラス
/// 巡回ポイントを進む順に線で結び、始点→最後のポイント→始点に戻る、の順に色が変わるグラデーションにする
/// 巡回ポイントは、グループの子オブジェクトがあれば子オブジェクト(並んでいる順)、無ければ巡回ルートの配列を使う
/// </summary>
public static class CSED_PoliceSquadGizmo
{
    // グラデーションの線を細かく区切る長さ(短いほど色の変化がなめらかになる)
    private const float _lineStep = 0.25f;

    // 巡回ポイントの目印の球の半径
    private const float _markerRadius = 0.2f;

    // 巡回ポイントの番号を表示する高さ
    private const float _labelHeight = 0.5f;

    /// <summary>
    /// 巡回ルートを描画するメソッド(グループか、その子オブジェクト(巡回ポイント)を選択している時だけ表示する)
    /// </summary>
    /// <param name="squad">描画するグループ</param>
    /// <param name="gizmoType">描画の種類(Unityから渡される)</param>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawPatrolRoute(CS_PoliceSquad squad, GizmoType gizmoType)
    {
        if (!IsSelectedOrChildSelected(squad.transform)) return;

        // 未設定(None)の巡回ポイントは飛ばす
        List<Transform> points = squad.GetRouteDrawPoints();
        List<int> validIndices = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null) validIndices.Add(i);
        }
        if (validIndices.Count == 0) return;

        // 最後のポイントから最初のポイントへ戻る区間も含めた、ルート全体の長さ
        float totalLength = 0.0f;
        for (int i = 0; i < validIndices.Count; i++)
        {
            totalLength += Vector3.Distance(GetPatrolPosition(points, validIndices, i), GetPatrolPosition(points, validIndices, i + 1));
        }

        float traveledLength = 0.0f;
        for (int i = 0; i < validIndices.Count; i++)
        {
            Vector3 start = GetPatrolPosition(points, validIndices, i);
            Vector3 end = GetPatrolPosition(points, validIndices, i + 1);
            float startRate = totalLength > 0.0f ? traveledLength / totalLength : 0.0f;
            traveledLength += Vector3.Distance(start, end);
            float endRate = totalLength > 0.0f ? traveledLength / totalLength : 0.0f;

            DrawGradientLine(squad, start, end, startRate, endRate);
            DrawPatrolPointMarker(squad, start, validIndices[i], startRate);
        }
    }

    /// <summary>
    /// 対象か、その子孫のオブジェクトが選択されているかを判定するメソッド(巡回ドローンのルート表示も使う)
    /// 子オブジェクトだけを選んでいる時(巡回ポイントを動かす時など)も、親のルートを表示するのに使う
    /// </summary>
    /// <param name="root">対象(巡回ポイントの親)</param>
    /// <returns>対象か、その子孫が選択されていればtrue</returns>
    public static bool IsSelectedOrChildSelected(Transform root)
    {
        foreach (Transform selected in Selection.GetTransforms(SelectionMode.Unfiltered))
        {
            if (selected == root || selected.IsChildOf(root)) return true;
        }
        return false;
    }

    /// <summary>
    /// 有効な巡回ポイントの位置を取得するメソッド(最後の次は最初に戻る)
    /// </summary>
    /// <param name="points">巡回ポイント</param>
    /// <param name="validIndices">有効な巡回ポイントの番号</param>
    /// <param name="order">何番目の有効なポイントか</param>
    /// <returns>巡回ポイントの位置</returns>
    private static Vector3 GetPatrolPosition(List<Transform> points, List<int> validIndices, int order)
    {
        return points[validIndices[order % validIndices.Count]].position;
    }

    /// <summary>
    /// 2点の間を、ルート全体での位置に応じた色のグラデーションの線で描画するメソッド
    /// </summary>
    /// <param name="squad">グループ(線の色と太さを使う)</param>
    /// <param name="start">線の始点</param>
    /// <param name="end">線の終点</param>
    /// <param name="startRate">始点がルート全体のどこか(0〜1)</param>
    /// <param name="endRate">終点がルート全体のどこか(0〜1)</param>
    private static void DrawGradientLine(CS_PoliceSquad squad, Vector3 start, Vector3 end, float startRate, float endRate)
    {
        // 線を細かく区切り、区切りごとに色を変えてグラデーションに見せる
        int pieceCount = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / _lineStep));
        for (int i = 0; i < pieceCount; i++)
        {
            float pieceStart = (float)i / pieceCount;
            float pieceEnd = (float)(i + 1) / pieceCount;
            float pieceMiddle = (pieceStart + pieceEnd) * 0.5f;

            Handles.color = squad.routeGradient.Evaluate(Mathf.Lerp(startRate, endRate, pieceMiddle));
            Handles.DrawLine(Vector3.Lerp(start, end, pieceStart), Vector3.Lerp(start, end, pieceEnd), squad.routeLineThickness);
        }
    }

    /// <summary>
    /// 巡回ポイントの位置に、ルートの色の球と巡回する順番の番号を描画するメソッド
    /// </summary>
    /// <param name="squad">グループ(線の色を使う)</param>
    /// <param name="position">巡回ポイントの位置</param>
    /// <param name="index">巡回ポイントの番号</param>
    /// <param name="rate">ポイントがルート全体のどこか(0〜1)</param>
    private static void DrawPatrolPointMarker(CS_PoliceSquad squad, Vector3 position, int index, float rate)
    {
        Gizmos.color = squad.routeGradient.Evaluate(rate);
        Gizmos.DrawSphere(position, _markerRadius);
        Handles.Label(position + Vector3.up * _labelHeight, index.ToString());
    }
}
