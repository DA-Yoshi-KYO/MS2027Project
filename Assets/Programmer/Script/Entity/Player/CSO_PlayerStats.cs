using UnityEngine;

/*
 * プレイヤーステータスの基準値(ScriptableObject)
 * 実行中に変化する「現在値」はCS_PlayerStatsが持つ。これはあくまで初期値・リセット先
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・数値だけ変えたい場合は、右クリック → Create → Player → Player Stats でアセットを複製する
 * ・attackPower は CSO_AttackData.damage に掛ける倍率として使う(既定 1 = 等倍)
 * ・jumpPower は値の保持のみ。ジャンプ処理自体は未実装
 * ・dashSpeed はダッシュ中の速度。継続時間・クールタイムはCS_Player側の固定値で調整する
 * ・maxGauge は必殺ゲージの上限。現在値はCS_PlayerSpecialGaugeが持つが、
 *   上限だけはこの基準値からCS_PlayerStatsが持つ(maxHpと同じ考え方)
 * ・specialAttackPower は必殺技のダメージに掛ける倍率(通常攻撃のattackPowerとは別枠)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_PlayerStats", menuName = "Player/Player Stats")]
public class CSO_PlayerStats : ScriptableObject
{
    [Header("基礎値")]
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _attackPower = 1f;    // CSO_AttackData.damage に掛ける倍率
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpPower = 5f;      // 未実装のジャンプ機能で使用予定
    [SerializeField] private float _dashSpeed = 12f;

    [Header("必殺技")]
    [SerializeField] private float _maxGauge = 100f;
    [SerializeField] private float _specialAttackPower = 1f;   // 必殺技のダメージに掛ける倍率

    public float maxHp => _maxHp;
    public float attackPower => _attackPower;
    public float moveSpeed => _moveSpeed;
    public float jumpPower => _jumpPower;
    public float dashSpeed => _dashSpeed;
    public float maxGauge => _maxGauge;
    public float specialAttackPower => _specialAttackPower;

    private void OnValidate()
    {
        _maxHp = Mathf.Max(1f, _maxHp);
        _attackPower = Mathf.Max(0f, _attackPower);
        _moveSpeed = Mathf.Max(0f, _moveSpeed);
        _jumpPower = Mathf.Max(0f, _jumpPower);
        _dashSpeed = Mathf.Max(0f, _dashSpeed);
        _maxGauge = Mathf.Max(1f, _maxGauge);
        _specialAttackPower = Mathf.Max(0f, _specialAttackPower);
    }
}
