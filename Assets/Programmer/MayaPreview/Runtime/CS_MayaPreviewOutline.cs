using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewOutline
 Summary: Outline made in the preview (right click > アウトライン作成).
          A child object "<name>_Outline" draws the same mesh with SHG_Outline
          (Shader Graphs/SHG_Outline: the hull pushed out along the normals, front faces cut),
          so the object gets a line around it. Width and colour are set in the Inspector.
          - Follows the object: same transform, and the mesh is taken over again when Maya
            sends a new one (live meshes are rebuilt on every edit).
          - Marked internal: never picked, listed in the Hierarchy or exported (it is a preview
            of how the outline will look).
 History: 2026/11/13 New
=====================================+
*/
[DisallowMultipleComponent]
public class CS_MayaPreviewOutline : MonoBehaviour
{
    public const string ShaderName = "Shader Graphs/SHG_Outline";
    public const string Suffix = "_Outline";
    private const string WidthProp = "_OutlineWidth";
    private const string ColorProp = "_OutlineColor";
    private const float DefaultWidthRatio = 0.01f;   // of the mesh size

    private Renderer _source;
    private Renderer _renderer;
    private Material _material;

    public Material Material => _material;
    public Renderer Source => _source;

    public float Width
    {
        get => _material != null && _material.HasProperty(WidthProp) ? _material.GetFloat(WidthProp) : 0f;
        set { if (_material != null && _material.HasProperty(WidthProp)) _material.SetFloat(WidthProp, Mathf.Max(0f, value)); }
    }

    public Color Color
    {
        get => _material != null && _material.HasProperty(ColorProp) ? _material.GetColor(ColorProp) : Color.black;
        set { if (_material != null && _material.HasProperty(ColorProp)) _material.SetColor(ColorProp, value); }
    }

    // The outline of target (a direct child made by Create), or null.
    public static CS_MayaPreviewOutline Of(GameObject target)
    {
        if (target == null) return null;
        foreach (Transform child in target.transform)
            if (child.TryGetComponent(out CS_MayaPreviewOutline outline)) return outline;
        return null;
    }

    public static bool CanHave(GameObject target) =>
        target != null && Of(target) == null && !CS_MayaPreviewInternal.Is(target.transform) &&
        (target.TryGetComponent(out SkinnedMeshRenderer _) || (target.TryGetComponent(out MeshRenderer _) && target.TryGetComponent(out MeshFilter _)));

    // null + error when the shader is not in the player or the object has no mesh.
    public static CS_MayaPreviewOutline Create(GameObject target, out string error)
    {
        error = null;
        if (!CanHave(target)) { error = "メッシュがないので、アウトラインを作れません。"; return null; }
        Material template = Template();
        Shader shader = template != null ? template.shader : Shader.Find(ShaderName);
        if (shader == null) { error = "SHG_Outline シェーダーが exe に入っていません。"; return null; }

        var holder = new GameObject(target.name + Suffix);
        holder.layer = target.layer;
        holder.AddComponent<CS_MayaPreviewInternal>();
        holder.transform.SetParent(target.transform, false);
        var outline = holder.AddComponent<CS_MayaPreviewOutline>();
        outline._source = target.GetComponent<Renderer>();
        // A copy of an outline material of the scene keeps HDRP's render state (queue, depth,
        // keywords); a bare new Material(shader) drew a grey hull over the object.
        outline._material = template != null ? new Material(template) : new Material(shader);
        outline._material.name = "MT_Outline_" + target.name;
        try { HDMaterial.ValidateMaterial(outline._material); } catch (System.Exception) { /* not HDRP */ }
        if (outline._source is SkinnedMeshRenderer)
            outline._renderer = holder.AddComponent<SkinnedMeshRenderer>();
        else
        {
            holder.AddComponent<MeshFilter>();
            outline._renderer = holder.AddComponent<MeshRenderer>();
        }
        outline._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outline._renderer.receiveShadows = false;
        outline.Sync(true);

        Mesh mesh = outline.SourceMesh();
        float size = mesh != null ? mesh.bounds.extents.magnitude : 1f;
        outline.Width = Mathf.Max(0.0001f, size * DefaultWidthRatio);
        outline.Color = Color.black;
        outline.UseSourceTexture();
        return outline;
    }

    // An SHG_Outline material already in the scene (MT_Mutou_Outline_*), or null.
    private static Material Template()
    {
        Material found = null;
        foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (material == null || material.shader == null || material.shader.name != ShaderName) continue;
            if (material.name.StartsWith("MT_Outline_")) continue;   // one made here
            found = material;
            if (material.name.IndexOf("Skin", System.StringComparison.OrdinalIgnoreCase) >= 0) break;
        }
        return found;
    }

    // SHG_Outline colours the line with the object's own texture (darkened, _OutlineTexBlend):
    // give it the texture of this object, or plain _OutlineColor when it has none.
    // The width mask of the copied material belongs to another model: full width everywhere.
    private void UseSourceTexture()
    {
        if (_material == null) return;
        Texture texture = null;
        Material first = _source != null ? _source.sharedMaterial : null;
        if (first != null)
            foreach (string prop in new[] { "_BaseMap", "_BaseColorMap", "_MainTex" })
                if (first.HasProperty(prop) && first.GetTexture(prop) != null) { texture = first.GetTexture(prop); break; }
        if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", texture);
        if (texture == null && _material.HasProperty("_OutlineTexBlend")) _material.SetFloat("_OutlineTexBlend", 0f);
        if (_material.HasProperty("_OutlineWidthMask")) _material.SetTexture("_OutlineWidthMask", Texture2D.whiteTexture);
    }

    private Mesh SourceMesh()
    {
        if (_source is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        return _source != null && _source.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
    }

    // Takes over the mesh (Maya sends a new one on every edit) and follows show / hide.
    private void Sync(bool force)
    {
        if (_source == null || _renderer == null) return;
        Mesh mesh = SourceMesh();
        if (_renderer is SkinnedMeshRenderer target && _source is SkinnedMeshRenderer skinned)
        {
            if (force || target.sharedMesh != mesh)
            {
                target.sharedMesh = mesh;
                target.bones = skinned.bones;
                target.rootBone = skinned.rootBone;
                target.sharedMaterials = Materials(mesh);
            }
        }
        else if (TryGetComponent(out MeshFilter filter) && (force || filter.sharedMesh != mesh))
        {
            filter.sharedMesh = mesh;
            _renderer.sharedMaterials = Materials(mesh);
        }
        _renderer.enabled = _source.enabled;
    }

    private Material[] Materials(Mesh mesh)
    {
        var materials = new Material[Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1)];
        for (int i = 0; i < materials.Length; i++) materials[i] = _material;
        return materials;
    }

    private void LateUpdate()
    {
        if (_source == null) { Destroy(gameObject); return; }
        Sync(false);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
