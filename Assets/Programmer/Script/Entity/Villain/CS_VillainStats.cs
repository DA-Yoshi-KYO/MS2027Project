using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 悪人の現在のステータス(HP上限、移動速度、攻撃力、臨戦態勢範囲、犯罪完遂時間)を持つクラス
 * アイテムやイベントによる強化・弱体化を想定し、値の変更用メソッドを公開する
 * 悪人に影響する数値の変更は、基本的にここを通す(窓口を一本化する)
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・起動時は Base Stats(CSO_VillainStats)の値で初期化する
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・変更方法
 *   固定値にする   : SetMaxHp / SetMoveSpeedMultiplier / SetAttackPower
 *                   / SetEngageRange / SetCrimeCompleteTime
 *   増減させる場合 : 例) SetAttackPower(attackPower + 1) のように現在値+差分を渡す
 *   基準値に戻す   : ResetToBase()
 * ・onXxxChangedは値が変わった時に呼ばれる。HPバー表示や他システムからの購読用
 * ・moveSpeedMultiplierは倍率なので、実際の速度は移動処理側でプレイヤーの基準速度に掛けて使う
 */
// ========================================

public class CS_VillainStats : NetworkBehaviour
{
    [SerializeField] private CSO_VillainStats _baseStats;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _maxHp = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _moveSpeedMultiplier = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _attackPower = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _engageRange = new NetworkVariable<float>();
    private readonly NetworkVariable<float> _crimeCompleteTime = new NetworkVariable<float>();

    public float maxHp => _maxHp.Value;
    public float moveSpeedMultiplier => _moveSpeedMultiplier.Value;
    public float attackPower => _attackPower.Value;
    public float engageRange => _engageRange.Value;
    public float crimeCompleteTime => _crimeCompleteTime.Value;

    public event Action<float> onMaxHpChanged;
    public event Action<float> onMoveSpeedMultiplierChanged;
    public event Action<float> onAttackPowerChanged;
    public event Action<float> onEngageRangeChanged;
    public event Action<float> onCrimeCompleteTimeChanged;

    private void Awake()
    {
        if (_baseStats == null)
        {
            Debug.LogError("CS_VillainStats: Base Stats が未設定です", this);
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
        _moveSpeedMultiplier.OnValueChanged += HandleMoveSpeedMultiplierChanged;
        _attackPower.OnValueChanged += HandleAttackPowerChanged;
        _engageRange.OnValueChanged += HandleEngageRangeChanged;
        _crimeCompleteTime.OnValueChanged += HandleCrimeCompleteTimeChanged;

        if (IsServer)
        {
            ApplyBaseStats();
        }
    }

    public override void OnNetworkDespawn()
    {
        _maxHp.OnValueChanged -= HandleMaxHpChanged;
        _moveSpeedMultiplier.OnValueChanged -= HandleMoveSpeedMultiplierChanged;
        _attackPower.OnValueChanged -= HandleAttackPowerChanged;
        _engageRange.OnValueChanged -= HandleEngageRangeChanged;
        _crimeCompleteTime.OnValueChanged -= HandleCrimeCompleteTimeChanged;
    }

    // HP上限を変更する(サーバーのみ)
    public void SetMaxHp(float value)
    {
        if (IsSpawned && !IsServer) return;

        _maxHp.Value = Mathf.Max(1f, value);
    }

    // 移動速度(倍率)を変更する(サーバーのみ)
    public void SetMoveSpeedMultiplier(float value)
    {
        if (IsSpawned && !IsServer) return;

        _moveSpeedMultiplier.Value = Mathf.Max(0f, value);
    }

    // 攻撃力を変更する(サーバーのみ)
    public void SetAttackPower(float value)
    {
        if (IsSpawned && !IsServer) return;

        _attackPower.Value = Mathf.Max(0f, value);
    }

    // 臨戦態勢範囲を変更する(サーバーのみ)
    public void SetEngageRange(float value)
    {
        if (IsSpawned && !IsServer) return;

        _engageRange.Value = Mathf.Max(0f, value);
    }

    // 犯罪完遂までの時間を変更する(サーバーのみ)
    public void SetCrimeCompleteTime(float value)
    {
        if (IsSpawned && !IsServer) return;

        _crimeCompleteTime.Value = Mathf.Max(0f, value);
    }

    // 全ステータスをBase Statsの値に戻す(サーバーのみ)
    public void ResetToBase()
    {
        if (IsSpawned && !IsServer) return;

        ApplyBaseStats();
    }

    private void ApplyBaseStats()
    {
        if (_baseStats == null) return;

        _maxHp.Value = _baseStats.maxHp;
        _moveSpeedMultiplier.Value = _baseStats.moveSpeedMultiplier;
        _attackPower.Value = _baseStats.attackPower;
        _engageRange.Value = _baseStats.engageRange;
        _crimeCompleteTime.Value = _baseStats.crimeCompleteTime;
    }

    private void HandleMaxHpChanged(float previous, float current) => onMaxHpChanged?.Invoke(current);
    private void HandleMoveSpeedMultiplierChanged(float previous, float current) => onMoveSpeedMultiplierChanged?.Invoke(current);
    private void HandleAttackPowerChanged(float previous, float current) => onAttackPowerChanged?.Invoke(current);
    private void HandleEngageRangeChanged(float previous, float current) => onEngageRangeChanged?.Invoke(current);
    private void HandleCrimeCompleteTimeChanged(float previous, float current) => onCrimeCompleteTimeChanged?.Invoke(current);
}
