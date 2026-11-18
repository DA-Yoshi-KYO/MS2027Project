/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-18 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// SAヒントUIの調整値を保持するデータ。
///
/// ・SAポイントの検索距離
/// ・最大表示数
/// ・プレイヤー周囲の楕円半径
/// ・警戒UIとの重なり防止
/// ・矢印の拡縮アニメーション
/// </summary>
[CreateAssetMenu(
    fileName = "UI_SAHintData",
    menuName = "Game/UI/SA Hint Data")]
public sealed class CSO_UISAHintData :
    ScriptableObject
{
    // =========================================================
    // SAポイント検索
    // =========================================================

    [Header("SAポイント検索")]

    [Tooltip("プレイヤーからSAポイントを検索する距離")]
    [Min(0f)]
    [SerializeField]
    private float _searchDistance = 50f;

    [Tooltip("同時に表示する最大矢印数")]
    [Range(1, 3)]
    [SerializeField]
    private int _maximumDisplayCount = 3;

    // =========================================================
    // 楕円配置
    // =========================================================

    [Header("プレイヤー周囲の楕円")]

    [Tooltip("ヒント矢印を配置する楕円の横半径")]
    [Min(0f)]
    [SerializeField]
    private float _horizontalRadius = 200f;

    [Tooltip("ヒント矢印を配置する楕円の縦半径")]
    [Min(0f)]
    [SerializeField]
    private float _verticalRadius = 100f;

    // =========================================================
    // 警戒UIとの重なり防止
    // =========================================================

    [Header("警戒UIとの重なり防止")]

    [Tooltip("警戒UI楕円の横半径")]
    [Min(0f)]
    [SerializeField]
    private float _policeHorizontalRadius = 700f;

    [Tooltip("警戒UI楕円の縦半径")]
    [Min(0f)]
    [SerializeField]
    private float _policeVerticalRadius = 350f;

    [Tooltip("警戒UI楕円との最低間隔")]
    [Min(0f)]
    [SerializeField]
    private float _policeMargin = 30f;

    // =========================================================
    // 拡縮
    // =========================================================

    [Header("矢印の拡縮")]

    [Tooltip("拡縮の最小倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _minimumScale = 0.9f;

    [Tooltip("拡縮の最大倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float _maximumScale = 1.05f;

    [Tooltip("最小から最大へ変化する時間")]
    [Min(0.01f)]
    [SerializeField]
    private float _scaleDuration = 0.8f;

    // =========================================================
    // 公開プロパティ
    // =========================================================

    public float searchDistance =>
        _searchDistance;

    public int maximumDisplayCount =>
        _maximumDisplayCount;

    /// <summary>
    /// 警戒UI楕円より内側に制限された横半径。
    /// </summary>
    public float horizontalRadius
    {
        get
        {
            float maximumRadius =
                Mathf.Max(
                    0f,
                    _policeHorizontalRadius -
                    _policeMargin
                );

            return Mathf.Min(
                _horizontalRadius,
                maximumRadius
            );
        }
    }

    /// <summary>
    /// 警戒UI楕円より内側に制限された縦半径。
    /// </summary>
    public float verticalRadius
    {
        get
        {
            float maximumRadius =
                Mathf.Max(
                    0f,
                    _policeVerticalRadius -
                    _policeMargin
                );

            return Mathf.Min(
                _verticalRadius,
                maximumRadius
            );
        }
    }

    public float minimumScale =>
        _minimumScale;

    public float maximumScale =>
        _maximumScale;

    public float scaleDuration =>
        _scaleDuration;

    // =========================================================
    // Inspector補正
    // =========================================================

    private void OnValidate()
    {
        _searchDistance =
            Mathf.Max(
                0f,
                _searchDistance
            );

        _maximumDisplayCount =
            Mathf.Clamp(
                _maximumDisplayCount,
                1,
                3
            );

        _horizontalRadius =
            Mathf.Max(
                0f,
                _horizontalRadius
            );

        _verticalRadius =
            Mathf.Max(
                0f,
                _verticalRadius
            );

        _policeHorizontalRadius =
            Mathf.Max(
                0f,
                _policeHorizontalRadius
            );

        _policeVerticalRadius =
            Mathf.Max(
                0f,
                _policeVerticalRadius
            );

        _policeMargin =
            Mathf.Max(
                0f,
                _policeMargin
            );

        _minimumScale =
            Mathf.Max(
                0.01f,
                _minimumScale
            );

        _maximumScale =
            Mathf.Max(
                _minimumScale,
                _maximumScale
            );

        _scaleDuration =
            Mathf.Max(
                0.01f,
                _scaleDuration
            );
    }
}