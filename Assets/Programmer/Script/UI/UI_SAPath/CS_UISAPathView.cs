/* ================================================
 * 動くSAポイントの軌道表示のView
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #507)
 * 2026-11-16 | ホログラム風に光るシェーダー(SH_HologramLine)の仮の点線に変更(色のプロパティ名を設定できるようにした)
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 動くSAポイントの軌道(道筋)のView。描画とフェードだけを持つ。
/// ・ワールド上の点線(LineRenderer)で描く。地形に隠れる物は隠れる(隠れても見えるようにはしない)
/// ・点線の柄・流れ・走査線のちらつきは、マテリアルのシェーダー(今は仮の SH_HologramLine)が、シェーダーの中の時間で動かす
///   このViewがマテリアルへ渡すのは、色(出し入れのフェードのアルファ込み)だけ。渡す先のプロパティ名は_colorPropertyで変えられる(本番のシェーダーに差し替える時用)
///   LineRendererのTexture Modeは、UVのUが「線の始まりからの長さ(m)」になるTile・Texture Scale(1, 1)にしている(点線の長さがワールドの長さで決まる)
/// ・出し入れはふわっとしたフェード(_fadeTime)
/// ・線は、シーン直下の「SAPathLines」の子に実行時に作る(Canvasの子にしないので、Canvasの大きさに影響されない)
/// ・道筋の点は軌道を登録した時に1回だけ渡す(毎フレームは渡さない)
/// 時間はTime.timeScaleの影響を受けない。
/// </summary>
public class CS_UISAPathView :
    CS_BaseView<CS_UISAPathPresenter>
{
    // 軌道1つ分の線
    private class PathLine
    {
        public LineRenderer line;
        public float alpha;             // 今の濃さの割合(0〜1)
        public bool isTargetVisible;    // 出すか
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("見た目")]
    [Tooltip("点線のマテリアル(ホログラム風の光る点線。今は仮の MT_SAPathDash = SH_HologramLine。本番のシェーダーができたら差し替える)")]
    [SerializeField] private Material _material;

    [Tooltip("色を渡すマテリアルのプロパティ名(アルファは出し入れのフェードに使う)。シェーダーを差し替えて名前が違う時は、ここを合わせる")]
    [SerializeField] private string _colorProperty = "_HologramColor";

    [Tooltip("線の色(アルファが一番濃い時の濃さ。HDRの明るさはシェーダー側で掛ける)")]
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(0.5f, 1.8f, 2.4f, 1f);

    [Tooltip("線の太さ(m)")]
    [SerializeField][Min(0.001f)] private float _width = 0.35f;

    [Header("動き")]
    [Tooltip("出し入れのフェードの時間(秒。0ですぐ出し入れ)")]
    [SerializeField][Min(0f)] private float _fadeTime = 0.4f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private int _colorId;                                                      // 色を渡すプロパティのID

    private Transform _root;                                                   // 線を置く場所(シーン直下)
    private MaterialPropertyBlock _propertyBlock;                              // 線の色と濃さを渡す(共有マテリアルを書き換えないため)
    private readonly Dictionary<int, PathLine> _lines = new Dictionary<int, PathLine>();
    private readonly List<PathLine> _fadingLines = new List<PathLine>();      // フェード中の線(毎フレームの処理の対象)

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _root = new GameObject("SAPathLines").transform;
        _propertyBlock = new MaterialPropertyBlock();
        _colorId = Shader.PropertyToID(_colorProperty);
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    private void Update()
    {
        // フェード中の線だけ、濃さを変える(出し切った・消し切った線は対象から外す)
        for (int i = _fadingLines.Count - 1; i >= 0; i--)
        {
            PathLine pathLine = _fadingLines[i];
            float target = pathLine.isTargetVisible ? 1f : 0f;
            pathLine.alpha = _fadeTime > 0f
                ? Mathf.MoveTowards(pathLine.alpha, target, Time.unscaledDeltaTime / _fadeTime)
                : target;

            ApplyAlpha(pathLine);
            if (Mathf.Approximately(pathLine.alpha, target)) _fadingLines.RemoveAt(i);
        }
    }

    // =========================================================
    // 軌道の登録・削除・出し入れ
    // =========================================================

    /// <summary>軌道の線を作る(最初は消えている。同じ番号がすでにあれば作り直す)</summary>
    public void AddPath(int id, Vector3[] worldPoints)
    {
        RemovePath(id);
        if (worldPoints == null || worldPoints.Length < 2 || _root == null) return;

        GameObject lineObject = new GameObject($"SAPath{id}");
        lineObject.transform.SetParent(_root, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = worldPoints.Length;
        line.SetPositions(worldPoints);
        line.widthMultiplier = _width;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(1f, 1f);   // UVのUが、線の始まりからの長さ(m)になる
        line.alignment = LineAlignment.View;
        line.numCapVertices = 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = _material;
        line.enabled = false;

        PathLine pathLine = new PathLine { line = line };
        _lines.Add(id, pathLine);
        ApplyAlpha(pathLine);
    }

    /// <summary>軌道の線を消して外す</summary>
    public void RemovePath(int id)
    {
        if (!_lines.TryGetValue(id, out PathLine pathLine)) return;

        _lines.Remove(id);
        _fadingLines.Remove(pathLine);
        if (pathLine.line != null) Destroy(pathLine.line.gameObject);
    }

    /// <summary>すべての軌道の線を消して外す</summary>
    public void Clear()
    {
        foreach (PathLine pathLine in _lines.Values)
        {
            if (pathLine.line != null) Destroy(pathLine.line.gameObject);
        }

        _lines.Clear();
        _fadingLines.Clear();
    }

    /// <summary>軌道を出す・消す(フェードしながら)</summary>
    public void SetVisible(int id, bool isVisible)
    {
        if (!_lines.TryGetValue(id, out PathLine pathLine) || pathLine.isTargetVisible == isVisible) return;

        pathLine.isTargetVisible = isVisible;
        if (!_fadingLines.Contains(pathLine)) _fadingLines.Add(pathLine);
    }

    // =========================================================
    // 内部処理
    // =========================================================

    // 線の色(色 × 濃さ。アルファが濃さ)をMaterialPropertyBlockで渡す。流れ・ちらつきはシェーダーが動かす。濃さが0の間は線を描かない
    private void ApplyAlpha(PathLine pathLine)
    {
        LineRenderer line = pathLine.line;
        if (line == null) return;

        Color color = _color;
        color.a *= pathLine.alpha;

        line.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_colorId, color);
        line.SetPropertyBlock(_propertyBlock);
        line.enabled = pathLine.alpha > 0f;
    }
}
