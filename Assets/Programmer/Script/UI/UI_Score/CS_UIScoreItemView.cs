/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-08 | 初回作成
 * 2026-11-02 | ソロの時は順位を出さないようにする
 * ================================================ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ランキング1人分の表示を担当するView。
///
/// ・キャラクター種類に対応するアイコンを表示する
/// ・順位とスコアを表示する
/// ・ソロでは順位を非表示にする
/// ・必殺技ゲージ満タン時はアイコンの色を変える
/// </summary>
public sealed class CS_UIScoreItemView :
    MonoBehaviour
{
    // =========================================================
    // キャラクターごとのアイコン設定
    // =========================================================

    [System.Serializable]
    private sealed class CharacterIconSet
    {
        [SerializeField]
        private CSE_CharacterType _characterType;

        [SerializeField]
        private Sprite _iconSprite;

        public CSE_CharacterType characterType =>
            _characterType;

        public Sprite iconSprite =>
            _iconSprite;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("表示UI")]

    [SerializeField]
    private TextMeshProUGUI _rankText;

    [SerializeField]
    private Image _iconImage;

    [SerializeField]
    private TextMeshProUGUI _scoreText;

    [Header("キャラクターごとのアイコン")]

    [SerializeField]
    private CharacterIconSet[] _characterIconSets;

    [Header("必殺技満タン時の色設定")]

    [SerializeField]
    private Color _normalColor =
        Color.white;

    [SerializeField]
    private Color _fullColor =
        Color.yellow;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_rankText != null)
        {
            _rankText.raycastTarget = false;
        }

        if (_iconImage != null)
        {
            _iconImage.raycastTarget = false;
        }

        if (_scoreText != null)
        {
            _scoreText.raycastTarget = false;
        }
    }

    // =========================================================
    // 描画
    // =========================================================

    /// <summary>
    /// ランキング1人分の表示を更新する。
    /// </summary>
    public void UpdateView(
        int rank,
        int playerNumber,
        int score,
        CSE_CharacterType characterType,
        bool isFull)
    {
        if (_rankText != null)
        {
            _rankText.text =
                $"{rank}位";
        }

        ApplyCharacterIcon(
            characterType
        );

        if (_scoreText != null)
        {
            _scoreText.text =
                $"{score}点";
        }

        SetIconFull(isFull);
    }

    // =========================================================
    // キャラクターアイコン
    // =========================================================

    /// <summary>
    /// キャラクター種類に対応するアイコンを設定する。
    /// </summary>
    private void ApplyCharacterIcon(
        CSE_CharacterType characterType)
    {
        if (_iconImage == null)
        {
            return;
        }

        CharacterIconSet iconSet =
            FindCharacterIconSet(
                characterType
            );

        if (iconSet == null)
        {
            Debug.LogWarning(
                $"{nameof(CS_UIScoreItemView)}: " +
                $"キャラクターアイコンがありません: " +
                $"{characterType}",
                this
            );

            _iconImage.sprite = null;
            _iconImage.enabled = false;

            return;
        }

        _iconImage.sprite =
            iconSet.iconSprite;

        _iconImage.enabled =
            iconSet.iconSprite != null;
    }

    /// <summary>
    /// キャラクター種類に対応する設定を探す。
    /// </summary>
    private CharacterIconSet FindCharacterIconSet(
        CSE_CharacterType characterType)
    {
        if (_characterIconSets == null)
        {
            return null;
        }

        for (int i = 0;
             i < _characterIconSets.Length;
             i++)
        {
            CharacterIconSet iconSet =
                _characterIconSets[i];

            if (iconSet == null)
            {
                continue;
            }

            if (iconSet.characterType ==
                characterType)
            {
                return iconSet;
            }
        }

        return null;
    }

    // =========================================================
    // 順位表示
    // =========================================================

    /// <summary>
    /// 順位テキストの表示状態を切り替える。
    /// </summary>
    public void SetRankVisible(
        bool isVisible)
    {
        if (_rankText != null)
        {
            _rankText.enabled =
                isVisible;
        }
    }

    // =========================================================
    // 必殺技満タン表示
    // =========================================================

    /// <summary>
    /// 必殺技ゲージ満タン状態に応じて
    /// ランキングアイコンの色を切り替える。
    /// </summary>
    public void SetIconFull(bool isFull)
    {
        if (_iconImage == null)
        {
            return;
        }

        _iconImage.color =
            isFull
                ? _fullColor
                : _normalColor;
    }
}