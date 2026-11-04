using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * カメラとプレイヤーの間に入ったオブジェクトを透過(アルファディザ)させるクラス
 * 透過させたいオブジェクトにはタグ(Building・Propなど)を付けるだけでよい。タグを外せば透過しなくなる
 * (ゲームロジック側はこのクラスの存在を知らない。外してもゲームは動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・どれだけ透過させるかはタグごとに_settingsで決める(既定: Building = 0(全部抜ける)、Prop = 0.5)
 * ・透過はマテリアルの_DitherFadeプロパティ(1 = 不透明、0 = 全部抜ける)で行う
 *   SHG_DitherLit(ディザ対応のOpaqueシェーダーグラフ)のマテリアルなら効く。_DitherFadeを持たないマテリアルは何も変わらない
 *   値はMaterialPropertyBlockで渡す(共有マテリアルを書き換えると、同じマテリアルの建物が全部抜けてしまうため)
 *   ディザは影に掛からないので、透過中も影は残る
 * ・判定は「カメラ → プレイヤーの各高さ(_targetHeights)」へのSphereCastと、カメラ位置のOverlapSphere
 *   (カメラが建物の中に入り込んだ時も透過させるため)
 *   当たったコライダーから親をたどり、最初に_settingsのタグが付いているオブジェクトを透過の単位にする
 *   (その子のRendererをまとめて透過する。コライダーが子に付いていても大丈夫)
 * ・透過・復帰は_fadeSpeed(1秒あたりに変わる量)で一定の速さで変える
 * ・自分の画面のプレイヤー(CS_Player.isLocalPlayer)のカメラだけで判定する
 *   (他人のプレイヤー・NPCのカメラは無効化されている)
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerCameraOcclusion : MonoBehaviour
{
    // タグごとの透過の設定
    [Serializable]
    private class FadeSetting
    {
        public string tag = "Building";
        [Range(0f, 1f)] public float fadeValue = 0f;    // 遮っている間の_DitherFade(0 = 全部抜ける、1 = 不透明)
    }

    // 透過中のオブジェクトの状態
    private class FadeTarget
    {
        public Renderer[] renderers;
        public float current = 1f;    // 今の_DitherFade
        public float target = 1f;     // 目標の_DitherFade(このフレーム遮っていなければ1)
    }

    private static readonly int DitherFadeId = Shader.PropertyToID("_DitherFade");
    private const int MaxHits = 32;

    [Header("タグごとの透過")]
    [SerializeField] private FadeSetting[] _settings =
    {
        new FadeSetting { tag = "Building", fadeValue = 0f },
        new FadeSetting { tag = "Prop", fadeValue = 0.5f },
    };

    [Header("判定")]
    [SerializeField] private float[] _targetHeights = { 0.3f, 1.6f };    // プレイヤーの足元からの高さ。カメラからここへ向けて判定する
    [SerializeField][Min(0f)] private float _castRadius = 0.2f;          // 判定の太さ(m)
    [SerializeField] private LayerMask _layerMask = ~0;                  // 判定するレイヤー

    [Header("変わる速さ")]
    [SerializeField][Min(0.1f)] private float _fadeSpeed = 6f;    // 1秒あたりに_DitherFadeが変わる量(6なら約0.17秒で切り替わる)

    private CS_Player _player;
    private MaterialPropertyBlock _propertyBlock;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
    private readonly Collider[] _overlaps = new Collider[MaxHits];
    private readonly Dictionary<Transform, FadeTarget> _targets = new Dictionary<Transform, FadeTarget>();
    private readonly List<Transform> _removeList = new List<Transform>();

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        Camera playerCamera = _player.playerCamera;
        if (playerCamera == null || !_player.isLocalPlayer) return;

        // 一旦すべて「遮っていない」にし、判定で当たったものだけ目標を下げる
        foreach (FadeTarget fadeTarget in _targets.Values)
        {
            fadeTarget.target = 1f;
        }

        DetectOccluders(playerCamera.transform.position);
        UpdateFade();
    }

    private void OnDisable()
    {
        // 外した時・無効にした時は全部元に戻す
        foreach (FadeTarget fadeTarget in _targets.Values)
        {
            ResetRenderers(fadeTarget);
        }
        _targets.Clear();
    }

    // カメラとプレイヤーの間にあるオブジェクトを探し、目標の_DitherFadeを設定する
    private void DetectOccluders(Vector3 cameraPosition)
    {
        // カメラが建物の中に入り込んでいる時
        int overlapCount = Physics.OverlapSphereNonAlloc(cameraPosition, _castRadius, _overlaps, _layerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            MarkOccluder(_overlaps[i]);
        }

        // カメラからプレイヤーの各高さまでの間
        foreach (float height in _targetHeights)
        {
            Vector3 toTarget = transform.position + Vector3.up * height - cameraPosition;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon) continue;

            int hitCount = Physics.SphereCastNonAlloc(cameraPosition, _castRadius, toTarget / distance, _hits, distance, _layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                MarkOccluder(_hits[i].collider);
            }
        }
    }

    // 当たったコライダーから透過の単位(タグが付いたオブジェクト)を探して、目標を下げる
    private void MarkOccluder(Collider hitCollider)
    {
        if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) return;    // 自分自身は無視

        Transform root = FindTaggedRoot(hitCollider.transform, out FadeSetting setting);
        if (root == null) return;

        if (!_targets.TryGetValue(root, out FadeTarget fadeTarget))
        {
            fadeTarget = new FadeTarget { renderers = root.GetComponentsInChildren<Renderer>() };
            _targets.Add(root, fadeTarget);
        }

        // 複数のタグに当たった時は、より透過する方を使う
        fadeTarget.target = Mathf.Min(fadeTarget.target, setting.fadeValue);
    }

    // 親をたどり、最初に_settingsのタグが付いているオブジェクトを返す
    private Transform FindTaggedRoot(Transform current, out FadeSetting setting)
    {
        for (; current != null; current = current.parent)
        {
            foreach (FadeSetting candidate in _settings)
            {
                if (!current.CompareTag(candidate.tag)) continue;

                setting = candidate;
                return current;
            }
        }

        setting = null;
        return null;
    }

    // 目標へ一定の速さで近づけ、元に戻りきったものは管理から外す
    private void UpdateFade()
    {
        float step = _fadeSpeed * Time.deltaTime;
        _removeList.Clear();

        foreach (KeyValuePair<Transform, FadeTarget> pair in _targets)
        {
            FadeTarget fadeTarget = pair.Value;

            // 消えたオブジェクトは外す
            if (pair.Key == null)
            {
                _removeList.Add(pair.Key);
                continue;
            }

            fadeTarget.current = Mathf.MoveTowards(fadeTarget.current, fadeTarget.target, step);

            if (fadeTarget.current >= 1f)
            {
                ResetRenderers(fadeTarget);
                _removeList.Add(pair.Key);
                continue;
            }

            ApplyFade(fadeTarget);
        }

        foreach (Transform key in _removeList)
        {
            _targets.Remove(key);
        }
    }

    // _DitherFadeをRendererごとに渡す
    private void ApplyFade(FadeTarget fadeTarget)
    {
        foreach (Renderer renderer in fadeTarget.renderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(DitherFadeId, fadeTarget.current);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    // 不透明に戻す(PropertyBlockを外して、SRP Batcherが効く状態に戻す)
    private void ResetRenderers(FadeTarget fadeTarget)
    {
        foreach (Renderer renderer in fadeTarget.renderers)
        {
            if (renderer == null) continue;

            renderer.SetPropertyBlock(null);
        }
    }
}
