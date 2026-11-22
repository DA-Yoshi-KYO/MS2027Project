using UnityEngine;
using UnityEngine.AI;

/*
 * スポーン位置で使う巡回パターン(CSO_VillainPatrolPattern)を指定するクラス
 * スポーン位置(CS_VillainSpawnPoint)と同じオブジェクトに付ける
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・巡回はSceneビューの巡回エディタで作る(スポーン位置を選ぶとツールバーが出る。ClaudeDocs/巡回エディタの使い方.md)
 *   「悪人を置く」を押すと、このコンポーネントとパターンのアセットが無ければ自動で作る
 * ・パターンのポイントはスポーン位置からの相対位置。ワールド座標への変換はこのクラスで行う
 *   スポーン位置の向き(Y軸の回転)だけを掛け、Scaleは掛けない(犯罪完遂ゲージがスポーン位置のScaleを変えるため)
 * ・パターンが無い・ルートが1本も無いスポーン位置は、今までどおり路地裏の両端を全員で往復する(CS_VillainSpawnPoint.CreatePatrolPaths)
 */
// ========================================

[RequireComponent(typeof(CS_VillainSpawnPoint))]
public class CS_VillainPatrolRoute : MonoBehaviour
{
    private const float _navMeshSampleRadius = 2f;   // ポイントをNavMeshの上に合わせる時に探す半径(m)

    [SerializeField]
    [Tooltip("このスポーン位置で使う巡回パターン。同じパターンを別のスポーン位置でも使える")]
    private CSO_VillainPatrolPattern _pattern;

    public CSO_VillainPatrolPattern pattern
    {
        get => _pattern;
        set => _pattern = value;
    }

    // 巡回パターンを使うか(犯罪実行役か、ポイントのあるルートが1つ以上ある)
    public bool hasPattern => _pattern != null && _pattern.slotCount > 0;

    // スポーン位置の向き(Y軸の回転だけ)
    public Quaternion yawRotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

    // スポーン位置からの相対位置 → ワールド座標
    public Vector3 ToWorld(Vector3 localPosition)
    {
        return transform.position + yawRotation * localPosition;
    }

    // ワールド座標 → スポーン位置からの相対位置
    public Vector3 ToLocal(Vector3 worldPosition)
    {
        return Quaternion.Inverse(yawRotation) * (worldPosition - transform.position);
    }

    // ルートのポイントのワールド座標(NavMeshの上に合わせる。見つからない時はそのままの位置)
    public Vector3 GetPointWorldPosition(CSO_VillainPatrolPattern.Route route, int pointIndex)
    {
        return SnapToNavMesh(ToWorld(route.points[pointIndex].localPosition));
    }

    // ルートの悪人が湧いた時の向き(ワールド)
    public Quaternion GetStartRotation(CSO_VillainPatrolPattern.Route route)
    {
        return yawRotation * Quaternion.Euler(0f, route.startAngle, 0f);
    }

    // 先頭が湧く位置(ポイント1)。付いてくる人の位置はCS_VillainPatrolCrew.GetSpawnPositionで求める(ルートの上に並べるため)
    public Vector3 GetStartPosition(int routeIndex)
    {
        return GetPointWorldPosition(_pattern.routes[routeIndex], 0);
    }

    // ---- 犯罪実行役 ----

    // 実行役の位置(運ぶ時は貨物の位置。NavMeshの上)
    public Vector3 GetExecutorPosition(int executorIndex)
    {
        return SnapToNavMesh(ToWorld(_pattern.executors[executorIndex].localPosition));
    }

    // 運ぶ実行役の、車の位置(NavMeshの上)
    public Vector3 GetExecutorCarryPosition(int executorIndex)
    {
        return SnapToNavMesh(ToWorld(_pattern.executors[executorIndex].carryLocalPosition));
    }

    // 実行役の向き(ワールド)
    public Quaternion GetExecutorRotation(int executorIndex)
    {
        return yawRotation * Quaternion.Euler(0f, _pattern.executors[executorIndex].angle, 0f);
    }

    // 実行役の犯罪モーション。「エリアに合わせる」なら、スポーン位置の担当のエリアのモーション(エリアが無ければ蹴る)
    public CSE_VillainCrimeMotion GetExecutorMotion(int executorIndex)
    {
        CSE_VillainCrimeMotion motion = _pattern.executors[executorIndex].motion;
        if (motion != CSE_VillainCrimeMotion.Auto) return motion;

        CS_VillainSpawnPoint spawnPoint = GetComponent<CS_VillainSpawnPoint>();
        CS_CrimeArea area = spawnPoint != null ? spawnPoint.crimeArea : null;
        return area != null && area.crimeMotion != CSE_VillainCrimeMotion.Auto ? area.crimeMotion : CSE_VillainCrimeMotion.Kick;
    }

    public static Vector3 SnapToNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSampleRadius, NavMesh.AllAreas) ? hit.position : position;
    }
}
