/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-12 | 初回作成
 * 2026-10-14 | 自分の画面のプレイヤーかをBindで受け取れるようにした(NPCはホストでIsOwnerになるため。秋野)
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マルチプレイヤーの頭上に現在順位を画像で表示するUI。
/// ・CS_UIScoreModelのランキング更新を購読する
/// ・同点は同順位として扱う
/// ・順位に対応するSpriteへ切り替える
/// ・World Space UIをカメラ方向へ向け続ける
/// </summary>
public class CS_UIPlayerRankOverhead : NetworkBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("順位表示UI")]
    [SerializeField] private GameObject _rankRoot;
    [SerializeField] private Image _rankImage;

    [Header("順位画像（0番目＝1位、1番目＝2位）")]
    [SerializeField] private Sprite[] _rankSprites;

    [Header("配列範囲外の順位で使用する画像（任意）")]
    [SerializeField] private Sprite _defaultRankSprite;

    [Header("全員0点の間は順位を非表示にする")]
    [SerializeField] private bool _hideWhenAllScoresAreZero;

    [Header("ビルボード設定")]
    [SerializeField] private bool _lookAtCamera = true;

    [Header("ローカルプレイヤーにも表示するか")]
    [SerializeField] private bool _showLocalPlayer;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Camera _mainCamera;
    private int _playerNumber = -1;
    private bool _isBound;
    private bool? _isLocalPlayer;   // Bindで渡された「自分の画面のプレイヤーか」(nullならIsOwnerで判定する)

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _mainCamera = Camera.main;
        SetRankVisible(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (_isBound)
            HandleRankingUpdated();
    }

    // =========================================================
    // Bind / Unbind
    // =========================================================

    /// <summary>
    /// このUIが表示するプレイヤー番号を設定する。
    /// プレイヤー番号の確定後、全プレイヤーで呼ぶ。
    /// </summary>
    /// <param name="isLocalPlayer">自分の画面のプレイヤーか(省略時はIsOwnerで判定。NPCはホストでIsOwnerになるため、プレイヤー側から渡す)</param>
    public void Bind(int playerNumber, Camera camera, bool? isLocalPlayer = null)
    {
        Unbind();

        _playerNumber = playerNumber;
        _isBound = true;

        _mainCamera = camera;
        _isLocalPlayer = isLocalPlayer;

        CS_UIScoreModel.OnRankingUpdated += HandleRankingUpdated;
        HandleRankingUpdated();
    }

    public void Unbind()
    {
        if (!_isBound)
            return;

        CS_UIScoreModel.OnRankingUpdated -= HandleRankingUpdated;

        _playerNumber = -1;
        _isBound = false;

        SetRankVisible(false);
    }

    // =========================================================
    // ランキング更新
    // =========================================================

    private void HandleRankingUpdated()
    {
        if (!_isBound || _playerNumber < 0)
            return;

        List<(int playerNumber, int score)> ranking =
            CS_UIScoreModel.GetRanking();

        if (_hideWhenAllScoresAreZero && AreAllScoresZero(ranking))
        {
            SetRankVisible(false);
            return;
        }

        int rank = FindCompetitionRank(ranking, _playerNumber);

        if (rank <= 0)
        {
            SetRankVisible(false);
            return;
        }

        Sprite rankSprite = GetRankSprite(rank);

        if (_rankImage == null || rankSprite == null)
        {
            SetRankVisible(false);
            return;
        }

        _rankImage.sprite = rankSprite;
        _rankImage.preserveAspect = true;
        _rankImage.raycastTarget = false;

        SetRankVisible(ShouldShow());
    }

    /// <summary>
    /// 同点を同順位にする。
    /// 順位 = 自分よりスコアが高いプレイヤー数 + 1。
    /// 例：100、100、50点なら、1位・1位・3位。
    /// </summary>
    private int FindCompetitionRank(
        List<(int playerNumber, int score)> ranking,
        int playerNumber)
    {
        int myScore = int.MinValue;
        bool found = false;

        foreach (var result in ranking)
        {
            if (result.playerNumber != playerNumber)
                continue;

            myScore = result.score;
            found = true;
            break;
        }

        if (!found)
            return -1;

        int higherScorePlayerCount = 0;

        foreach (var result in ranking)
        {
            if (result.score > myScore)
                higherScorePlayerCount++;
        }

        return higherScorePlayerCount + 1;
    }

    private bool AreAllScoresZero(
        List<(int playerNumber, int score)> ranking)
    {
        if (ranking == null || ranking.Count == 0)
            return true;

        foreach (var result in ranking)
        {
            if (result.score != 0)
                return false;
        }

        return true;
    }

    private Sprite GetRankSprite(int rank)
    {
        int spriteIndex = rank - 1;

        if (_rankSprites != null &&
            spriteIndex >= 0 &&
            spriteIndex < _rankSprites.Length &&
            _rankSprites[spriteIndex] != null)
        {
            return _rankSprites[spriteIndex];
        }

        return _defaultRankSprite;
    }

    private void SetRankVisible(bool visible)
    {
        if (_rankRoot != null)
            _rankRoot.SetActive(visible);
    }

    private bool ShouldShow()
    {
        // オフラインでは表示する。
        if (!IsSpawned)
            return true;

        // 順位UI自身にNetworkObjectを付けず、Playerルートの
        // NetworkObjectに関連付けることでIsOwnerがPlayer所有者を示す。
        bool isLocal = _isLocalPlayer ?? IsOwner;
        return _showLocalPlayer || !isLocal;
    }

    // =========================================================
    // カメラ追従
    // =========================================================

    private void LateUpdate()
    {
        if (!_lookAtCamera ||
            _rankRoot == null ||
            !_rankRoot.activeInHierarchy)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null)
            return;

        Vector3 direction =
            _rankRoot.transform.position -
            _mainCamera.transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        _rankRoot.transform.rotation =
            Quaternion.LookRotation(direction);
    }

    // =========================================================
    // 終了処理
    // =========================================================

    public override void OnNetworkDespawn()
    {
        Unbind();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        Unbind();
        base.OnDestroy();
    }
}
