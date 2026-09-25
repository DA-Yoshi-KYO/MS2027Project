/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | チャージしてから攻撃判定を生成する方式に変更
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察の攻撃を管理するクラス
/// 標的が攻撃範囲に入ったらチャージを始め、溜め終わったら一定時間残る攻撃判定(CS_PoliceAttackHitbox)を生成する
/// チャージを始めた後は、標的が範囲外に離れても中断せずに攻撃する(その場合は空振りになる)
/// </summary>
public class CS_PoliceAttack : MonoBehaviour
{
    // 攻撃力
    private float _attackPower = 0.0f;

    // 次にチャージを始められるまでの残り時間
    private float _cooldownTimer = 0.0f;

    // チャージ中か・チャージが終わるまでの残り時間
    private bool _isCharging = false;
    private float _chargeTimer = 0.0f;

    // チャージを始めた時に標的がいた位置(攻撃判定はこの位置に向かって出す)
    private Vector3 _chargeTargetPosition = Vector3.zero;

    // 初期化処理を行わずに攻撃するのを防ぐためのフラグ
    private bool _isInitialized = false;

    [Header("＝＝＝ 攻撃 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("チャージを始める距離(警察と標的の中心同士の水平距離)")]
    private float _attackRange = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃判定を出してから、次のチャージを始められるまでの間隔(秒)")]
    private float _attackInterval = 1.0f;

    [SerializeField, Min(1f)]
    [Tooltip("攻撃をするまでの溜め時間(秒)")]
    private float _attackChargeTime = 2.0f;

    [Header("＝＝＝ 攻撃判定 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("攻撃判定を出す位置(警察の中心から標的の方向へどれだけ離すか)")]
    private float _hitboxForwardOffset = 1.0f;

    [SerializeField, Min(0.1f)]
    [Tooltip("攻撃判定の球の半径")]
    private float _hitboxRadius = 1.0f;

    [SerializeField, Min(0.1f)]
    [Tooltip("攻撃判定が残る時間(秒)")]
    private float _hitboxLifetime = 1.0f;

    // チャージを始める距離
    public float attackRange => _attackRange;

    // チャージ中か
    public bool isCharging => _isCharging;

    // チャージを始めた時に標的がいた位置
    public Vector3 chargeTargetPosition => _chargeTargetPosition;

    /// <summary>
    /// 警察の攻撃に関する初期化メソッド
    /// </summary>
    /// <param name="attackPower">攻撃力</param>
    public void Setting(float attackPower)
    {
        _attackPower = attackPower;
        _isInitialized = true;
    }

    private void Update()
    {
        if (_cooldownTimer > 0.0f) _cooldownTimer -= Time.deltaTime;

        if (!_isCharging) return;

        // チャージを始めた後は、標的が範囲外に離れても中断せずに溜め終わるまで待つ
        _chargeTimer -= Time.deltaTime;
        if (_chargeTimer > 0.0f) return;

        _isCharging = false;
        SpawnHitbox();
        _cooldownTimer = _attackInterval;
    }

    /// <summary>
    /// 標的が攻撃範囲内にいれば、攻撃のチャージを始めるメソッド
    /// </summary>
    /// <param name="target">攻撃する標的(HPを持つオブジェクト)</param>
    /// <returns>チャージを始めた場合はtrue</returns>
    public bool TryStartCharge(Transform target)
    {
        if (!_isInitialized || target == null || _isCharging || _cooldownTimer > 0.0f) return false;

        // 高さの差は無視し、水平方向の距離で攻撃範囲を判定する
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0.0f;
        if (toTarget.sqrMagnitude > _attackRange * _attackRange) return false;

        // 壁越しの標的にはチャージを始めない(仲間から共有された標的は、自分からは見えていないことがある)
        if (!CS_PoliceLineOfSight.IsClear(transform.position, GetTargetCenter(target), target)) return false;

        _isCharging = true;
        _chargeTimer = _attackChargeTime;
        _chargeTargetPosition = target.position;
        return true;
    }

    /// <summary>
    /// チャージを始めた時に標的がいた位置に向かって、攻撃判定を生成するメソッド
    /// </summary>
    private void SpawnHitbox()
    {
        Vector3 center = transform.position + GetAttackDirection() * _hitboxForwardOffset;

        // 攻撃判定には攻撃を出した位置(警察の中心)を渡し、そこから壁越しになる相手には当たらないようにする
        CS_PoliceAttackHitbox.Create(center, transform.position, _attackPower, _hitboxRadius, _hitboxLifetime);
    }

    /// <summary>
    /// 標的の中心(コライダーがあればその中心、無ければTransformの位置)を取得するメソッド
    /// </summary>
    /// <param name="target">標的</param>
    /// <returns>標的の中心</returns>
    private Vector3 GetTargetCenter(Transform target)
    {
        return target.TryGetComponent(out Collider targetCollider) ? targetCollider.bounds.center : target.position;
    }

    /// <summary>
    /// 攻撃判定を出す向き(水平方向)を取得するメソッド
    /// 警察の現在位置から、チャージを始めた時に標的がいた位置へ向かう
    /// </summary>
    /// <returns>攻撃判定を出す向き(長さ1)</returns>
    private Vector3 GetAttackDirection()
    {
        Vector3 direction = _chargeTargetPosition - transform.position;
        direction.y = 0.0f;

        // チャージ中に警察がその位置まで移動していた場合など、水平方向の向きが無い場合は警察の正面を使う
        if (direction.sqrMagnitude < 0.0001f) direction = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
        return direction.normalized;
    }
}
