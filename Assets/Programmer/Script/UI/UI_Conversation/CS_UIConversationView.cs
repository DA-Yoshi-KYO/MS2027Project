/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 会話ウィンドウの描画を担当する View
/// ・文字を表示する
/// ・CanvasGroup の alpha でウィンドウをフェードイン / アウトする
/// ・「いつ出すか」「何秒で消すか」は判断しない(Presenter の仕事)
/// ・Image と CanvasGroup がある GameObject にアタッチする
///   (RequireComponent により CanvasGroup は自動で追加される)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CS_UIConversationView : CS_BaseView<CS_UIConversationPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("文字を表示するテキスト(子の MessageText)")]
    [SerializeField]
    private TMP_Text _label;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CanvasGroup _group;

    // 今動いているフェード(途中で止めるために覚えておく)
    private Coroutine _fade;

    // =========================================================
    // 初期化
    // =========================================================

    void Awake()
    {
        Init();
    }

    /// <summary>
    /// CanvasGroup の初期設定。何度呼んでも最初の1回しか動かない
    /// Awake より先に Presenter から呼ばれる場合に備えて、SetVisible からも呼ぶ
    /// </summary>
    private void Init()
    {
        if (_group != null) return;

        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;              // 最初は透明
        _group.interactable = false;    // 操作を受け付けない
        _group.blocksRaycasts = false;  // クリックを素通しにして、ゲーム操作の邪魔をしない
    }

    // =========================================================
    // 描画(Presenter から呼ばれる)
    // =========================================================

    /// <summary>表示する文字を変える</summary>
    public void SetText(string text)
    {
        _label.text = text;
    }

    /// <summary>
    /// ウィンドウを出す / 消す
    /// isVisible: true=出す、false=消す
    /// fadeSeconds: フェードにかける秒数(0なら即切り替え)
    /// </summary>
    public void SetVisible(bool isVisible, float fadeSeconds)
    {
        Init();

        float targetAlpha = isVisible ? 1f : 0f;

        // 前のフェードが途中なら止める(出す/消すが連続しても動きがぶつからないように)
        if (_fade != null)
        {
            StopCoroutine(_fade);
            _fade = null;
        }

        // 非アクティブなオブジェクトではコルーチンを動かせないので、即切り替えにする
        if (!isActiveAndEnabled)
        {
            _group.alpha = targetAlpha;
            return;
        }

        _fade = StartCoroutine(FadeRoutine(targetAlpha, fadeSeconds));
    }

    // =========================================================
    // フェード
    // =========================================================

    /// <summary>
    /// alpha を今の値から目標の値まで少しずつ変える
    /// コルーチン = 「1フレームごとに少しずつ進めて、続きは次のフレームで」という書き方
    /// </summary>
    private IEnumerator FadeRoutine(float targetAlpha, float seconds)
    {
        float startAlpha = _group.alpha; // 今の値から始める(途中からでも滑らかにつながる)

        // 秒数が0なら、フェードせず一気に目標値にして終了
        if (seconds <= 0f)
        {
            _group.alpha = targetAlpha;
            _fade = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            // unscaledDeltaTime = timeScale=0(ポーズ中)でも進む時間
            elapsed += Time.unscaledDeltaTime;

            // Lerp = 開始値と目標値の間を、割合(0〜1)で補間する
            _group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / seconds));

            yield return null;
        }

        _group.alpha = targetAlpha; // 最後に誤差なく目標値にそろえる
        _fade = null;
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    void OnDisable()
    {
        // 無効化でコルーチンは止まるので、覚えていた参照だけ捨てる
        _fade = null;
    }
}
