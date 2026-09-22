using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーのHPを管理するクラス
 * CS_PlayerAttackなどからのダメージをIDamageable経由で受け取る
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・HPと生死状態はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   → ダメージ処理は必ずサーバーで実行される(CS_PlayerAttack側の設計による)
 * ・フレンドリーファイアは常に有効。誰の攻撃でも当たる(このクラスでは区別しない)
 * ・onHpChanged / onDeath は、HPバーなどのUIやリスポーン処理から購読して使う
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

public class CS_PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private float _maxHp = 100f;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentHp = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> _isDead = new NetworkVariable<bool>();

    public float maxHp => _maxHp;
    public float currentHp => _currentHp.Value;
    public bool isDead => _isDead.Value;

    public event Action<float, float> onHpChanged;   // (current, max)
    public event Action onDeath;

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        _currentHp.Value = _maxHp;
    }

    public override void OnNetworkSpawn()
    {
        _currentHp.OnValueChanged += HandleHpChanged;
        _isDead.OnValueChanged += HandleDeathChanged;

        if (IsServer)
        {
            _currentHp.Value = _maxHp;
        }
    }

    public override void OnNetworkDespawn()
    {
        _currentHp.OnValueChanged -= HandleHpChanged;
        _isDead.OnValueChanged -= HandleDeathChanged;
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

    // HPを満タンにして復帰させる(サーバーのみ。今後のリスポーン処理からの呼び出しを想定)
    public void Revive()
    {
        if (IsSpawned && !IsServer) return;

        _currentHp.Value = _maxHp;
        _isDead.Value = false;
    }

    private void HandleHpChanged(float previous, float current)
    {
        onHpChanged?.Invoke(current, _maxHp);
    }

    private void HandleDeathChanged(bool previous, bool current)
    {
        if (current)
        {
            onDeath?.Invoke();
        }
    }
}
