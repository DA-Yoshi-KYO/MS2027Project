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
 * ・ゲージは通常攻撃(CS_PlayerAttack)がヒットする度に、各段のGauge Gain分だけ溜まる
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・TryConsumeFull()は満タンの時だけ消費してtrueを返す。満タンでなければ何もせずfalseを返す
 *   (必殺技側は、発動判定と実際の消費を分けて、判定が通る瞬間に改めてこれを呼んでいる)
 * ・onGaugeChangedはHUD用の変更通知
 */
// ========================================

public class CS_PlayerSpecialGauge : NetworkBehaviour
{
    [SerializeField] private float _maxGauge = 100f;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentGauge = new NetworkVariable<float>();

    public float maxGauge => _maxGauge;
    public float currentGauge => _currentGauge.Value;
    public bool isFull => _currentGauge.Value >= _maxGauge;

    public event Action<float, float> onGaugeChanged;   // (current, max)

    public override void OnNetworkSpawn()
    {
        _currentGauge.OnValueChanged += HandleGaugeChanged;
    }

    public override void OnNetworkDespawn()
    {
        _currentGauge.OnValueChanged -= HandleGaugeChanged;
    }

    // ゲージを増やす(サーバーのみ)
    public void Fill(float amount)
    {
        if (IsSpawned && !IsServer) return;
        if (amount <= 0f) return;

        _currentGauge.Value = Mathf.Min(_maxGauge, _currentGauge.Value + amount);
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
        onGaugeChanged?.Invoke(current, _maxGauge);
    }
}
