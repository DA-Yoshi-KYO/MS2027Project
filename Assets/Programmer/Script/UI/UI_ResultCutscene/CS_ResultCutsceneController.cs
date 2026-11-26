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

/// <summary>
/// リザルトへのカットシーンの Controller
/// ・Model を生成して Presenter に渡す(Modelの使用者)
/// ・外から PlayCover() / PlayReveal() で動かす
/// ・動きが終わったら OnCovered / OnRevealed で知らせる
///
/// インゲームのシーンでの使い方
///   _startCovered = OFF にする。
///   ゲーム終了時に PlayCover() を呼ぶ → 覆い終わると OnCovered が来るので、そこでシーン遷移する
///
/// リザルトのシーンでの使い方
///   _startCovered = ON にする(画面を覆った状態から始まる)。
///   _autoReveal = ON なら、_revealDelay 秒後に自動で開く
/// </summary>
public class CS_ResultCutsceneController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("カットシーンのPresenter(ResultCutsceneオブジェクト)")]
    [SerializeField]
    private CS_UIResultCutscenePresenter _presenter;

    [Header("最初の状態")]

    [Tooltip("ON: 画面を覆った状態から始める(リザルトのシーン用)。OFF: 画面の上の外から始める(インゲームのシーン用)")]
    [SerializeField]
    private bool _startCovered;

    [Header("リザルトのシーン用")]

    [Tooltip("ON: シーンが始まったら、自動で開く(G2は左へ、G3は右へ)")]
    [SerializeField]
    private bool _autoReveal = true;

    [Tooltip("自動で開き始めるまでの待ち時間(秒)。リザルトの準備ができるまで待たせたい時に使う")]
    [Min(0f)]
    [SerializeField]
    private float _revealDelay = 0.3f;

    // =========================================================
    // 通知
    // =========================================================

    /// <summary>画面を覆い終わった時(インゲームでは、ここでシーン遷移する)</summary>
    public event Action OnCovered;

    /// <summary>画面を開き終わった時</summary>
    public event Action OnRevealed;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private CS_UIResultCutsceneModel _model;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _model = new CS_UIResultCutsceneModel(
            _startCovered
                ? CSE_ResultCutsceneState.Covered
                : CSE_ResultCutsceneState.Hidden
        );

        _model.OnCovered += HandleCovered;
        _model.OnRevealed += HandleRevealed;
    }

    private void Start()
    {
        if (_presenter == null)
        {
            Debug.LogError(
                "[CS_ResultCutsceneController] _presenterが未設定です。"
            );
            return;
        }

        _presenter.BindModel(_model);

        if (_startCovered &&
            _autoReveal)
        {
            StartCoroutine(AutoRevealRoutine());
        }
    }

    // =========================================================
    // 外から使う入口
    // =========================================================

    /// <summary>
    /// 上から下へ動かして画面を覆う。覆い終わると OnCovered が来る。
    /// 戻り値: 始められたか(すでに動いている/覆っている場合は false)
    /// </summary>
    public bool PlayCover()
    {
        return _model.RequestCover();
    }

    /// <summary>
    /// G2を左へ、G3を右へ動かして画面を開く。開き終わると OnRevealed が来る。
    /// 戻り値: 始められたか(覆った状態でない場合は false)
    /// </summary>
    public bool PlayReveal()
    {
        return _model.RequestReveal();
    }

    // =========================================================
    // 自動で開く
    // =========================================================

    private IEnumerator AutoRevealRoutine()
    {
        if (_revealDelay > 0f)
        {
            // WaitForSecondsRealtime = timeScale=0 でも進む待ち時間
            yield return new WaitForSecondsRealtime(_revealDelay);
        }

        _model.RequestReveal();
    }

    // =========================================================
    // Model イベントハンドラ
    // =========================================================

    private void HandleCovered()
    {
        OnCovered?.Invoke();
    }

    private void HandleRevealed()
    {
        OnRevealed?.Invoke();
    }

    // =========================================================
    // OnDestroy
    // =========================================================

    private void OnDestroy()
    {
        OnCovered = null;
        OnRevealed = null;

        if (_model != null)
        {
            _model.OnCovered -= HandleCovered;
            _model.OnRevealed -= HandleRevealed;
            _model.Dispose();
        }
    }
}
