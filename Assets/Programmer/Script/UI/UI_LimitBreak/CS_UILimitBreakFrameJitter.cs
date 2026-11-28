/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// タイトルメニューの選択フレームを「あらぶらせる」アニメーション
/// ・黒と赤の2枚の画像を、それぞれランダムな方向へ動かし続ける
/// ・2枚は別々のタイミング・別々の方向に動くので、ずれて重なって見える
/// ・動くのは画像(子)だけ。フレーム全体(親)の移動は CS_UITitleMenuView が行うので干渉しない
/// ・SelectFrame(CS_UITitleMenuView の _frame に割り当てたオブジェクト)にアタッチする
/// ・View / Presenter / Model とは独立した、見た目だけの演出
///
/// 動き方
///   一定時間ごとに「新しい行き先(ランダムな方向・距離)」を決めて、そこへ素早く動く。
///   行き先を変える間隔も毎回ランダムなので、規則的でない動きになる。
/// </summary>
public class CS_UILimitBreakFrameJitter : MonoBehaviour
{
    // =========================================================
    // 画像1枚ぶんの動きの状態
    // =========================================================

    /// <summary>
    /// 画像1枚ぶんの、実行中の状態。
    /// 基準位置(動く前の位置)、今のずれ、行き先、次に行き先を変える時刻を覚えておく。
    /// </summary>
    private sealed class JitterState
    {
        private readonly RectTransform _rect;
        private readonly Vector2 _basePosition;

        private Vector2 _offset;
        private Vector2 _velocity;
        private Vector2 _targetOffset;
        private float _nextChangeTime;

        public RectTransform rect =>
            _rect;

        public Vector2 basePosition =>
            _basePosition;

        public Vector2 offset
        {
            get => _offset;
            set => _offset = value;
        }

        public Vector2 velocity
        {
            get => _velocity;
            set => _velocity = value;
        }

        public Vector2 targetOffset
        {
            get => _targetOffset;
            set => _targetOffset = value;
        }

        public float nextChangeTime
        {
            get => _nextChangeTime;
            set => _nextChangeTime = value;
        }

        public JitterState(
            RectTransform rect)
        {
            _rect = rect;
            _basePosition = rect.anchoredPosition;

            _offset = Vector2.zero;
            _velocity = Vector2.zero;
            _targetOffset = Vector2.zero;

            // 0 にしておくと、最初のフレームですぐ行き先が決まる
            _nextChangeTime = 0f;
        }
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("画像(SelectFrameの子)")]

    [Tooltip("黒のフレーム画像")]
    [SerializeField]
    private RectTransform _blackRect;

    [Tooltip("赤のフレーム画像")]
    [SerializeField]
    private RectTransform _redRect;

    [Header("動く範囲")]

    [Tooltip("基準位置から動く最大の距離(ピクセル)。X が横、Y が縦")]
    [SerializeField]
    private Vector2 _moveRange = new Vector2(14f, 10f);

    [Tooltip("1回の移動の最小距離。最大距離に対する割合(0.4なら最大の4割以上は動く)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float _minDistanceRate = 0.4f;

    [Header("動くタイミング")]

    [Tooltip("行き先を変える間隔の最小(秒)")]
    [Min(0.01f)]
    [SerializeField]
    private float _changeIntervalMin = 0.04f;

    [Tooltip("行き先を変える間隔の最大(秒)。最小と最大の間でランダムに決まる")]
    [Min(0.01f)]
    [SerializeField]
    private float _changeIntervalMax = 0.14f;

    [Tooltip("行き先へ動く速さ(秒)。小さいほどカクカク素早く、大きいほどなめらか。0なら瞬間移動")]
    [Min(0f)]
    [SerializeField]
    private float _smoothTime = 0.03f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private JitterState _blackState;
    private JitterState _redState;

    // =========================================================
    // Unityイベント
    // =========================================================

    private void OnEnable()
    {
        // 有効になった時点の位置を「基準位置」として覚える
        _blackState = CreateState(_blackRect);
        _redState = CreateState(_redRect);
    }

    private void OnDisable()
    {
        // 無効になる時は、基準位置へ戻しておく(ずれたまま残らないように)
        RestoreBasePosition(_blackState);
        RestoreBasePosition(_redState);

        _blackState = null;
        _redState = null;
    }

    private void Update()
    {
        UpdateJitter(_blackState);
        UpdateJitter(_redState);
    }

    // =========================================================
    // 状態の生成 / 復元
    // =========================================================

    private JitterState CreateState(
        RectTransform rect)
    {
        if (rect == null)
        {
            return null;
        }

        return new JitterState(rect);
    }

    private void RestoreBasePosition(
        JitterState state)
    {
        if (state == null ||
            state.rect == null)
        {
            return;
        }

        state.rect.anchoredPosition =
            state.basePosition;
    }

    // =========================================================
    // 動き
    // =========================================================

    /// <summary>
    /// 画像1枚を動かす。毎フレーム呼ばれる。
    /// 1. 行き先を変える時刻になったら、新しい行き先とつぎの時刻を決める
    /// 2. 今のずれを、行き先へ近づける
    /// 3. 基準位置 + ずれ を、画像の位置にする
    /// </summary>
    private void UpdateJitter(
        JitterState state)
    {
        if (state == null ||
            state.rect == null)
        {
            return;
        }

        // unscaledTime = timeScale=0 でも進む時間
        float now =
            Time.unscaledTime;

        if (now >= state.nextChangeTime)
        {
            state.targetOffset =
                PickRandomOffset();

            state.nextChangeTime =
                now +
                Random.Range(
                    _changeIntervalMin,
                    _changeIntervalMax
                );
        }

        if (_smoothTime <= 0f)
        {
            // 瞬間移動
            state.offset =
                state.targetOffset;
        }
        else
        {
            // 行き先へ素早く近づける(プロパティは ref で渡せないので、いったん変数に取り出す)
            Vector2 velocity =
                state.velocity;

            state.offset =
                Vector2.SmoothDamp(
                    state.offset,
                    state.targetOffset,
                    ref velocity,
                    _smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );

            state.velocity =
                velocity;
        }

        state.rect.anchoredPosition =
            state.basePosition +
            state.offset;
    }

    /// <summary>
    /// ランダムな方向・距離の「ずれ」を1つ決める。
    /// 方向は全方向からランダム、距離は「最小の割合 〜 最大」の間でランダム。
    /// </summary>
    private Vector2 PickRandomOffset()
    {
        Vector2 direction =
            Random.insideUnitCircle;

        // ほぼ真ん中が出た時は、方向が決まらないので固定の向きにする
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        float distanceRate =
            Random.Range(
                _minDistanceRate,
                1f
            );

        return new Vector2(
            direction.x *
            _moveRange.x,

            direction.y *
            _moveRange.y
        ) * distanceRate;
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _moveRange =
            new Vector2(
                Mathf.Max(
                    0f,
                    _moveRange.x
                ),

                Mathf.Max(
                    0f,
                    _moveRange.y
                )
            );

        _changeIntervalMin =
            Mathf.Max(
                0.01f,
                _changeIntervalMin
            );

        // 最大が最小より小さくならないようにする
        _changeIntervalMax =
            Mathf.Max(
                _changeIntervalMin,
                _changeIntervalMax
            );

        _smoothTime =
            Mathf.Max(
                0f,
                _smoothTime
            );
    }
}
