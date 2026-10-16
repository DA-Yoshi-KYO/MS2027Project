using UnityEngine;

/*
 * 悪人の攻撃の溜めと攻撃判定を見えるようにするクラス
 * 溜め中は体の色を変え、攻撃判定が出る直前は赤い予告を出し、攻撃判定が出ている間は判定の範囲(球)を表示する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・攻撃の段階はCS_VillainCombat.attackPhase(NetworkVariable)を見るので、全クライアントで表示される
 * ・体の色はMaterialPropertyBlockで上書きする(マテリアル自体は書き換えない)
 *   溜めが終わったら上書きを消して元の色に戻す
 * ・攻撃の予告(回避タイミングを教える)
 *   攻撃判定が出る warningLeadTime 秒前(= 溜め開始から chargeTime - warningLeadTime 秒後)に予告を出し、攻撃判定が出たら消す
 *   予告中は体の色を Warning Color(赤)にし、Warning Effect Prefab が設定されていればエフェクトも出す
 *   エフェクトの位置 = Warning Effect Bone(既定: 右手 hand.r)の位置 + Warning Effect Offset
 *   → Offsetは悪人の向き基準(X: 右 / Y: 上 / Z: 前)。ボーンの回転・スケールの影響は受けない
 *   → ボーンが見つからない(名前が空・モデル差し替えで名前が変わった)時は、悪人の中心を基準にする
 *   → アニメーションで動いた後のこぶしに合わせるため、位置はLateUpdateで毎フレーム合わせる
 *   → warningLeadTime が chargeTime 以上なら、溜めの最初から予告する
 *   溜めの経過時間は同期していないので、各クライアントが溜めに入ったのを見た時刻から数える
 *   (通信の遅れの分だけ遅れて始まるが、消えるのは攻撃判定が出た瞬間なのでサーバーと揃う)
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

    [Header("攻撃の予告")]
    [SerializeField, Min(0f)]
    [Tooltip("攻撃判定が出る何秒前から予告を出すか")]
    private float _warningLeadTime = 0.3f;

    [SerializeField]
    [Tooltip("予告中の体の色")]
    private Color _warningColor = Color.red;

    [SerializeField]
    [Tooltip("予告中に出すエフェクト(任意)。未設定の場合は体の色だけで予告する")]
    private GameObject _warningEffectPrefab;

    [SerializeField]
    [Tooltip("予告エフェクトを出す基準のボーン名(こぶしなど)。空・見つからない時は悪人の中心")]
    private string _warningEffectBone = "hand.r";

    [SerializeField]
    [Tooltip("基準からのずれ(m)。悪人の向き基準で X: 右 / Y: 上 / Z: 前")]
    private Vector3 _warningEffectOffset = new Vector3(0f, 0f, 0.1f);

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
    private GameObject _warningEffect;
    private Transform _warningEffectAnchor;   // 予告エフェクトの基準(見つからない時は自分)
    private bool _isShowingCharge;   // 溜めの色を表示中か(溜めが終わった時に1回だけ元の色へ戻すため)
    private CSE_VillainAttackPhase _previousPhase;
    private float _chargeStartTime;  // このクライアントで溜めに入ったのを見た時刻

    private void Awake()
    {
        _combat = GetComponent<CS_VillainCombat>();
        _bodyRenderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_showHitArea && _hitAreaMaterial != null) _hitArea = CreateHitArea();

        // 体の色の対象に含めないよう、体のRendererを集めた後に作る
        if (_warningEffectPrefab != null) _warningEffect = CreateWarningEffect();
        _warningEffectAnchor = FindChild(transform, _warningEffectBone);
        if (_warningEffectAnchor == null) _warningEffectAnchor = transform;
    }

    // アニメーションで動いた後のボーンに合わせる
    private void LateUpdate()
    {
        if (_warningEffect == null || !_warningEffect.activeSelf) return;

        _warningEffect.transform.position = _warningEffectAnchor.position + transform.rotation * _warningEffectOffset;
    }

    private void Update()
    {
        // 逃走などで反撃が止まったら、攻撃の表示もしない
        CSE_VillainAttackPhase phase = _combat.enabled ? _combat.attackPhase : CSE_VillainAttackPhase.None;
        if (phase == CSE_VillainAttackPhase.Charge && _previousPhase != CSE_VillainAttackPhase.Charge)
        {
            _chargeStartTime = Time.time;
        }
        _previousPhase = phase;

        bool isCharging = phase == CSE_VillainAttackPhase.Charge;
        bool isWarning = isCharging && IsWarningTime();

        UpdateChargeColor(isCharging, isWarning);
        UpdateWarningEffect(isWarning);
        UpdateHitArea(phase == CSE_VillainAttackPhase.Hit);
    }

    private void OnDisable()
    {
        UpdateChargeColor(false, false);
        UpdateWarningEffect(false);
        if (_hitArea != null) _hitArea.SetActive(false);
        _previousPhase = CSE_VillainAttackPhase.None;
    }

    private void OnDestroy()
    {
        if (_hitArea != null) Destroy(_hitArea);
    }

    // 攻撃判定が出る warningLeadTime 秒前を過ぎたか
    private bool IsWarningTime()
    {
        float warningStartTime = Mathf.Max(0f, _combat.chargeTime - _warningLeadTime);
        return Time.time - _chargeStartTime >= warningStartTime;
    }

    // 溜め中は体の色を変える(予告中は予告の色)
    private void UpdateChargeColor(bool isCharging, bool isWarning)
    {
        if (isCharging)
        {
            SetBodyColor(isWarning ? _warningColor : _chargeColor);
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

    private void UpdateWarningEffect(bool isWarning)
    {
        if (_warningEffect == null) return;
        if (_warningEffect.activeSelf == isWarning) return;

        _warningEffect.SetActive(isWarning);
    }

    // 予告エフェクトを作る(普段は非表示)
    // ボーンの子にするとボーンのスケールで大きさが変わるため、悪人の子にして位置だけLateUpdateで合わせる
    private GameObject CreateWarningEffect()
    {
        GameObject effect = Instantiate(_warningEffectPrefab, transform);
        effect.SetActive(false);
        return effect;
    }

    // 名前の一致する子孫を探す(見つからなければnull)
    private static Transform FindChild(Transform parent, string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName) return child;
        }
        return null;
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
