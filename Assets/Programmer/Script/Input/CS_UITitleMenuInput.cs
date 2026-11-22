/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// タイトルメニューの入力を受け取る
/// ・入力は CS_CustomInputActionManager の「UI」マップ(PlayerControls.inputactions)から受け取る
///     UI/Navigate : 上下左右の移動(WASD・矢印キー・左スティック・十字キー)
///     UI/Submit   : 決定(F・Enter・Space・パッドの下ボタン)
///     UI/Point    : ポインタの位置(マウス)
///     UI/Click    : クリック(マウス左ボタン)
/// ・入力を「上下移動」「決定」「ポインタ移動」「クリック」の通知に変換するだけで、
///   メニューのことは知らない(通知を受けて動くのは Presenter)
/// ・InputAction の有効化 / 無効化は CS_CustomInputActionManager が行うので、ここでは行わない
///   (ここでやるのは購読と購読解除だけ)
/// ・Presenter と同じ GameObject にアタッチする
/// </summary>
public class CS_UITitleMenuInput : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("スティック入力のしきい値")]

    [Tooltip("これ以上倒したら「1回移動した」と判定する")]
    [Range(0.1f, 0.9f)]
    [SerializeField]
    private float _pressThreshold = 0.5f;

    [Tooltip("これ以下に戻したら「離した」と判定する(次の移動を受け付ける)")]
    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float _releaseThreshold = 0.3f;

    // =========================================================
    // 通知
    // =========================================================

    /// <summary>上下移動した時(-1=前、+1=次)</summary>
    public event Action<int> OnNavigate;

    /// <summary>決定ボタンが押された時</summary>
    public event Action OnSubmit;

    /// <summary>ポインタが動いた時(スクリーン座標)</summary>
    public event Action<Vector2> OnPointerMoved;

    /// <summary>クリックされた時(スクリーン座標)</summary>
    public event Action<Vector2> OnClick;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 購読中の InputAction(購読解除のために覚えておく)
    private InputAction _navigateAction;
    private InputAction _submitAction;
    private InputAction _pointAction;
    private InputAction _clickAction;

    // 移動入力を倒したままか(スティックを倒している間に連続で移動しないようにする)
    private bool _isNavigateHeld;

    // ポインタの前回位置(動いた時だけ通知するため)
    private bool _hasPointerPosition;
    private Vector2 _lastPointerPosition;

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnEnable()
    {
        CS_CustomInputActionManager manager =
            CS_CustomInputActionManager.instance;

        // 「UI」マップ。保存後に CustomInputAction が再生成されていないとここでエラーになる
        var uiActions = manager.customInputAction.TitleUI;

        _navigateAction = uiActions.Navigate;
        _submitAction = uiActions.Submit;
        _pointAction = uiActions.Point;
        _clickAction = uiActions.Click;

        _navigateAction.performed += HandleNavigate;
        _navigateAction.canceled += HandleNavigateCanceled;
        _submitAction.performed += HandleSubmit;
        _pointAction.performed += HandlePoint;
        _clickAction.performed += HandleClick;
    }

    private void OnDisable()
    {
        // Manager は static で残り続けるので、解除しないと
        // シーン遷移後も、壊れた後のこのオブジェクトへ入力が届いてしまう
        if (_navigateAction != null)
        {
            _navigateAction.performed -= HandleNavigate;
            _navigateAction.canceled -= HandleNavigateCanceled;
        }

        if (_submitAction != null)
            _submitAction.performed -= HandleSubmit;

        if (_pointAction != null)
            _pointAction.performed -= HandlePoint;

        if (_clickAction != null)
            _clickAction.performed -= HandleClick;

        _navigateAction = null;
        _submitAction = null;
        _pointAction = null;
        _clickAction = null;

        _isNavigateHeld = false;
        _hasPointerPosition = false;
    }

    // =========================================================
    // 入力ハンドラ
    // =========================================================

    /// <summary>
    /// 移動入力。縦(Y)の入力だけを見る(W/S・↑/↓・スティックと十字キーの上下)
    /// 左右の入力(A/D・←/→・スティックと十字キーの左右)は無視する
    /// スティックは倒している間ずっと値が来るので、
    /// 「しきい値を超えた瞬間の1回だけ」移動として扱う
    /// </summary>
    private void HandleNavigate(InputAction.CallbackContext context)
    {
        float vertical = context.ReadValue<Vector2>().y;
        float magnitude = Mathf.Abs(vertical);

        // ほぼ中央に戻ったら「離した」扱いにして、次の移動を受け付ける
        if (magnitude <= _releaseThreshold)
        {
            _isNavigateHeld = false;
            return;
        }

        // すでに倒している間、または倒し足りない間は何もしない
        if (_isNavigateHeld || magnitude < _pressThreshold)
            return;

        _isNavigateHeld = true;

        // 上(+) = 前の項目、下(-) = 次の項目
        OnNavigate?.Invoke(vertical > 0f ? -1 : 1);
    }

    private void HandleNavigateCanceled(InputAction.CallbackContext context)
    {
        // 入力が完全になくなった(キーを全部離した等)
        _isNavigateHeld = false;
    }

    private void HandleSubmit(InputAction.CallbackContext context)
    {
        OnSubmit?.Invoke();
    }

    /// <summary>
    /// ポインタ位置。実際に動いた時だけ通知する
    /// ・有効になった直後の初期位置の通知は無視する(起動時にマウス下の項目へ選択が飛ばないように)
    /// ・コントローラー操作中は通知しない(置きっぱなしのマウスの位置が選択を奪わないように)
    /// </summary>
    private void HandlePoint(InputAction.CallbackContext context)
    {
        Vector2 position = context.ReadValue<Vector2>();

        if (!_hasPointerPosition)
        {
            _hasPointerPosition = true;
            _lastPointerPosition = position;
            return;
        }

        // 1ピクセル未満の動きは無視する
        if ((position - _lastPointerPosition).sqrMagnitude < 1f)
            return;

        _lastPointerPosition = position;

        // コントローラー操作中のマウスの揺れは無視する
        if (CS_CustomInputActionManager.instance.currentInputType ==
            CS_CustomInputActionManager.InputType.Gamepad)
        {
            return;
        }

        OnPointerMoved?.Invoke(position);
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        // クリックした瞬間のポインタ位置を一緒に渡す
        OnClick?.Invoke(_pointAction.ReadValue<Vector2>());
    }
}