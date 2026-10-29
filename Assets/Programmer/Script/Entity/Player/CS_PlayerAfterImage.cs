using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * 回避(ブリンク)・SA(ハイドポイントへ飛ぶ)・奇襲の飛び込みの間、プレイヤーの残像を出すクラス
 * 一定間隔でモデルのその瞬間のポーズを写し取り、一色で光るシルエットとしてその場に置いて、少しずつ消す
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・残像を出す間
 *   回避 : ブリンクで高速移動している間(CS_Player.isDashing)
 *   SA   : ハイドポイントへ飛んでいる間(CS_PlayerHide.isHiding で、まだ張り付いていない間)
 *   奇襲 : 飛び込み〜当たる瞬間(CS_PlayerAmbush.isLunging)
 * ・上の状態は操作している本人(NPCはホスト)の手元でしか分からないので、
 *   「残像を出している最中か」だけをNetworkVariable(書き込みはOwner)で全員に同期し、各クライアントが自分の画面で残像を作る
 *   オフライン(NetworkManagerが動いていない)の時は、自分の画面で作るだけ
 * ・残像の作り方
 *   Interval 秒ごとに、子のSkinnedMeshRenderer(モデル)のポーズをBakeMeshでMeshに写し取り、
 *   MeshRendererを持つ残像のオブジェクトをその位置・向きに置く。Lifetime 秒かけて透明にして消す
 *   残像のオブジェクトはプールして使い回す(Max Count を超えたら一番古いものを使い回す)
 * ・見た目はシルエット(テクスチャは使わず、Material を Color の一色で光らせる)
 *   Material は加算合成のもの(既定はNovaのGlow_Additive)を想定。テクスチャは白に差し替えて使う
 *   色の濃さはNovaのTint Color(_TintColor)のアルファで変える(加算合成なので、アルファが下がると暗く=透明になる)
 * ・時間はTime.timeScaleの影響を受ける(ゲームの速さに合わせて遅くなる)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAfterImage : NetworkBehaviour
{
    [SerializeField] private Material _material;                     // 残像のマテリアル(加算合成のもの)
    [SerializeField][ColorUsage(true, true)] private Color _color = new Color(0.3f, 1.2f, 2f, 0.6f);   // 残像の色(HDR。アルファで濃さ)
    [SerializeField][Min(0.01f)] private float _interval = 0.05f;   // 残像を置く間隔(秒)
    [SerializeField][Min(0.01f)] private float _lifetime = 0.3f;    // 残像が消えるまでの時間(秒)
    [SerializeField][Min(1)] private int _maxCount = 24;            // 同時に出せる残像の数

    private static readonly int _tintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int _baseMapId = Shader.PropertyToID("_BaseMap");

    // 残像1つ分(モデルのSkinnedMeshRenderer1つにつき1つ)
    private class Ghost
    {
        public GameObject gameObject;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh mesh;
        public float age;
    }

    private CS_Player _player;
    private CS_PlayerHide _hide;
    private CS_PlayerAmbush _ambush;
    private SkinnedMeshRenderer[] _skinnedRenderers;

    private readonly List<Ghost> _ghosts = new List<Ghost>();
    private readonly List<Color32> _whiteColors = new List<Color32>();
    private MaterialPropertyBlock _propertyBlock;
    private Material _ghostMaterial;
    private Transform _ghostRoot;
    private int _nextGhost;
    private float _spawnTimer;
    private bool _isActiveOffline;

    // 書き込みはOwner(操作している本人。NPCはホスト)。読み取りは全員可
    private readonly NetworkVariable<bool> _isActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private bool isActive => IsSpawned ? _isActive.Value : _isActiveOffline;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _hide = GetComponent<CS_PlayerHide>();
        _ambush = GetComponent<CS_PlayerAmbush>();
        _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        UpdateActive();
        UpdateSpawn();
        UpdateGhosts();
    }

    public override void OnDestroy()
    {
        foreach (Ghost ghost in _ghosts)
        {
            if (ghost.mesh != null) Destroy(ghost.mesh);
        }
        if (_ghostRoot != null) Destroy(_ghostRoot.gameObject);
        if (_ghostMaterial != null) Destroy(_ghostMaterial);

        base.OnDestroy();
    }

    // ========================================
    // 残像を出している最中か(Owner、またはオフラインで決める)
    // ========================================

    private void UpdateActive()
    {
        if (IsSpawned && !IsOwner) return;

        bool active = IsDodging() || IsFlyingToHidePoint() || IsAmbushLunging();
        if (!IsSpawned)
        {
            _isActiveOffline = active;
            return;
        }

        if (_isActive.Value != active) _isActive.Value = active;
    }

    private bool IsDodging()
    {
        return _player.isDashing;
    }

    private bool IsFlyingToHidePoint()
    {
        return _hide != null && _hide.isHiding && !_hide.isAttached;
    }

    private bool IsAmbushLunging()
    {
        return _ambush != null && _ambush.isLunging;
    }

    // ========================================
    // 残像を作る(全クライアント)
    // ========================================

    private void UpdateSpawn()
    {
        if (!isActive || _material == null)
        {
            _spawnTimer = 0f;   // 出し始めた瞬間に1つ目を置く
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        _spawnTimer += _interval;
        if (_spawnTimer < 0f) _spawnTimer = _interval;   // 止まっていた時に、まとめて置かないようにする

        foreach (SkinnedMeshRenderer skinnedRenderer in _skinnedRenderers)
        {
            if (skinnedRenderer == null || !skinnedRenderer.gameObject.activeInHierarchy) continue;

            SpawnGhost(skinnedRenderer);
        }
    }

    // モデルの今のポーズを写し取って、その位置に置く
    private void SpawnGhost(SkinnedMeshRenderer skinnedRenderer)
    {
        Ghost ghost = GetGhost();

        skinnedRenderer.BakeMesh(ghost.mesh, true);
        SetWhiteVertexColors(ghost.mesh);

        Transform ghostTransform = ghost.gameObject.transform;
        ghostTransform.SetPositionAndRotation(skinnedRenderer.transform.position, skinnedRenderer.transform.rotation);

        Material[] materials = ghost.meshRenderer.sharedMaterials;
        if (materials.Length != ghost.mesh.subMeshCount)
        {
            materials = new Material[ghost.mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = _ghostMaterial;
            ghost.meshRenderer.sharedMaterials = materials;
        }

        ghost.age = 0f;
        ApplyColor(ghost, 1f);
        ghost.gameObject.SetActive(true);
    }

    // 空いている残像を返す(無ければ作る。Max Countに達していたら一番古いものを使い回す)
    private Ghost GetGhost()
    {
        EnsureGhostRoot();

        foreach (Ghost ghost in _ghosts)
        {
            if (!ghost.gameObject.activeSelf) return ghost;
        }

        if (_ghosts.Count < _maxCount)
        {
            Ghost created = CreateGhost();
            _ghosts.Add(created);
            return created;
        }

        Ghost oldest = _ghosts[_nextGhost % _ghosts.Count];
        _nextGhost++;
        return oldest;
    }

    private Ghost CreateGhost()
    {
        GameObject ghostObject = new GameObject("AfterImage");
        ghostObject.transform.SetParent(_ghostRoot, false);
        ghostObject.SetActive(false);

        Mesh mesh = new Mesh { name = "AfterImage" };
        mesh.MarkDynamic();

        MeshFilter meshFilter = ghostObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = ghostObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        return new Ghost { gameObject = ghostObject, meshFilter = meshFilter, meshRenderer = meshRenderer, mesh = mesh };
    }

    // 残像を置く親(プレイヤーと一緒に動かないよう、シーン直下に置く)と、シルエット用のマテリアルを作る(最初の1回だけ)
    private void EnsureGhostRoot()
    {
        if (_ghostRoot != null) return;

        _ghostRoot = new GameObject($"{name}_AfterImages").transform;

        _ghostMaterial = new Material(_material) { name = "AfterImage" };
        if (_ghostMaterial.HasProperty(_baseMapId)) _ghostMaterial.SetTexture(_baseMapId, Texture2D.whiteTexture);
    }

    // 頂点カラーを白にする(Novaのシェーダーは頂点カラーを掛けるため。モデルに頂点カラーが無いと黒くなって見えない)
    private void SetWhiteVertexColors(Mesh mesh)
    {
        int vertexCount = mesh.vertexCount;
        while (_whiteColors.Count < vertexCount) _whiteColors.Add(new Color32(255, 255, 255, 255));
        if (_whiteColors.Count > vertexCount) _whiteColors.RemoveRange(vertexCount, _whiteColors.Count - vertexCount);

        mesh.SetColors(_whiteColors);
    }

    // ========================================
    // 残像を消していく(全クライアント)
    // ========================================

    private void UpdateGhosts()
    {
        foreach (Ghost ghost in _ghosts)
        {
            if (!ghost.gameObject.activeSelf) continue;

            ghost.age += Time.deltaTime;
            float remaining = 1f - ghost.age / _lifetime;
            if (remaining <= 0f)
            {
                ghost.gameObject.SetActive(false);
                continue;
            }

            ApplyColor(ghost, remaining);
        }
    }

    // 色を設定する(rateが下がるほど薄くなる)
    private void ApplyColor(Ghost ghost, float rate)
    {
        Color color = _color;
        color.a *= rate;

        _propertyBlock.Clear();
        _propertyBlock.SetColor(_tintColorId, color);
        ghost.meshRenderer.SetPropertyBlock(_propertyBlock);
    }
}
