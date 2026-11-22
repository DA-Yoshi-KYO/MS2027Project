/* ================================================
 * HDRP Toon Shader - Shade
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-11-22 | 初回作成（物陰に入ったら面の向きに関係なく暗くなる）
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「キャラが何かの陰に入っているか」を Shader が調べられるよう、キャラ1体分の箱を渡すクラス。
///
/// Shader (NTE_InShade) は各点から光の方向へ、この箱の外まで進んだ所で影マップを見る。
/// 箱の外にある物（建物・屋根・別キャラ）に遮られた時だけ「日陰」になるので、
/// 自分の頭や髪が体に落とす影で全身が暗くなることはない。
///
/// キャラのルートに付けると、子の Renderer 全部で1体として扱う。
/// Maya Preview は付けなくても、重なっているトゥーンの部品を1体にまとめる (TickScene)。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CS_ToonShade : MonoBehaviour
{
    private const int MaxBoxes = 16;            // NTE_SHADE_BOXES
    private const float Padding = 0.02f;        // m
    private const float TouchMargin = 0.02f;    // 部品の大きさに対する割合。これだけ近ければ同じ1体
    private const float ScanInterval = 0.5f;
    private const string ToonShaderPrefix = "Shader Graphs/SHG_NTE";

    private static readonly int CountId = Shader.PropertyToID("_NTEShadeBoxCount");
    private static readonly int MinId = Shader.PropertyToID("_NTEShadeBoxMin");
    private static readonly int MaxId = Shader.PropertyToID("_NTEShadeBoxMax");

    private static readonly List<CS_ToonShade> s_all = new List<CS_ToonShade>();
    private static readonly List<List<Renderer>> s_scene = new List<List<Renderer>>();
    private static readonly List<Bounds> s_boxes = new List<Bounds>();
    private static readonly List<Material> s_materials = new List<Material>();
    private static readonly Vector4[] s_min = new Vector4[MaxBoxes];
    private static readonly Vector4[] s_max = new Vector4[MaxBoxes];
    private static float s_nextScan;
    private static int s_publishedFrame = -1;

    private readonly List<Renderer> _renderers = new List<Renderer>();

    // =========================================================
    // Component（書き出したPrefab、ゲーム内のキャラ）
    // =========================================================

    private void OnEnable()
    {
        Collect();
        if (!s_all.Contains(this)) s_all.Add(this);
        Publish();
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        Publish();
    }

    private void OnTransformChildrenChanged() => Collect();

    private void LateUpdate()
    {
        // 編集中は毎フレーム回らないので、呼ばれたら毎回渡す
        if (Application.isPlaying && s_publishedFrame == Time.frameCount) return;
        Publish();
    }

    private void Collect()
    {
        _renderers.Clear();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            if (IsToon(renderer)) _renderers.Add(renderer);
    }

    // =========================================================
    // シーンから自動（Maya Preview）
    // =========================================================

    /// <summary>毎フレーム。0.5秒ごとに分け直し、箱は毎フレーム動きに合わせる。</summary>
    public static void TickScene()
    {
        if (Time.unscaledTime >= s_nextScan)
        {
            s_nextScan = Time.unscaledTime + ScanInterval;
            Scan();
        }
        Publish();
    }

    /// <summary>待たずに今すぐ。</summary>
    public static void ScanNow()
    {
        Scan();
        Publish();
    }

    private static void Scan()
    {
        s_scene.Clear();
        var boxes = new List<Bounds>();
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || !IsToon(renderer)) continue;
            Bounds bounds = renderer.bounds;
            bounds.Expand(bounds.size.magnitude * TouchMargin);
            boxes.Add(bounds);
            s_scene.Add(new List<Renderer> { renderer });
        }

        bool joined = true;
        while (joined)
        {
            joined = false;
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = boxes.Count - 1; j > i; j--)
                {
                    if (!boxes[i].Intersects(boxes[j])) continue;
                    Bounds both = boxes[i];
                    both.Encapsulate(boxes[j]);
                    boxes[i] = both;
                    s_scene[i].AddRange(s_scene[j]);
                    boxes.RemoveAt(j);
                    s_scene.RemoveAt(j);
                    joined = true;
                }
            }
        }
    }

    private static bool IsToon(Renderer renderer)
    {
        renderer.GetSharedMaterials(s_materials);
        foreach (Material material in s_materials)
            if (material != null && material.shader != null && material.shader.name.StartsWith(ToonShaderPrefix)) return true;
        return false;
    }

    // =========================================================
    // Shader へ
    // =========================================================

    private static void Publish()
    {
        s_publishedFrame = Time.frameCount;
        s_boxes.Clear();
        foreach (CS_ToonShade shade in s_all) AddBox(shade._renderers);
        foreach (List<Renderer> group in s_scene) AddBox(group);

        if (s_boxes.Count > MaxBoxes)
        {
            Camera main = Camera.main;
            Vector3 eye = main != null ? main.transform.position : Vector3.zero;
            s_boxes.Sort((a, b) => a.SqrDistance(eye).CompareTo(b.SqrDistance(eye)));
        }
        int count = Mathf.Min(s_boxes.Count, MaxBoxes);
        for (int i = 0; i < count; i++)
        {
            s_min[i] = s_boxes[i].min;
            s_max[i] = s_boxes[i].max;
        }
        Shader.SetGlobalVectorArray(MinId, s_min);
        Shader.SetGlobalVectorArray(MaxId, s_max);
        Shader.SetGlobalFloat(CountId, count);
    }

    private static void AddBox(List<Renderer> renderers)
    {
        bool any = false;
        Bounds box = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (any) box.Encapsulate(renderer.bounds);
            else box = renderer.bounds;
            any = true;
        }
        if (!any) return;
        box.Expand(Padding * 2f);
        s_boxes.Add(box);
    }
}
