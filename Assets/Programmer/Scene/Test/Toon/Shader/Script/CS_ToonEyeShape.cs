/* ================================================
 * HDRP Toon Shader - Eye Shape
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-11-22 | 初回作成（両目で同じ視線・同じ形の角膜にする）
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 目のメッシュから「視線」と「それぞれの眼球のどこか」を測るクラス。
///
/// 目のメッシュは顔の丸みに沿って左右が外を向いているが、眼球はどちらも同じ方を見ている。
/// 面の向きをそのまま反射に使うと左右で映る物がずれるので、
///   ・視線   = 両目の向きの平均（角膜の向き）
///   ・位置   = その目の円盤の正面から見た、目の中の -1..1（描かれた瞳孔と輪郭に張り付く）
/// にする。どの角度から見ても、描かれた瞳に対して両目で同じ所に映る。
/// UVの向き・左右の共有・頂点の分割・左右が別オブジェクトかには左右されない。
/// 目の中に重なっている部品（瞳孔・描いたハイライト）は、その目の一部として扱う。
/// </summary>
public class CS_ToonEyeShape
{
    public const float Offset = 2f;             // NTE_EYE_UV_OFFSET
    public const int LossSteps = 8;             // NTE_EYE_LOSS_STEPS
    public const float LossSpacing = 4f;        // NTE_EYE_LOSS_SPACING
    private const float FacingEnough = 0.2f;    // 向きの平均がこれより短い = 閉じた球

    /// <summary>視線。オブジェクト空間。</summary>
    public Vector3 front;
    /// <summary>両目のまんなか。オブジェクト空間。</summary>
    public Vector3 center;
    /// <summary>測った頂点の番号。Coords と同じ並び。</summary>
    public int[] vertices;

    private Vector3[] _points;
    private int[] _ownerOf;
    private Piece[] _pieces;
    private int[] _pieceOf;
    private Vector3 _right, _up;

    private class Piece
    {
        public Vector3 facing;                  // 面積 × 向き の合計
        public double area;
        public Vector2 min, max;                // 視線の正面から見た範囲
        public float near, far;
        public float round;                     // 1 = 丸い, 0 = 細い帯（角膜にならない）

        public Vector2 Middle => (min + max) * 0.5f;
        public float Width => max.x - min.x;
        public float Height => max.y - min.y;
    }

    private readonly struct Key : IEquatable<Key>
    {
        private readonly float _x, _y, _z;
        private readonly int _hash;

        public Key(Vector3 p)
        {
            _x = p.x + 0f; _y = p.y + 0f; _z = p.z + 0f;    // -0 と 0 は同じ
            _hash = Mix(Mix(Mix(17, _x.GetHashCode()), _y.GetHashCode()), _z.GetHashCode());
        }

        // Vector3 のハッシュは格子状の頂点で衝突して遅い
        private static int Mix(int hash, int value)
        {
            unchecked
            {
                uint h = ((uint)hash ^ (uint)value) * 0x9E3779B1u;
                h ^= h >> 15;
                h *= 0x85EBCA77u;
                return (int)(h ^ (h >> 13));
            }
        }

        public bool Equals(Key other) => _x == other._x && _y == other._y && _z == other._z;
        public override bool Equals(object other) => other is Key key && Equals(key);
        public override int GetHashCode() => _hash;
    }

    /// <summary>目のパーツ（submesh）全部をまとめて測る。形が無ければ null。</summary>
    public static CS_ToonEyeShape Measure(Vector3[] points, List<int[]> parts)
    {
        var shape = new CS_ToonEyeShape { _points = points };
        return shape.Build(parts) ? shape : null;
    }

    private bool Build(List<int[]> parts)
    {
        var triangles = new List<int>();
        foreach (int[] part in parts)
            for (int i = 0; i + 2 < part.Length; i += 3)
            {
                triangles.Add(part[i]);
                triangles.Add(part[i + 1]);
                triangles.Add(part[i + 2]);
            }
        if (triangles.Count == 0) return false;
        vertices = triangles.ToArray();

        // 同じ位置でつながっている = 1つの部品
        var pointOf = new Dictionary<Key, int>(vertices.Length);
        var parent = new List<int>();
        var corner = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var key = new Key(_points[vertices[i]]);
            if (!pointOf.TryGetValue(key, out int point))
            {
                point = parent.Count;
                parent.Add(point);
                pointOf.Add(key, point);
            }
            corner[i] = point;
        }
        for (int i = 0; i < corner.Length; i += 3)
        {
            for (int k = 1; k < 3; k++)
            {
                int a = Root(parent, corner[i]), b = Root(parent, corner[i + k]);
                if (a != b) parent[b] = a;
            }
        }

        var indexOf = new Dictionary<int, int>();
        var pieces = new List<Piece>();
        _pieceOf = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            int root = Root(parent, corner[i]);
            if (!indexOf.TryGetValue(root, out int index))
            {
                index = pieces.Count;
                indexOf.Add(root, index);
                pieces.Add(new Piece());
            }
            _pieceOf[i] = index;
        }
        _pieces = pieces.ToArray();

        Vector3 all = Vector3.zero;
        double total = 0;
        for (int i = 0; i < vertices.Length; i += 3)
        {
            Vector3 a = _points[vertices[i]];
            Vector3 normal = Vector3.Cross(_points[vertices[i + 1]] - a, _points[vertices[i + 2]] - a);
            Piece piece = _pieces[_pieceOf[i]];
            piece.facing += normal;
            piece.area += normal.magnitude;
            all += normal;
            total += normal.magnitude;
        }

        // 閉じた眼球は向きが出ない: モデルの正面（Mayaの +Z = Unityの -Z）
        front = total > 0 && all.magnitude > total * FacingEnough ? Unit(all) : Vector3.back;
        Project();
        FindOwners();

        // 視線は眼球だけで決める（細い帯や、上に乗った部品の向きは混ぜない）
        Vector3 gaze = Vector3.zero;
        double gazeArea = 0;
        for (int i = 0; i < _pieces.Length; i++)
        {
            if (_ownerOf[i] != i) continue;
            gaze += _pieces[i].facing * _pieces[i].round;
            gazeArea += _pieces[i].area * _pieces[i].round;
        }
        if (gazeArea > 0)
        {
            front = gaze.magnitude > gazeArea * FacingEnough ? Unit(gaze) : Vector3.back;
            Project();
        }

        Vector3 middle = Vector3.zero;
        float weights = 0f;
        for (int i = 0; i < _pieces.Length; i++)
        {
            if (_ownerOf[i] != i) continue;
            Piece piece = _pieces[i];
            float weight = piece.Width * piece.Height * piece.round + 1e-12f;
            Vector2 m = piece.Middle;
            middle += (_right * m.x + _up * m.y + front * ((piece.near + piece.far) * 0.5f)) * weight;
            weights += weight;
        }
        center = middle / Mathf.Max(weights, 1e-20f);
        return true;
    }

    private void Project()
    {
        Vector3 upright = Mathf.Abs(front.y) < 0.99f ? Vector3.up : Vector3.forward;
        _up = Unit(upright - front * Vector3.Dot(upright, front));
        _right = Vector3.Cross(front, _up);     // 正面から見た人の右
        foreach (Piece piece in _pieces)
        {
            piece.min = new Vector2(float.MaxValue, float.MaxValue);
            piece.max = new Vector2(float.MinValue, float.MinValue);
            piece.near = float.MaxValue;
            piece.far = float.MinValue;
        }
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = _points[vertices[i]];
            var flat = new Vector2(Vector3.Dot(p, _right), Vector3.Dot(p, _up));
            float depth = Vector3.Dot(p, front);
            Piece piece = _pieces[_pieceOf[i]];
            piece.min = Vector2.Min(piece.min, flat);
            piece.max = Vector2.Max(piece.max, flat);
            piece.near = Mathf.Min(piece.near, depth);
            piece.far = Mathf.Max(piece.far, depth);
        }
        foreach (Piece piece in _pieces)
        {
            float longer = Mathf.Max(piece.Width, piece.Height);
            piece.round = longer > 0f ? Mathf.Clamp01(2f * Mathf.Min(piece.Width, piece.Height) / longer) : 0f;
        }
    }

    // 丸くて大きい物から順に眼球。中心がその中にある部品は、その眼球の一部。
    private void FindOwners()
    {
        int count = _pieces.Length;
        _ownerOf = new int[count];
        var order = new int[count];
        var rank = new float[count];
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
            rank[i] = -_pieces[i].Width * _pieces[i].Height * _pieces[i].round;
        }
        Array.Sort(rank, order);

        var owners = new List<int>();
        foreach (int index in order)
        {
            Piece piece = _pieces[index];
            Vector2 middle = piece.Middle;
            float depth = (piece.near + piece.far) * 0.5f;
            int owner = index;
            foreach (int candidate in owners)
            {
                Piece eye = _pieces[candidate];
                if (middle.x < eye.min.x || middle.x > eye.max.x || middle.y < eye.min.y || middle.y > eye.max.y) continue;
                float reach = Mathf.Max(eye.Width, eye.Height);
                if (depth < eye.near - reach || depth > eye.far + reach) continue;      // 後ろの別人の目
                owner = candidate;
                break;
            }
            _ownerOf[index] = owner;
            if (owner == index) owners.Add(index);
        }
    }

    /// <summary>
    /// 第2UVに入れる値。x = Offset + 0..1 + LossSpacing × 丸くない度合い（0..LossSteps）、y = Offset + 0..1。
    /// </summary>
    public Vector2[] Coords()
    {
        int count = _pieces.Length;
        var rights = new Vector3[count];
        var ups = new Vector3[count];
        var min = new Vector2[count];
        var max = new Vector2[count];
        for (int k = 0; k < count; k++)
        {
            if (_ownerOf[k] != k) continue;
            Piece disc = _pieces[k];
            Vector3 facing = Unit(disc.facing);
            // 閉じた球には円盤の向きが無い: 視線から見る
            if (disc.facing.magnitude <= disc.area * FacingEnough || Vector3.Dot(facing, front) <= 0f) facing = front;
            Vector3 upright = Mathf.Abs(facing.y) < 0.99f ? Vector3.up : Vector3.forward;
            ups[k] = Unit(upright - facing * Vector3.Dot(upright, facing));
            rights[k] = Vector3.Cross(facing, ups[k]);
            min[k] = new Vector2(float.MaxValue, float.MaxValue);
            max[k] = new Vector2(float.MinValue, float.MinValue);
        }
        for (int i = 0; i < vertices.Length; i++)
        {
            int k = _pieceOf[i];
            if (_ownerOf[k] != k) continue;     // 大きさは眼球そのものだけで決める
            Vector3 p = _points[vertices[i]];
            var flat = new Vector2(Vector3.Dot(p, rights[k]), Vector3.Dot(p, ups[k]));
            min[k] = Vector2.Min(min[k], flat);
            max[k] = Vector2.Max(max[k], flat);
        }

        var result = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            int k = _ownerOf[_pieceOf[i]];
            Piece eye = _pieces[k];
            Vector3 p = _points[vertices[i]];
            Vector2 size = max[k] - min[k];
            float x = size.x > 0f ? Mathf.Clamp01((Vector3.Dot(p, rights[k]) - min[k].x) / size.x) : 0.5f;
            float y = size.y > 0f ? Mathf.Clamp01((Vector3.Dot(p, ups[k]) - min[k].y) / size.y) : 0.5f;
            int loss = Mathf.RoundToInt((1f - eye.round) * LossSteps);
            result[i] = new Vector2(Offset + x + LossSpacing * loss, Offset + y);
        }
        return result;
    }

    private static int Root(List<int> parent, int point)
    {
        while (parent[point] != point)
        {
            parent[point] = parent[parent[point]];
            point = parent[point];
        }
        return point;
    }

    // Vector3.normalized は、とても小さい三角形（m単位のモデル）で 0 になる
    private static Vector3 Unit(Vector3 v)
    {
        float length = v.magnitude;
        return length > 1e-20f ? v / length : Vector3.zero;
    }
}
