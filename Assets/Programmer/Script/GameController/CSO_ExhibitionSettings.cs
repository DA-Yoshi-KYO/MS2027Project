/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-06 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 展示用の実行環境(終了操作・カーソル)の設定
/// </summary>
// ========================================
/*
 * メモ
 * ・CS_ExhibitionRuntimeが起動時にResourcesから読む。名前と置き場所を変えないこと
 *   (Assets/Programmer/Database/Exhibition/Resources/DB_ExhibitionSettings.asset)
 * ・カーソル画像はインポート設定のTexture Typeを「Cursor」にする
 */
// ========================================
[CreateAssetMenu(fileName = "DB_ExhibitionSettings", menuName = "Scriptable Objects/CSO_ExhibitionSettings")]
public class CSO_ExhibitionSettings : ScriptableObject
{
    public const string resourcePath = "DB_ExhibitionSettings";

    [Header("終了")]
    [SerializeField][Tooltip("ESCをこの秒数押し続けると終了する")][Min(0f)] private float _quitHoldSeconds = 2.5f;

    [Header("カーソル(自分のプレイヤーがいない画面で出す)")]
    [SerializeField][Tooltip("独自カーソルの画像(未設定ならOSのカーソル)")] private Texture2D _cursorTexture;
    [SerializeField][Tooltip("画像の中でクリック位置になる点(左上が0,0)")] private Vector2 _cursorHotspot;

    public float quitHoldSeconds => _quitHoldSeconds;
    public Texture2D cursorTexture => _cursorTexture;
    public Vector2 cursorHotspot => _cursorHotspot;
}
