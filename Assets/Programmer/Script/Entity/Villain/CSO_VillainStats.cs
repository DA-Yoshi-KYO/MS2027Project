using UnityEngine;

/*
 * 悪人ステータスの基準値(ScriptableObject)
 * 実行中に変化する「現在値」はCS_VillainStatsが持つ。これはあくまで初期値・リセット先
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・悪人の種類(A, Bなど)ごとにアセットを作る
 *   右クリック → Create → Villain → Villain Stats
 * ・moveSpeedMultiplier はプレイヤーの通常移動速度を1とした倍率
 * ・attackPower はプレイヤーに与えるダメージ量(プレイヤーのHPと同じ単位)
 * ・engageRange はプレイヤーがこの距離(m)以内に入ると臨戦態勢になる範囲
 * ・crimeCompleteTime は放置されてから犯罪を完遂するまでの時間(秒)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_VillainStats", menuName = "Villain/Villain Stats")]
public class CSO_VillainStats : ScriptableObject
{
    [Header("基礎値")]
    [SerializeField] private float _maxHp = 3f;
    [SerializeField] private float _moveSpeedMultiplier = 1f;   // プレイヤーの通常移動速度を1とした倍率
    [SerializeField] private float _attackPower = 1f;           // プレイヤーに与えるダメージ量

    [Header("行動")]
    [SerializeField] private float _engageRange = 3f;           // 臨戦態勢になる範囲(m)
    [SerializeField] private float _crimeCompleteTime = 30f;    // 犯罪完遂までの時間(秒)

    public float maxHp => _maxHp;
    public float moveSpeedMultiplier => _moveSpeedMultiplier;
    public float attackPower => _attackPower;
    public float engageRange => _engageRange;
    public float crimeCompleteTime => _crimeCompleteTime;

    private void OnValidate()
    {
        _maxHp = Mathf.Max(1f, _maxHp);
        _moveSpeedMultiplier = Mathf.Max(0f, _moveSpeedMultiplier);
        _attackPower = Mathf.Max(0f, _attackPower);
        _engageRange = Mathf.Max(0f, _engageRange);
        _crimeCompleteTime = Mathf.Max(0f, _crimeCompleteTime);
    }
}
