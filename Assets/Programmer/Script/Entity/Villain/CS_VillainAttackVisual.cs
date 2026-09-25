using UnityEngine;

/*
 * 悪人の攻撃の溜めと攻撃判定を見えるようにするクラス
 * 溜め中は体の色を変え、攻撃判定が出ている間は判定の範囲(球)を表示する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・攻撃の段階はCS_VillainCombat.attackPhase(NetworkVariable)を見るので、全クライアントで表示される
 * ・体の色はMaterialPropertyBlockで上書きする(マテリアル自体は書き換えない)
 *   溜めが終わったら上書きを消して元の色に戻す
 * ・判定の範囲は、起動時に作る球(当たり判定なし)で表示する。見た目はHit Area Materialで決める
 *   → 半透明にしたい場合は、Surface TypeがTransparentのマテリアルを設定する
 *   → CS_VillainCrimeのフェードに巻き込まれないよう、球は悪人の子にしない
 */
// ========================================

[RequireComponent(typeof(CS_VillainCombat))]
public class CS_VillainAttackVisual : MonoBehaviour
{
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

    [Header("溜め")]
    [SerializeField]
    [Tooltip("溜め中の体の色")]
    private Color _chargeColor = new Color(1f, 0.5f, 0f);

    [Header("攻撃判定")]
    [SerializeField]
    [Tooltip("攻撃判定の範囲を表示するか")]
    private bool _showHitArea = true;

    [SerializeField]
    [Tooltip("攻撃判定の範囲(球)の見た目。未設定の場合は範囲を表示しない")]
    private Material _hitAreaMaterial;

    private CS_VillainCombat _combat;
    private Renderer[] _bodyRenderers;
    private MaterialPropertyBlock _propertyBlock;
    private GameObject _hitArea;
    private bool _isShowingCharge;   // 溜めの色を表示中か(溜めが終わった時に1回だけ元の色へ戻すため)

    private void Awake()
    {
        _combat = GetComponent<CS_VillainCombat>();
        _bodyRenderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_showHitArea && _hitAreaMaterial != null) _hitArea = CreateHitArea();
    }

    private void Update()
    {
        // 逃走などで反撃が止まったら、攻撃の表示もしない
        CSE_VillainAttackPhase phase = _combat.enabled ? _combat.attackPhase : CSE_VillainAttackPhase.None;

        UpdateChargeColor(phase == CSE_VillainAttackPhase.Charge);
        UpdateHitArea(phase == CSE_VillainAttackPhase.Hit);
    }

    private void OnDisable()
    {
        UpdateChargeColor(false);
        if (_hitArea != null) _hitArea.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_hitArea != null) Destroy(_hitArea);
    }

    private void UpdateChargeColor(bool isCharging)
    {
        if (isCharging)
        {
            SetBodyColor(_chargeColor);
            _isShowingCharge = true;
            return;
        }

        if (!_isShowingCharge) return;

        // 溜めが終わったら、色の上書きを消して元の見た目に戻す
        foreach (Renderer bodyRenderer in _bodyRenderers)
        {
            if (bodyRenderer != null) bodyRenderer.SetPropertyBlock(null);
        }
        _isShowingCharge = false;
    }

    private void SetBodyColor(Color color)
    {
        foreach (Renderer bodyRenderer in _bodyRenderers)
        {
            bodyRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_baseColorId, color);
            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    // 攻撃判定が出ている間だけ、判定と同じ位置・大きさで球を表示する
    private void UpdateHitArea(bool isHitActive)
    {
        if (_hitArea == null) return;

        _hitArea.SetActive(isHitActive);
        if (!isHitActive) return;

        _hitArea.transform.position = _combat.hitCenter;
        _hitArea.transform.localScale = Vector3.one * (_combat.attackData.hitRadius * 2f);
    }

    // 攻撃判定の範囲を表示する球を作る(当たり判定・影は無し)
    private GameObject CreateHitArea()
    {
        GameObject area = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        area.name = $"{name}_AttackArea";
        Destroy(area.GetComponent<Collider>());

        Renderer areaRenderer = area.GetComponent<Renderer>();
        areaRenderer.sharedMaterial = _hitAreaMaterial;
        areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        area.SetActive(false);
        return area;
    }
}
