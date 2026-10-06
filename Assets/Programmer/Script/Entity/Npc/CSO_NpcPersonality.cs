using UnityEngine;

/*
 * NPCの性格(ライバル/雑魚/妨害など)ごとのデータ(ScriptableObject)
 * 基本パラメーター(HP・移動速度など)はプレイヤーと同じで、ここでは「頭の良さ」と「行動の好み」だけを持つ
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・性格ごとにアセットを作る(右クリック → Create → Npc → Npc Personality)
 *   実体: Assets/Programmer/Database/Npc/DB_NpcRival / DB_NpcWeak / DB_NpcHarasser
 * ・数値はデータ表にNPCのデータが出るまでの仮(プランナーに確認中)
 * ・行動の選びやすさ(〇〇Weight)は、各行動の「やる価値(0〜1)」に掛ける重み
 *   一番大きい行動が選ばれるので、性格の違いはこの重みの差で表す(0にするとその行動をしない)
 * ・強さの調整(#167)は、強さの段階(0〜levelCount-1)で判断の速さ・攻撃の間隔・迷いやすさ・悪人を狙う積極性を変える
 *   段階が高いほど強い。数値は「一番弱い段階」と「一番強い段階」の値を持ち、その間は段階に応じて補間する
 */
// ========================================

[CreateAssetMenu(fileName = "DB_NpcPersonality", menuName = "Npc/Npc Personality")]
public class CSO_NpcPersonality : ScriptableObject
{
    [Header("表示")]
    [SerializeField] private string _displayName = "NPC";             // 表示名・リザルトでの判別用

    [Header("判断")]
    [SerializeField] private float _reactionDelay = 0.2f;             // 行動を切り替えてから、ボタンを押し始めるまでの遅れ(秒)

    [Header("見える範囲(m)")]
    [SerializeField] private float _villainSightRange = 25f;          // 悪人の位置が分かる範囲
    [SerializeField] private float _itemSightRange = 20f;             // アイテムの位置が分かる範囲
    [SerializeField] private float _playerSightRange = 30f;           // 他のプレイヤーの位置が分かる範囲
    [SerializeField] private float _policeSightRange = 12f;           // 警察の位置が分かる範囲

    [Header("行動の選びやすさ")]
    [SerializeField] private float _huntVillainWeight = 1f;           // 悪人を探して倒す
    [SerializeField] private float _seekItemWeight = 0.5f;            // アイテムを拾いに行く
    [SerializeField] private float _harassWeight = 0f;                // 他のプレイヤーの周りをうろつく(妨害)
    [SerializeField] private float _fleePoliceWeight = 1f;            // 警察から逃げる
    [SerializeField] private float _wanderWeight = 0.1f;              // 当てもなくうろつく(他にやることが無い時)

    [Header("戦闘")]
    [SerializeField] private float _attackRange = 1.8f;               // この距離まで近づいたら攻撃する(m)
    [SerializeField] private float _policeAvoidRange = 8f;            // この距離に警察がいる間は変身しない(m)
    [SerializeField] private int _specialMinTargets = 2;              // 必殺技の範囲にこの人数以上の悪人がいたら使う
    [SerializeField] private float _specialRange = 4f;                // 必殺技を当てられるとみなす距離(m)

    [Header("強さの調整")]
    [SerializeField] private CSE_NpcDifficultyMode _difficultyMode = CSE_NpcDifficultyMode.None;
    [SerializeField] private float _adjustInterval = 5f;              // スコア差を見る間隔(秒)
    [SerializeField] private int _scoreThreshold = 150;               // ライバル: これ以上スコアが離れたら段階を変える
    [SerializeField] private int _weakScoreMargin = 50;               // 雑魚: 一番低い人のスコアからこれだけ下を保つ
    [SerializeField] private int _levelCount = 5;                     // 強さの段階の数
    [SerializeField] private int _startLevel = 2;                     // 最初の段階(0始まり)

    [Header("強さの段階ごとの値(一番弱い段階 → 一番強い段階)")]
    [SerializeField] private Vector2 _thinkInterval = new Vector2(0.8f, 0.2f);       // 状況を判断する間隔(秒)
    [SerializeField] private Vector2 _attackInterval = new Vector2(1.0f, 0.3f);      // 攻撃ボタンを押す間隔(秒)
    [SerializeField] private Vector2 _hesitationChance = new Vector2(0.4f, 0f);      // 判断のたびに迷って立ち止まる確率(0〜1)
    [SerializeField] private Vector2 _huntAggressiveness = new Vector2(0.5f, 1.5f);  // 悪人を狙う積極性(huntVillainWeightに掛ける)

    public string displayName => _displayName;
    public float reactionDelay => _reactionDelay;

    public float villainSightRange => _villainSightRange;
    public float itemSightRange => _itemSightRange;
    public float playerSightRange => _playerSightRange;
    public float policeSightRange => _policeSightRange;

    public float huntVillainWeight => _huntVillainWeight;
    public float seekItemWeight => _seekItemWeight;
    public float harassWeight => _harassWeight;
    public float fleePoliceWeight => _fleePoliceWeight;
    public float wanderWeight => _wanderWeight;

    public float attackRange => _attackRange;
    public float policeAvoidRange => _policeAvoidRange;
    public int specialMinTargets => _specialMinTargets;
    public float specialRange => _specialRange;

    public CSE_NpcDifficultyMode difficultyMode => _difficultyMode;
    public float adjustInterval => _adjustInterval;
    public int scoreThreshold => _scoreThreshold;
    public int weakScoreMargin => _weakScoreMargin;
    public int levelCount => _levelCount;
    public int startLevel => _startLevel;

    // 強さ(0: 一番弱い 〜 1: 一番強い)に応じた値
    public float GetThinkInterval(float strength) => Mathf.Lerp(_thinkInterval.x, _thinkInterval.y, strength);
    public float GetAttackInterval(float strength) => Mathf.Lerp(_attackInterval.x, _attackInterval.y, strength);
    public float GetHesitationChance(float strength) => Mathf.Lerp(_hesitationChance.x, _hesitationChance.y, strength);
    public float GetHuntAggressiveness(float strength) => Mathf.Lerp(_huntAggressiveness.x, _huntAggressiveness.y, strength);

    private void OnValidate()
    {
        _reactionDelay = Mathf.Max(0f, _reactionDelay);
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _specialMinTargets = Mathf.Max(1, _specialMinTargets);
        _adjustInterval = Mathf.Max(0.5f, _adjustInterval);
        _levelCount = Mathf.Max(1, _levelCount);
        _startLevel = Mathf.Clamp(_startLevel, 0, _levelCount - 1);
    }
}
