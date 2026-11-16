/* ================================================
 * グラップルの矢印(飛んでいく道筋に並べる山形)のView
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * 2026-11-16 | 的の上に出す矢印から、プレイヤーから的へ向かう道筋に山形を並べて流す形に変更
 * 2026-11-16 | 山形の向きを、カメラに左右されない形(飛ぶ方向の坂に沿って寝かせる)に変更
 * ================================================ */

using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// グラップルの矢印のView。描画とアニメーションだけを持つ。
/// ・画面に貼る2DのUIではなく、ワールド上に置く(道の遠近感が出るように)
/// ・道筋(始点 → 終点)の上に、山形(>)を等間隔に並べ、的の方へ一定の速さで流す
///   始点から_startOffset先から並べ始め、終点の_endOffset手前で終える(プレイヤーの体に重ならず、的に被らないように)
///   山形の数は「区間の長さ ÷ _spacing」で決め、_maxChevronCountまで。近すぎて1つも置けない時は出さない
///   終点まで進んだ山形は、始点から出直す。始点側・終点側の端(_edgeFadeLength)は、薄くなってなめらかに消える
/// ・山形の先は飛ぶ方向を向き、面は飛ぶ方向の坂に沿って寝かせる(幅は水平、長さは飛ぶ方向。道路に描いた矢印のような置き方)
///   カメラの位置・向きは、矢印の向きの計算に使わない(どこから見ても、矢印の向きは空間に固定されている。両面描画)
/// ・出し入れは短いフェード(_fadeTime)
/// ・山形は、最初にまとめて作って使い回す(毎フレーム作らない・壊さない)。シーン直下の「GrappleArrowChevrons」の子(Canvasの大きさに影響されない)
/// 時間はTime.timeScaleの影響を受けない。
/// </summary>
public class CS_UIGrappleArrowView :
    CS_BaseView<CS_UIGrappleArrowPresenter>
{
    // 山形1つ分
    private class Chevron
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Mesh mesh;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("見た目")]
    [Tooltip("山形のマテリアル(HDRP/Unlit の透過。色と濃さはMaterialPropertyBlockの _UnlitColor で渡す。今は MT_GrappleArrow)")]
    [SerializeField] private Material _material;

    [Tooltip("矢印の色(アルファが一番濃い時の濃さ)")]
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(0.5f, 1.6f, 2f, 1f);

    [Tooltip("山形1つの大きさ(m。x: 進行方向の長さ, y: 幅)")]
    [SerializeField] private Vector2 _chevronSize = new Vector2(3.2f, 1.2f);

    [Header("並べ方")]
    [Tooltip("山形どうしの間隔(m)")]
    [SerializeField][Min(0.1f)] private float _spacing = 3.5f;

    [Tooltip("山形の数の上限(最初にこの数だけ作って使い回す)")]
    [SerializeField][Range(1, 64)] private int _maxChevronCount = 12;

    [Tooltip("始点(プレイヤーの体の中心あたり)から、並べ始めるまでの距離(m。体に重ならないように)")]
    [SerializeField][Min(0f)] private float _startOffset = 2.5f;

    [Tooltip("終点(的へ引っ張られる先)の、どれだけ手前で終えるか(m)")]
    [SerializeField][Min(0f)] private float _endOffset = 1f;

    [Header("動き")]
    [Tooltip("山形が的の方へ流れる速さ(m/秒。0で止まる)")]
    [SerializeField][Min(0f)] private float _flowSpeed = 6f;

    [Tooltip("始点側・終点側の端で、薄くなってなめらかに消える長さ(m)")]
    [SerializeField][Min(0f)] private float _edgeFadeLength = 1.5f;

    [Tooltip("出し入れのフェードの時間(秒。0ですぐ出し入れ)")]
    [SerializeField][Min(0f)] private float _fadeTime = 0.15f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private static readonly int _colorId = Shader.PropertyToID("_UnlitColor");   // HDRP/Unlitの色

    private Transform _root;                                   // 山形を置く場所(シーン直下)
    private MaterialPropertyBlock _propertyBlock;              // 山形の色と濃さを渡す(共有マテリアルを書き換えないため)
    private Chevron[] _chevrons = new Chevron[0];
    private bool _isTargetVisible;                             // 出すか(Presenterから)
    private float _fade;                                       // 今の濃さの割合(0〜1)
    private float _flowDistance;                               // 山形が流れた距離(m。足し込んだ値)

    /// <summary>出ている最中か(消えるフェードの間も含む)</summary>
    public bool isShown => _fade > 0f;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        CreateChevrons();
        SetRenderersEnabled(0);
    }

    private void OnDestroy()
    {
        foreach (Chevron chevron in _chevrons)
        {
            if (chevron.mesh != null) Destroy(chevron.mesh);
        }

        if (_root != null) Destroy(_root.gameObject);
    }

    private void OnDisable()
    {
        _isTargetVisible = false;
        _fade = 0f;
        SetRenderersEnabled(0);
    }

    // =========================================================
    // 表示・非表示
    // =========================================================

    /// <summary>矢印を出す・消す(フェードしながら)</summary>
    public void SetVisible(bool isVisible)
    {
        _isTargetVisible = isVisible;
    }

    /// <summary>
    /// 道筋の始点・終点を渡す。フェードと流れもここで進める(Presenterが毎フレーム、出ている間に呼ぶ)
    /// </summary>
    public void SetLine(Vector3 start, Vector3 end)
    {
        UpdateFade();

        // 山形を置ける区間(始点から_startOffset先 〜 終点の_endOffset手前)
        Vector3 line = end - start;
        float length = line.magnitude;
        float usable = length - _startOffset - _endOffset;
        int count = Mathf.Min(_chevrons.Length, Mathf.FloorToInt(usable / _spacing));
        if (_fade <= 0f || count < 1 || length < 0.0001f)
        {
            SetRenderersEnabled(0);
            return;
        }

        Vector3 direction = line / length;
        float region = count * _spacing;    // 山形が流れる区間の長さ(この中で、終点まで進んだら始点から出直す)
        _flowDistance = Mathf.Repeat(_flowDistance + _flowSpeed * Time.unscaledDeltaTime, region);
        SetRenderersEnabled(count);

        Quaternion rotation = GetRotation(direction);
        for (int i = 0; i < count; i++)
        {
            // 区間の中の位置(始点側から)。終点まで進んだら、始点側へ出直す
            float along = Mathf.Repeat(i * _spacing + _flowDistance, region);

            // 始点側・終点側の端で薄くなる
            float edge = _edgeFadeLength <= 0f ? 1f : Mathf.Clamp01(Mathf.Min(along, region - along) / _edgeFadeLength);

            Chevron chevron = _chevrons[i];
            chevron.transform.SetPositionAndRotation(start + direction * (_startOffset + along), rotation);
            ApplyColor(chevron, edge * _fade);
        }
    }

    // =========================================================
    // 内部処理
    // =========================================================

    // 山形の向き: 先は飛ぶ方向を向き、面は飛ぶ方向の坂に沿って寝かせる(道路に描いた矢印のような置き方)
    // 面の法線は「飛ぶ方向」と「水平な横方向(飛ぶ方向 × 真上)」の両方に垂直な向き。つまり幅は水平、長さは飛ぶ方向
    // カメラの位置・向きは使わない(どこから見ても、矢印の向きは空間に固定されている)
    private static Quaternion GetRotation(Vector3 direction)
    {
        // 水平な横方向。飛ぶ方向がほぼ真上・真下で決まらない時は、世界の右(X)を使う
        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(direction, Vector3.right);
        side.Normalize();

        // Quadの幅(+Y)を横方向、長さ(+X)を飛ぶ方向にする。+Zは、Cross(飛ぶ方向, 横方向)
        return Quaternion.LookRotation(Vector3.Cross(direction, side), side);
    }

    // 出す・消すに合わせて、濃さを一定の速さで変える
    private void UpdateFade()
    {
        float target = _isTargetVisible ? 1f : 0f;
        _fade = _fadeTime > 0f
            ? Mathf.MoveTowards(_fade, target, Time.unscaledDeltaTime / _fadeTime)
            : target;
    }

    // 山形を作る(実行時に、シーン直下へ。上限の数だけまとめて作って使い回す)
    private void CreateChevrons()
    {
        _root = new GameObject("GrappleArrowChevrons").transform;
        _propertyBlock = new MaterialPropertyBlock();

        _chevrons = new Chevron[Mathf.Max(1, _maxChevronCount)];
        for (int i = 0; i < _chevrons.Length; i++)
        {
            GameObject chevronObject = new GameObject($"Chevron{i}");
            chevronObject.transform.SetParent(_root, false);

            Chevron chevron = new Chevron { transform = chevronObject.transform };
            chevron.mesh = CreateQuadMesh(_chevronSize);
            chevronObject.AddComponent<MeshFilter>().sharedMesh = chevron.mesh;

            chevron.renderer = chevronObject.AddComponent<MeshRenderer>();
            chevron.renderer.sharedMaterial = _material;
            chevron.renderer.shadowCastingMode = ShadowCastingMode.Off;
            chevron.renderer.receiveShadows = false;
            _chevrons[i] = chevron;
        }
    }

    // 山形1つ分のQuad(中心が原点。X: 進行方向、Y: 幅。-Z側が表)
    private static Mesh CreateQuadMesh(Vector2 size)
    {
        float halfX = size.x * 0.5f;
        float halfY = size.y * 0.5f;

        Mesh mesh = new Mesh { name = "GrappleArrowChevron" };
        mesh.vertices = new[]
        {
            new Vector3(-halfX, -halfY, 0f),
            new Vector3(-halfX, halfY, 0f),
            new Vector3(halfX, halfY, 0f),
            new Vector3(halfX, -halfY, 0f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // 色(色 × 濃さ。アルファが濃さ)をMaterialPropertyBlockで渡す
    private void ApplyColor(Chevron chevron, float strength)
    {
        Color color = _color;
        color.a *= strength;

        chevron.renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_colorId, color);
        chevron.renderer.SetPropertyBlock(_propertyBlock);
    }

    // 先頭からcount個だけ描く(残りは描かない)
    private void SetRenderersEnabled(int count)
    {
        for (int i = 0; i < _chevrons.Length; i++)
        {
            Chevron chevron = _chevrons[i];
            bool isEnabled = i < count;
            if (chevron.renderer != null && chevron.renderer.enabled != isEnabled) chevron.renderer.enabled = isEnabled;
        }
    }
}
