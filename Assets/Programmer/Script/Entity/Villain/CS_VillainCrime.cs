using System.Collections;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人1人分の、犯罪への参加状態と犯罪完遂後の逃走を管理するクラス
 * 犯罪の進行・完遂の判定はグループ単位でCS_VillainGroupが行う
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・isCommittingCrime : スポーン位置で犯罪を進めているか(CS_VillainGroupが参照)
 *   臨戦態勢中・スポーン位置へ戻っている間・逃走中はfalse
 * ・crimeCompleteTime : CS_VillainStats.crimeCompleteTime(CS_VillainGroupが完遂時間の計算に使う)
 * ・Escape() : グループが犯罪を完遂した時にCS_VillainGroupから呼ばれる(サーバー、またはオフライン)
 *   1. 反撃・当たり判定を止め、fadeDuration秒かけてフェードアウトする(全クライアントで表示)
 *   2. フェードが終わったらDespawn(オフライン時はDestroy)する
 * ・フェードはマテリアルの_BaseColorのアルファ値を下げる
 *   → マテリアルのSurface TypeがTransparentでないと見た目は変わらない(最後に消えるだけになる)
 * ・スポナーを通さずシーンに直接置いた悪人はグループに属さないため、犯罪は進まない
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

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。クライアントはこれを見てフェードを始める
    private readonly NetworkVariable<bool> _isEscaping = new NetworkVariable<bool>();

    public bool isEscaping => _isEscaping.Value;
    public bool isCommittingCrime => !_isEscaping.Value && _combat.isCommittingCrime;
    public float crimeCompleteTime => _stats.crimeCompleteTime;

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

    // 犯罪を完遂したので逃走する(サーバー、またはオフライン)
    public void Escape()
    {
        if (IsSpawned && !IsServer) return;
        if (_isEscaping.Value) return;

        _isEscaping.Value = true;

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
