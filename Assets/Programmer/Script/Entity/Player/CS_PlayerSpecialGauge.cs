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
 * ・ゲージは通常攻撃(CS_PlayerAttack)がヒットする度に、各段のGauge Gain分だけ溜まる
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・TryConsumeFull()は満タンの時だけ消費してtrueを返す。満タンでなければ何もせずfalseを返す
 *   (必殺技側は、発動判定と実際の消費を分けて、判定が通る瞬間に改めてこれを呼んでいる)
 * ・onGaugeChangedはHUD用の変更通知
 */
// ========================================

[RequireComponent(typeof(CS_PlayerStats))]
public class CS_PlayerSpecialGauge : NetworkBehaviour
{
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

    // ゲージを増やす(サーバーのみ)
    public void Fill(float amount)
    {
        if (IsSpawned && !IsServer) return;
        if (amount <= 0f) return;

        _currentGauge.Value = Mathf.Min(maxGauge, _currentGauge.Value + amount);
    }

    // 満タンの時だけ全消費してtrueを返す。満タンでなければ何もしない(サーバーのみ)
    public bool TryConsumeFull()
    {
        if (IsSpawned && !IsServer) return false;
        if (!isFull) return false;

        _currentGauge.Value = 0f;
        return true;
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
