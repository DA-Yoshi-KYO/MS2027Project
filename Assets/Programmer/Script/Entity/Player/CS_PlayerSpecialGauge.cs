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
 *   2. 悪人の撃退 : 自分が悪人を1体倒すたびに _villainDefeatGaugeGain だけ溜まる(既定10)
 *      誰が倒したかはCS_PlayerScoringが判定している(onVillainDefeatedを購読)
 *      ※ スコアと同じく、通常攻撃・必殺技(CS_PlayerScoring.DealDamage経由)で倒した分だけが対象。アイテムで倒した分は溜まらない
 *   ※ 仕様(必殺技ゲージは時間経過と悪人を倒すことで溜まる)に合わせ、通常攻撃のヒットでは溜めない
 *     (CSO_AttackDataのGauge Gainは、プレイヤーの攻撃データでは0にしている)
 * ・値はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   オフライン(スポーン前・ソロのテスト)ではNetworkVariableに書かず、ふつうの変数(_offlineGauge)で持つ
 *   (スポーン前のNetworkVariableに書くと、そのたびにNetcodeが警告ログをスタックトレース付きで出し、毎フレーム溜まるゲージでは
 *    大量のメモリ確保(GC)になってカクつきの原因になっていたため)
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
    [SerializeField] private float _villainDefeatGaugeGain = 10f;   // 自分が悪人を1体倒すたびに溜まる量

    private CS_PlayerStats _stats;
    private CS_PlayerScoring _scoring;   // 誰が倒したかの判定元。付いていなければ撃退では溜まらない

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<float> _currentGauge = new NetworkVariable<float>();
    private float _offlineGauge;   // オフライン(スポーン前)の現在値

    public float maxGauge => _stats.maxGauge;
    public float currentGauge => IsSpawned ? _currentGauge.Value : _offlineGauge;
    public bool isFull => currentGauge >= maxGauge;

    public event Action<float, float> onGaugeChanged;   // (current, max)

    private void Awake()
    {
        _stats = GetComponent<CS_PlayerStats>();
        _scoring = GetComponent<CS_PlayerScoring>();
        if (_scoring != null) _scoring.onVillainDefeated += HandleVillainDefeated;
    }

    public override void OnDestroy()
    {
        if (_scoring != null) _scoring.onVillainDefeated -= HandleVillainDefeated;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && _offlineGauge > 0f) _currentGauge.Value = _offlineGauge;   // スポーン前に溜まっていた分を引き継ぐ
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

    // 自分が悪人を倒す度に呼ばれる(サーバー、またはオフラインのみ)
    private void HandleVillainDefeated(CS_VillainHealth villain)
    {
        Fill(_villainDefeatGaugeGain);
    }

    // ゲージを増やす(サーバーのみ)
    public void Fill(float amount)
    {
        if (!this.IsServerOrOffline()) return;
        if (amount <= 0f) return;

        float previous = currentGauge;
        float next = Mathf.Min(maxGauge, previous + amount);
        if (next != previous) SetGauge(next);
    }

    // 満タンの時だけ全消費してtrueを返す。満タンでなければ何もしない(サーバーのみ)
    public bool TryConsumeFull()
    {
        if (!this.IsServerOrOffline()) return false;
        if (!isFull) return false;

        SetGauge(0f);
        return true;
    }

    // 現在値を変える。スポーン中はNetworkVariableへ書く(変更通知はOnValueChangedから)。オフラインはふつうの変数へ書き、ここで直接通知する
    private void SetGauge(float value)
    {
        if (IsSpawned)
        {
            _currentGauge.Value = value;
            return;
        }

        _offlineGauge = value;
        onGaugeChanged?.Invoke(_offlineGauge, maxGauge);
    }

    private void HandleGaugeChanged(float previous, float current)
    {
        onGaugeChanged?.Invoke(current, maxGauge);
    }

    // 上限が下がった時、現在値が上限を超えないようにする(サーバーのみ)
    private void HandleMaxGaugeChanged(float newMaxGauge)
    {
        if (!this.IsServerOrOffline()) return;
        if (currentGauge <= newMaxGauge) return;

        SetGauge(newMaxGauge);
    }
}
