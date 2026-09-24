using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人のHPと撃退処理を管理するクラス
 * プレイヤーの攻撃やアイテムからのダメージをIDamageable経由で受け取り、
 * HPが0になったら撃退(Destroy)する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・HP上限はCS_VillainStats.maxHpを使う(実際の変更はCS_VillainStats側で行う)
 * ・現在HPはNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   → 敵体力UIはonHpChangedを購読して使う
 * ・撃退の流れ(サーバー、またはオフラインで実行)
 *   1. HPが0になる
 *   2. onDefeated / onAnyVillainDefeated を呼ぶ
 *      → スコア加算はスコア側がonAnyVillainDefeatedを購読して行う
 *   3. ネットワーク時はDespawn、オフライン時はDestroyする
 * ・CS_VillainStatsより後に初期化する必要があるため、
 *   コンポーネントはCS_VillainStatsより下にアタッチする(RequireComponentで自動追加した場合はそうなる)
 */
// ========================================

[RequireComponent(typeof(CS_VillainStats))]
[DefaultExecutionOrder(1)] // オフライン時、CS_VillainStats.Startの後にStartを呼ぶため
public class CS_VillainHealth : NetworkBehaviour, IDamageable
{
    private CS_VillainStats _stats;
    private bool _isDefeated;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentHp = new NetworkVariable<float>();

    public float maxHp => _stats.maxHp;
    public float currentHp => _currentHp.Value;
    public bool isDefeated => _isDefeated;

    public event Action<float, float> onHpChanged;   // (current, max)
    public event Action onDefeated;                   // この悪人が撃退された時(サーバーのみ)

    // どの悪人が撃退されても呼ばれる(サーバーのみ)。スコア加算など、悪人全体を見る側の購読用
    public static event Action<CS_VillainHealth> onAnyVillainDefeated;

    private void Awake()
    {
        _stats = GetComponent<CS_VillainStats>();
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        _currentHp.Value = maxHp;
    }

    public override void OnNetworkSpawn()
    {
        _currentHp.OnValueChanged += HandleHpChanged;
        _stats.onMaxHpChanged += HandleMaxHpChanged;

        if (IsServer)
        {
            _currentHp.Value = maxHp;
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentHp.OnValueChanged -= HandleHpChanged;
        _stats.onMaxHpChanged -= HandleMaxHpChanged;
    }

    // IDamageable実装。攻撃側から呼ばれる(サーバー、またはオフラインで実行される想定)
    public void TakeDamage(float damage)
    {
        // ネットワーク時はサーバーのみが処理する(オフラインはそのまま通す)
        if (IsSpawned && !IsServer) return;
        if (_isDefeated) return;
        if (damage <= 0f) return;

        _currentHp.Value = Mathf.Max(0f, _currentHp.Value - damage);

        if (_currentHp.Value <= 0f)
        {
            Defeat();
        }
    }

    // 撃退処理。通知してから消す
    private void Defeat()
    {
        _isDefeated = true;

        onDefeated?.Invoke();
        onAnyVillainDefeated?.Invoke(this);

        // Despawn(true)でサーバー・クライアント両方のオブジェクトが破棄される
        if (IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void HandleHpChanged(float previous, float current)
    {
        onHpChanged?.Invoke(current, maxHp);
    }

    // HP上限が変わったとき、現在HPが上限を超えないようにする(サーバーのみ)
    private void HandleMaxHpChanged(float newMaxHp)
    {
        if (IsSpawned && !IsServer) return;
        if (_currentHp.Value <= newMaxHp) return;

        _currentHp.Value = newMaxHp;
    }
}
