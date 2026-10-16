using UnityEngine;

/*
 * 悪人の攻撃の予告エフェクト(頭の上で赤くぴかっと光る)を動かすクラス
 * CS_VillainAttackVisualが予告中だけ表示(SetActive)し、表示されている間このクラスが光り方を動かす
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・見た目は板(Quad)1枚。常にカメラの方を向く(ビルボード)
 * ・表示された瞬間に大きく光り(popScale倍)、popTime秒で元の大きさに縮む。その後は blinkSpeed 回/秒で明るさが脈打つ
 * ・色はMaterialPropertyBlockで上書きする(マテリアル自体は書き換えない)
 *   HDRP/Unlitの色(_UnlitColor)。1より大きい明るさ(intensity)を掛けると、Bloomで光って見える
 * ・プレハブは Tools/Villain/攻撃予告エフェクトを作る で作る(CSED_VillainWarningEffectBuilder)
 */
// ========================================

public class CS_VillainAttackWarningEffect : MonoBehaviour
{
    private static readonly int _unlitColorId = Shader.PropertyToID("_UnlitColor");

    [SerializeField]
    [Tooltip("光の色")]
    private Color _color = new Color(1f, 0.05f, 0.05f, 1f);

    [SerializeField, Min(0f)]
    [Tooltip("光の明るさ(1より大きいとBloomで光って見える)")]
    private float _intensity = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("表示された瞬間の大きさ(倍)")]
    private float _popScale = 1.8f;

    [SerializeField, Min(0.01f)]
    [Tooltip("表示された瞬間の大きさから、元の大きさに縮むまでの時間(秒)")]
    private float _popTime = 0.12f;

    [SerializeField, Min(0f)]
    [Tooltip("縮んだ後に明るさが脈打つ速さ(回/秒)")]
    private float _blinkSpeed = 8f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("脈打つ時の一番暗い明るさ(割合)")]
    private float _blinkMin = 0.5f;

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _baseScale;
    private float _shownTime;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _shownTime = Time.time;
        Animate();
    }

    private void LateUpdate()
    {
        Animate();
    }

    private void Animate()
    {
        float elapsed = Time.time - _shownTime;

        // 表示された瞬間に大きく光り、すぐ元の大きさに縮む
        float pop = Mathf.Clamp01(elapsed / _popTime);
        float scale = Mathf.Lerp(_popScale, 1f, 1f - (1f - pop) * (1f - pop));
        transform.localScale = _baseScale * scale;

        // 縮んだ後は明るさを脈打たせる
        float brightness = 1f;
        if (pop >= 1f)
        {
            float wave = 0.5f + 0.5f * Mathf.Cos((elapsed - _popTime) * _blinkSpeed * Mathf.PI * 2f);
            brightness = Mathf.Lerp(_blinkMin, 1f, wave);
        }
        SetColor(_color * (_intensity * brightness * (pop < 1f ? 1.5f : 1f)));

        FaceCamera();
    }

    private void SetColor(Color color)
    {
        if (_renderer == null) return;

        color.a = _color.a;
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_unlitColorId, color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    // カメラの方を向く(Quadは裏から見ると映らないので、カメラと同じ向きにする)
    private void FaceCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        transform.rotation = mainCamera.transform.rotation;
    }
}
