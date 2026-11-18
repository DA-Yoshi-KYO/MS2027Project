using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * スキャン(CS_PlayerScan)で見えている相手を、強調表示で描き直すカスタムパス
 * CS_PlayerScanHighlightが渡した「Renderer + マテリアル」を、全サブメッシュ分、マテリアル(SH_ScanHighlight)で描き直す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・元のモデルの描画には触らない。元のモデルの上に、強調を重ねて描く
 * ・描き分け(SH_ScanHighlightのパス番号)
 *   見え始めた直後(flash)    : パス1 どこでも描くシルエット(モデル全体)
 *   それ以降                  : パス0 隠れている部分だけのシルエット
 *                               + アウトライン(パス2で、映っている部分の形をマスクの画像に描き、パス3で、その形の外側に線を重ねる)
 * ・アウトラインは、モデルを膨らませずに、画面上で形の外側へ足す(角ばったモデルでも、均一な太さの線になる)
 * ・描く相手は、CS_PlayerScanHighlightが毎フレーム targets に入れる(このクラスは相手の種類を知らない)
 * ・CustomPassVolume(全体適用)は、CS_PlayerScanHighlightが実行時に作る。シーン・プレハブには置かない
 * ・ゲーム画面とシーンビューにだけ描く(リフレクションプローブ・プレビューのカメラには描かない)
 * ・深度バッファは書き込み先にしない(シェーダーが深度テクスチャとして読んで、隠れているかを判定するため)
 */
// ========================================

[Serializable]
public class CS_ScanHighlightPass : CustomPass
{
    // 描く相手1つ分
    public struct Target
    {
        public Renderer renderer;
        public Material material;
        public bool flash;   // 見え始めた直後か(trueの間は、どこでも描くシルエットを出す)
    }

    // SH_ScanHighlightのパス番号(シェーダーに書いた順)
    private const int HiddenSilhouettePass = 0;
    private const int AlwaysSilhouettePass = 1;
    private const int OutlineMaskPass = 2;
    private const int OutlineCompositePass = 3;

    private static readonly int MaskTextureId = Shader.PropertyToID("_ScanMaskTexture");

    // 毎フレーム、CS_PlayerScanHighlightが入れ替える
    public static readonly List<Target> targets = new List<Target>();

    private static readonly List<Material> _materialBuffer = new List<Material>();   // サブメッシュ数を数える用(毎フレーム作らないよう使い回す)

    private RTHandle _mask;   // 映っている部分の形(rgb: 線の色 / a: 形)

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        _mask = RTHandles.Alloc(Vector2.one, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, useDynamicScale: true, name: "ScanHighlightMask");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (targets.Count == 0) return;

        CameraType cameraType = ctx.hdCamera.camera.cameraType;
        if (cameraType != CameraType.Game && cameraType != CameraType.SceneView) return;

        // 深度バッファは書き込み先にしない(シェーダーが深度テクスチャとして読んで、隠れているかを判定するため)
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer);

        // シルエット(見え始めた直後は全体、それ以降は隠れている部分だけ)
        Material outlineMaterial = null;
        foreach (Target target in targets)
        {
            if (!IsDrawable(target)) continue;

            int subMeshCount = SubMeshCount(target.renderer);
            for (int i = 0; i < subMeshCount; i++)
            {
                ctx.cmd.DrawRenderer(target.renderer, target.material, i, target.flash ? AlwaysSilhouettePass : HiddenSilhouettePass);
            }
            if (!target.flash) outlineMaterial = target.material;
        }

        if (outlineMaterial == null) return;

        // アウトライン: 映っている部分の形をマスクに描いて(見え始めた直後の相手は描かない)
        CoreUtils.SetRenderTarget(ctx.cmd, _mask, ClearFlag.Color, Color.clear);
        foreach (Target target in targets)
        {
            if (target.flash || !IsDrawable(target)) continue;

            int subMeshCount = SubMeshCount(target.renderer);
            for (int i = 0; i < subMeshCount; i++)
            {
                ctx.cmd.DrawRenderer(target.renderer, target.material, i, OutlineMaskPass);
            }
        }

        // 画面に、形の外側へ線を重ねる
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer);
        ctx.cmd.SetGlobalTexture(MaskTextureId, _mask);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, outlineMaterial, OutlineCompositePass, MeshTopology.Triangles, 3, 1);
    }

    protected override void Cleanup()
    {
        if (_mask != null) _mask.Release();
        _mask = null;
    }

    // 相手が消えている時はRendererもなくなっている
    private static bool IsDrawable(Target target)
    {
        return target.renderer != null && target.material != null && target.renderer.enabled && target.renderer.gameObject.activeInHierarchy;
    }

    private static int SubMeshCount(Renderer renderer)
    {
        renderer.GetSharedMaterials(_materialBuffer);
        return _materialBuffer.Count;
    }
}
