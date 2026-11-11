using UnityEngine;

/*
 * 光学迷彩(CS_OpticalCamo)を使っている間の、プレイヤーの見た目を担当するクラス
 * 使っている間は、モデルを「透明になっている」とわかるガラスのような見た目(半透明・屈折で後ろが歪む)に変える
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・使っているかはCS_OpticalCamo.IsConcealed(自分)で毎フレーム調べる
 *   CS_OpticalCamoが使用者を全員のマシンで同期しているので、各クライアントが自分で判定するだけで他の人の画面にも反映される(通信なし)
 * ・見え方
 *   自分の画面の自分(CS_Player.isLocalPlayer): _selfAlphaの濃さで、輪郭と後ろの歪みがわかる程度に見える(自分がどこにいるかわかるように)
 *   他の人の画面(他のプレイヤー・NPC)       : _othersAlphaの濃さで、ほとんど見えない(歪みだけがうっすらわかる)
 *   ※ 今はCS_OpticalCamo側が、他の人の画面で使用者のモデルを丸ごと隠している(forceRenderingOff)ため、他の人の画面では何も見えない
 *     CS_OpticalCamo側で隠すのをやめれば、他の人の画面では_othersAlphaの見た目になる
 * ・やり方: _modelRoot(Model)の下のRendererのマテリアルを、使っている間だけ_camoMaterialに差し替え、終わったら元に戻す
 *   濃さはMaterialPropertyBlockで_BaseColorのアルファを変えて、自分と他の人で分ける(マテリアルは1つを共有する)
 *   _camoMaterialはHDRP/Litの透明(Transparent)・屈折(Refraction)あり(MT_PlayerOpticalCamo.mat)
 * ・復活直後の点滅(CS_PlayerVisual)はforceRenderingOffを使うので、この処理とはぶつからない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerOpticalCamoVisual : MonoBehaviour
{
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Transform _modelRoot;              // 見た目を変えるモデル(空なら子の"Model")
    [SerializeField] private Material _camoMaterial;            // 使っている間に差し替えるマテリアル(MT_PlayerOpticalCamo)
    [SerializeField][Range(0f, 1f)] private float _selfAlpha = 0.25f;     // 自分の画面での濃さ
    [SerializeField][Range(0f, 1f)] private float _othersAlpha = 0.05f;   // 他の人の画面での濃さ

    private CS_Player _player;
    private Renderer[] _renderers = new Renderer[0];
    private Material[][] _originalMaterials = new Material[0][];
    private MaterialPropertyBlock _propertyBlock;
    private bool _isCamo;

    public bool isCamo => _isCamo;  // 光学迷彩の見た目になっているか

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_modelRoot == null) _modelRoot = transform.Find("Model");
    }

    private void Start()
    {
        CacheRenderers();
    }

    private void OnDisable()
    {
        SetCamo(false);
    }

    private void LateUpdate()
    {
        SetCamo(CS_OpticalCamo.IsConcealed(transform));
    }

    // モデルのRendererと、元のマテリアルを控える
    private void CacheRenderers()
    {
        _renderers = _modelRoot != null ? _modelRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        _originalMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _originalMaterials[i] = _renderers[i].sharedMaterials;
        }
    }

    private void SetCamo(bool isCamo)
    {
        if (isCamo == _isCamo || _camoMaterial == null) return;

        _isCamo = isCamo;
        if (isCamo) ApplyCamo();
        else RestoreMaterials();
    }

    // 全部のマテリアルを光学迷彩のマテリアルにし、見る人(自分か他の人か)に合わせた濃さにする
    private void ApplyCamo()
    {
        Color color = _camoMaterial.GetColor(_baseColorId);
        color.a = _player.isLocalPlayer ? _selfAlpha : _othersAlpha;
        _propertyBlock.Clear();
        _propertyBlock.SetColor(_baseColorId, color);

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer target = _renderers[i];
            if (target == null) continue;

            Material[] camo = new Material[_originalMaterials[i].Length];
            for (int j = 0; j < camo.Length; j++) camo[j] = _camoMaterial;

            target.sharedMaterials = camo;
            target.SetPropertyBlock(_propertyBlock);
        }
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer target = _renderers[i];
            if (target == null) continue;

            target.sharedMaterials = _originalMaterials[i];
            target.SetPropertyBlock(null);
        }
    }
}
