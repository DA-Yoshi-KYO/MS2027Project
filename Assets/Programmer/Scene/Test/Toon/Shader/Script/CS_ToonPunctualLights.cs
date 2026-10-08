/* ================================================
 * HDRP Toon Shader - Spot / Point Light Sender
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-10-08 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// HDRP の Unlit (自作トゥーン) はシーンのライトを受けないため、
/// 指定した Spot / Point Light の情報をグローバルシェーダー変数で渡し、
/// NTE_Toon.hlsl 側でトゥーンの明暗帯と同じ考え方で照らす。
///
/// 使い方：シーンに 1 つ置き、Lights に当てたいスポットライトを登録する。
/// （空なら Target 付近の有効な Spot / Point Light を自動で最大 8 個拾う）
/// </summary>
[ExecuteAlways]
public class CS_ToonPunctualLights : MonoBehaviour
{
    private const int MaxLights = 8;

    /// <summary>トゥーンに反映するライト。空の場合は自動収集。</summary>
    [SerializeField]
    private Light[] lights;

    /// <summary>自動収集の基準位置（キャラクター）。未設定ならこのオブジェクトの位置。</summary>
    [SerializeField]
    private Transform target;

    /// <summary>トゥーン上の明るさ倍率。</summary>
    [SerializeField, Range(0f, 4f)]
    private float toonIntensity = 0.6f;

    /// <summary>この Intensity (HDRP の単位) のライトをトゥーンの明るさ 1.0 として扱う。</summary>
    [SerializeField]
    private float referenceIntensity = 3000f;

    private static readonly int CountID = Shader.PropertyToID("_NTE_LightCount");
    private static readonly int PosID = Shader.PropertyToID("_NTE_LightPos");
    private static readonly int DirID = Shader.PropertyToID("_NTE_LightDir");
    private static readonly int ColorID = Shader.PropertyToID("_NTE_LightColor");
    private static readonly int ConeID = Shader.PropertyToID("_NTE_LightCone");

    private readonly Vector4[] pos = new Vector4[MaxLights];
    private readonly Vector4[] dir = new Vector4[MaxLights];
    private readonly Vector4[] col = new Vector4[MaxLights];
    private readonly Vector4[] cone = new Vector4[MaxLights];

    private void OnDisable()
    {
        Shader.SetGlobalFloat(CountID, 0f);
    }

    private void LateUpdate()
    {
        Send();
    }

    private void OnValidate()
    {
        Send();
    }

    private void Send()
    {
        Light[] src = (lights != null && lights.Length > 0) ? lights : Collect();
        int n = 0;
        foreach (Light l in src)
        {
            if (n >= MaxLights) break;
            if (l == null || !l.isActiveAndEnabled) continue;
            if (l.type != LightType.Spot && l.type != LightType.Point) continue;

            Vector3 p = l.transform.position;
            float range = Mathf.Max(l.range, 0.01f);
            pos[n] = new Vector4(p.x, p.y, p.z, 1f / (range * range));

            Vector3 f = l.transform.forward;
            dir[n] = new Vector4(f.x, f.y, f.z, l.type == LightType.Point ? 1f : 0f);

            Color c = l.color.linear;
            if (l.useColorTemperature) c *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature);
            float k = toonIntensity * l.intensity / Mathf.Max(referenceIntensity, 1e-3f);
            col[n] = new Vector4(c.r * k, c.g * k, c.b * k, 1f);

            float cosOuter = Mathf.Cos(0.5f * l.spotAngle * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(0.5f * Mathf.Min(l.innerSpotAngle, l.spotAngle - 0.1f) * Mathf.Deg2Rad);
            cone[n] = new Vector4(cosOuter, 1f / Mathf.Max(cosInner - cosOuter, 1e-4f), 0f, 0f);
            n++;
        }

        Shader.SetGlobalFloat(CountID, n);
        Shader.SetGlobalVectorArray(PosID, pos);
        Shader.SetGlobalVectorArray(DirID, dir);
        Shader.SetGlobalVectorArray(ColorID, col);
        Shader.SetGlobalVectorArray(ConeID, cone);
    }

    /// <summary>Target に近い順に、有効な Spot / Point Light を集める。</summary>
    private Light[] Collect()
    {
        Vector3 center = target != null ? target.position : transform.position;
        Light[] all = FindObjectsByType<Light>(FindObjectsSortMode.None);
        System.Array.Sort(all, (a, b) =>
            (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
        return all;
    }
}
