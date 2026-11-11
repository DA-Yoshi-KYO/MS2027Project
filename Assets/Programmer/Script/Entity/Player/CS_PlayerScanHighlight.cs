using System.Collections.Generic;
using UnityEngine;

/*
 * スキャン(CS_PlayerScan)で見えている相手のモデルを、強調・透過表示にするクラス
 * 見えている間だけ、モデルのマテリアルを縁が光る半透明のマテリアル(壁越しでも見える)に差し替え、見えなくなったら元に戻す
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・見えている相手はCS_PlayerScanの一覧(revealedVillainGroups / revealedPolice / revealedDrones)を毎フレーム見て決める
 *   このクラスはCS_PlayerScanの状態を読むだけで、CS_PlayerScanはこのクラスを知らない(外してもスキャンは動く)
 * ・差し替えるのは、相手の下のMeshRendererとSkinnedMeshRendererだけ(LineRenderer・パーティクルなどには触らない)
 *   元のマテリアルは差し替える時に控えておき、見えなくなった時・消えた時・このクラスが無効になった時に戻す
 * ・マテリアルは種類ごと(悪人・警察・ドローン)に1つずつ(MT_ScanHighlightVillain / Police / Drone。シェーダーはSH_ScanHighlight)
 *   色などを変えたい時は、マテリアルを変える(プレハブは触らない)
 * ・シェーダーは元のマテリアルのプロパティ(_BaseColorなど)を使わないので、他のスクリプトのMaterialPropertyBlockとはぶつからない
 *   (悪人が倒されて薄くなる時は、倒された時点で見えている状態から外れるので元に戻る)
 * ・3Dモデルの見た目の処理でUIではないため、MVPにはしない(CS_PlayerScanEffectと同じ扱い)
 * ・自分のプレイヤー(CS_Player.isLocalPlayer)でだけ動く。見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_PlayerScan))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScanHighlight : MonoBehaviour
{
    [Header("差し替えるマテリアル(SH_ScanHighlight)")]
    [SerializeField] private Material _villainMaterial;   // 悪人(MT_ScanHighlightVillain)
    [SerializeField] private Material _policeMaterial;    // 警察(MT_ScanHighlightPolice)
    [SerializeField] private Material _droneMaterial;     // 巡回ドローン(MT_ScanHighlightDrone)

    // 差し替え中の相手1つ分(元のマテリアルを控えておく)
    private class Entry
    {
        public Renderer[] renderers;
        public Material[][] originals;
    }

    private CS_Player _player;
    private CS_PlayerScan _scan;

    private readonly Dictionary<Component, Entry> _applied = new Dictionary<Component, Entry>();   // 差し替え中の相手
    private readonly Dictionary<Component, Material> _current = new Dictionary<Component, Material>();   // 今見えている相手と、使うマテリアル
    private readonly List<Component> _removeBuffer = new List<Component>();   // 戻す相手(毎フレーム作らないよう使い回す)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scan = GetComponent<CS_PlayerScan>();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer) return;

        CollectRevealed();
        RestoreHidden();
        ApplyRevealed();
    }

    // 今見えている相手を集める
    private void CollectRevealed()
    {
        _current.Clear();

        foreach (CS_PlayerScan.RevealedVillainGroup group in _scan.revealedVillainGroups)
        {
            foreach (CS_VillainHealth villain in group.members)
            {
                if (villain != null) _current[villain] = _villainMaterial;
            }
        }
        foreach (CS_PoliceBrain police in _scan.revealedPolice)
        {
            if (police != null) _current[police] = _policeMaterial;
        }
        foreach (CS_SecurityDrone drone in _scan.revealedDrones)
        {
            if (drone != null) _current[drone] = _droneMaterial;
        }
    }

    // 見えなくなった(時間切れ・倒された・消えた)相手を元に戻す
    private void RestoreHidden()
    {
        _removeBuffer.Clear();
        foreach (KeyValuePair<Component, Entry> pair in _applied)
        {
            if (pair.Key == null || !_current.ContainsKey(pair.Key)) _removeBuffer.Add(pair.Key);
        }

        foreach (Component target in _removeBuffer)
        {
            Restore(_applied[target]);
            _applied.Remove(target);
        }
    }

    // 新しく見えた相手のマテリアルを差し替える
    private void ApplyRevealed()
    {
        foreach (KeyValuePair<Component, Material> pair in _current)
        {
            if (pair.Value == null || _applied.ContainsKey(pair.Key)) continue;

            _applied.Add(pair.Key, Apply(pair.Key, pair.Value));
        }
    }

    // 相手の下のMeshRenderer・SkinnedMeshRendererのマテリアルを、highlightに差し替える
    private static Entry Apply(Component target, Material highlight)
    {
        List<Renderer> found = new List<Renderer>();
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) found.Add(renderer);
        }

        Entry entry = new Entry { renderers = found.ToArray(), originals = new Material[found.Count][] };
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer renderer = entry.renderers[i];
            Material[] original = renderer.sharedMaterials;
            entry.originals[i] = original;

            Material[] replaced = new Material[original.Length];
            for (int j = 0; j < replaced.Length; j++) replaced[j] = highlight;
            renderer.sharedMaterials = replaced;
        }
        return entry;
    }

    private static void Restore(Entry entry)
    {
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            // 相手が消えている時はRendererもなくなっている
            if (entry.renderers[i] != null) entry.renderers[i].sharedMaterials = entry.originals[i];
        }
    }

    private void RestoreAll()
    {
        foreach (Entry entry in _applied.Values) Restore(entry);
        _applied.Clear();
        _current.Clear();
    }
}
