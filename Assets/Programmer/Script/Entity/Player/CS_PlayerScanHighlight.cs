using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * スキャン(CS_PlayerScan)で見えている相手のモデルを、強調・透過表示にするクラス
 * 見えている相手のRendererを、見え始めてからの経過と一緒にカスタムパス(CS_ScanHighlightPass)に渡す。元のモデルのマテリアルは触らない
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・見えている相手はCS_PlayerScanの一覧(revealedVillainGroups / revealedPolice / revealedDrones)を毎フレーム見て決める
 *   このクラスはCS_PlayerScanの状態を読むだけで、CS_PlayerScanはこのクラスを知らない(外してもスキャンは動く)
 * ・渡すのは、相手の下のMeshRendererとSkinnedMeshRendererだけ(LineRenderer・パーティクルなどには触らない)
 *   相手のマテリアルは差し替えないので、元の見た目はそのまま。その上に強調が重なる(描き直しはCS_ScanHighlightPassが行う)
 * ・見え方: 見え始めてからFlash Frames(既定10)フレームは、モデル全体にシルエット(隠れていない部分も透過の見た目)
 *   その後は、隠れている部分にシルエット、画面に映っている部分に太めのアウトライン(太さ・濃さはマテリアル)
 * ・マテリアルは種類ごと(悪人・警察・ドローン)に1つずつ(MT_ScanHighlightVillain / Police / Drone。シェーダーはSH_ScanHighlight)
 *   色などを変えたい時は、マテリアルを変える(プレハブは触らない)
 * ・描き直すためのCustomPassVolume(全体適用)は、自分のプレイヤーで動き始めた時に実行時に作り、無効になった時に消す
 *   シーン・プレハブには何も置かない
 * ・3Dモデルの見た目の処理でUIではないため、MVPにはしない(CS_PlayerScanEffectと同じ扱い)
 * ・自分のプレイヤー(CS_Player.isLocalPlayer)でだけ動く。見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_PlayerScan))]
[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScanHighlight : MonoBehaviour
{
    [Header("強調表示のマテリアル(SH_ScanHighlight)")]
    [SerializeField] private Material _villainMaterial;   // 悪人(MT_ScanHighlightVillain)
    [SerializeField] private Material _policeMaterial;    // 警察(MT_ScanHighlightPolice)
    [SerializeField] private Material _droneMaterial;     // 巡回ドローン(MT_ScanHighlightDrone)

    [Header("見え始めの見た目")]
    [SerializeField, Min(0)] private int _flashFrames = 10;   // 見え始めてから、モデル全体にシルエットを出すフレーム数(以降はアウトライン)

    private CS_Player _player;
    private CS_PlayerScan _scan;

    private GameObject _passVolumeObject;   // カスタムパスを載せるオブジェクト(実行時に作る)

    private readonly Dictionary<Component, Material> _current = new Dictionary<Component, Material>();   // 今見えている相手と、使うマテリアル
    private readonly Dictionary<Component, int> _startFrames = new Dictionary<Component, int>();   // 相手ごとの、見え始めたフレーム
    private readonly Dictionary<Component, Renderer[]> _renderers = new Dictionary<Component, Renderer[]>();   // 相手ごとのRenderer(新しく見えた時に集める)
    private readonly List<Component> _removeBuffer = new List<Component>();   // 見えなくなった相手(毎フレーム作らないよう使い回す)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scan = GetComponent<CS_PlayerScan>();
    }

    private void OnDisable()
    {
        Clear();
        if (_passVolumeObject != null) Destroy(_passVolumeObject);
        _passVolumeObject = null;
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer) return;

        EnsurePass();
        CollectRevealed();
        ForgetHidden();
        SendToPass();
    }

    // カスタムパスを載せたCustomPassVolumeを作る(1回だけ)
    private void EnsurePass()
    {
        if (_passVolumeObject != null) return;

        _passVolumeObject = new GameObject("ScanHighlightPass");
        _passVolumeObject.transform.SetParent(transform, false);

        CustomPassVolume volume = _passVolumeObject.AddComponent<CustomPassVolume>();
        volume.isGlobal = true;
        volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        volume.AddPassOfType<CS_ScanHighlightPass>();
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

    // 見えなくなった(時間切れ・倒された・消えた)相手のRendererを忘れる
    private void ForgetHidden()
    {
        _removeBuffer.Clear();
        foreach (Component target in _renderers.Keys)
        {
            if (target == null || !_current.ContainsKey(target)) _removeBuffer.Add(target);
        }
        foreach (Component target in _removeBuffer)
        {
            _renderers.Remove(target);
            _startFrames.Remove(target);
        }
    }

    // 見えている相手のRendererとマテリアルを、カスタムパスへ渡す
    private void SendToPass()
    {
        List<CS_ScanHighlightPass.Target> targets = CS_ScanHighlightPass.targets;
        targets.Clear();

        foreach (KeyValuePair<Component, Material> pair in _current)
        {
            if (pair.Value == null) continue;

            if (!_renderers.TryGetValue(pair.Key, out Renderer[] renderers))
            {
                renderers = FindRenderers(pair.Key);
                _renderers.Add(pair.Key, renderers);
                _startFrames[pair.Key] = Time.frameCount;
            }

            bool flash = Time.frameCount - _startFrames[pair.Key] < _flashFrames;

            foreach (Renderer renderer in renderers)
            {
                targets.Add(new CS_ScanHighlightPass.Target { renderer = renderer, material = pair.Value, flash = flash });
            }
        }
    }

    // 相手の下のMeshRenderer・SkinnedMeshRendererを集める
    private static Renderer[] FindRenderers(Component target)
    {
        List<Renderer> found = new List<Renderer>();
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) found.Add(renderer);
        }
        return found.ToArray();
    }

    private void Clear()
    {
        CS_ScanHighlightPass.targets.Clear();
        _current.Clear();
        _renderers.Clear();
        _startFrames.Clear();
    }
}
