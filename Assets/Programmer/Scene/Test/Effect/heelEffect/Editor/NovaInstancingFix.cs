using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// メニュー: Tools > HealEffect > Fix Nova GPU Instancing
// 開いているシーン内の、Nova シェーダーを使う Mesh モードの Particle System すべてに
//   - GPU Instancing オン
//   - Custom Vertex Streams を Nova のインスタンスデータ構造に合わせる
// を適用する。他のモジュール設定には触らない。
public static class NovaInstancingFix
{
    static readonly List<ParticleSystemVertexStream> Streams = new List<ParticleSystemVertexStream>
    {
        ParticleSystemVertexStream.Position,
        ParticleSystemVertexStream.Normal,
        ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.Custom1XYZW,
        ParticleSystemVertexStream.Custom2XYZW,
    };

    [MenuItem("Tools/HealEffect/Fix Nova GPU Instancing")]
    static void Fix()
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null || m.shader == null || !m.shader.name.StartsWith("Nova/")) continue;
            if (r.renderMode != ParticleSystemRenderMode.Mesh) continue;

            Undo.RecordObject(r, "Fix Nova GPU Instancing");
            r.enableGPUInstancing = true;
            r.SetActiveVertexStreams(Streams);
            EditorUtility.SetDirty(r);
            var ps = r.GetComponent<ParticleSystem>();
            if (ps) { ps.Clear(true); ps.Play(true); }
            Debug.Log("[NovaInstancingFix] fixed: " + r.name, r);
            n++;
        }
        Debug.Log($"[NovaInstancingFix] {n} renderer(s) fixed.");
    }
}
