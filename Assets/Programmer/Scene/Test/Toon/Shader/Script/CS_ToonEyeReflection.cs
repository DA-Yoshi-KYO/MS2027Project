/* ================================================
 * HDRP Toon Shader - Eye Reflection
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-11-22 | 初回作成（瞳に、その目が見ている景色を映す）
 * 2026-11-22 | 視線と中心を両目で共通に（左右で反射がばらける不具合）
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// SHG_NTE_Eye の瞳に映す景色を、HDRPの小さいカメラで撮って Shader に渡すクラス。
///
/// 目のメッシュから「目の位置・向き・大きさ」を測り、目の少し前から正面を撮る。
/// （測るだけなので、どのモデルでも同じ処理になる）
/// どこが映るかは Shader が視線の反射で決めるので、カメラを動かすと映る物が動く。
///
/// このスクリプトが無い所では、Shader は HDRP の空を映す。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class CS_ToonEyeReflection : MonoBehaviour
{
    public const string EyeShader = "Shader Graphs/SHG_NTE_Eye";

    private const string StrengthProp = "_EyeMirrorStrength";
    private const int Resolution = 256;
    private const float FieldOfView = 110f;
    private const float Interval = 0.5f;        // 撮り直す間隔（秒）。視点を動かした分は毎フレーム Shader が計算する
    private const float InFront = 2f;           // 目の高さの何倍ぶん前から撮るか（前髪やまつ毛の内側に入らない）
    private const float PairDistance = 8f;      // 目の高さの何倍までが同じ顔の目か（左右が別オブジェクトでも1つの視線）

    private static readonly int MapId = Shader.PropertyToID("_EyeViewMap");
    private static readonly int ForwardId = Shader.PropertyToID("_EyeViewForward");
    private static readonly int UpId = Shader.PropertyToID("_EyeViewUp");
    private static readonly int GazeId = Shader.PropertyToID("_EyeGaze");
    private static readonly int GazeCenterId = Shader.PropertyToID("_EyeGazeCenter");

    // 映り込みに要らない、重い物
    private static readonly FrameSettingsField[] Unneeded =
    {
        FrameSettingsField.RayTracing, FrameSettingsField.VolumetricClouds, FrameSettingsField.Volumetrics,
        FrameSettingsField.SSR, FrameSettingsField.SSAO, FrameSettingsField.SSGI, FrameSettingsField.ContactShadows,
        FrameSettingsField.PlanarProbe, FrameSettingsField.SubsurfaceScattering, FrameSettingsField.Decals,
        FrameSettingsField.Bloom, FrameSettingsField.DepthOfField, FrameSettingsField.MotionBlur,
        FrameSettingsField.Antialiasing,
    };

    private static readonly List<CS_ToonEyeReflection> s_all = new List<CS_ToonEyeReflection>();
    private static readonly List<Material> s_materials = new List<Material>();

    private Renderer _renderer;
    private Camera _camera;
    private RenderTexture _texture;
    private MaterialPropertyBlock _block;
    private readonly List<int> _parts = new List<int>();
    private bool _measured;
    private bool _usable;
    private Vector3 _center, _front;            // どちらもオブジェクト空間
    private Vector3[] _corners;                 // 大きさを測るための目の頂点
    private Vector3 _viewForward, _viewUp;
    private float _next;
    private float _height;                      // オブジェクト空間
    private Vector3 _faceFront, _faceCenter;    // 両目で共通。ワールド空間
    private bool _captured;

    // =========================================================
    // 外から
    // =========================================================

    /// <summary>目のマテリアルを使っている Renderer に付ける（付いていれば測り直す）。</summary>
    public static void Setup(Renderer renderer)
    {
        if (renderer == null) return;
        bool has = renderer.TryGetComponent(out CS_ToonEyeReflection reflection);
        if (!has)
        {
            if (!HasEye(renderer)) return;
            reflection = renderer.gameObject.AddComponent<CS_ToonEyeReflection>();
        }
        reflection.MeasureAgain();
        reflection.UpdateGaze();        // 送られた瞬間に左右が揃う
    }

    /// <summary>メッシュが同じまま書き換わった。</summary>
    public static void NotifyMeshChanged(Mesh mesh)
    {
        foreach (CS_ToonEyeReflection reflection in s_all)
            if (reflection.MeshOf() == mesh) reflection.MeasureAgain();
    }

    public static bool HasEye(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
            if (IsEye(material)) return true;
        return false;
    }

    /// <summary>待たずに今すぐ撮る。</summary>
    public void CaptureNow()
    {
        UpdateGaze();
        if (!Prepare()) return;
        _camera.Render();
        _camera.enabled = false;
        _captured = true;
        Hand();
    }

    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        if (!s_all.Contains(this)) s_all.Add(this);
        _measured = false;
        _next = 0f;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        s_all.Remove(this);
        ClearParts();
        if (_camera != null) Release(_camera.gameObject);
        _camera = null;
        if (_texture != null)
        {
            _texture.Release();
            Release(_texture);
        }
        _texture = null;
        _captured = false;
    }

    private void LateUpdate()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _next || !OnDemandRendering.willCurrentFrameRender) return;
        if (_camera != null && _camera.enabled) return;     // まだ描かれていない
        _next = now + Interval;
        UpdateGaze();                                       // 撮らない時も光の反射に要る
        if (!InView() || !Prepare()) return;
        _camera.enabled = true;                             // 次の描画で1回だけ撮る
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (_camera == null || camera != _camera) return;
        _camera.enabled = false;
        _captured = true;
        Hand();
    }

    // =========================================================
    // 測る
    // =========================================================

    private void MeasureAgain()
    {
        _measured = false;
        _next = 0f;
    }

    private Mesh MeshOf()
    {
        if (_renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        return TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
    }

    private static bool IsEye(Material material) =>
        material != null && material.shader != null && material.shader.name == EyeShader;

    private void Measure()
    {
        _measured = true;
        _usable = false;
        ClearParts();
        Mesh mesh = MeshOf();
        if (_renderer == null || mesh == null) return;

        _renderer.GetSharedMaterials(s_materials);
        for (int i = 0; i < Mathf.Min(s_materials.Count, mesh.subMeshCount); i++)
            if (IsEye(s_materials[i])) _parts.Add(i);
        if (_parts.Count == 0 || !mesh.isReadable) return;

        Vector3[] vertices = mesh.vertices;
        var triangles = new List<int[]>();
        foreach (int part in _parts)
            if (mesh.GetTopology(part) == MeshTopology.Triangles) triangles.Add(mesh.GetTriangles(part));
        CS_ToonEyeShape shape = CS_ToonEyeShape.Measure(vertices, triangles);
        if (shape == null) return;

        var used = new Vector3[shape.vertices.Length];
        Vector3 up = (Mathf.Abs(shape.front.y) < 0.99f ? Vector3.up : Vector3.forward);
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i < used.Length; i++)
        {
            used[i] = vertices[shape.vertices[i]];
            float at = Vector3.Dot(used[i], up);
            low = Mathf.Min(low, at);
            high = Mathf.Max(high, at);
        }
        _height = high - low;
        _front = shape.front;
        _center = shape.center;
        _corners = used;
        _usable = true;
    }

    // =========================================================
    // 視線
    // =========================================================

    private void UpdateGaze()
    {
        if (_renderer == null) return;
        if (!_measured) Measure();
        if (!_usable) return;

        Matrix4x4 toWorld = _renderer.localToWorldMatrix;
        Vector3 front = toWorld.MultiplyVector(_front).normalized;
        Vector3 center = toWorld.MultiplyPoint3x4(_center);
        float size = toWorld.MultiplyVector(Vector3.up * _height).magnitude;

        // 左右が別オブジェクトでも、同じ顔の目なら視線も中心も1つ
        Vector3 fronts = Vector3.zero, centers = Vector3.zero;
        int count = 0;
        foreach (CS_ToonEyeReflection other in s_all)
        {
            if (other == null || other._renderer == null || !other._renderer.enabled) continue;
            if (!other._measured) other.Measure();
            if (!other._usable) continue;
            Matrix4x4 m = other._renderer.localToWorldMatrix;
            Vector3 otherFront = m.MultiplyVector(other._front).normalized;
            Vector3 otherCenter = m.MultiplyPoint3x4(other._center);
            if (Vector3.Dot(otherFront, front) <= 0f) continue;
            float reach = PairDistance * Mathf.Max(size, m.MultiplyVector(Vector3.up * other._height).magnitude);
            if ((otherCenter - center).magnitude > reach) continue;
            fronts += otherFront;
            centers += otherCenter;
            count++;
        }
        _faceFront = count > 0 && fronts.sqrMagnitude > 1e-6f ? fronts.normalized : front;
        _faceCenter = count > 0 ? centers / count : center;
        Hand();
    }

    // =========================================================
    // 撮る
    // =========================================================

    private bool Wanted()
    {
        foreach (int part in _parts)
        {
            Material material = part < s_materials.Count ? s_materials[part] : null;
            if (material != null && material.HasProperty(StrengthProp) && material.GetFloat(StrengthProp) > 0f) return true;
        }
        return false;
    }

    private bool InView()
    {
        if (_renderer == null || !_renderer.enabled) return false;
        Camera main = Camera.main;
        return main == null || GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(main), _renderer.bounds);
    }

    private bool Prepare()
    {
        if (_renderer == null) return false;
        _renderer.GetSharedMaterials(s_materials);
        if (!_measured) Measure();
        if (!_usable) return false;
        _renderer.GetSharedMaterials(s_materials);
        if (!Wanted()) return false;

        Matrix4x4 toWorld = _renderer.localToWorldMatrix;
        Vector3 forward = _faceFront;
        if (forward.sqrMagnitude < 0.5f) return false;
        Vector3 upright = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.forward;
        Vector3 up = (upright - forward * Vector3.Dot(upright, forward)).normalized;
        Vector3 center = _faceCenter;

        float ahead = 0f, low = float.MaxValue, high = float.MinValue;
        foreach (Vector3 corner in _corners)
        {
            Vector3 d = toWorld.MultiplyPoint3x4(corner) - center;
            ahead = Mathf.Max(ahead, Vector3.Dot(d, forward));
            float height = Vector3.Dot(d, up);
            low = Mathf.Min(low, height);
            high = Mathf.Max(high, height);
        }
        float size = high - low;
        if (size <= 1e-6f) return false;

        EnsureCamera();
        _camera.transform.SetPositionAndRotation(center + forward * (ahead + size * InFront), Quaternion.LookRotation(forward, up));
        _camera.nearClipPlane = Mathf.Clamp(size * 0.2f, 0.001f, 0.1f);
        _viewForward = forward;
        _viewUp = up;
        return true;
    }

    private void EnsureCamera()
    {
        if (_texture == null)
        {
            _texture = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RT_ToonEyeReflection", useMipMap = true, autoGenerateMips = true,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave
            };
            _texture.Create();
        }
        if (_camera == null)
        {
            // モデルの子にしない（書き出し・階層に混ざらない）
            var holder = new GameObject("ToonEyeReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            _camera = holder.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.fieldOfView = FieldOfView;
            _camera.aspect = 1f;
            _camera.farClipPlane = 1000f;

            var data = holder.AddComponent<HDAdditionalCameraData>();
            data.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            data.hasPersistentHistory = true;
            data.customRenderingSettings = true;
            FrameSettingsOverrideMask mask = data.renderingPathCustomFrameSettingsOverrideMask;
            foreach (FrameSettingsField field in Unneeded)
            {
                mask.mask[(uint)field] = true;
                data.renderingPathCustomFrameSettings.SetEnabled(field, false);
            }
            data.renderingPathCustomFrameSettingsOverrideMask = mask;

            // 画面に見えている物・明るさと同じ条件で撮る
            Camera main = Camera.main;
            if (main != null)
            {
                _camera.cullingMask = main.cullingMask;
                if (main.TryGetComponent(out HDAdditionalCameraData from))
                {
                    data.volumeLayerMask = from.volumeLayerMask;
                    data.clearColorMode = from.clearColorMode;
                    data.backgroundColorHDR = from.backgroundColorHDR;
                }
            }
        }
        _camera.targetTexture = _texture;
    }

    private void Hand()
    {
        if (_renderer == null || !_usable) return;
        if (_block == null) _block = new MaterialPropertyBlock();
        float reach = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        // オブジェクト空間で渡す: 撮り直しを待たずに動きについていく
        Matrix4x4 toObject = _renderer.worldToLocalMatrix;
        Vector3 gaze = toObject.MultiplyVector(_faceFront).normalized;
        Vector3 middle = toObject.MultiplyPoint3x4(_faceCenter);
        foreach (int part in _parts)
        {
            _renderer.GetPropertyBlock(_block, part);
            if (gaze.sqrMagnitude > 0.5f)
            {
                _block.SetVector(GazeId, new Vector4(gaze.x, gaze.y, gaze.z, 1f));
                _block.SetVector(GazeCenterId, new Vector4(middle.x, middle.y, middle.z, 1f));
            }
            if (_captured && _texture != null)
            {
                _block.SetTexture(MapId, _texture);
                _block.SetVector(ForwardId, new Vector4(_viewForward.x, _viewForward.y, _viewForward.z, reach));
                _block.SetVector(UpId, new Vector4(_viewUp.x, _viewUp.y, _viewUp.z, 0f));
            }
            _renderer.SetPropertyBlock(_block, part);
        }
    }

    private void ClearParts()
    {
        if (_renderer != null)
        {
            int count = _renderer.sharedMaterials.Length;
            foreach (int part in _parts)
                if (part < count) _renderer.SetPropertyBlock(null, part);
        }
        _parts.Clear();
    }

    private static void Release(Object made)
    {
        if (Application.isPlaying) Destroy(made);
        else DestroyImmediate(made);
    }
}
