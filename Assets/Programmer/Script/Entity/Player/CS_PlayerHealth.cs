using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのHPを管理するクラス
 * CS_PlayerAttackなどからのダメージをIDamageable経由で、
 * アイテムなどからの回復をIHealable経由で受け取る
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・HP上限はCS_PlayerStats.maxHpを使う(実際の変更はCS_PlayerStats側で行う)
 * ・現在HPと生死状態はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   → ダメージ処理は必ずサーバーで実行される(CS_PlayerAttack側の設計による)
 * ・フレンドリーファイアは常に有効。誰の攻撃でも当たる(このクラスでは区別しない)
 * ・onHpChanged / onDeath は、HPバーなどのUIやリスポーン処理から購読して使う
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerStats))]
public class CS_PlayerHealth : NetworkBehaviour, IDamageable, IHealable
{
    private CS_PlayerStats _stats;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentHp = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> _isDead = new NetworkVariable<bool>();

    public float maxHp => _stats.maxHp;
    public float currentHp => _currentHp.Value;
    public bool isDead => _isDead.Value;

    public event Action<float, float> onHpChanged;   // (current, max)
    public event Action onDeath;

    private void Awake()
    {
        _stats = GetComponent<CS_PlayerStats>();
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
        _isDead.OnValueChanged += HandleDeathChanged;
        _stats.onMaxHpChanged += HandleMaxHpChanged;

        if (IsServer)
        {
            _currentHp.Value = maxHp;
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentHp.OnValueChanged -= HandleHpChanged;
        _isDead.OnValueChanged -= HandleDeathChanged;
        _stats.onMaxHpChanged -= HandleMaxHpChanged;
    }

    // IDamageable実装。攻撃側から呼ばれる(サーバー、またはオフラインで実行される想定)
    public void TakeDamage(float damage)
    {
        // ネットワーク時はサーバーのみが処理する(オフラインはそのまま通す)
        if (IsSpawned && !IsServer) return;
        if (_isDead.Value) return;
        if (damage <= 0f) return;

        _currentHp.Value = Mathf.Max(0f, _currentHp.Value - damage);

        if (_currentHp.Value <= 0f)
        {
            _isDead.Value = true;
        }
    }

    // IHealable実装。回復アイテムなどから呼ばれる(サーバー、またはオフラインで実行される想定)
    // 死亡中は回復しない(復帰させたい場合はRevive()を使う)
    public void Heal(float amount)
    {
        if (IsSpawned && !IsServer) return;
        if (_isDead.Value) return;
        if (amount <= 0f) return;

        _currentHp.Value = Mathf.Min(maxHp, _currentHp.Value + amount);
    }

    // HPを満タンにして復帰させる(サーバーのみ。今後のリスポーン処理からの呼び出しを想定)
    public void Revive()
    {
        if (IsSpawned && !IsServer) return;

        _currentHp.Value = maxHp;
        _isDead.Value = false;
    }

    private void HandleHpChanged(float previous, float current)
    {
        onHpChanged?.Invoke(current, maxHp);
    }

    private void HandleDeathChanged(bool previous, bool current)
    {
        if (current)
        {
            onDeath?.Invoke();
        }
    }

    // HP上限が変わったとき、現在HPが上限を超えないようにする(サーバーのみ)
    private void HandleMaxHpChanged(float newMaxHp)
    {
        if (IsSpawned && !IsServer) return;
        if (_currentHp.Value <= newMaxHp) return;

        _currentHp.Value = newMaxHp;
    }
}
