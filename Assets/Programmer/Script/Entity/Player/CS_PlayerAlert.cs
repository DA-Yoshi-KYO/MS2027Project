using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーごとの警戒状態(なし / 発見中 / 捜索中)を管理するクラス
 * 手配度に代わる、メタルギア式の状態変化の土台
 * 警察側(CS_PoliceAlertManager)が、警察・巡回ドローンの実際の状態から決めた警戒状態をSetPoliceState()で渡し、UIはonAlertChangedを購読して表示を変える
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は警察仕様書「警戒状態(旧手配度)」、UI仕様書「②警戒状態」。警戒状態はプレイヤーごとに持ち、全員で共有しない
 * ・状態の決め方(タイマーは使わない。警察側の実際の状態にそろえる。2026-11-09に変更)
 *   発見中: そのプレイヤーを実際に見つけて追いかけている警察(巡回ドローンの見ている間も含む)がいる
 *   捜索中: 追いかけている警察はいないが、そのプレイヤーを探している・怪しんで近づいている・駆け付けている警察(巡回ドローンの追跡も含む)がいる
 *   なし  : どちらもいない
 *   どの状態 → なし: 警察陣営に倒された時(倒した相手にCS_PoliceAttack(警察)かCS_SecurityDrone(巡回ドローン)が付いていれば警察陣営とみなす)
 *                    悪人・他のプレイヤーに倒された時は解除しない(仕様「警察に負けリスポーン」)
 *   ※ 以前の「最後に見つかってから0.5秒で捜索中、20秒でなし」のタイマー(_lostDelay・_clearTime)は、やめた
 * ・警察に倒された時は、onDefeatedByPolice(倒されたプレイヤー)も発生させる(サーバー、またはオフラインのみ)
 *   自分を警戒対象にしているもの(警察の追跡・駆け付け・探索、CS_PoliceAlertManagerの派遣、ドローンの追跡など)は、
 *   警察側がこれを購読して解除する(それぞれの状態は警察側が持っているため)
 * ・SetPoliceState()は、警察側が毎フレーム呼ぶ。サーバー(またはオフライン)で呼ぶ。クライアントで呼んでも何もしない
 *   死亡中は、渡された状態に関係なく「なし」にする
 * ・警戒状態はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 * ・onAlertChangedは全クライアントで発生する(UI・警察側用)
 * ・手配度(CS_PlayerWantedLevel)は、警察側が警戒状態へ移った(Issue #271)ため削除した
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 * ・自分のプレイヤー(isLocalPlayer)は、シーンにCS_InGameBgmControllerがあれば登録する(警戒状態でBGMを通常⇔警戒に切り替えるため)
 *   BGMのないテストシーンでは何もしない。NPCや他人のプレイヤーは登録しない
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerAlert : NetworkBehaviour
{
    private CS_PlayerHealth _health;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<CSE_PlayerAlertState> _state = new NetworkVariable<CSE_PlayerAlertState>();

    public CSE_PlayerAlertState alertState => _state.Value;
    public bool isAlerted => _state.Value != CSE_PlayerAlertState.None;     // 発見中か捜索中か
    public bool isDetected => _state.Value == CSE_PlayerAlertState.Detected;

    public event Action<CSE_PlayerAlertState> onAlertChanged;    // (今の警戒状態)全クライアントで発生する

    // 警察陣営に倒された時(倒されたプレイヤー)。サーバー、またはオフラインのみ。警察側が、このプレイヤーへの追跡・警戒を解除するのに使う
    public static event Action<CS_PlayerHealth> onDefeatedByPolice;

    private CS_InGameBgmController _bgmController;     // 自分のプレイヤーとして登録した時のBGM切り替え(未登録ならnull)

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
        _health.onDeath += HandleDeath;
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        RegisterBgmController();
    }

    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += HandleStateChanged;
        RegisterBgmController();
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= HandleStateChanged;
        UnregisterBgmController();
    }

    public override void OnDestroy()
    {
        UnregisterBgmController();
        if (_health != null) _health.onDeath -= HandleDeath;
        base.OnDestroy();
    }

    // 自分のプレイヤーだけ、インゲームBGMの切り替えに登録する(NPCはホストでIsOwnerになるため、isLocalPlayerで判定する)
    private void RegisterBgmController()
    {
        if (_bgmController != null) return;

        CS_Player player = GetComponent<CS_Player>();
        if (player == null || !player.isLocalPlayer) return;

        CS_InGameBgmController controller = FindAnyObjectByType<CS_InGameBgmController>();
        if (controller == null) return;

        controller.RegisterLocalPlayer(this);
        _bgmController = controller;
    }

    private void UnregisterBgmController()
    {
        if (_bgmController == null) return;

        _bgmController.UnregisterLocalPlayer();
        _bgmController = null;
    }

    // 警察側が、警察・巡回ドローンの実際の状態から決めた警戒状態を渡す(毎フレーム呼んでよい。サーバー、またはオフラインのみ)
    public void SetPoliceState(CSE_PlayerAlertState state)
    {
        if (!HasAuthority()) return;

        // 死亡中は、警察が追跡をやめきっていなくても、警戒状態にしない
        SetState(_health.isDead ? CSE_PlayerAlertState.None : state);
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
}
