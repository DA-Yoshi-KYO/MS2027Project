using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーごとの警戒状態(なし / 発見中 / 捜索中)を管理するクラス
 * 手配度に代わる、メタルギア式の状態変化の土台
 * 警察陣営(警察・防犯装置)はNotifyDetected()で見つけたことを知らせ、UIはonAlertChangedを購読して表示を変える
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は警察仕様書「警戒状態(旧手配度)」、UI仕様書「②警戒状態」。警戒状態はプレイヤーごとに持ち、全員で共有しない
 * ・状態の変わり方
 *   なし / 捜索中 → 発見中: 警察陣営がNotifyDetected()を呼んだ時
 *   発見中 → 捜索中: NotifyDetected()が_lostDelay秒(既定0.5秒)呼ばれなくなった時(見失われた)
 *   捜索中 → なし: 最後に見つかってから_clearTime秒(既定20秒)、新たに見つからなかった時
 *   どの状態 → なし: 警察陣営に倒された時(倒した相手にCS_PoliceAttack(警察)かCS_SecurityDrone(巡回ドローン)が付いていれば警察陣営とみなす)
 *                    悪人・他のプレイヤーに倒された時は解除しない(仕様「警察に負けリスポーン」)
 * ・警察陣営に倒された時は、onDefeatedByPolice(倒されたプレイヤー)も発生させる(サーバー、またはオフラインのみ)
 *   自分を警戒対象にしているもの(警察の追跡・駆け付け・探索、CS_PoliceAlertManagerの派遣、ドローンの追跡など)は、
 *   警察側がこれを購読して解除する(それぞれの状態は警察側が持っているため)
 * ・NotifyDetected()は、見えている間は呼び続けてもらう(毎フレームでも、一定間隔でもよい。間隔は_lostDelayより短くする)
 *   死亡中は見つかっても無視する
 * ・警戒状態はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   NotifyDetected()はサーバー(またはオフライン)で呼ぶ。クライアントで呼んでも何もしない
 * ・onAlertChangedは全クライアントで発生する(UI・警察側用)
 * ・手配度(CS_PlayerWantedLevel)は、警察側が警戒状態へ移った(Issue #271)ため削除した
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerAlert : NetworkBehaviour
{
    [Header("警戒状態")]
    [SerializeField] private float _lostDelay = 0.5f;   // 見つかったと知らされなくなってから、捜索中になるまでの秒数
    [SerializeField] private float _clearTime = 20f;    // 最後に見つかってから、警戒状態が解除されるまでの秒数

    private CS_PlayerHealth _health;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<CSE_PlayerAlertState> _state = new NetworkVariable<CSE_PlayerAlertState>();

    private double _lastDetectedTime;   // 最後に見つかった時刻(サーバー、またはオフラインのみ使う)

    public CSE_PlayerAlertState alertState => _state.Value;
    public bool isAlerted => _state.Value != CSE_PlayerAlertState.None;     // 発見中か捜索中か
    public bool isDetected => _state.Value == CSE_PlayerAlertState.Detected;
    public float clearTime => _clearTime;

    public event Action<CSE_PlayerAlertState> onAlertChanged;    // (今の警戒状態)全クライアントで発生する

    // 警察陣営に倒された時(倒されたプレイヤー)。サーバー、またはオフラインのみ。警察側が、このプレイヤーへの追跡・警戒を解除するのに使う
    public static event Action<CS_PlayerHealth> onDefeatedByPolice;

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
        _health.onDeath += HandleDeath;
    }

    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += HandleStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= HandleStateChanged;
    }

    public override void OnDestroy()
    {
        if (_health != null) _health.onDeath -= HandleDeath;
        base.OnDestroy();
    }

    // 警察陣営が見つけた時に呼ぶ(見えている間は呼び続ける。サーバー、またはオフラインのみ)
    public void NotifyDetected()
    {
        if (!HasAuthority()) return;
        if (_health.isDead) return;

        _lastDetectedTime = Time.timeAsDouble;
        SetState(CSE_PlayerAlertState.Detected);
    }

    private void Update()
    {
        if (!HasAuthority()) return;

        UpdateState(Time.timeAsDouble - _lastDetectedTime);
    }

    // 最後に見つかってからの時間で、発見中 → 捜索中 → なし と進める
    private void UpdateState(double elapsed)
    {
        switch (_state.Value)
        {
            case CSE_PlayerAlertState.Detected:
                if (elapsed >= _lostDelay) SetState(CSE_PlayerAlertState.Searching);
                break;

            case CSE_PlayerAlertState.Searching:
                if (elapsed >= _clearTime) SetState(CSE_PlayerAlertState.None);
                break;
        }
    }

    // 警察に倒されたら警戒状態を解除し、自分を警戒対象にしているもの(警察側)へも解除を知らせる
    private void HandleDeath()
    {
        if (!HasAuthority()) return;
        if (!IsPolice(_health.lastAttacker)) return;

        SetState(CSE_PlayerAlertState.None);
        onDefeatedByPolice?.Invoke(_health);
    }

    // 倒した相手が警察陣営か
    // 警察の攻撃はCS_PoliceAttackが、巡回ドローンの即死攻撃はCS_SecurityDroneが付いたオブジェクトを攻撃者として渡してくる
    private static bool IsPolice(GameObject attacker)
    {
        if (attacker == null) return false;

        return attacker.GetComponentInParent<CS_PoliceAttack>() != null
            || attacker.GetComponentInParent<CS_SecurityDrone>() != null;
    }

    private void SetState(CSE_PlayerAlertState next)
    {
        CSE_PlayerAlertState previous = _state.Value;
        if (next == previous) return;

        _state.Value = next;

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接通知する
        if (IsSpawned) return;

        HandleStateChanged(previous, next);
    }

    private void HandleStateChanged(CSE_PlayerAlertState previous, CSE_PlayerAlertState current)
    {
        onAlertChanged?.Invoke(current);
    }

    // 警戒状態を変えてよいか(サーバー、またはオフラインのみ)
    private bool HasAuthority()
    {
        return !IsSpawned || IsServer;
    }

    private void OnValidate()
    {
        _lostDelay = Mathf.Max(0.05f, _lostDelay);
        _clearTime = Mathf.Max(_lostDelay, _clearTime);
    }
}
