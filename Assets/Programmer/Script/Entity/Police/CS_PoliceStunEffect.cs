/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-23 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 警察がスタンしている間、頭の上に「めまい」の見た目(黄色い光の玉がくるくる回る)を出すクラス
/// ・スタンしているかを知っているのはサーバー(またはオフライン)だけなので、NetworkVariableで全員へ同期し、各クライアントが自分の画面に出す
/// ・光の玉は、起動時にプリミティブ(球)で作る(当たり判定なし・影なし)。スタンしていない間は非表示
/// ・玉は頭の上の高さを、決まった半径で回りながら、上下にもゆれる
/// </summary>
[RequireComponent(typeof(CS_PoliceBrain))]
public class CS_PoliceStunEffect : NetworkBehaviour
{
    // 光の色のプロパティ(HDRP/Unlitの色)
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    // スタンしているか(書き込みはサーバーのみ)
    private readonly NetworkVariable<bool> _isStunned = new NetworkVariable<bool>();

    private CS_PoliceBrain _brain = null;

    // 光の玉をまとめる親(スタンしていない間は非表示。回転させる)
    private Transform _effectRoot = null;

    // 光の玉と、その見た目
    private Transform[] _orbs = new Transform[0];
    private Renderer[] _orbRenderers = new Renderer[0];

    // 光の玉の色の上書き(デバッグ表示が見た目の色を消した時に、付け直すために使う)
    private MaterialPropertyBlock _propertyBlock = null;

    [Header("＝＝＝ めまいの見た目 ＝＝＝")]
    [SerializeField]
    [Tooltip("光の玉のマテリアル(Unlit。MT_SecurityLaserなど)")]
    private Material _orbMaterial = null;

    [SerializeField]
    [ColorUsage(false, true)]
    [Tooltip("光の玉の色(明るさも含む)")]
    private Color _orbColor = new Color(3.0f, 2.4f, 0.2f);

    [SerializeField, Min(1)]
    [Tooltip("光の玉の数")]
    private int _orbCount = 3;

    [SerializeField, Min(0.01f)]
    [Tooltip("光の玉の大きさ(m)")]
    private float _orbSize = 0.16f;

    [SerializeField, Min(0.0f)]
    [Tooltip("頭の上の、光の玉が回る中心の高さ(警察の中心から。m)")]
    private float _height = 1.55f;

    [SerializeField, Min(0.0f)]
    [Tooltip("光の玉が回る半径(m)")]
    private float _radius = 0.45f;

    [SerializeField]
    [Tooltip("光の玉が回る速さ(度/秒)")]
    private float _spinSpeed = 300.0f;

    [SerializeField, Min(0.0f)]
    [Tooltip("光の玉が上下にゆれる幅(m)")]
    private float _bobHeight = 0.08f;

    [SerializeField, Min(0.0f)]
    [Tooltip("光の玉が上下にゆれる速さ(回/秒)")]
    private float _bobSpeed = 3.0f;

    private void Awake()
    {
        _brain = GetComponent<CS_PoliceBrain>();
        CreateOrbs();
    }

    private void Update()
    {
        // スタンしているかを、サーバー(またはオフライン)が調べる。クライアントは同期された値を使う
        bool isStunned = !CS_PoliceSquad.IsNetworkClientOnly() && _brain.isStunned;
        if (IsSpawned && IsServer) _isStunned.Value = isStunned;
        if (IsSpawned) isStunned = _isStunned.Value;

        if (_effectRoot.gameObject.activeSelf != isStunned) _effectRoot.gameObject.SetActive(isStunned);
        if (isStunned) AnimateOrbs();
    }

    /// <summary>
    /// 光の玉と、それをまとめる親を作るメソッド(スタンしていない間は、非表示にしておく)
    /// </summary>
    private void CreateOrbs()
    {
        _effectRoot = new GameObject("StunEffect").transform;
        _effectRoot.SetParent(transform, false);

        _orbs = new Transform[_orbCount];
        _orbRenderers = new Renderer[_orbCount];
        _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.SetColor(_unlitColorId, _orbColor);

        for (int i = 0; i < _orbCount; i++)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"Orb_{i}";
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(_effectRoot, false);
            orb.transform.localScale = Vector3.one * _orbSize;

            Renderer orbRenderer = orb.GetComponent<Renderer>();
            if (_orbMaterial != null) orbRenderer.sharedMaterial = _orbMaterial;
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orbRenderer.SetPropertyBlock(_propertyBlock);
            _orbs[i] = orb.transform;
            _orbRenderers[i] = orbRenderer;
        }

        _effectRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 光の玉を、頭の上で回しながら、上下にゆらすメソッド
    /// </summary>
    private void AnimateOrbs()
    {
        _effectRoot.position = transform.position + Vector3.up * _height;
        float spin = Time.time * _spinSpeed;

        for (int i = 0; i < _orbs.Length; i++)
        {
            float phase = (float)i / _orbs.Length;
            float angle = (spin + phase * 360.0f) * Mathf.Deg2Rad;
            float bob = Mathf.Sin((Time.time * _bobSpeed + phase) * Mathf.PI * 2.0f) * _bobHeight;

            // 見た目の色が消されていたら(デバッグ表示が、警察の全部の見た目の色を戻すため)、付け直す
            if (!_orbRenderers[i].HasPropertyBlock()) _orbRenderers[i].SetPropertyBlock(_propertyBlock);

            // 警察の向きに関係なく回るよう、ワールドの向きで位置を決める(親の回転は使わない)
            _orbs[i].position = _effectRoot.position + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _radius + Vector3.up * bob;
        }
    }
}
