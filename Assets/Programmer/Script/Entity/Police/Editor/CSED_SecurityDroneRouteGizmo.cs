/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-02 | 初回作成(巡回ドローンの巡回ルートの表示を、CS_SecurityDroneSpawnerから移した)
 * ================================================ */

using UnityEditor;
using UnityEngine;

/// <summary>
/// CS_SecurityDroneSpawnerの巡回ルートを、Sceneビューに表示するエディタ専用クラス
/// 巡回ポイント(出現位置の子オブジェクト)を、並んでいる順に線で結ぶ(最後は最初へ戻る)
/// 出現位置か、その子オブジェクト(巡回ポイント)を選択している時だけ表示する
/// </summary>
public static class CSED_SecurityDroneRouteGizmo
{
    // 巡回ポイントの目印の球の半径
    private const float _markerRadius = 0.2f;

    // ルートの色
    private static readonly Color _routeColor = new Color(1.0f, 0.8f, 0.2f, 0.9f);

    /// <summary>
    /// 巡回ルートを描画するメソッド
    /// </summary>
    /// <param name="spawner">描画する出現クラス</param>
    /// <param name="gizmoType">描画の種類(Unityから渡される)</param>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawPatrolRoute(CS_SecurityDroneSpawner spawner, GizmoType gizmoType)
    {
        if (!CSED_PoliceSquadGizmo.IsSelectedOrChildSelected(spawner.transform)) return;

        Transform[] points = CS_PoliceSquad.CollectChildPoints(spawner.transform, null);
        if (points.Length == 0) return;

        Gizmos.color = _routeColor;
        for (int i = 0; i < points.Length; i++)
        {
            Transform current = points[i];
            Transform next = points[(i + 1) % points.Length];
            if (current == null || next == null) continue;

            Gizmos.DrawSphere(current.position, _markerRadius);
            Gizmos.DrawLine(current.position, next.position);
        }
    }
}
