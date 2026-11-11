using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
+=====================================
 File   : CS_MayaPreviewMaterialPreview
 Summary: Material ball for the Material panel.
          A sphere far away from the scene, seen by its own camera into a small texture.
          It uses the same Material instance as the scene, so every edit shows at once,
          lit by the scene's sun / sky plus one small key light that only reaches the ball.
          The camera only renders while the panel asks for the picture.
 History: 2026/10/11 New
=====================================+
*/
public class CS_MayaPreviewMaterialPreview : MonoBehaviour
{
    // Far from the scene horizontally (same altitude, so sky and height fog look the same).
    private static readonly Vector3 Origin = new Vector3(9000f, 1.5f, 9000f);
    private const int Resolution = 256;

    private Camera _camera;
    private Transform _ball;
    private MeshRenderer _renderer;
    private RenderTexture _texture;
    private int _requestedFrame = -10;
    private Vector2 _orbit = Vector2.zero;
    private Mesh _sphere, _cube;
    private Light _key;
    private float _keyScale = 1f;
    // The scene uses a fixed, night-time exposure: a few candela is already a bright key.
    private const float KeyIntensity = 12f;
    private bool _useCube;
    public bool Cube { get => _useCube; set { if (_useCube != value) { _useCube = value; _dirty = true; } } }

    // The ball is drawn at most RefreshInterval apart while nothing changes (it is a full
    // HDRP camera); turning it or switching material / mesh draws it at once.
    private const float RefreshInterval = 0.25f;
    private float _nextRender;
    private float _lastOrbit = -10f;
    private bool _dirty = true;
    private Material _lastMaterial;
    public float KeyLight
    {
        get => _keyScale;
        set { float v = Mathf.Max(0f, value); if (v != _keyScale) _dirty = true; _keyScale = v; if (_key != null) _key.intensity = KeyIntensity * _keyScale; }
    }

    public static CS_MayaPreviewMaterialPreview Create()
    {
        var root = new GameObject("Maya Preview Material Ball");
        root.AddComponent<CS_MayaPreviewInternal>();
        root.transform.position = Origin;
        return root.AddComponent<CS_MayaPreviewMaterialPreview>();
    }

    private void Awake()
    {
        _texture = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32)
        {
            name = "Maya Preview Material Ball", antiAliasing = 1, hideFlags = HideFlags.DontSave
        };
        _texture.Create();

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        var collider = ball.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        ball.transform.SetParent(transform, false);
        _ball = ball.transform;
        _renderer = ball.GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _sphere = ball.GetComponent<MeshFilter>().sharedMesh;

        var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cube = cubeObject.GetComponent<MeshFilter>().sharedMesh;
        Destroy(cubeObject);

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(transform, false);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.fieldOfView = 24f;
        _camera.nearClipPlane = 0.5f;
        _camera.farClipPlane = 8f;
        _camera.targetTexture = _texture;
        _camera.depth = -50;
        _camera.enabled = false;
        if (!cameraObject.TryGetComponent(out HDAdditionalCameraData data))
            data = cameraObject.AddComponent<HDAdditionalCameraData>();
        data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
        // A material ball needs none of the expensive screen effects.
        data.customRenderingSettings = true;
        foreach (FrameSettingsField field in new[]
        {
            FrameSettingsField.VolumetricClouds, FrameSettingsField.Volumetrics, FrameSettingsField.SSR,
            FrameSettingsField.SSAO, FrameSettingsField.SSGI, FrameSettingsField.RayTracing,
            FrameSettingsField.ContactShadows, FrameSettingsField.MotionBlur, FrameSettingsField.DepthOfField
        })
        {
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            data.renderingPathCustomFrameSettings.SetEnabled(field, false);
        }

        var lightObject = new GameObject("Key light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(-1.4f, 1.6f, -1.8f);
        lightObject.AddHDLight(LightType.Point);
        _key = lightObject.GetComponent<Light>();
        _key.range = 5f;
        _key.intensity = KeyIntensity * _keyScale;
        _key.shadows = LightShadows.None;

        UpdateCamera();
    }

    private void OnDestroy()
    {
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
    }

    // Shows the material on the ball this frame and returns the picture (one frame late).
    public Texture Render(Material material)
    {
        if (_renderer.sharedMaterial != material) _renderer.sharedMaterial = material;
        var filter = _renderer.GetComponent<MeshFilter>();
        Mesh mesh = Cube ? _cube : _sphere;
        if (filter.sharedMesh != mesh) filter.sharedMesh = mesh;
        if (material != _lastMaterial) { _lastMaterial = material; _dirty = true; }
        float now = Time.unscaledTime;
        if (_dirty || now >= _nextRender || now - _lastOrbit < 0.3f)
        {
            _dirty = false;
            _nextRender = now + RefreshInterval;
            _requestedFrame = Time.frameCount;
            if (!_camera.enabled) _camera.enabled = true;
        }
        return _texture;
    }

    public void Orbit(Vector2 delta)
    {
        _lastOrbit = Time.unscaledTime;
        _orbit.x += delta.x * 0.5f;
        _orbit.y = Mathf.Clamp(_orbit.y + delta.y * 0.5f, -80f, 80f);
        UpdateCamera();
    }

    // Dragging turns the ball (the camera and the key light stay put, so the lighting reads the same).
    private void UpdateCamera()
    {
        _camera.transform.localPosition = new Vector3(0f, 0.45f, -2.6f);
        _camera.transform.localRotation = Quaternion.LookRotation(-_camera.transform.localPosition.normalized, Vector3.up);
        if (_ball != null) _ball.localRotation = Quaternion.Euler(_orbit.y, -_orbit.x, 0f);
    }

    private void LateUpdate()
    {
        // Stop rendering as soon as nobody looks at the ball.
        // Only the frame after a request renders the ball.
        if (_camera.enabled && Time.frameCount - _requestedFrame > 1) _camera.enabled = false;
    }
}
