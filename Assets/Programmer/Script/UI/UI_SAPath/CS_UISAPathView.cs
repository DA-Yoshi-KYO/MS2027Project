/* ================================================
 * 動くSAポイントの軌道表示のView
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-11-16 | 初回作成(Issue #507)
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 動くSAポイントの軌道(道筋)のView。描画とフェードだけを持つ。
/// ・ワールド上の点線(LineRenderer)で描く。地形に隠れる物は隠れる(隠れても見えるようにはしない)
/// ・点線は、点と点の間の長さ(_dashPeriod)ごとに、点線の柄を繰り返す
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
    [Tooltip("点線のマテリアル(頂点カラーを使うもの。今は Nova の MT_SAPathDash)")]
    [SerializeField] private Material _material;

    [Tooltip("線の色(アルファが一番濃い時の濃さ)")]
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(1.6f, 1.1f, 0.3f, 0.9f);

    [Tooltip("線の太さ(m)")]
    [SerializeField][Min(0.001f)] private float _width = 0.2f;

    [Tooltip("点線の柄が一回りする長さ(m。点と点の間隔の目安)")]
    [SerializeField][Min(0.01f)] private float _dashPeriod = 1f;

    [Header("動き")]
    [Tooltip("出し入れのフェードの時間(秒。0ですぐ出し入れ)")]
    [SerializeField][Min(0f)] private float _fadeTime = 0.4f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private Transform _root;                                                   // 線を置く場所(シーン直下)
    private readonly Dictionary<int, PathLine> _lines = new Dictionary<int, PathLine>();
    private readonly List<PathLine> _fadingLines = new List<PathLine>();      // フェード中の線(毎フレームの処理の対象)

    // =========================================================
    // 初期化
    // =========================================================

    private void Awake()
    {
        _root = new GameObject("SAPathLines").transform;
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
        line.textureScale = new Vector2(1f / _dashPeriod, 1f);   // 柄が_dashPeriod(m)ごとに繰り返す
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

    // 線の色(色 × 濃さ)を頂点カラーに入れる。濃さが0の間は線を描かない
    private void ApplyAlpha(PathLine pathLine)
    {
        LineRenderer line = pathLine.line;
        if (line == null) return;

        Color color = _color;
        color.a *= pathLine.alpha;
        line.startColor = color;
        line.endColor = color;
        line.enabled = pathLine.alpha > 0f;
    }
}
