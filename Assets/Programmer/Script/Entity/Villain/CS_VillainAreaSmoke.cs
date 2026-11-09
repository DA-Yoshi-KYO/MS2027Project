using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

/*
 * 悪人エリアの入口に出す、紫の瘴気の壁
 * スポーン位置(CS_VillainSpawnPoint)に付け、そこに悪人グループがいる間だけ瘴気を出す
 *
 * 制作者：　Claude
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/悪人エリアの煙の使い方.md参照
 * ・グループの有無はサーバーにしか無いので、サーバー(CS_VillainSpawner)がSetActiveでNetworkVariableに書き込み、
 *   全クライアントが瘴気を出す・止める(CS_VillainCrimeGaugeと同じ流れ)
 * ・止める時はVisualEffect.Stop(発生だけ止まり、今ある瘴気は寿命まで残る)にして、自然に消えるようにする
 * ・瘴気は子のVillainAreaSmoke(VisualEffect 1つ)。そのTransformが壁の中央と向きで、ローカルのX方向が壁の幅
 * ・壁の幅と高さは、出現点ごとにこのコンポーネントで変えられる(起動時にVisualEffectへ渡す)
 *   前後・上の消え方、量、色などの全部共通の値は、VEG_VillainAreaSmoke.vfxで変える
 * ・場所と向きはField.prefabで手で置く
 */
// ========================================

public class CS_VillainAreaSmoke : NetworkBehaviour
{
    private static readonly int _widthId = Shader.PropertyToID("Width");
    private static readonly int _heightId = Shader.PropertyToID("Height");
    private static readonly int _spawnRateId = Shader.PropertyToID("Spawn Rate");

    private static readonly int _particleScaleId = Shader.PropertyToID("Particle Scale");

    private const float _baseWidth = 6f;              // VFXのSpawn Rateと粒の大きさがちょうどよい壁の幅(m)
    private const float _maxSpawnRateScale = 4f;      // 幅が広い壁で量を増やす倍率の上限(VFXのCapacityに収まるように)
    private const float _minParticleScale = 0.55f;    // 幅が狭い壁で粒を小さくする倍率の下限(これより小さいと薄くて見えない)

    [SerializeField]
    [Tooltip("瘴気のVisualEffect(子のVillainAreaSmoke)。このTransformが壁の中央と向き")]
    private VisualEffect _smoke;

    [SerializeField, Min(0f)]
    [Tooltip("壁の幅(m)。両端は透明になっていく")]
    private float _width = 6f;

    [SerializeField, Min(0f)]
    [Tooltip("壁の高さ(m)")]
    private float _height = 4.5f;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)
    private readonly NetworkVariable<bool> _isActive = new NetworkVariable<bool>();

    private void Awake()
    {
        if (_smoke == null) return;

        _smoke.SetFloat(_widthId, _width);
        _smoke.SetFloat(_heightId, _height);

        // 幅が広い壁は、薄くならないように量を増やす
        float widthRatio = _width / _baseWidth;
        _smoke.SetFloat(_spawnRateId, _smoke.GetFloat(_spawnRateId) * Mathf.Clamp(widthRatio, 1f, _maxSpawnRateScale));

        // 幅が狭い壁は、幅からはみ出さないように粒を小さくする
        _smoke.SetFloat(_particleScaleId, Mathf.Clamp(widthRatio, _minParticleScale, 1f));
    }

    public override void OnNetworkSpawn()
    {
        _isActive.OnValueChanged += HandleActiveChanged;

        // 途中参加したクライアントも、今の状態で表示する
        Apply();
    }

    public override void OnNetworkDespawn()
    {
        _isActive.OnValueChanged -= HandleActiveChanged;
    }

    // 瘴気を出す・止める(サーバー、またはオフライン)
    public void SetActive(bool isActive)
    {
        if (IsSpawned && !IsServer) return;

        _isActive.Value = isActive;

        // オフラインではOnValueChangedが呼ばれないので、ここで直接反映する
        if (!IsSpawned) Apply();
    }

    private void HandleActiveChanged(bool previous, bool current) => Apply();

    // 今の状態を瘴気に反映する(全クライアント)
    private void Apply()
    {
        if (_smoke == null) return;

        if (_isActive.Value) _smoke.Play();
        else _smoke.Stop();
    }

    // 選択中、壁の範囲を表示する
    private void OnDrawGizmosSelected()
    {
        if (_smoke == null) return;

        Transform smokeTransform = _smoke.transform;
        Vector3 center = smokeTransform.position;
        Vector3 halfWidth = smokeTransform.right * _width * 0.5f;
        Vector3 up = Vector3.up * _height;

        Gizmos.color = new Color(0.7f, 0.3f, 1f);
        Gizmos.DrawLine(center - halfWidth, center + halfWidth);
        Gizmos.DrawLine(center - halfWidth + up, center + halfWidth + up);
        Gizmos.DrawLine(center - halfWidth, center - halfWidth + up);
        Gizmos.DrawLine(center + halfWidth, center + halfWidth + up);
    }
}
