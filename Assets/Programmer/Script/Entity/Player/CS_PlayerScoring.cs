using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのスコアの点数を計算し、CS_PlayerResultDataHolderへ加算するクラス
 * 暗躍ボーナス(連続撃破ボーナス)もここで扱う
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・点数(仕様書「ゲームループ」のスコア、データ表の悪人の撃破スコア)
 *   悪人を倒した    : +その悪人の撃破スコア × 暗躍ボーナスの倍率
 *                     撃破スコアは相手のIDefeatScoreから読む(無ければ_defaultVillainScore。データ表: A 100 / B 150)
 *   犯罪を完遂された: -_crimeCompletedPenalty(既定20)。どの悪人グループの犯罪でも、全プレイヤーが減点される
 *   警察に倒された  : -_killedByPolicePenalty(既定50)
 *   他プレイヤーを倒した: +_defeatPlayerScore(仕様に点数が無いため既定0。回数は数える)
 * ・誰が倒したかは、攻撃側(CS_PlayerAttack / CS_PlayerSpecialAttack)がDealDamage経由でダメージを与え、
 *   「当てる前は生きていた相手が、当てた後に倒れたか」で判定する(悪人側・警察側のコードは変えない)
 * ・暗躍ボーナス(仕様書「プレイヤー」)
 *   連続撃破数が_bonusStartCount人目(既定5)からは、倍率が_bonusStep(既定0.2)ずつ上がる
 *   (4人目まで×1、5人目×1.2、6人目×1.4…)
 *   次のどれかで連続撃破数を0に戻す(次の撃破が1人目になる)
 *     変身を解く(CS_PlayerTransformationの状態が通常に戻る) / HPが0になる / 警察に見つかる(ReportFoundByPolice)
 * ・警察に見つかったこと・警察に倒されたことは、警察側からの通知が必要
 *   見つかった: 警察側がReportFoundByPolice()を呼ぶ
 *   倒された  : 警察側が攻撃者付きのTakeDamage(damage, 警察のGameObject)でダメージを与える
 *              (CS_PlayerHealth.lastAttackerに警察(CS_PoliceBrainを持つもの)が入っていれば、警察に倒されたとみなす)
 * ・スコアはサーバーだけが計算する(攻撃判定・悪人の処理がサーバーで行われるため)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerResultDataHolder))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerTransformation))]
public class CS_PlayerScoring : NetworkBehaviour
{
    [Header("悪人撃破")]
    [SerializeField] private int _defaultVillainScore = 100;    // 相手が撃破スコア(IDefeatScore)を持たない時の点数

    [Header("暗躍ボーナス")]
    [SerializeField] private int _bonusStartCount = 5;          // 倍率が上がり始める連続撃破数(この人数目から)
    [SerializeField] private float _bonusStep = 0.2f;           // 1人ごとに上がる倍率

    [Header("減点")]
    [SerializeField] private int _crimeCompletedPenalty = 20;   // 犯罪を完遂された時に減る点数
    [SerializeField] private int _killedByPolicePenalty = 50;   // 警察に倒された時に減る点数

    [Header("他プレイヤー撃破")]
    [SerializeField] private int _defeatPlayerScore = 0;        // 他プレイヤーを倒した時の点数(仕様未定のため既定0)

    private CS_PlayerResultDataHolder _holder;
    private CS_PlayerHealth _health;
    private CS_PlayerTransformation _transformation;

    private int _streak;    // 暗躍ボーナスの連続撃破数(サーバー、またはオフラインでのみ意味を持つ)

    public int streak => _streak;

    private void Awake()
    {
        _holder = GetComponent<CS_PlayerResultDataHolder>();
        _health = GetComponent<CS_PlayerHealth>();
        _transformation = GetComponent<CS_PlayerTransformation>();

        _health.onDeath += HandleDeath;
        _transformation.onStateChanged += HandleTransformStateChanged;
        CS_VillainGroup.onAnyCrimeCompleted += HandleCrimeCompleted;
    }

    public override void OnDestroy()
    {
        if (_health != null) _health.onDeath -= HandleDeath;
        if (_transformation != null) _transformation.onStateChanged -= HandleTransformStateChanged;
        CS_VillainGroup.onAnyCrimeCompleted -= HandleCrimeCompleted;

        base.OnDestroy();
    }

    // 攻撃者として相手にダメージを与え、倒したらスコアに反映する(サーバー、またはオフラインで呼ぶ)
    public void DealDamage(IDamageable target, float damage)
    {
        bool wasAlive = IsAlive(target);
        target.TakeDamage(damage, gameObject);

        if (!wasAlive || IsAlive(target)) return;

        HandleDefeated(target);
    }

    // 警察に見つかった時に、警察側から呼んでもらう(サーバー、またはオフラインのみ)
    public void ReportFoundByPolice()
    {
        if (!CanScore()) return;

        ResetStreak();
    }

    // 相手を倒した時の加算
    private void HandleDefeated(IDamageable target)
    {
        if (!CanScore()) return;

        if (target is CS_VillainHealth villain)
        {
            _streak++;
            int point = Mathf.RoundToInt(GetDefeatScore(villain) * GetBonusMultiplier(_streak));
            _holder.AddDefeatVillain(point);
            return;
        }

        // 自分自身への攻撃は数えない
        if (target is CS_PlayerHealth player && player != _health)
        {
            _holder.AddDefeatPlayer(_defeatPlayerScore);
        }
    }

    // 連続撃破数に応じた暗躍ボーナスの倍率(_bonusStartCount人目から_bonusStepずつ上がる)
    private float GetBonusMultiplier(int count)
    {
        if (count < _bonusStartCount) return 1f;

        return 1f + _bonusStep * (count - _bonusStartCount + 1);
    }

    // 倒した悪人の撃破スコア(悪人側がIDefeatScoreを持っていなければ既定値)
    private int GetDefeatScore(CS_VillainHealth villain)
    {
        IDefeatScore source = villain.GetComponentInParent<IDefeatScore>();
        return source != null ? source.defeatScore : _defaultVillainScore;
    }

    // HPが0になったら連続撃破を途切れさせ、警察に倒された場合は減点する
    private void HandleDeath()
    {
        if (!CanScore()) return;

        ResetStreak();

        GameObject attacker = _health.lastAttacker;
        if (attacker != null && attacker.GetComponentInParent<CS_PoliceBrain>() != null)
        {
            _holder.AddFoundByPolice(-_killedByPolicePenalty);
        }
    }

    // 変身を解いたら(通常の状態に戻ったら)連続撃破を途切れさせる
    private void HandleTransformStateChanged(CSE_PlayerTransformState state)
    {
        if (!CanScore()) return;
        if (state != CSE_PlayerTransformState.Normal) return;

        ResetStreak();
    }

    // どこかの悪人グループに犯罪を完遂されたら減点する
    private void HandleCrimeCompleted(CS_VillainGroup group)
    {
        if (!CanScore()) return;

        _holder.AddCrimeCompleted(-_crimeCompletedPenalty);
    }

    private void ResetStreak()
    {
        _streak = 0;
    }

    // スコアを計算してよいか(サーバー、またはオフラインのみ)
    private bool CanScore()
    {
        return !IsSpawned || IsServer;
    }

    // 倒されていないか(悪人・プレイヤー以外は倒れることが無いものとして扱う)
    private static bool IsAlive(IDamageable target)
    {
        switch (target)
        {
            case CS_VillainHealth villain:
                return !villain.isDefeated;
            case CS_PlayerHealth player:
                return !player.isDead;
            default:
                return true;
        }
    }
}
