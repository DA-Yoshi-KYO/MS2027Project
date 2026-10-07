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
 * ・生成時の上書き : SetSpawnOverrides(HP上限, 攻撃力, 犯罪完遂時間)
 *   時間経過による強化用。Base Statsの一部をこの悪人だけ差し替える(0以下の値は差し替えない)
 *   生成直後・Spawnより前に呼ぶ(初期化時にこの値で始まり、現在HPも満タンになる)
 *   ResetToBaseで戻る値も、差し替えた値になる
 *   生成後に差し替える値を変える(時間経過の段階が変わった時) : UpdateSpawnOverrides
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

    // 生成時にBase Statsから差し替える値(0以下なら差し替えない)。サーバー(またはオフライン)だけが持つ
    private float _overrideMaxHp;
    private float _overrideAttackPower;
    private float _overrideCrimeCompleteTime;

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

    // 生成時にBase Statsの一部を差し替える(0以下の値は差し替えない)。生成直後・Spawnより前に呼ぶ
    public void SetSpawnOverrides(float maxHp, float attackPower, float crimeCompleteTime)
    {
        _overrideMaxHp = maxHp;
        _overrideAttackPower = attackPower;
        _overrideCrimeCompleteTime = crimeCompleteTime;
    }

    // 生成後に、差し替える値を変える(サーバー、またはオフライン)。時間経過の段階が変わった時用
    // 0より大きい値だけ、差し替える値と現在値の両方を変える(0以下の値はそのまま)
    // HP上限を変えた時は、呼んだ側でCS_VillainHealth.ApplyMaxHpChangeを呼んで現在HPを合わせる
    public void UpdateSpawnOverrides(float maxHp, float attackPower, float crimeCompleteTime)
    {
        if (IsSpawned && !IsServer) return;

        if (maxHp > 0f)
        {
            _overrideMaxHp = maxHp;
            SetMaxHp(maxHp);
        }
        if (attackPower > 0f)
        {
            _overrideAttackPower = attackPower;
            SetAttackPower(attackPower);
        }
        if (crimeCompleteTime > 0f)
        {
            _overrideCrimeCompleteTime = crimeCompleteTime;
            SetCrimeCompleteTime(crimeCompleteTime);
        }
    }

    private void ApplyBaseStats()
    {
        if (_baseStats == null) return;

        _maxHp.Value = OverrideOrBase(_overrideMaxHp, _baseStats.maxHp);
        _moveSpeedMultiplier.Value = _baseStats.moveSpeedMultiplier;
        _attackPower.Value = OverrideOrBase(_overrideAttackPower, _baseStats.attackPower);
        _engageRange.Value = _baseStats.engageRange;
        _crimeCompleteTime.Value = OverrideOrBase(_overrideCrimeCompleteTime, _baseStats.crimeCompleteTime);
    }

    private static float OverrideOrBase(float overrideValue, float baseValue)
    {
        return overrideValue > 0f ? overrideValue : baseValue;
    }

    private void HandleMaxHpChanged(float previous, float current) => onMaxHpChanged?.Invoke(current);
    private void HandleMoveSpeedMultiplierChanged(float previous, float current) => onMoveSpeedMultiplierChanged?.Invoke(current);
    private void HandleAttackPowerChanged(float previous, float current) => onAttackPowerChanged?.Invoke(current);
    private void HandleEngageRangeChanged(float previous, float current) => onEngageRangeChanged?.Invoke(current);
    private void HandleCrimeCompleteTimeChanged(float previous, float current) => onCrimeCompleteTimeChanged?.Invoke(current);
}
