/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-12 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マルチプレイヤーの頭上に現在順位を画像で表示するUI。
/// ・CS_UIScoreModelのランキング更新を購読する
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

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _mainCamera = Camera.main;

        if (_rankRoot != null)
            _rankRoot.SetActive(false);
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
    public void Bind(int playerNumber)
    {
        Unbind();

        _playerNumber = playerNumber;
        _isBound = true;

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

        if (_rankRoot != null)
            _rankRoot.SetActive(false);
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

        int rank = FindRank(ranking, _playerNumber);

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

        SetRankVisible(ShouldShow());
    }

    private int FindRank(
        List<(int playerNumber, int score)> ranking,
        int playerNumber)
    {
        for (int i = 0; i < ranking.Count; i++)
        {
            if (ranking[i].playerNumber == playerNumber)
                return i + 1;
        }

        return -1;
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
        if (!IsSpawned)
            return true;

        return _showLocalPlayer || !IsOwner;
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
