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
/// ・選択中の項目は「選択時の画像」、それ以外は「通常の画像」を表示する(ON/OFFの切り替え)
/// ・選択フレーム(画像)を使う場合は、選択中の項目の位置へ移動する(未設定なら使わない)
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
    /// 項目の種類と、その項目の見た目を対応づける。
    ///
    /// ・rect           : 項目の範囲(マウスの当たり判定と、フレームの移動先に使う)
    /// ・normalObject   : 通常の画像(選択されていない時だけ表示する)
    /// ・selectedObject : 選択時の画像(選択されている時だけ表示する)
    /// </summary>
    [Serializable]
    private sealed class ItemSet
    {
        [SerializeField]
        private CSE_TitleMenuItem _itemType;

        [Tooltip("項目の範囲。通常の画像と選択時の画像の親にする")]
        [SerializeField]
        private RectTransform _rect;

        [Tooltip("通常の画像のGameObject。選択されていない時に表示する")]
        [SerializeField]
        private GameObject _normalObject;

        [Tooltip("選択時の画像のGameObject。選択されている時に表示する")]
        [SerializeField]
        private GameObject _selectedObject;

        public CSE_TitleMenuItem itemType =>
            _itemType;

        public RectTransform rect =>
            _rect;

        public GameObject normalObject =>
            _normalObject;

        public GameObject selectedObject =>
            _selectedObject;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("メニュー項目(SoloMode / MultiMode / End)")]
    [Tooltip("項目の種類と、通常/選択時の画像を対応づける。順番は問わない")]
    [SerializeField]
    private ItemSet[] _items;

    [Header("選択フレーム(任意)")]
    [Tooltip("使う場合だけ設定する。選択中の項目へ移動する。空なら使わない。Layout Group の子には置かない")]
    [SerializeField]
    private RectTransform _frame;

    [Tooltip("フレームが項目へ移動する時間(秒)。0なら即移動")]
    [Min(0f)]
    [SerializeField]
    private float _moveDuration = 0.12f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 項目番号(enumの値)で引ける設定の一覧
    private ItemSet[] _itemSets;

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
        _itemSets = new ItemSet[itemCount];

        // enumの値を番号にして、設定を並べ直す(Inspectorの並び順に依存しない)
        if (_items != null)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                ItemSet item = _items[i];

                if (item == null)
                {
                    continue;
                }

                int index = (int)item.itemType;

                if (index < 0 ||
                    index >= itemCount)
                {
                    continue;
                }

                _itemSets[index] = item;
            }
        }

        for (int i = 0; i < itemCount; i++)
        {
            ItemSet item = _itemSets[i];

            if (item == null ||
                item.rect == null)
            {
                Debug.LogWarning(
                    $"{nameof(CS_UITitleMenuView)}: " +
                    $"メニュー項目の範囲(Rect)が設定されていません: {(CSE_TitleMenuItem)i}",
                    this
                );

                continue;
            }

            if (item.normalObject == null ||
                item.selectedObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(CS_UITitleMenuView)}: " +
                    "通常の画像または選択時の画像が設定されていません: " +
                    $"{(CSE_TitleMenuItem)i}",
                    this
                );
            }
            else if (item.normalObject == item.selectedObject)
            {
                Debug.LogWarning(
                    $"{nameof(CS_UITitleMenuView)}: " +
                    "通常の画像と選択時の画像に同じGameObjectが設定されています: " +
                    $"{(CSE_TitleMenuItem)i}",
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

        // Presenter から最初の選択が届くまでの間も、全項目を通常の画像にしておく
        ApplySelectionVisual(-1);
    }

    // =========================================================
    // 選択の表示
    // =========================================================

    /// <summary>
    /// 選択中の項目を表示する。Presenter から呼ばれる。
    /// ・選択中の項目は「選択時の画像」ON、「通常の画像」OFF
    /// ・それ以外の項目は「選択時の画像」OFF、「通常の画像」ON
    /// ・選択フレームを設定している場合は、フレームも選択中の項目へ移動する
    /// animate が false なら、フレームを即座に移動する(最初の表示用)。
    /// </summary>
    public void SetSelected(
        int index,
        bool animate)
    {
        ApplySelectionVisual(index);
        MoveFrame(index, animate);
    }

    /// <summary>
    /// 全項目の「通常の画像」と「選択時の画像」を、ON/OFFで切り替える。
    /// selectedIndex が範囲外(-1など)なら、全項目が通常の画像になる。
    /// </summary>
    private void ApplySelectionVisual(
        int selectedIndex)
    {
        if (_itemSets == null)
        {
            return;
        }

        for (int i = 0;
             i < _itemSets.Length;
             i++)
        {
            ItemSet item = _itemSets[i];

            if (item == null)
            {
                continue;
            }

            bool isSelected =
                i == selectedIndex;

            if (item.normalObject != null)
            {
                item.normalObject.SetActive(
                    !isSelected
                );
            }

            if (item.selectedObject != null)
            {
                item.selectedObject.SetActive(
                    isSelected
                );
            }
        }
    }

    // =========================================================
    // 選択フレーム(任意)
    // =========================================================

    /// <summary>
    /// 選択フレームを、指定した項目の位置へ移動する。
    /// フレームが未設定なら何もしない。
    /// </summary>
    private void MoveFrame(
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
        if (_itemSets == null)
        {
            return -1;
        }

        for (int i = 0;
             i < _itemSets.Length;
             i++)
        {
            if (!TryGetItemRect(
                    i,
                    out RectTransform itemRect))
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

        if (_itemSets == null ||
            index < 0 ||
            index >= _itemSets.Length ||
            _itemSets[index] == null)
        {
            return false;
        }

        itemRect = _itemSets[index].rect;
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
