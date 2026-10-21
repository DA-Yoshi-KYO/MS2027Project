/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 警察が標的を追いかける時に、どこを狙って向かうかを決めるクラス(警察1人につき1つ)
/// 追いかけ方は警察のステータス(CSO_PoliceStatus)で決まり、その警察は常にその方法で追いかける
/// ・追尾: 標的のいる位置を狙う
/// ・先回り: 標的の速度から、追いつく頃の位置を予測して狙う(標的の先へ回り込む)
///   予測の先は歩ける場所(NavMesh)の範囲内にとどめる。標的に近づいたら追尾に切り替え、標的を追い越さないようにする
///   標的がほぼ止まっている時も、追尾と同じ動きになる
/// 標的の速度は、狙う位置を求めるたびの位置の変化から測る(判断は一定間隔で行われるので、その間隔の平均になる)
/// </summary>
public class CS_PoliceChaseAim
{
    // 新しく測った速度を、今までの速度へ混ぜる割合(大きいほど、標的の動きの変化に早く追いつく)
    private const float _velocityBlend = 0.5f;

    // この時間(秒)以上、標的を測っていなければ、速度を測り直す(追いかけ直した時に、古い速度を使わない)
    private const float _resetGap = 1.0f;

    // 狙う位置の近くの歩ける場所(NavMesh)を探す距離(m)
    private const float _navSampleDistance = 2.5f;

    // 先回りで、速さが0に近い警察の速さで割らないようにする下限(m/秒)
    private const float _minSelfSpeed = 0.1f;

    private readonly CSE_PoliceChaseStyle _style;
    private readonly float _maxLeadTime;
    private readonly float _closeDistance;
    private readonly float _minTargetSpeed;

    // 速度を測っている標的と、前回測った位置・時刻・求めた速度(水平方向)
    private Transform _trackedTarget = null;
    private Vector3 _lastPosition = Vector3.zero;
    private float _lastTime = 0.0f;
    private Vector3 _velocity = Vector3.zero;

    // この警察の追いかけ方
    public CSE_PoliceChaseStyle style => _style;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="status">警察のステータス(追いかけ方と、その調整値を読む)</param>
    public CS_PoliceChaseAim(CSO_PoliceStatus status)
    {
        _style = status.chaseStyle;
        _maxLeadTime = status.interceptMaxLeadTime;
        _closeDistance = status.interceptCloseDistance;
        _minTargetSpeed = status.interceptMinTargetSpeed;
    }

    /// <summary>
    /// 標的を追いかける時に狙う位置を求めるメソッド(追いかけている間、判断のたびに呼ぶ)
    /// </summary>
    /// <param name="target">標的</param>
    /// <param name="selfPosition">警察の位置</param>
    /// <param name="selfSpeed">警察の追跡速度(m/秒)</param>
    /// <returns>狙う位置</returns>
    public Vector3 GetAimPosition(Transform target, Vector3 selfPosition, float selfSpeed)
    {
        if (_style != CSE_PoliceChaseStyle.Intercept) return target.position;

        UpdateVelocity(target);

        // 近い・標的がほぼ止まっている時は、先回りせずに標的の位置を狙う
        Vector3 toTarget = target.position - selfPosition;
        toTarget.y = 0.0f;
        if (toTarget.magnitude <= _closeDistance || _velocity.magnitude < _minTargetSpeed) return target.position;

        // 追いつくまでにかかる時間(上限あり)の分だけ、標的が動いた先を狙う
        float leadTime = Mathf.Min(toTarget.magnitude / Mathf.Max(selfSpeed, _minSelfSpeed), _maxLeadTime);
        return ClampToNavMesh(target.position, target.position + _velocity * leadTime);
    }

    /// <summary>
    /// 標的の位置の変化から、標的の速度(水平方向)を測るメソッド
    /// </summary>
    private void UpdateVelocity(Transform target)
    {
        float now = Time.time;
        bool isNewTarget = target != _trackedTarget || now - _lastTime > _resetGap;
        if (isNewTarget)
        {
            _trackedTarget = target;
            _velocity = Vector3.zero;
        }
        else if (now - _lastTime > 0.0001f)
        {
            Vector3 measured = (target.position - _lastPosition) / (now - _lastTime);
            measured.y = 0.0f;
            _velocity = Vector3.Lerp(_velocity, measured, _velocityBlend);
        }

        _lastPosition = target.position;
        _lastTime = now;
    }

    /// <summary>
    /// 狙う位置を、標的の位置から歩ける場所(NavMesh)の範囲内にとどめるメソッド
    /// 壁などで歩けなくなる所までしか進まない(壁の向こうを狙わない)
    /// </summary>
    /// <param name="origin">標的の位置</param>
    /// <param name="aim">予測した位置</param>
    /// <returns>歩ける範囲内の、狙う位置</returns>
    private static Vector3 ClampToNavMesh(Vector3 origin, Vector3 aim)
    {
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, _navSampleDistance, NavMesh.AllAreas)) return origin;

        // 途中で歩けなくなる場所があれば、そこまで。無ければ、予測した位置の近くの歩ける場所
        if (NavMesh.Raycast(start.position, aim, out NavMeshHit edge, NavMesh.AllAreas)) return edge.position;
        return NavMesh.SamplePosition(aim, out NavMeshHit end, _navSampleDistance, NavMesh.AllAreas) ? end.position : origin;
    }
}
