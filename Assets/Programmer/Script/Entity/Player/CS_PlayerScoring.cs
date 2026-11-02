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
 *   警察に見つかった: -_foundByPolicePenalty(既定50)。警察が新しく自分を標的にするたびに減点される
 *                     ただし一度見つかったら_foundByPoliceInterval秒(既定5)は見つかっていない扱いにする
 *                     (その間は減点も、暗躍ボーナスの途切れも起きない。複数の警察に続けて見つかっても1回分)
 *   他プレイヤーを倒した: +_defeatPlayerScore(仕様に点数が無いため既定0。回数は数える)
 *   ※ 警察に倒されたこと自体では減点しない(仕様の減点は「見つかった数」のため)
 * ・誰が倒したかは、攻撃側(CS_PlayerAttack / CS_PlayerSpecialAttack)がDealDamage経由でダメージを与え、
 *   「当てる前は生きていた相手が、当てた後に倒れたか」で判定する(悪人側・警察側のコードは変えない)
 * ・暗躍ボーナス(仕様書「暗躍ボーナス」)の連続撃破数・倍率は、CS_PlayerStealthBonusが持つ(Issue #295)
 *   悪人を倒したらCS_PlayerStealthBonus.AddDefeat()を呼び、その時点のscoreMultiplierを撃破スコアに掛ける
 *   (4人目まで×1、5人目×1.2、6人目×1.4 … 10人目以降×2.2)
 *   警察に見つかった(ReportFoundByPolice)時はResetStreak()で途切れさせる(HPが0になった時はCS_PlayerStealthBonus自身が途切れさせる)
 * ・警察に見つかったことは、警察側(CS_PoliceBrain)が新しく標的にした瞬間にReportFoundByPolice()で知らせてくれる
 * ・他プレイヤーを倒した時は、倒された相手の番号をRPCで全員へ配り、各クライアントでキル通知(CS_NotificationController.ShowKill)を出す
 *   表示名は「Player{番号+1}」(仮)。NPCも同じプレハブなので同じ形で出る
 * ・悪人を倒した時は、onVillainDefeatedで知らせる(必殺ゲージ(CS_PlayerSpecialGauge)が、倒した本人の分だけ溜める)
 * ・悪人を倒した時は、倒した本人の画面に、加算された点数を悪人の頭上へポップアップする(Issue #368。UI仕様書「悪人撃退時のスコア表示」)
 *   ポップアップのUIはVillainScorePopupCanvas.prefab(CS_UIVillainScorePopupPresenter)。生成して、Modelを作ってBindし、Show(点数, 位置)を呼ぶ
 *   (Modelはポップアップが消える時にPresenterが破棄する)
 *   スコアの計算はサーバーで行うので、サーバーから倒した本人(Owner)へRPCで点数と位置を送り、受け取った側で生成する
 *   出す位置は、悪人の位置 + _scorePopupHeight(倒した瞬間の位置。悪人が消えても出せるよう、位置を送る)
 *   出すのは人が操作している自分のプレイヤー(CS_Player.isLocalPlayer)だけ。他のプレイヤー・NPCが倒した分は出さない
 *   (NPCはホストが持っているため、そのままだとホストの画面に出てしまう)
 *   0点の時は出さない(レイドボスは通常の撃破スコアが0点で、貢献度の点数はCS_VillainRaidBossが別に足すため)
 * ・スコアはサーバーだけが計算する(攻撃判定・悪人の処理がサーバーで行われるため)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerResultDataHolder))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerStealthBonus))]
public class CS_PlayerScoring : NetworkBehaviour
{
    [Header("悪人撃破")]
    [SerializeField] private int _defaultVillainScore = 100;    // 相手が撃破スコア(IDefeatScore)を持たない時の点数

    [Header("減点")]
    [SerializeField] private int _crimeCompletedPenalty = 20;   // 犯罪を完遂された時に減る点数
    [SerializeField] private int _foundByPolicePenalty = 50;    // 警察に見つかった時に減る点数
    [SerializeField] private float _foundByPoliceInterval = 5f; // 一度見つかってから、次に見つかった扱いにするまでの秒数

    [Header("他プレイヤー撃破")]
    [SerializeField] private int _defeatPlayerScore = 0;        // 他プレイヤーを倒した時の点数(仕様未定のため既定0)

    [Header("撃退スコアのポップアップ")]
    [SerializeField] private CS_UIVillainScorePopupPresenter _scorePopupPrefab;   // VillainScorePopupCanvas.prefab(空なら出さない)
    [SerializeField] private float _scorePopupHeight = 2.2f;                      // 悪人の足元から、ポップアップを出す高さ(m)

    private CS_Player _player;
    private CS_PlayerResultDataHolder _holder;
    private CS_PlayerHealth _health;
    private CS_PlayerStealthBonus _bonus;

    private double _nextFoundByPoliceTime = double.MinValue;    // この時刻までは警察に見つかっても数えない(サーバー、またはオフラインのみ)

    // 自分が悪人を倒した時に発生する(サーバー、またはオフラインのみ)
    public event System.Action<CS_VillainHealth> onVillainDefeated;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _holder = GetComponent<CS_PlayerResultDataHolder>();
        _health = GetComponent<CS_PlayerHealth>();
        _bonus = GetComponent<CS_PlayerStealthBonus>();

        CS_VillainGroup.onAnyCrimeCompleted += HandleCrimeCompleted;
    }

    public override void OnDestroy()
    {
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

        // 前に見つかってから_foundByPoliceInterval秒経っていなければ数えない
        double now = GetCurrentTime();
        if (now < _nextFoundByPoliceTime) return;
        _nextFoundByPoliceTime = now + _foundByPoliceInterval;

        _bonus.ResetStreak();
        _holder.AddFoundByPolice(-_foundByPolicePenalty);
    }

    // 相手を倒した時の加算
    private void HandleDefeated(IDamageable target)
    {
        if (!CanScore()) return;

        if (target is CS_VillainHealth villain)
        {
            _bonus.AddDefeat();
            int point = Mathf.RoundToInt(GetDefeatScore(villain) * _bonus.scoreMultiplier);
            _holder.AddDefeatVillain(point);
            onVillainDefeated?.Invoke(villain);
            NotifyScorePopup(point, villain.transform.position + Vector3.up * _scorePopupHeight);
            return;
        }

        // 自分自身への攻撃は数えない
        if (target is CS_PlayerHealth player && player != _health)
        {
            _holder.AddDefeatPlayer(_defeatPlayerScore);
            NotifyKill(player);
        }
    }

    // 倒した本人の画面に、点数のポップアップを出してもらう(サーバー、またはオフラインで呼ぶ)
    private void NotifyScorePopup(int point, Vector3 position)
    {
        if (point == 0 || _scorePopupPrefab == null) return;
        if (_player != null && _player.isNpc) return;   // NPCが倒した分は出さない

        if (IsSpawned) ShowScorePopupRpc(point, position);
        else ShowScorePopup(point, position);   // オフラインのテストシーン
    }

    [Rpc(SendTo.Owner)]
    private void ShowScorePopupRpc(int point, Vector3 position)
    {
        ShowScorePopup(point, position);
    }

    // 点数のポップアップを生成する(表示が終わるとポップアップ自身が消える)
    private void ShowScorePopup(int point, Vector3 position)
    {
        if (_scorePopupPrefab == null) return;
        if (_player == null || !_player.isLocalPlayer) return;

        CS_UIVillainScorePopupPresenter popup = Instantiate(_scorePopupPrefab, position, Quaternion.identity);
        CS_UIVillainScorePopupModel model = new CS_UIVillainScorePopupModel();
        popup.BindModel(model, _player.playerCamera);
        model.Show(point, position);
    }

    // 倒された相手の番号を全員へ配り、キル通知を出す
    private void NotifyKill(CS_PlayerHealth victim)
    {
        CS_Player victimPlayer = victim.GetComponentInParent<CS_Player>();
        if (victimPlayer == null) return;

        if (IsSpawned) NotifyKillRpc(victimPlayer.playerNumber);
        else ShowKill(victimPlayer.playerNumber);  // オフラインのテストシーン
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyKillRpc(int victimNumber)
    {
        ShowKill(victimNumber);
    }

    private static void ShowKill(int victimNumber)
    {
        CS_NotificationController notification = FindAnyObjectByType<CS_NotificationController>();
        if (notification == null) return;

        notification.ShowKill($"Player{victimNumber + 1}");
    }

    // 倒した悪人の撃破スコア(悪人側がIDefeatScoreを持っていなければ既定値)
    private int GetDefeatScore(CS_VillainHealth villain)
    {
        IDefeatScore source = villain.GetComponentInParent<IDefeatScore>();
        return source != null ? source.defeatScore : _defaultVillainScore;
    }

    // どこかの悪人グループに犯罪を完遂されたら減点する
    private void HandleCrimeCompleted(CS_VillainGroup group)
    {
        if (!CanScore()) return;

        _holder.AddCrimeCompleted(-_crimeCompletedPenalty);
    }

    // スコアを計算してよいか(サーバー、またはオフラインのみ)
    private bool CanScore()
    {
        return !IsSpawned || IsServer;
    }

    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }

    private void OnValidate()
    {
        _foundByPoliceInterval = Mathf.Max(0f, _foundByPoliceInterval);
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
