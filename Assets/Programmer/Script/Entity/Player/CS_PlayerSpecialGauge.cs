using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 必殺ゲージを管理するクラス
 * ゲージが満タンの時だけ、必殺技(CS_PlayerSpecialAttack)を使用できる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・上限(maxGauge)はCS_PlayerStatsが持つ(maxHpと同じ考え方)。ここでは現在値だけを持つ
 * ・ゲージが溜まる要因(数値はすべてInspectorで調整可能)
 *   1. 時間経過   : 毎秒 _gaugePerSecond ずつ自動で溜まる(既定1)
 *   2. 通常攻撃   : ヒットする度に、各段のGauge Gain分だけ溜まる
 *   3. 悪人の撃退 : 誰が倒したかに関わらず、悪人が1体倒されるたびに全プレイヤーが
 *      _villainDefeatGaugeGain分だけ溜まる(既定10。CS_VillainHealth.onAnyVillainDefeatedを購読)
 *      ※ 攻撃側に「誰が倒したか」を記録する仕組みが無いため、討伐者だけに加算する形にはしていない
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・TryConsumeFull()は満タンの時だけ消費してtrueを返す。満タンでなければ何もせずfalseを返す
 *   (必殺技側は、発動判定と実際の消費を分けて、判定が通る瞬間に改めてこれを呼んでいる)
 * ・onGaugeChangedはHUD用の変更通知(オフラインのテストシーンでも発生する)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerStats))]
public class CS_PlayerSpecialGauge : NetworkBehaviour
{
    [Header("自動回復・撃退報酬")]
    [SerializeField] private float _gaugePerSecond = 1f;            // 時間経過で毎秒溜まる量
    [SerializeField] private float _villainDefeatGaugeGain = 10f;   // 悪人が1体倒されるたびに溜まる量(全プレイヤー共通)

    private CS_PlayerStats _stats;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentGauge = new NetworkVariable<float>();

    public float maxGauge => _stats.maxGauge;
    public float currentGauge => _currentGauge.Value;
    public bool isFull => _currentGauge.Value >= maxGauge;

    public event Action<float, float> onGaugeChanged;   // (current, max)

    private void Awake()
    {
        _stats = GetComponent<CS_PlayerStats>();
        CS_VillainHealth.onAnyVillainDefeated += HandleVillainDefeated;
    }

    public override void OnDestroy()
    {
        CS_VillainHealth.onAnyVillainDefeated -= HandleVillainDefeated;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        _currentGauge.OnValueChanged += HandleGaugeChanged;
        _stats.onMaxGaugeChanged += HandleMaxGaugeChanged;
    }

    public override void OnNetworkDespawn()
    {
        _currentGauge.OnValueChanged -= HandleGaugeChanged;
        _stats.onMaxGaugeChanged -= HandleMaxGaugeChanged;
    }

    // 時間経過による自動回復(サーバー・オフライン以外ではFill内部で無視される)
    private void Update()
    {
        Fill(_gaugePerSecond * Time.deltaTime);
    }

    // 悪人が撃退される度に呼ばれる(誰が倒したかは問わず、全プレイヤーに加算する)
    private void HandleVillainDefeated(CS_VillainHealth villain)
    {
        Fill(_villainDefeatGaugeGain);
    }

    // ゲージを増やす(サーバーのみ)
    public void Fill(float amount)
    {
        if (IsSpawned && !IsServer) return;
        if (amount <= 0f) return;

        float previous = _currentGauge.Value;
        _currentGauge.Value = Mathf.Min(maxGauge, _currentGauge.Value + amount);
        if (_currentGauge.Value != previous)
        {
            NotifyOffline();
        }
    }

    // 満タンの時だけ全消費してtrueを返す。満タンでなければ何もしない(サーバーのみ)
    public bool TryConsumeFull()
    {
        if (IsSpawned && !IsServer) return false;
        if (!isFull) return false;

        _currentGauge.Value = 0f;
        NotifyOffline();
        return true;
    }

    // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接イベントを発生させる
    private void NotifyOffline()
    {
        if (IsSpawned) return;

        onGaugeChanged?.Invoke(_currentGauge.Value, maxGauge);
    }

    private void HandleGaugeChanged(float previous, float current)
    {
        onGaugeChanged?.Invoke(current, maxGauge);
    }

    // 上限が下がった時、現在値が上限を超えないようにする(サーバーのみ)
    private void HandleMaxGaugeChanged(float newMaxGauge)
    {
        if (IsSpawned && !IsServer) return;
        if (_currentGauge.Value <= newMaxGauge) return;

        _currentGauge.Value = newMaxGauge;
    }
}
