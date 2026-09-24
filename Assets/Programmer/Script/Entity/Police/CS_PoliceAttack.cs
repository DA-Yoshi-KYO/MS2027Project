/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察の攻撃を管理するクラス
/// 標的が攻撃範囲内にいれば、IDamageable経由で一撃分のダメージを与える(単発攻撃)
/// </summary>
public class CS_PoliceAttack : MonoBehaviour
{
    // 攻撃力
    private float _attackPower = 0.0f;

    // 次に攻撃できるまでの残り時間
    private float _cooldownTimer = 0.0f;

    // 初期化処理を行わずに攻撃するのを防ぐためのフラグ
    private bool _isInitialized = false;

    [Header("＝＝＝ 攻撃 ＝＝＝")]
    [SerializeField, Min(0f)]
    [Tooltip("攻撃が届く距離(警察と標的の中心同士の水平距離)")]
    private float _attackRange = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("次の攻撃までの間隔(秒)")]
    private float _attackInterval = 1.0f;

    // 攻撃が届く距離
    public float attackRange => _attackRange;

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
    }

    /// <summary>
    /// 標的が攻撃範囲内にいれば攻撃するメソッド
    /// </summary>
    /// <param name="target">攻撃する標的(HPを持つオブジェクト)</param>
    /// <returns>攻撃した場合はtrue</returns>
    public bool TryAttack(Transform target)
    {
        if (!_isInitialized || target == null || _cooldownTimer > 0.0f) return false;

        // 高さの差は無視し、水平方向の距離で攻撃範囲を判定する
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0.0f;
        if (toTarget.sqrMagnitude > _attackRange * _attackRange) return false;

        if (!target.TryGetComponent(out IDamageable damageable)) return false;

        damageable.TakeDamage(_attackPower);
        _cooldownTimer = _attackInterval;
        return true;
    }
}
