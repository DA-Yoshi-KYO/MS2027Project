/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-25 | 初回作成
 * ================================================ */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// リザルトへのカットシーンの描画を担当するView(Timeline版)
///
/// 画像の動きは、Timeline(PlayableDirector)の中で作る。このViewは、それを再生するだけ。
///   ・覆うTimeline(coverDirector)  : Group(G1〜G3の親)を、画面の上の外から下へ動かす。
///                                     G1が画面の下の外へ出た所(G2とG3が画面を覆った状態)で終わる
///   ・開くTimeline(revealDirector) : G2を左へ、G3を右へ、画面の外まで動かす
///   ・閉じる動き(追加)             : 開くTimelineを逆再生する。G2は左から右へ、G3は右から左へ動いて、画面を覆う
///                                     (新しいTimelineは作らない)
///
/// 2つのDirectorは、どちらも省略できる(空でよい)。シーンごとに必要な方だけ設定する。
///   ・インゲーム : 覆うTimelineだけ設定する(開くは空)
///   ・リザルト   : 開くTimelineだけ設定する(覆うは空)。閉じる動きも、この開くTimelineを使う
///                  → 覆う動きは再生されない。画像は、覆った状態の位置にシーン上で置いておく
///   空のDirectorに「再生して」と指示が来た時は、何もせずにすぐ「終わった」と返す
///   (後の処理が止まらないようにするため)
///
/// 最初の表示(インゲーム)
///   Hidden(まだ動かす前)の間は、_visualRoot を非表示にしておく(_deactivateWhenHidden)。
///   覆う動きを始める瞬間(PlayCover)に、_visualRoot を表示する。
///   → 時間が過ぎるまで、G1〜G3は画面にも描画にも出ない
///
/// ・位置の計算はしない(Timeline のキーフレームで決める)
/// ・「いつ動かすか」は判断しない(Presenter の仕事)
/// ・Presenter と同じ GameObject にアタッチする
///
/// 状態の置き方(アニメーションなし。設定されているDirectorだけ反映する)
///   Hidden   : 覆うTimelineの「最初」、開くTimelineの「最初」(+ _visualRoot を非表示)
///   Covered  : 覆うTimelineの「最後」、開くTimelineの「最初」(閉じ終わりも同じ)
///   Revealed : 覆うTimelineの「最後」、開くTimelineの「最後」(+ 必要なら _visualRoot を非表示)
/// </summary>
public sealed class CS_UIResultCutsceneView :
    CS_BaseView<CS_UIResultCutscenePresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("Timeline(PlayableDirector)。どちらも空にできる")]

    [Tooltip("覆うTimeline(上から下へ)。インゲームで使う。リザルトでは空にする。Play On Awake はオフにする")]
    [SerializeField]
    private PlayableDirector _coverDirector;

    [Tooltip("開くTimeline(G2は左へ、G3は右へ)。リザルトで使う。閉じる動きもこれを逆再生する。Play On Awake はオフにする")]
    [SerializeField]
    private PlayableDirector _revealDirector;

    [Tooltip("ON: Time.timeScale が 0 でも進む時間で再生する")]
    [SerializeField]
    private bool _useUnscaledTime = true;

    [Header("表示")]

    [Tooltip("G1〜G3をまとめた親(Group)。非表示/表示の切り替えに使う。空なら切り替えない")]
    [SerializeField]
    private GameObject _visualRoot;

    [Tooltip("ON: Hidden(動かす前)の間は _visualRoot を非表示にする。インゲームはON、リザルトは関係なし")]
    [SerializeField]
    private bool _deactivateWhenHidden = true;

    [Tooltip("開き終わった後に、_visualRoot を非表示にする(画面外の画像を描画し続けないため)")]
    [SerializeField]
    private bool _deactivateAfterReveal = true;

    [Header("閉じる動き(リザルトの最後)")]

    [Tooltip("開くTimelineを逆再生する速さの倍率。1なら開く時と同じ速さ、2なら2倍の速さ")]
    [Min(0.1f)]
    [SerializeField]
    private float _closeSpeed = 1f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 準備できているか(Directorが1つでも設定されているか)
    private bool _isReady;

    // 今、再生の終わりを待っている処理(途中で止めるために覚えておく)
    private Coroutine _waitCoroutine;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        if (_coverDirector == null &&
            _revealDirector == null)
        {
            Debug.LogError(
                $"{nameof(CS_UIResultCutsceneView)}: " +
                "Cover Director と Reveal Director のどちらかは設定してください。",
                this
            );

            return;
        }

        PrepareDirector(_coverDirector);
        PrepareDirector(_revealDirector);

        _isReady = true;
    }

    /// <summary>
    /// Directorの再生設定。空なら何もしない。
    /// </summary>
    private void PrepareDirector(
        PlayableDirector director)
    {
        if (director == null)
        {
            return;
        }

        if (_useUnscaledTime)
        {
            director.timeUpdateMode =
                DirectorUpdateMode.UnscaledGameTime;
        }

        // 自動再生されると、Presenter からの指示より先に動いてしまうので止めておく
        director.playOnAwake = false;
    }

    // =========================================================
    // 状態の即時反映(アニメーションなし)
    // =========================================================

    /// <summary>
    /// 画像全体を、画面の上の外に置く。インゲームの最初の状態。
    /// _deactivateWhenHidden が ON なら、置いた後で _visualRoot を非表示にする。
    /// (姿勢を反映するには表示されている必要があるので、いったん表示してから反映し、最後に非表示にする)
    /// </summary>
    public void SetHidden()
    {
        if (!_isReady)
        {
            return;
        }

        StopWait();
        SetVisualActive(true);

        ApplyPose(_coverDirector, false);
        ApplyPose(_revealDirector, false);

        if (_deactivateWhenHidden)
        {
            SetVisualActive(false);
        }
    }

    /// <summary>
    /// G1を画面の下の外へ、G2とG3で画面を覆った状態に置く。リザルトの最初の状態。閉じ終わりも同じ姿。
    /// 覆うDirectorが空なら、シーン上に置いてある位置のまま。
    /// </summary>
    public void SetCovered()
    {
        if (!_isReady)
        {
            return;
        }

        StopWait();
        SetVisualActive(true);

        ApplyPose(_coverDirector, true);
        ApplyPose(_revealDirector, false);
    }

    /// <summary>
    /// G2を左の外へ、G3を右の外へ置く。必要なら親を非表示にする。
    /// </summary>
    public void SetRevealed()
    {
        if (!_isReady)
        {
            return;
        }

        StopWait();
        SetVisualActive(true);

        ApplyPose(_coverDirector, true);
        ApplyPose(_revealDirector, true);

        if (_deactivateAfterReveal)
        {
            SetVisualActive(false);
        }
    }

    // =========================================================
    // アニメーション(Timelineの再生)
    // =========================================================

    /// <summary>
    /// 覆うTimelineを再生する。終わったら onFinished を呼ぶ。
    /// 始める瞬間に _visualRoot を表示する(Hidden の間は非表示にしてあるため)。
    /// 覆うDirectorが空なら、何もせずにすぐ onFinished を呼ぶ。
    /// </summary>
    public void PlayCover(
        Action onFinished)
    {
        PlayDirector(
            _coverDirector,
            onFinished
        );
    }

    /// <summary>
    /// 開くTimelineを再生する。終わったら onFinished を呼ぶ。
    /// 開くDirectorが空なら、何もせずにすぐ onFinished を呼ぶ。
    /// </summary>
    public void PlayReveal(
        Action onFinished)
    {
        PlayDirector(
            _revealDirector,
            onFinished
        );
    }

    /// <summary>
    /// 追加: 閉じる動き。開くTimelineを「最後 → 最初」へ逆再生する。
    /// G2は左から右へ、G3は右から左へ動いて、画面を覆う。終わったら onFinished を呼ぶ。
    /// ・開き終わった後は _visualRoot が非表示になっているので、始める瞬間に表示する
    /// ・開くDirectorが空なら、何もせずにすぐ onFinished を呼ぶ
    /// ・逆再生は、時間を自分で少しずつ戻して、1コマごとに反映する(Director の再生機能は使わない)
    /// </summary>
    public void PlayClose(
        Action onFinished)
    {
        if (!_isReady ||
            _revealDirector == null ||
            !isActiveAndEnabled)
        {
            // 動かせなくても、後の処理(シーン遷移など)が止まらないように終了を知らせる
            onFinished?.Invoke();
            return;
        }

        StopWait();
        SetVisualActive(true);

        _waitCoroutine =
            StartCoroutine(
                CloseRoutine(
                    _revealDirector,
                    onFinished
                )
            );
    }

    /// <summary>
    /// Timeline を最初から再生し、終わるまで待って onFinished を呼ぶ。
    /// </summary>
    private void PlayDirector(
        PlayableDirector director,
        Action onFinished)
    {
        // 動かせない/動かす対象がなくても、後の処理(シーン遷移など)が止まらないように終了を知らせる
        if (!_isReady ||
            director == null ||
            !isActiveAndEnabled)
        {
            onFinished?.Invoke();
            return;
        }

        StopWait();

        // 非表示にしていた画像を、動かし始める前に表示する
        SetVisualActive(true);

        // 最初から再生する(Pause していた時の続きから始まらないように、時間を戻す)
        director.time = 0d;
        director.Play();

        _waitCoroutine =
            StartCoroutine(
                WaitForFinishRoutine(
                    director,
                    onFinished
                )
            );
    }

    /// <summary>
    /// Timeline が終わるまで待つ。
    /// 終わりの判定は「再生中でなくなった」または「時間が長さに届いた」。
    /// (Wrap Mode が Hold でも None でも動くようにしてある)
    /// 終わったら、最後の姿勢で止めて(ApplyPose)から onFinished を呼ぶ。
    /// </summary>
    private IEnumerator WaitForFinishRoutine(
        PlayableDirector director,
        Action onFinished)
    {
        while (director != null &&
               director.state == PlayState.Playing &&
               director.time < director.duration - 0.0001d)
        {
            yield return null;
        }

        if (director != null)
        {
            // 最後の姿勢で固定する。Wrap Mode が None の時に、元の位置へ戻ってしまうのを防ぐ
            ApplyPose(director, true);
        }

        // 終了の通知で次の処理(シーン遷移など)が始まるので、先に参照を捨てておく
        _waitCoroutine = null;

        onFinished?.Invoke();
    }

    /// <summary>
    /// 追加: Timeline の時間を、最後から 0 まで戻しながら、1コマごとに反映する(逆再生)。
    /// 終わったら、最初の姿勢(= 画面を覆った状態)で止めて、onFinished を呼ぶ。
    /// </summary>
    private IEnumerator CloseRoutine(
        PlayableDirector director,
        Action onFinished)
    {
        // 自動で進まないように止めてから、自分で時間を決める
        director.Pause();

        double time = director.duration;

        ApplyTime(director, time);

        while (time > 0d)
        {
            // unscaledDeltaTime = timeScale=0 でも進む時間
            time -= Time.unscaledDeltaTime * _closeSpeed;

            ApplyTime(director, Math.Max(0d, time));

            yield return null;
        }

        ApplyTime(director, 0d);

        // 終了の通知で次の処理(タイトルへの遷移など)が始まるので、先に参照を捨てておく
        _waitCoroutine = null;

        onFinished?.Invoke();
    }

    /// <summary>Timeline の時間を決めて、その時間の姿勢を1回だけ反映する</summary>
    private static void ApplyTime(
        PlayableDirector director,
        double time)
    {
        director.time = time;
        director.Evaluate();
    }

    private void StopWait()
    {
        if (_waitCoroutine == null)
        {
            return;
        }

        StopCoroutine(_waitCoroutine);
        _waitCoroutine = null;
    }

    // =========================================================
    // 姿勢の固定
    // =========================================================

    /// <summary>
    /// Timeline の「最初」または「最後」の姿勢を、その場で反映して止める。
    /// atEnd が true なら最後、false なら最初。Director が空なら何もしない。
    /// </summary>
    private static void ApplyPose(
        PlayableDirector director,
        bool atEnd)
    {
        if (director == null)
        {
            return;
        }

        // 再生中なら止めてから、時間を決めて、1回だけ反映する
        director.Pause();

        director.time =
            atEnd
                ? director.duration
                : 0d;

        director.Evaluate();
    }

    // =========================================================
    // 表示
    // =========================================================

    private void SetVisualActive(bool isActive)
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.SetActive(isActive);
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 無効化でコルーチンは止まるので、覚えていた参照だけ捨てる
        _waitCoroutine = null;
    }
}
