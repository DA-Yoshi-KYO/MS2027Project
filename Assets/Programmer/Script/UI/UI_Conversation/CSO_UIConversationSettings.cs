/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 会話ウィンドウの設定値(表示時間・フェード時間)を入れておく ScriptableObject
/// ・コードを書き換えずに Inspector で変えられるようにするためのもの
/// ・Project 右クリック > Create > AiGuide > UI Conversation Settings
///   → DB_UIConversationSettings ができる。Presenter の _settings に割り当てる
/// </summary>
[CreateAssetMenu(menuName = "AiGuide/UI Conversation Settings", fileName = "DB_UIConversationSettings")]
public sealed class CSO_UIConversationSettings : ScriptableObject
{
    // =========================================================
    // Inspector
    // =========================================================

    [Tooltip("Say() で秒数を指定しなかった時の表示時間(秒)")]
    [Min(0.1f)]
    [SerializeField]
    private float _defaultDuration = 4f;

    [Tooltip("表示時間の下限(秒)。極端に短い指定で一瞬で消えるのを防ぐ")]
    [Min(0.1f)]
    [SerializeField]
    private float _minDuration = 0.5f;

    [Tooltip("ウィンドウが出てくる時のフェードイン時間(秒)。0なら即表示")]
    [Min(0f)]
    [SerializeField]
    private float _fadeInSeconds = 0.15f;

    [Tooltip("ウィンドウが消える時のフェードアウト時間(秒)。0なら即消える")]
    [Min(0f)]
    [SerializeField]
    private float _fadeOutSeconds = 0.25f;

    // =========================================================
    // 公開(読み取り専用)
    // =========================================================

    public float defaultDuration => _defaultDuration;
    public float minDuration => _minDuration;
    public float fadeInSeconds => _fadeInSeconds;
    public float fadeOutSeconds => _fadeOutSeconds;
}
