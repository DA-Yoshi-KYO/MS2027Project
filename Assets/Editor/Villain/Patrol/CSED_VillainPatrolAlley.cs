using UnityEngine;

/*
 * 巡回エディタで使う、路地裏の範囲(スポーン位置のPatrol Area)の座標
 * 路地裏の中の位置を、縦(長い方向)・横(短い方向)の割合(0〜1)で表す
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・路地裏の範囲は CS_VillainSpawnPoint.patrolArea(NavMeshModifierVolumeの箱。空なら自動で探したもの)
 * ・縦: 箱の長い方向 / 横: 短い方向。どちらも、箱のローカル座標の − 側の端が0、＋ 側の端が1
 *   (路地裏の外は、0未満・1より大きい値になる)
 * ・高さは割合に入れない(位置の高さはそのまま。ゲーム中にNavMeshの上に合わせる)
 * ・Sceneビューでは、位置の基準が「路地裏」の時に、箱の枠と 縦0・縦1・横1 の目印を描く(CSED_VillainPatrolSceneGUI)
 */
// ========================================

public struct CSED_VillainPatrolAlley
{
    public Vector3 origin;   // 縦0・横0の角(ワールド)
    public Vector3 length;   // 縦0 → 縦1(ワールド。水平)
    public Vector3 width;    // 横0 → 横1(ワールド。水平)

    public Vector3 center => origin + (length + width) * 0.5f;

    // スポーン位置の路地裏の範囲。無ければfalse
    public static bool TryGet(CS_VillainSpawnPoint spawnPoint, out CSED_VillainPatrolAlley alley)
    {
        alley = default;
        if (spawnPoint == null || spawnPoint.patrolArea == null) return false;

        Unity.AI.Navigation.NavMeshModifierVolume area = spawnPoint.patrolArea;
        Transform areaTransform = area.transform;
        Vector3 center = areaTransform.TransformPoint(area.center);
        Vector3 halfX = Flat(areaTransform.TransformVector(Vector3.right * area.size.x * 0.5f));
        Vector3 halfZ = Flat(areaTransform.TransformVector(Vector3.forward * area.size.z * 0.5f));
        bool isXLong = halfX.sqrMagnitude >= halfZ.sqrMagnitude;
        Vector3 halfLength = isXLong ? halfX : halfZ;
        Vector3 halfWidth = isXLong ? halfZ : halfX;
        if (halfLength.sqrMagnitude <= 0f || halfWidth.sqrMagnitude <= 0f) return false;

        center.y = spawnPoint.transform.position.y;
        alley.origin = center - halfLength - halfWidth;
        alley.length = halfLength * 2f;
        alley.width = halfWidth * 2f;
        return true;
    }

    // ワールド座標 → 割合(x: 縦 / y: 横)
    public Vector2 ToRatio(Vector3 world)
    {
        Vector3 offset = Flat(world - origin);
        return new Vector2(Vector3.Dot(offset, length) / length.sqrMagnitude, Vector3.Dot(offset, width) / width.sqrMagnitude);
    }

    // 割合(x: 縦 / y: 横) → ワールド座標(高さはheight)
    public Vector3 FromRatio(Vector2 ratio, float height)
    {
        Vector3 world = origin + length * ratio.x + width * ratio.y;
        world.y = height;
        return world;
    }

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
