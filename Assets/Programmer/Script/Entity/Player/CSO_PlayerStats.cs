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

    public float maxHp => _maxHp;
    public float attackPower => _attackPower;
    public float moveSpeed => _moveSpeed;
    public float jumpPower => _jumpPower;

    private void OnValidate()
    {
        _maxHp = Mathf.Max(1f, _maxHp);
        _attackPower = Mathf.Max(0f, _attackPower);
        _moveSpeed = Mathf.Max(0f, _moveSpeed);
        _jumpPower = Mathf.Max(0f, _jumpPower);
    }
}
