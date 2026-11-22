/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-20 | 初回作成
 * ================================================ */

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// タイトルメニューの描画を担当するView
/// ・選択フレーム(画像)を、選択中の項目の位置へ移動する
/// ・画面上の座標がどの項目の上かを調べる(マウス用)
/// ・「いつ選択を変えるか」「決定するか」は判断しない(Presenter の仕事)
/// ・Presenter / Input と同じ GameObject にアタッチする
/// </summary>
public sealed class CS_UITitleMenuView :
    CS_BaseView<CS_UITitleMenuPresenter>
{
    // =========================================================
    // 項目の設定
    // =========================================================

    /// <summary>
    /// メニュー項目1つ分の設定。
    /// 項目の種類と、その項目の見た目(RectTransform)を対応づける。
    /// </summary>
    [Serializable]
    private sealed class ItemSet
    {
        [SerializeField]
        private CSE_TitleMenuItem _itemType;

        [SerializeField]
        private RectTransform _rect;

        public CSE_TitleMenuItem itemType =>
            _itemType;

        public RectTransform rect =>
            _rect;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("選択フレーム(画像)")]
    [Tooltip("選択中の項目へ移動するフレーム。Layout Group の子には置かない")]
    [SerializeField]
    private RectTransform _frame;

    [Header("メニュー項目(SoloMode / MultiMode / End)")]
    [Tooltip("項目の種類と、ボタン画像のRectTransformを対応づける。順番は問わない")]
    [SerializeField]
    private ItemSet[] _items;

    [Header("フレーム移動")]
    [Tooltip("フレームが項目へ移動する時間(秒)。0なら即移動")]
    [Min(0f)]
    [SerializeField]
    private float _moveDuration = 0.12f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 項目番号(enumの値)で引ける RectTransform の一覧
    private RectTransform[] _itemRects;

    // マウスの当たり判定に使うカメラ(Overlay の Canvas なら null)
    private Camera _uiCamera;

    // 今動いているフレーム移動(途中で止めるために覚えておく)
    private Coroutine _moveCoroutine;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        int itemCount = Enum.GetValues(typeof(CSE_TitleMenuItem)).Length;
        _itemRects = new RectTransform[itemCount];

        // enumの値を番号にして、RectTransform を並べ直す(Inspectorの並び順に依存しない)
        if (_items != null)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                ItemSet item = _items[i];

                if (item == null ||
                    item.rect == null)
                {
                    continue;
                }

                int index = (int)item.itemType;

                if (index < 0 ||
                    index >= itemCount)
                {
                    continue;
                }

                _itemRects[index] = item.rect;
            }
        }

        for (int i = 0; i < itemCount; i++)
        {
            if (_itemRects[i] == null)
            {
                Debug.LogWarning(
                    $"{nameof(CS_UITitleMenuView)}: " +
                    $"メニュー項目が設定されていません: {(CSE_TitleMenuItem)i}",
                    this
                );
            }
        }

        // Overlay 以外の Canvas では、当たり判定にカメラが必要
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            canvas = canvas.rootCanvas;

            _uiCamera =
                canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
        }
    }

    // =========================================================
    // 選択フレーム
    // =========================================================

    /// <summary>
    /// 選択フレームを、指定した項目の位置へ移動する。
    /// animate が false なら即座に移動する(最初の表示用)。
    /// </summary>
    public void SetSelected(
        int index,
        bool animate)
    {
        if (_frame == null)
        {
            return;
        }

        if (!TryGetItemRect(
                index,
                out RectTransform itemRect))
        {
            return;
        }

        // 起動直後は Layout Group などの配置が未確定のことがあるので、
        // 即移動の時は先に配置を確定させてから位置を読む
        if (!animate)
        {
            Canvas.ForceUpdateCanvases();
        }

        Vector3 targetPosition =
            GetFrameTargetPosition(itemRect);

        StopMove();

        if (!animate ||
            _moveDuration <= 0f ||
            !isActiveAndEnabled)
        {
            _frame.position = targetPosition;
            return;
        }

        _moveCoroutine =
            StartCoroutine(
                MoveRoutine(targetPosition)
            );
    }

    /// <summary>
    /// フレームが項目の中央に重なる位置(ワールド座標)を求める。
    /// フレームの Pivot が中央でなくてもずれないようにしてある。
    /// </summary>
    private Vector3 GetFrameTargetPosition(
        RectTransform itemRect)
    {
        Vector3 itemCenter =
            itemRect.TransformPoint(
                itemRect.rect.center
            );

        Vector3 frameCenter =
            _frame.TransformPoint(
                _frame.rect.center
            );

        // フレームの基準点(Pivot)と中心のずれ
        Vector3 pivotOffset =
            _frame.position - frameCenter;

        return itemCenter + pivotOffset;
    }

    /// <summary>
    /// フレームを目標位置まで少しずつ動かす。
    /// 最初は速く、終わりに近づくほどゆっくりになる。
    /// </summary>
    private IEnumerator MoveRoutine(
        Vector3 targetPosition)
    {
        Vector3 startPosition =
            _frame.position;

        float elapsed = 0f;

        while (elapsed < _moveDuration)
        {
            // unscaledDeltaTime = timeScale=0 でも進む時間
            elapsed +=
                Time.unscaledDeltaTime;

            float rate =
                Mathf.Clamp01(
                    elapsed /
                    _moveDuration
                );

            // EaseOutCubic
            float inverse = 1f - rate;

            float easedRate =
                1f -
                inverse *
                inverse *
                inverse;

            _frame.position =
                Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    easedRate
                );

            yield return null;
        }

        _frame.position = targetPosition;
        _moveCoroutine = null;
    }

    private void StopMove()
    {
        if (_moveCoroutine == null)
        {
            return;
        }

        StopCoroutine(_moveCoroutine);
        _moveCoroutine = null;
    }

    // =========================================================
    // 当たり判定(マウス用)
    // =========================================================

    /// <summary>
    /// スクリーン座標が、どの項目の上にあるかを返す。
    /// どの項目の上でもなければ -1。
    /// </summary>
    public int GetItemIndexAt(
        Vector2 screenPoint)
    {
        if (_itemRects == null)
        {
            return -1;
        }

        for (int i = 0;
             i < _itemRects.Length;
             i++)
        {
            RectTransform itemRect =
                _itemRects[i];

            if (itemRect == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    itemRect,
                    screenPoint,
                    _uiCamera))
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // 項目の取得
    // =========================================================

    private bool TryGetItemRect(
        int index,
        out RectTransform itemRect)
    {
        itemRect = null;

        if (_itemRects == null ||
            index < 0 ||
            index >= _itemRects.Length)
        {
            return false;
        }

        itemRect = _itemRects[index];
        return itemRect != null;
    }

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnDisable()
    {
        // 無効化でコルーチンは止まるので、覚えていた参照だけ捨てる
        _moveCoroutine = null;
    }

    // =========================================================
    // Inspector確認
    // =========================================================

    private void OnValidate()
    {
        if (_items == null)
        {
            return;
        }

        int itemCount =
            Enum.GetValues(typeof(CSE_TitleMenuItem)).Length;

        if (_items.Length != itemCount)
        {
            Debug.LogWarning(
                $"{nameof(CS_UITitleMenuView)}: " +
                $"メニュー項目は{itemCount}個必要です。 " +
                $"現在: {_items.Length}個",
                this
            );
        }
    }
}