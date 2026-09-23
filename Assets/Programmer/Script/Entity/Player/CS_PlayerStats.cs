using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの現在のステータス(HP上限、攻撃力、スピード、ジャンプ力)を持つクラス
 * 装備・バフ・レベルアップなどによる変更を想定し、値の変更用メソッドを公開する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・起動時は Base Stats(CSO_PlayerStats)の値で初期化する
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・変更方法
 *   固定値にする   : SetMaxHp / SetAttackPower / SetMoveSpeed / SetJumpPower
 *   増減させる場合 : 例) SetAttackPower(attackPower + 5) のように現在値+差分を渡す
 *   基準値に戻す   : ResetToBase()(リスポーン時などに使う想定)
 * ・onXxxChangedは値が変わった時に呼ばれる。HUD表示や他システムからの購読用
 * ・attackPowerはCSO_AttackData.damageに掛ける倍率としてCS_PlayerAttackが使う
 * ・moveSpeedはCS_Playerの移動速度として使う
 * ・maxHpが変化した際、現在HPの上限クランプはCS_PlayerHealth側が行う
 * ・jumpPowerは値の保持のみ。ジャンプ処理自体は未実装
 */
// ========================================

public class CS_PlayerStats : NetworkBehaviour
{
    [SerializeField] private CSO_PlayerStats _baseStats;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _maxHp = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _attackPower = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _moveSpeed = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _jumpPower = new NetworkVariable<float>();

    public float maxHp => _maxHp.Value;
    public float attackPower => _attackPower.Value;
    public float moveSpeed => _moveSpeed.Value;
    public float jumpPower => _jumpPower.Value;

    public event Action<float> onMaxHpChanged;
    public event Action<float> onAttackPowerChanged;
    public event Action<float> onMoveSpeedChanged;
    public event Action<float> onJumpPowerChanged;

    private void Awake()
    {
        if (_baseStats == null)
        {
            Debug.LogError("CS_PlayerStats: Base Stats が未設定です", this);
        }
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        ApplyBaseStats();
    }

    public override void OnNetworkSpawn()
    {
        _maxHp.OnValueChanged += HandleMaxHpChanged;
        _attackPower.OnValueChanged += HandleAttackPowerChanged;
        _moveSpeed.OnValueChanged += HandleMoveSpeedChanged;
        _jumpPower.OnValueChanged += HandleJumpPowerChanged;

        if (IsServer)
        {
            ApplyBaseStats();
        }
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= HandleMaxHpChanged;
        _attackPower.OnValueChanged -= HandleAttackPowerChanged;
        _moveSpeed.OnValueChanged -= HandleMoveSpeedChanged;
        _jumpPower.OnValueChanged -= HandleJumpPowerChanged;
    }

    // HP上限を変更する(サーバーのみ)
    public void SetMaxHp(float value)
    {
        if (IsSpawned && !IsServer) return;

        _maxHp.Value = Mathf.Max(1f, value);
    }

    // 攻撃力(倍率)を変更する(サーバーのみ)
    public void SetAttackPower(float value)
    {
        if (IsSpawned && !IsServer) return;

        _attackPower.Value = Mathf.Max(0f, value);
    }

    // 移動速度を変更する(サーバーのみ)
    public void SetMoveSpeed(float value)
    {
        if (IsSpawned && !IsServer) return;

        _moveSpeed.Value = Mathf.Max(0f, value);
    }

    // ジャンプ力を変更する(サーバーのみ)
    public void SetJumpPower(float value)
    {
        if (IsSpawned && !IsServer) return;

        _jumpPower.Value = Mathf.Max(0f, value);
    }

    // 全ステータスをBase Statsの値に戻す(サーバーのみ。リスポーン処理などからの呼び出しを想定)
    public void ResetToBase()
    {
        if (IsSpawned && !IsServer) return;

        ApplyBaseStats();
    }

    private void ApplyBaseStats()
    {
        if (_baseStats == null) return;

        _maxHp.Value = _baseStats.maxHp;
        _attackPower.Value = _baseStats.attackPower;
        _moveSpeed.Value = _baseStats.moveSpeed;
        _jumpPower.Value = _baseStats.jumpPower;
    }

    private void HandleMaxHpChanged(float previous, float current) => onMaxHpChanged?.Invoke(current);
    private void HandleAttackPowerChanged(float previous, float current) => onAttackPowerChanged?.Invoke(current);
    private void HandleMoveSpeedChanged(float previous, float current) => onMoveSpeedChanged?.Invoke(current);
    private void HandleJumpPowerChanged(float previous, float current) => onJumpPowerChanged?.Invoke(current);
}
