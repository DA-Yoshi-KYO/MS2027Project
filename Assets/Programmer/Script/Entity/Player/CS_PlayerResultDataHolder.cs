using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのスコア(点数と回数)を持ち、ゲーム中のスコアUIとリザルトへ渡すクラス
 * 点数の計算(何点入るか、暗躍ボーナスなど)はCS_PlayerScoringが行い、ここは加算と同期だけを行う
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・スコアの仕組み自体はClaudeDocs/スコアの使い方.md参照。ここでは実際のプレイヤーへの組み込みを行う
 * ・点数と回数はNetworkVariable<CS_ResultData.Score>で持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   各クライアントは値が届くたびに、自分の手元のCS_ResultDataとスコアUI(CS_UIScoreModel)へ反映する
 * ・Scoreは読み取り専用のstructなので、「取り出す → 足した新しいScoreを作る → 入れ直す」で更新する
 * ・ゲーム中のスコアUIは全プレイヤー分を同じ画面に出す想定のため、Modelの生成・Bindは全クライアントで行う
 *   (表示先はCS_Player.playerNumber。CS_PlayerHpUIと同じ考え方)
 * ・ゲーム終了時は、CollectResults()で全プレイヤー分のCS_ResultDataを集めてCS_ResultDataStoreへ保存する想定
 *   (Scoreは全クライアントに同期済みなので、各クライアントが手元のプレイヤー全員分を集めれば足りる)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く(playerNumberは既定値の0になる)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerResultDataHolder : NetworkBehaviour
{
    private static readonly List<CS_PlayerResultDataHolder> _holders = new List<CS_PlayerResultDataHolder>();

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<CS_ResultData.Score> _score = new NetworkVariable<CS_ResultData.Score>();

    private CS_Player _player;
    private CS_ResultData _resultData;
    private CS_UIScoreModel _scoreModel;

    public CS_ResultData resultData => _resultData;     // リザルトへ渡すデータ(初期化前はnull)
    public CS_ResultData.Score score => _score.Value;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
    }

    private void OnEnable()
    {
        _holders.Add(this);
    }

    private void OnDisable()
    {
        _holders.Remove(this);
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        Initialize();
    }

    public override void OnNetworkSpawn()
    {
        // playerNumberはSpawn後でないと確定しないので、ここで作る
        Initialize();
        _score.OnValueChanged += HandleScoreChanged;
    }

    public override void OnNetworkDespawn()
    {
        _score.OnValueChanged -= HandleScoreChanged;
        ReleaseScoreModel();
    }

    public override void OnDestroy()
    {
        ReleaseScoreModel();
        base.OnDestroy();
    }

    // 全プレイヤー分のリザルトデータを集める(ゲーム終了時、CS_ResultDataStore.Saveに渡す想定)
    public static List<CS_ResultData> CollectResults()
    {
        List<CS_ResultData> results = new List<CS_ResultData>();
        foreach (CS_PlayerResultDataHolder holder in _holders)
        {
            if (holder._resultData != null) results.Add(holder._resultData);
        }

        return results;
    }

    // Domain Reloadオフ対策
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _holders.Clear();
    }

    // ---- 加算(サーバー、またはオフラインのみ) ----

    // 悪人を倒した時。pointは暗躍ボーナス込みの点数
    public void AddDefeatVillain(int point)
    {
        Add(defeatVillainScore: point, defeatVillainCount: 1);
    }

    // 警察に倒された時。pointはマイナスの点数
    public void AddFoundByPolice(int point)
    {
        Add(foundByPoliceScore: point, foundByPoliceCount: 1);
    }

    // 悪人に犯罪を完遂された時。pointはマイナスの点数
    public void AddCrimeCompleted(int point)
    {
        Add(crimeCompletedScore: point, crimeCompletedCount: 1);
    }

    // 他のプレイヤーを倒した時
    public void AddDefeatPlayer(int point)
    {
        Add(defeatPlayerScore: point, defeatPlayerCount: 1);
    }

    // 今のScoreに足した新しいScoreを作って入れ直す(structなので、入れ直さないと同期されない)
    private void Add(
        int defeatVillainScore = 0, int foundByPoliceScore = 0, int crimeCompletedScore = 0, int defeatPlayerScore = 0,
        int defeatVillainCount = 0, int foundByPoliceCount = 0, int crimeCompletedCount = 0, int defeatPlayerCount = 0)
    {
        if (IsSpawned && !IsServer) return;

        CS_ResultData.Score current = _score.Value;
        _score.Value = new CS_ResultData.Score(
            current.defeatVillainScore + defeatVillainScore,
            current.foundByPoliceScore + foundByPoliceScore,
            current.crimeCompletedScore + crimeCompletedScore,
            current.defeatPlayerScore + defeatPlayerScore,
            current.defeatVillainCount + defeatVillainCount,
            current.foundByPoliceCount + foundByPoliceCount,
            current.crimeCompletedCount + crimeCompletedCount,
            current.defeatPlayerCount + defeatPlayerCount);

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接反映する
        if (IsSpawned) return;

        HandleScoreChanged(current, _score.Value);
    }

    // リザルトデータとスコアUIのModelを作り、同期済みの値を入れる
    private void Initialize()
    {
        _resultData = new CS_ResultData(_player.playerNumber);

        ReleaseScoreModel();
        _scoreModel = new CS_UIScoreModel();
        _scoreModel.Bind(_player.playerNumber);

        // 途中でSpawnしたクライアントも0から始まらないよう、同期済みの値を最初に入れる
        Apply(_score.Value);
    }

    private void ReleaseScoreModel()
    {
        if (_scoreModel == null) return;

        _scoreModel.Dispose();
        _scoreModel = null;
    }

    private void HandleScoreChanged(CS_ResultData.Score previous, CS_ResultData.Score current)
    {
        Apply(current);
    }

    // 届いた値をリザルトデータとスコアUIへ反映する
    private void Apply(CS_ResultData.Score current)
    {
        if (_resultData == null) return;

        _resultData.SetScore(current);
        _scoreModel?.SetScore(_resultData.totalScore);
    }
}
