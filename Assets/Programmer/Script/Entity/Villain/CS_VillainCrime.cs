using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人の犯罪の進行と完遂を管理するクラス
 * スポーンと同時に犯罪を始め、一定時間放置されると犯罪を完遂し、フェードアウトして逃げる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・犯罪が進むのは、スポーン位置で犯罪中(CS_VillainCombat.isCommittingCrime)の間だけ
 *   臨戦態勢中・スポーン位置へ戻っている間は手を止める(それまでの進行度は保持する)
 * ・進行時間がCS_VillainStats.crimeCompleteTimeに達したら完遂
 *   1. onCrimeCompleted / onAnyCrimeCompleted を呼ぶ(サーバーのみ)
 *      → 最終スコアのマイナス・犯罪完遂数の加算は、スコア側がonAnyCrimeCompletedを購読して行う
 *   2. 反撃・当たり判定を止め、fadeDuration秒かけてフェードアウトする(全クライアントで表示)
 *   3. フェードが終わったらDespawn(オフライン時はDestroy)する
 * ・フェードはマテリアルの_BaseColorのアルファ値を下げる
 *   → マテリアルのSurface TypeがTransparentでないと見た目は変わらない(最後に消えるだけになる)
 * ・犯罪完遂数は悪人1人ごとに数える(臨戦態勢の判定が悪人ごとのため)
 */
// ========================================

[RequireComponent(typeof(CS_VillainStats))]
[RequireComponent(typeof(CS_VillainCombat))]
public class CS_VillainCrime : NetworkBehaviour
{
    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField, Min(0f)]
    [Tooltip("犯罪完遂後、フェードアウトして消えるまでの時間(秒)")]
    private float _fadeDuration = 1f;

    private CS_VillainStats _stats;
    private CS_VillainCombat _combat;
    private Renderer[] _renderers;
    private float _crimeElapsed;   // 犯罪を進めた時間(サーバーのみ)

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。クライアントはこれを見てフェードを始める
    private readonly NetworkVariable<bool> _isEscaping = new NetworkVariable<bool>();

    public bool isEscaping => _isEscaping.Value;
    public float crimeProgress => _stats.crimeCompleteTime <= 0f ? 1f : Mathf.Clamp01(_crimeElapsed / _stats.crimeCompleteTime);   // 0～1(サーバーのみ正しい値)

    public event Action onCrimeCompleted;                        // この悪人が犯罪を完遂した時(サーバーのみ)
    public static event Action<CS_VillainCrime> onAnyCrimeCompleted;   // どの悪人が完遂しても呼ばれる(サーバーのみ)。スコア側の購読用

    // このマシンが犯罪を進める権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _stats = GetComponent<CS_VillainStats>();
        _combat = GetComponent<CS_VillainCombat>();
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        _isEscaping.OnValueChanged += HandleEscapingChanged;
    }

    public override void OnNetworkDespawn()
    {
        _isEscaping.OnValueChanged -= HandleEscapingChanged;
    }

    private void Update()
    {
        if (!hasAuthority) return;
        if (_isEscaping.Value) return;
        if (!_combat.isCommittingCrime) return;

        _crimeElapsed += Time.deltaTime;
        if (_crimeElapsed >= _stats.crimeCompleteTime)
        {
            CompleteCrime();
        }
    }

    // 犯罪を完遂する。通知してから逃走(フェードアウト)を始める(サーバー、またはオフライン)
    private void CompleteCrime()
    {
        _isEscaping.Value = true;

        onCrimeCompleted?.Invoke();
        onAnyCrimeCompleted?.Invoke(this);

        // オフラインではOnValueChangedが呼ばれないので、ここで直接始める
        if (!IsSpawned) StartEscape();

        StartCoroutine(DespawnAfterFade());
    }

    private void HandleEscapingChanged(bool previous, bool current)
    {
        if (current) StartEscape();
    }

    // 反撃・当たり判定を止めてフェードを始める(全クライアント)
    private void StartEscape()
    {
        _combat.enabled = false;

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        if (TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color[] baseColors = GetBaseColors();

        for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
        {
            SetAlpha(block, baseColors, 1f - t / _fadeDuration);
            yield return null;
        }

        SetAlpha(block, baseColors, 0f);
    }

    // フェードが終わるのを待ってから消す(サーバー、またはオフライン)
    private IEnumerator DespawnAfterFade()
    {
        yield return new WaitForSeconds(_fadeDuration);

        if (IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Color[] GetBaseColors()
    {
        Color[] colors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material material = _renderers[i].sharedMaterial;
            colors[i] = material != null && material.HasProperty(_baseColorId) ? material.GetColor(_baseColorId) : Color.white;
        }
        return colors;
    }

    private void SetAlpha(MaterialPropertyBlock block, Color[] baseColors, float alpha)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Color color = baseColors[i];
            color.a *= alpha;

            _renderers[i].GetPropertyBlock(block);
            block.SetColor(_baseColorId, color);
            _renderers[i].SetPropertyBlock(block);
        }
    }
}
