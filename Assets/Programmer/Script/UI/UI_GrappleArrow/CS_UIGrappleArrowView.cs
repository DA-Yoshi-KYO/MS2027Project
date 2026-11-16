/* ================================================
 * グラップルの矢印(飛んでいく方向を示す矢印)のView
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #505)
 * ================================================ */

using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// グラップルの矢印のView。描画とアニメーションだけを持つ。
/// ・画面に貼る2DのUIではなく、ワールド上に置く(的を正面から見ても、矢印の向きが奥行きになって分からなくならないように)
///   矢印は、進行方向の軸まわりで、面がカメラの方を向く
/// ・見た目の仮: 山形(>>>)が、飛ぶ方向へ流れる。両端で薄くなる
/// ・出し入れは短いフェード(_fadeTime)
/// ・山形は、シーン直下の「GrappleArrow」の子に実行時に作る(Canvasの子にしないので、Canvasの大きさに影響されない)
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
        public Color[] colors = new Color[4];
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("見た目")]
    [Tooltip("山形のマテリアル(頂点カラーを使うもの。今は Nova の MT_GrappleArrow)")]
    [SerializeField] private Material _material;

    [Tooltip("矢印の色(アルファが一番濃い時の濃さ)")]
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(0.5f, 1.6f, 2f, 1f);

    [Tooltip("山形の数")]
    [SerializeField][Min(1)] private int _chevronCount = 3;

    [Tooltip("山形1つの大きさ(m。x: 進行方向の長さ, y: 幅)")]
    [SerializeField] private Vector2 _chevronSize = new Vector2(0.7f, 0.9f);

    [Tooltip("山形どうしの間隔(m)")]
    [SerializeField][Min(0.01f)] private float _spacing = 0.8f;

    [Header("動き")]
    [Tooltip("山形が飛ぶ方向へ流れる速さ(m/秒。0で止まる)")]
    [SerializeField][Min(0f)] private float _flowSpeed = 1.5f;

    [Tooltip("矢印の両端で薄くなる範囲(矢印の長さに対する割合)")]
    [SerializeField][Range(0f, 0.5f)] private float _edgeFade = 0.3f;

    [Tooltip("出し入れのフェードの時間(秒。0ですぐ出し入れ)")]
    [SerializeField][Min(0f)] private float _fadeTime = 0.15f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Transform _root;                                   // 山形を置く場所(シーン直下)
    private Chevron[] _chevrons = new Chevron[0];
    private bool _isTargetVisible;                             // 出すか(Presenterから)
    private float _fade;                                       // 今の濃さの割合(0〜1)
    private float _phase;                                      // 流れの進み具合(0〜1で一周)

    /// <summary>出ている最中か(消えるフェードの間も含む)</summary>
    public bool isShown => _fade > 0f;

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        CreateChevrons();
        SetRenderersEnabled(false);
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
        SetRenderersEnabled(false);
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
    /// 矢印の位置・向き・カメラの位置を渡す。フェードと流れもここで進める(Presenterが毎フレーム、出ている間に呼ぶ)
    /// </summary>
    public void SetPose(Vector3 position, Vector3 direction, Vector3 cameraPosition)
    {
        UpdateFade();
        if (_fade <= 0f)
        {
            SetRenderersEnabled(false);
            return;
        }

        float length = _spacing * _chevronCount;
        _phase = Mathf.Repeat(_phase + _flowSpeed * Time.unscaledDeltaTime / Mathf.Max(length, 0.0001f), 1f);
        SetRenderersEnabled(true);

        // 面をカメラの方へ向ける: 進行方向の軸まわりで回して、カメラに向ける
        Vector3 toCamera = cameraPosition - position;
        Vector3 facing = toCamera - direction * Vector3.Dot(toCamera, direction);
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.Cross(direction, Vector3.up);
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.Cross(direction, Vector3.right);
        facing.Normalize();

        // Quadは-Z側が表なので、-facingを前にする。山形の右(+X)が進行方向になるようにする
        Vector3 forward = -facing;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward, direction));

        for (int i = 0; i < _chevrons.Length; i++)
        {
            Chevron chevron = _chevrons[i];
            float t = Mathf.Repeat(_phase + i / (float)_chevronCount, 1f);

            // 両端で薄くなる
            float edge = _edgeFade <= 0f ? 1f : Mathf.Clamp01(Mathf.Min(t, 1f - t) / _edgeFade);
            chevron.transform.SetPositionAndRotation(position + direction * ((t - 0.5f) * length), rotation);
            ApplyColor(chevron, edge * _fade);
        }
    }

    // =========================================================
    // 内部処理
    // =========================================================

    // 出す・消すに合わせて、濃さを一定の速さで変える
    private void UpdateFade()
    {
        float target = _isTargetVisible ? 1f : 0f;
        _fade = _fadeTime > 0f
            ? Mathf.MoveTowards(_fade, target, Time.unscaledDeltaTime / _fadeTime)
            : target;
    }

    // 山形を作る(実行時に、シーン直下へ)
    private void CreateChevrons()
    {
        _root = new GameObject("GrappleArrowChevrons").transform;

        int count = Mathf.Max(1, _chevronCount);
        _chevrons = new Chevron[count];
        for (int i = 0; i < count; i++)
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
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.MarkDynamic();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 頂点カラー(色 × 濃さ)を変える
    private void ApplyColor(Chevron chevron, float strength)
    {
        Color color = _color;
        color.a *= strength;

        for (int i = 0; i < chevron.colors.Length; i++) chevron.colors[i] = color;
        chevron.mesh.SetColors(chevron.colors);
    }

    private void SetRenderersEnabled(bool isEnabled)
    {
        foreach (Chevron chevron in _chevrons)
        {
            if (chevron.renderer != null && chevron.renderer.enabled != isEnabled) chevron.renderer.enabled = isEnabled;
        }
    }
}
