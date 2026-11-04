using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの暗躍ボーナス(警察に見つからずに悪人を連続で倒すほど付くボーナス)を管理するクラス
 * スコアの倍率・ステータス・スキャンの範囲に反映する
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「暗躍ボーナス」、データ表「プレイヤーの暗躍ボーナスで変化するデータ」(Issue #295)
 * ・連続撃破数(streak)が_bonusStartCount人目(既定5)からボーナスが付き、_bonusMaxCount人目(既定10)まで上がる
 *   ボーナスの段階(bonusLevel) = 連続撃破数 - _bonusStartCount + 1(0〜上限)。例: 4人目まで0、5人目で1、10人目以降は6
 *   スコアの倍率     : 1 + _scoreStep(既定0.2) × 段階    (4人目まで×1、5人目×1.2、6人目×1.4 … 10人目×2.2)
 *   ステータスの倍率 : 1 + _statusStep(既定0.1) × 段階   (データ表「1.1倍ずつ」を、スコアと同じく足していく形で解釈した)
 *                      移動速度・ジャンプ力・攻撃力・コンボのモーション速度に掛かる(CS_PlayerStats.SetStealthBonusRate)
 *   スキャンの範囲   : + _scanRadiusStep(既定5m) × 段階  (CS_PlayerScan.SetBonusRadius)
 * ・次のどれかで連続撃破数を0に戻す(次の撃破が1人目になる)
 *   HPが0になる / 警察陣営に見つかる(CS_PlayerAlertが発見中になる、または警察がCS_PlayerScoring.ReportFoundByPoliceで知らせる)
 * ・連続撃破数はNetworkVariableで持つ(書き込みはサーバーのみ)。倒した判定はサーバーで行われるため
 *   スキャンの範囲は自分の画面のものなので、各クライアントが同期された値から手元で反映する
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerStats))]
public class CS_PlayerStealthBonus : NetworkBehaviour
{
    [Header("ボーナスが付く人数")]
    [SerializeField][Min(1)] private int _bonusStartCount = 5;     // この人数目からボーナスが付く
    [SerializeField][Min(1)] private int _bonusMaxCount = 10;      // この人数目でボーナスが最大になる

    [Header("1段階ごとに上がる量")]
    [SerializeField][Min(0f)] private float _scoreStep = 0.2f;        // スコアの倍率
    [SerializeField][Min(0f)] private float _statusStep = 0.1f;       // ステータスの倍率
    [SerializeField][Min(0f)] private float _scanRadiusStep = 5f;     // スキャンの範囲(m)

    private CS_PlayerHealth _health;
    private CS_PlayerStats _stats;
    private CS_PlayerAlert _alert;      // 付いていなければ、警察のReportFoundByPoliceだけで途切れる
    private CS_PlayerScan _scan;        // 付いていなければ、スキャンの範囲には反映しない

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<int> _streak = new NetworkVariable<int>();

    public int streak => _streak.Value;     // 今の連続撃破数
    public int bonusLevel => Mathf.Clamp(_streak.Value - _bonusStartCount + 1, 0, maxBonusLevel);    // ボーナスの段階(0で無し)
    public int maxBonusLevel => Mathf.Max(0, _bonusMaxCount - _bonusStartCount + 1);
    public float scoreMultiplier => 1f + _scoreStep * bonusLevel;
    public float statusMultiplier => 1f + _statusStep * bonusLevel;
    public float scanBonusRadius => _scanRadiusStep * bonusLevel;

    public event Action<int> onStreakChanged;   // (今の連続撃破数)全クライアントで発生する

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
        _stats = GetComponent<CS_PlayerStats>();
        _alert = GetComponent<CS_PlayerAlert>();
        _scan = GetComponent<CS_PlayerScan>();

        _health.onDeath += HandleDeath;
        if (_alert != null) _alert.onAlertChanged += HandleAlertChanged;
    }

    public override void OnNetworkSpawn()
    {
        _streak.OnValueChanged += HandleStreakChanged;
        ApplyScanRadius();
    }

    public override void OnNetworkDespawn()
    {
        _streak.OnValueChanged -= HandleStreakChanged;
    }

    public override void OnDestroy()
    {
        if (_health != null) _health.onDeath -= HandleDeath;
        if (_alert != null) _alert.onAlertChanged -= HandleAlertChanged;
        base.OnDestroy();
    }

    // 悪人を1人倒した時に呼ぶ(サーバー、またはオフラインのみ。CS_PlayerScoringが呼ぶ)
    public void AddDefeat()
    {
        if (!HasAuthority()) return;

        SetStreak(_streak.Value + 1);
    }

    // 連続撃破を途切れさせる(サーバー、またはオフラインのみ)
    public void ResetStreak()
    {
        if (!HasAuthority()) return;

        SetStreak(0);
    }

    // HPが0になったら途切れる
    private void HandleDeath()
    {
        ResetStreak();
    }

    // 警察陣営に見つかったら(発見中になったら)途切れる
    private void HandleAlertChanged(CSE_PlayerAlertState state)
    {
        if (state != CSE_PlayerAlertState.Detected) return;

        ResetStreak();
    }

    private void SetStreak(int value)
    {
        int previous = _streak.Value;
        if (value == previous) return;

        _streak.Value = value;
        _stats.SetStealthBonusRate(statusMultiplier);

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接反映する
        if (IsSpawned) return;

        HandleStreakChanged(previous, value);
    }

    // 全クライアントで、スキャンの範囲に反映して知らせる
    private void HandleStreakChanged(int previous, int current)
    {
        ApplyScanRadius();
        onStreakChanged?.Invoke(current);
    }

    private void ApplyScanRadius()
    {
        if (_scan == null) return;

        _scan.SetBonusRadius(scanBonusRadius);
    }

    // 連続撃破数を変えてよいか(サーバー、またはオフラインのみ)
    private bool HasAuthority()
    {
        return !IsSpawned || IsServer;
    }

    private void OnValidate()
    {
        _bonusMaxCount = Mathf.Max(_bonusStartCount, _bonusMaxCount);
    }
}
