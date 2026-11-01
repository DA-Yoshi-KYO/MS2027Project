/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * 2026-11-02 | Modelは使う側で作ってBindする形に変更(Initializeを削除)
 * ================================================ */

using System.Collections;
using R3;
using UnityEngine;

/// <summary>
/// 悪人撃退スコアポップアップのPresenter。
/// Modelの表示要求を受け、Viewへ上昇・フェードを反映する。
/// </summary>
[RequireComponent(typeof(CS_UIVillainScorePopupView))]
public sealed class CS_UIVillainScorePopupPresenter : CS_BasePresenter
{
    [Header("表示時間")]
    [Min(0f)]
    [SerializeField] private float _fadeInDuration = 0.2f;

    [Min(0f)]
    [SerializeField] private float _holdDuration = 0.5f;

    [Min(0f)]
    [SerializeField] private float _fadeOutDuration = 0.3f;

    [Header("上昇量（ワールド座標）")]
    [Min(0f)]
    [SerializeField] private float _fadeInMoveDistance = 0.2f;

    [Min(0f)]
    [SerializeField] private float _fadeOutMoveDistance = 0.3f;

    [Header("カメラ（未設定ならCamera.main）")]
    [SerializeField] private Camera _targetCamera;

    [SerializeField] private bool _lookAtCamera = true;

    private CS_UIVillainScorePopupModel _model;
    private CS_UIVillainScorePopupView _view;
    private readonly SerialDisposable _modelSubscription = new();
    private Coroutine _animationCoroutine;

    private void Awake()
    {
        _view = GetComponent<CS_UIVillainScorePopupView>();
        _view.SetPresenter(this);
        _view.SetAlpha(0f);

        _modelSubscription.AddTo(_disposables);
    }

    private void LateUpdate()
    {
        if (!_lookAtCamera)
            return;

        if (_targetCamera == null)
            _targetCamera = Camera.main;

        _view.LookAtCamera(_targetCamera);
    }

    /// <summary>
    /// このポップアップ専用のModelをBindする。
    /// Modelは使う側(CS_PlayerScoringなど)で作り、Bind後にShow()で再生する。
    /// ポップアップは表示が終わると自分で消えるので、BindしたModelはこのPresenterが破棄する。
    /// </summary>
    public void BindModel(
        CS_UIVillainScorePopupModel model,
        Camera targetCamera = null)
    {
        if (targetCamera != null)
            _targetCamera = targetCamera;
        else if (_targetCamera == null)
            _targetCamera = Camera.main;

        _model = model;

        if (_model == null)
            return;

        _modelSubscription.Disposable =
            _model.playRequested.Subscribe(_ => Play());
    }

    private void Play()
    {
        if (_model == null)
            return;

        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);

        _view.SetScore(_model.currentScore);
        _view.SetWorldPosition(_model.currentWorldPosition);
        _view.SetAlpha(0f);
        _view.LookAtCamera(_targetCamera);

        _animationCoroutine = StartCoroutine(
            PlayAnimation()
        );
    }

    private IEnumerator PlayAnimation()
    {
        Vector3 startPosition = _model.currentWorldPosition;
        Vector3 fadeInEndPosition =
            startPosition + Vector3.up * _fadeInMoveDistance;

        Vector3 fadeOutEndPosition =
            fadeInEndPosition + Vector3.up * _fadeOutMoveDistance;

        yield return AnimatePhase(
            startPosition,
            fadeInEndPosition,
            0f,
            1f,
            _fadeInDuration
        );

        _view.SetWorldPosition(fadeInEndPosition);
        _view.SetAlpha(1f);

        if (_holdDuration > 0f)
            yield return new WaitForSecondsRealtime(_holdDuration);

        yield return AnimatePhase(
            fadeInEndPosition,
            fadeOutEndPosition,
            1f,
            0f,
            _fadeOutDuration
        );

        _animationCoroutine = null;
        _model?.Dispose();
        _model = null;
        Destroy(gameObject);
    }

    private IEnumerator AnimatePhase(
        Vector3 fromPosition,
        Vector3 toPosition,
        float fromAlpha,
        float toAlpha,
        float duration)
    {
        if (duration <= 0f)
        {
            _view.SetWorldPosition(toPosition);
            _view.SetAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float rate = Mathf.Clamp01(
                elapsed / duration
            );

            float easedRate = Mathf.SmoothStep(
                0f,
                1f,
                rate
            );

            _view.SetWorldPosition(
                Vector3.Lerp(
                    fromPosition,
                    toPosition,
                    easedRate
                )
            );

            _view.SetAlpha(
                Mathf.Lerp(
                    fromAlpha,
                    toAlpha,
                    easedRate
                )
            );

            yield return null;
        }

        _view.SetWorldPosition(toPosition);
        _view.SetAlpha(toAlpha);
    }

    protected override void OnDestroy()
    {
        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);

        _modelSubscription.Disposable = null;
        _model?.Dispose();
        _model = null;

        base.OnDestroy();
    }

    private void OnValidate()
    {
        _fadeInDuration = Mathf.Max(0f, _fadeInDuration);
        _holdDuration = Mathf.Max(0f, _holdDuration);
        _fadeOutDuration = Mathf.Max(0f, _fadeOutDuration);
        _fadeInMoveDistance = Mathf.Max(0f, _fadeInMoveDistance);
        _fadeOutMoveDistance = Mathf.Max(0f, _fadeOutMoveDistance);
    }
}
