using UnityEngine;

/*
 * NPCの性格(強さ)のデータ
 * 仕様書「NPC(他プレイヤー)」・データ表「NPC(他プレイヤー)思考／行動指針データ」の Lv.1〜3 を、この数値で作り分ける
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・基本パラメーター(HP・移動速度・攻撃力など)はプレイヤーと同じ(CS_PlayerStatsをそのまま使う)
 *   性格で変えるのは「どれだけ敵の位置を把握できるか(索敵範囲)」と「頭の良さ(判断の間隔・反応の遅さ・行動の選び方)」だけ
 * ・行動は、行動ごとの点数(重み × 近さ)で一番高いものを選ぶ(CS_PlayerNpcBrain参照)
 *   重みを0にすると、その行動はしなくなる
 * ・実体: Assets/Programmer/Database/Npc/DB_NpcPersonality_Lv1〜3.asset
 *   データ表に具体的な数値がまだ無いため、すべて仮の値(遊んで要調整)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_NpcPersonality", menuName = "Player/NPC Personality")]
public class CSO_NpcPersonality : ScriptableObject
{
    [Header("基本")]
    [SerializeField] private string _displayName = "NPC";       // 表示名(リザルトなどで使う想定)
    [SerializeField][Min(1)] private int _level = 1;            // 強さ(1〜3)

    [Header("頭の良さ")]
    [SerializeField][Min(0.05f)] private float _decisionInterval = 0.5f;   // 行動を選び直す間隔(秒)
    [SerializeField][Min(0f)] private float _reactionDelay = 0.2f;         // 行動を変えてから動き出すまでの遅れ(秒)

    [Header("敵の位置の把握")]
    [SerializeField][Min(1f)] private float _senseRange = 30f;     // 悪人・アイテム・イベントに気付ける範囲(m)
    [SerializeField][Min(1f)] private float _fleeRange = 12f;      // 追ってくる警察から逃げ始める距離(m)

    [Header("行動の選びやすさ(重み。0でその行動をしない)")]
    [SerializeField][Min(0f)] private float _villainWeight = 1f;   // 悪人を倒しに行く
    [SerializeField][Min(0f)] private float _itemWeight = 0.5f;    // アイテムを拾いに行く
    [SerializeField][Min(0f)] private float _eventWeight = 0.8f;   // イベント(ケアパッケージなど)に参加する
    [SerializeField][Min(0f)] private float _harassWeight = 0f;    // 他のプレイヤーを邪魔しに行く
    [SerializeField][Min(0f)] private float _fleeWeight = 1.5f;    // 警察から逃げる
    [SerializeField][Min(0f)] private float _wanderWeight = 0.1f;  // 何も無い時にうろつく
    [SerializeField][Min(1f)] private float _harassRange = 20f;    // 邪魔しに行く相手を探す範囲(m)

    [Header("戦い方")]
    [SerializeField][Min(0.05f)] private float _attackInterval = 0.35f;   // 攻撃ボタンを押す間隔(秒)
    [SerializeField][Range(0f, 1f)] private float _attackMissChance = 0f; // 攻撃の向きがずれる確率(弱さの表現)
    [SerializeField][Range(0f, 1f)] private float _fleeHpRatio = 0.3f;    // HPがこの割合以下なら、逃げるのを優先する
    [SerializeField] private bool _skipsLateCrimes = false;   // 向かっても犯罪の完遂に間に合わない悪人には向かわない
    [SerializeField] private bool _usesSprint = true;         // 遠い目標へはダッシュ・スプリントで向かう

    [Header("必殺技")]
    [SerializeField][Min(1)] private int _specialMinTargets = 2;   // 範囲内にこの人数以上いたら必殺技を使う
    [SerializeField] private bool _savesSpecialForPlayers = false; // 必殺技を他のプレイヤーのために取っておく(悪人には使わない)

    [Header("アイテム")]
    [SerializeField][Range(0f, 1f)] private float _itemUseChance = 0.5f;   // 使いどころで、実際にアイテムを使う確率(判断1回ごと)
    [SerializeField] private bool _usesItemsOnPlayers = false;           // 他のプレイヤーへの妨害にアイテムを使う

    public string displayName => _displayName;
    public int level => _level;
    public float decisionInterval => _decisionInterval;
    public float reactionDelay => _reactionDelay;
    public float senseRange => _senseRange;
    public float fleeRange => _fleeRange;
    public float villainWeight => _villainWeight;
    public float itemWeight => _itemWeight;
    public float eventWeight => _eventWeight;
    public float harassWeight => _harassWeight;
    public float fleeWeight => _fleeWeight;
    public float wanderWeight => _wanderWeight;
    public float harassRange => _harassRange;
    public float attackInterval => _attackInterval;
    public float attackMissChance => _attackMissChance;
    public float fleeHpRatio => _fleeHpRatio;
    public bool skipsLateCrimes => _skipsLateCrimes;
    public bool usesSprint => _usesSprint;
    public int specialMinTargets => _specialMinTargets;
    public bool savesSpecialForPlayers => _savesSpecialForPlayers;
    public float itemUseChance => _itemUseChance;
    public bool usesItemsOnPlayers => _usesItemsOnPlayers;
}
