using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
 * キャラクターの体の残像を一定間隔でその場に置いていく仮エフェクト
 * 奇襲の飛び込み中(FX_PlayerAmbushAfterimage)で使っている
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・本番の素材ができるまでの仮
 * ・付いていく先(CS_EffectManagerのfollow。奇襲ならプレイヤー)の一番上の親から SkinnedMeshRenderer を集め、
 *   Interval 秒ごとに BakeMesh でその時の姿を固めて、その場に置く
 *   置いた残像は Fade Time 秒かけて薄くなって消える(Materialの Tint Color のアルファで薄くする)
 * ・残像はこのエフェクトの子にせず、シーン直下の「AmbushAfterimages」にまとめて置く(キャラクターが動いても、その場に残すため)
 * ・Emit Time 秒だけ残像を置き、その後は残っている残像が消えるのを待つ(CS_EffectManagerのLifetimeでも止まる)
 * ・Materialは Tint Color と Base Map を持つ加算・半透明のもの(今は Nova の MT_AmbushAfterimage)
 *   Tint Colorのプロパティ名は Tint Property で変える(Novaなら _TintColor)
 * ・プールに戻された(非表示になった)時は、残像もすぐ消す
 * ・CS_EffectManagerから IEffectPlayable として再生・停止される
 */
// ========================================

public class CS_EffectAfterimage : MonoBehaviour, IEffectPlayable
{
    [Header("見た目")]
    [SerializeField] private Material _material;                                   // 残像のマテリアル(Tint Colorで色と透け具合を決めるもの)
    [SerializeField] private string _tintProperty = "_TintColor";                  // 色を入れるプロパティ名
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(1.6f, 0.15f, 0.25f, 0.8f);   // 残像の色(アルファが最初の濃さ)

    [Header("時間")]
    [SerializeField][Min(0.01f)] private float _interval = 0.04f;                  // 残像を置く間隔(秒)
    [SerializeField][Min(0.01f)] private float _fadeTime = 0.3f;                   // 残像が消えるまでの時間(秒)
    [SerializeField][Min(0.01f)] private float _emitTime = 1f;                     // 残像を置く時間(秒)

    // 置いた残像1つ分
    private class Ghost
    {
        public GameObject gameObject;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Mesh mesh;
        public float age;
    }

    private Transform _ghostRoot;                                 // 残像を置く場所(シーン直下)
    private Transform _characterRoot;                             // 残像を取るキャラクターの一番上の親
    private SkinnedMeshRenderer[] _sources = new SkinnedMeshRenderer[0];
    private readonly List<Ghost> _activeGhosts = new List<Ghost>();
    private readonly Stack<Ghost> _freeGhosts = new Stack<Ghost>();
    private MaterialPropertyBlock _propertyBlock;
    private Color _currentColor;
    private float _elapsed;
    private float _timer;
    private bool _isEmitting;

    // 残像を置いている間と、置いた残像が消えきるまでは見えている
    public bool isAlive => _isEmitting || _activeGhosts.Count > 0;

    private void Awake()
    {
        _currentColor = _color;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateGhosts(deltaTime);
        if (!_isEmitting) return;

        _elapsed += deltaTime;
        _timer += deltaTime;
        if (_timer >= _interval)
        {
            _timer -= _interval;
            PutGhosts();
        }

        if (_elapsed >= _emitTime) Stop();
    }

    // プールに戻された時・無効にされた時は、残像もすぐ消す
    private void OnDisable()
    {
        _isEmitting = false;
        ReleaseAllGhosts();
    }

    private void OnDestroy()
    {
        foreach (Ghost ghost in _activeGhosts) DestroyGhost(ghost);
        foreach (Ghost ghost in _freeGhosts) DestroyGhost(ghost);
        _activeGhosts.Clear();
        _freeGhosts.Clear();

        if (_ghostRoot != null) Destroy(_ghostRoot.gameObject);
    }

    public void Play()
    {
        EnsureGhostRoot();
        ReleaseAllGhosts();
        FindSources();

        _elapsed = 0f;
        _timer = 0f;
        _isEmitting = true;
        _currentColor = _color;

        PutGhosts();   // 飛び込み始めの姿もすぐ置く
    }

    public void Stop()
    {
        _isEmitting = false;
    }

    public void SetColor(Color color)
    {
        _currentColor = color;
    }

    // 付いていく先の一番上の親から、残像にするSkinnedMeshRendererを集める(前回と同じ親なら集め直さない)
    private void FindSources()
    {
        Transform root = transform.root;
        if (root == _characterRoot && _sources.Length > 0) return;

        _characterRoot = root;
        _sources = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    // シーン直下の置き場所を用意する(シーンが切り替わって消えていたら作り直す)
    private void EnsureGhostRoot()
    {
        if (_ghostRoot != null) return;

        // 置き場所と一緒に残像も消えているので、控えも捨てる
        _activeGhosts.Clear();
        _freeGhosts.Clear();

        _ghostRoot = new GameObject("AmbushAfterimages").transform;
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    // 今の姿を、見えているSkinnedMeshRendererごとに固めて置く
    private void PutGhosts()
    {
        foreach (SkinnedMeshRenderer source in _sources)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;

            Ghost ghost = RentGhost();
            source.BakeMesh(ghost.mesh, true);   // 大きさも含めて固める(置く時は大きさ1)

            Transform ghostTransform = ghost.gameObject.transform;
            ghostTransform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            ghostTransform.localScale = Vector3.one;

            // サブメッシュの数だけマテリアルを並べる(足りないと一部が描かれない)
            int subMeshCount = Mathf.Max(1, ghost.mesh.subMeshCount);
            Material[] materials = ghost.renderer.sharedMaterials;
            if (materials.Length != subMeshCount)
            {
                materials = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++) materials[i] = _material;
                ghost.renderer.sharedMaterials = materials;
            }

            ghost.age = 0f;
            ApplyColor(ghost);
            ghost.gameObject.SetActive(true);
            _activeGhosts.Add(ghost);
        }
    }

    // 残像を時間で薄くし、消える時間が来たら控えに戻す
    private void UpdateGhosts(float deltaTime)
    {
        for (int i = _activeGhosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = _activeGhosts[i];
            ghost.age += deltaTime;

            if (ghost.age >= _fadeTime)
            {
                ReleaseGhost(i);
                continue;
            }

            ApplyColor(ghost);
        }
    }

    // 経過に合わせて、色のアルファを薄くする(加算なので色も一緒に薄くなる)
    private void ApplyColor(Ghost ghost)
    {
        float fade = 1f - Mathf.Clamp01(ghost.age / _fadeTime);
        Color color = _currentColor;
        color.a *= fade;

        ghost.renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_tintProperty, color);
        ghost.renderer.SetPropertyBlock(_propertyBlock);
    }

    private Ghost RentGhost()
    {
        while (_freeGhosts.Count > 0)
        {
            Ghost free = _freeGhosts.Pop();
            if (free.gameObject != null) return free;
        }

        GameObject ghostObject = new GameObject("Afterimage");
        ghostObject.transform.SetParent(_ghostRoot, false);
        ghostObject.SetActive(false);

        Ghost ghost = new Ghost
        {
            gameObject = ghostObject,
            filter = ghostObject.AddComponent<MeshFilter>(),
            renderer = ghostObject.AddComponent<MeshRenderer>(),
            mesh = new Mesh { name = "AfterimageMesh" },
        };
        ghost.mesh.MarkDynamic();
        ghost.filter.sharedMesh = ghost.mesh;
        ghost.renderer.shadowCastingMode = ShadowCastingMode.Off;
        ghost.renderer.receiveShadows = false;
        return ghost;
    }

    private void ReleaseGhost(int activeIndex)
    {
        Ghost ghost = _activeGhosts[activeIndex];
        _activeGhosts.RemoveAt(activeIndex);

        // シーンが切り替わって置き場所ごと消えている時は、控えに戻さない
        if (ghost.gameObject == null) return;

        ghost.gameObject.SetActive(false);
        _freeGhosts.Push(ghost);
    }

    private void ReleaseAllGhosts()
    {
        for (int i = _activeGhosts.Count - 1; i >= 0; i--)
        {
            ReleaseGhost(i);
        }
    }

    private static void DestroyGhost(Ghost ghost)
    {
        if (ghost.mesh != null) Destroy(ghost.mesh);
    }
}
