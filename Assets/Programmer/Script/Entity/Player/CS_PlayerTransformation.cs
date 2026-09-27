using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの変身を管理するクラス
 * 変身が完了している間だけ必殺技(CS_PlayerSpecialAttack)を使用できる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・状態は 通常(Normal) → 変身途中(Transforming) → 変身完了(Transformed) の3段階(CSE_PlayerTransformState)
 *   変身ボタン(キーボード: Q / コントローラー: Rスティック押し込み)で変身を始め、
 *   _transformDuration秒(既定5秒)で変身が完了する。変身完了中にもう一度押すと解除
 * ・変身途中は無敵(CS_PlayerHealth.SetInvincible)。変身途中は解除できない
 * ・変身・解除は移動を止めない(移動しながら変身できる)
 * ・解除してから_cooldown秒(既定5秒)経つまで再変身できない
 * ・タグは変身が完了した時に切り替える(変身途中はまだ通常のタグ)
 *   通常・変身途中: PlayerNoTransformation / 変身完了: PlayerTransformation
 * ・変身完了中は、変身が完了した瞬間から_policeNotifyInterval秒(既定10秒)ごとに、
 *   現在地をCS_PoliceSquad.NotifyIncidentで警察へ知らせる(警備エリア内なら、そのグループが駆け付ける)
 *   変身が完了した瞬間そのものは、警察側(CS_PoliceTransformationWatcher)がタグの変化で検知している
 * ・状態と各時刻はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   流れ: Ownerがボタンを押す → サーバーへ依頼(RPC) → サーバーが条件を確認して確定 → 全員のタグが切り替わる
 *   変身の完了と警察への通知も、サーバーが時間を見て行う
 * ・死亡すると自動で解除される(解除扱いなのでクールタイムも始まる)
 * ・必殺技を行っている間は解除できない(判定の途中で解除されて不発になるのを防ぐため)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerTransformation : NetworkBehaviour
{
    [Header("変身")]
    [SerializeField] private float _transformDuration = 5f;     // 変身にかかる時間(秒)。この間は無敵
    [SerializeField] private float _cooldown = 5f;              // 解除してから再変身できるまでの時間(秒)

    [Header("警察への通知")]
    [SerializeField] private float _policeNotifyInterval = 10f; // 変身完了中に現在地を警察へ知らせる間隔(秒)

    private const string _normalTag = "PlayerNoTransformation";
    private const string _transformedTag = "PlayerTransformation";

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerSpecialAttack _specialAttack;

    private double _nextPoliceNotifyTime;   // 次に警察へ知らせる時刻(サーバー、またはオフラインのみ使う)

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<CSE_PlayerTransformState> _state = new NetworkVariable<CSE_PlayerTransformState>();
    private readonly NetworkVariable<double> _transformEndTime = new NetworkVariable<double>(); // 変身が完了する時刻
    private readonly NetworkVariable<double> _cooldownEndTime = new NetworkVariable<double>();  // 再変身できるようになる時刻

    public CSE_PlayerTransformState state => _state.Value;
    public bool isTransforming => _state.Value == CSE_PlayerTransformState.Transforming;
    public bool isTransformed => _state.Value == CSE_PlayerTransformState.Transformed;
    public float transformRemaining => isTransforming ? Mathf.Max(0f, (float)(_transformEndTime.Value - GetCurrentTime())) : 0f;  // HUD用
    public float cooldown => _cooldown;
    public float cooldownRemaining => Mathf.Max(0f, (float)(_cooldownEndTime.Value - GetCurrentTime()));  // HUD用
    public bool canTransform => _state.Value == CSE_PlayerTransformState.Normal && cooldownRemaining <= 0f;

    public event Action<CSE_PlayerTransformState> onStateChanged;   // 見た目・HUD用

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();

        _health.onDeath += HandleDeath;
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;

        ApplyTag(_state.Value);
    }

    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += HandleStateChanged;

        // 途中参加したクライアントでも、現在の状態のタグに合わせる
        ApplyTag(_state.Value);
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= HandleStateChanged;
    }

    public override void OnDestroy()
    {
        if (_health != null)
        {
            _health.onDeath -= HandleDeath;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        // 変身の完了と警察への通知は、サーバー(またはオフライン)が時間を見て行う
        if (!IsSpawned || IsServer)
        {
            UpdateTransforming();
            UpdatePoliceNotify();
        }

        ReadInput();
    }

    // 変身ボタンを読み取り、変身/解除を依頼する(自分が操作するプレイヤーのみ)
    private void ReadInput()
    {
        if (!_player.canAct) return;
        if (!_player.transformationAction.WasPressedThisFrame()) return;

        // 押しても意味の無い状態はここで弾く(最終的な確認はサーバーで行う)
        if (!CanToggle()) return;

        RequestToggle();
    }

    // 変身/解除を依頼する(確定はサーバーで行う)
    private void RequestToggle()
    {
        // オフライン(テストシーン)では、その場で切り替える
        if (!IsSpawned)
        {
            ExecuteToggle();
            return;
        }

        ToggleRpc();
    }

    // Ownerからサーバーへ、変身/解除を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ToggleRpc()
    {
        ExecuteToggle();
    }

    // 条件を確認して変身開始/解除を行う(サーバー、またはオフラインで実行される)
    private void ExecuteToggle()
    {
        if (_health.isDead) return;
        if (!CanToggle()) return;

        if (isTransformed)
        {
            Untransform();
            return;
        }

        StartTransforming();
    }

    // 今、変身/解除の操作を受け付けられるか
    private bool CanToggle()
    {
        switch (_state.Value)
        {
            case CSE_PlayerTransformState.Normal:
                return canTransform;
            case CSE_PlayerTransformState.Transformed:
                return !IsPerformingSpecial();
            default:
                return false;   // 変身途中は解除できない
        }
    }

    // 変身を始める。変身途中は無敵(サーバー、またはオフラインで実行される)
    private void StartTransforming()
    {
        _transformEndTime.Value = GetCurrentTime() + _transformDuration;
        _health.SetInvincible(this, true);
        SetState(CSE_PlayerTransformState.Transforming);
    }

    // 変身途中なら、時間が来たら変身を完了させる(サーバー、またはオフラインのみ)
    private void UpdateTransforming()
    {
        if (!isTransforming) return;
        if (GetCurrentTime() < _transformEndTime.Value) return;

        _health.SetInvincible(this, false);
        _nextPoliceNotifyTime = GetCurrentTime() + _policeNotifyInterval;
        SetState(CSE_PlayerTransformState.Transformed);
    }

    // 変身完了中は、一定間隔で現在地を警察へ知らせる(サーバー、またはオフラインのみ)
    private void UpdatePoliceNotify()
    {
        if (!isTransformed) return;
        if (GetCurrentTime() < _nextPoliceNotifyTime) return;

        _nextPoliceNotifyTime += _policeNotifyInterval;
        CS_PoliceSquad.NotifyIncident(transform.position);
    }

    // 変身を解除し、クールタイムを開始する(サーバー、またはオフラインで実行される)
    private void Untransform()
    {
        _health.SetInvincible(this, false);
        _cooldownEndTime.Value = GetCurrentTime() + _cooldown;
        SetState(CSE_PlayerTransformState.Normal);
    }

    private void SetState(CSE_PlayerTransformState next)
    {
        CSE_PlayerTransformState previous = _state.Value;
        _state.Value = next;

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接反映する
        if (IsSpawned) return;

        HandleStateChanged(previous, next);
    }

    // 状態が変わったとき、全クライアントでタグを切り替えて通知する
    private void HandleStateChanged(CSE_PlayerTransformState previous, CSE_PlayerTransformState current)
    {
        ApplyTag(current);
        onStateChanged?.Invoke(current);
    }

    private void ApplyTag(CSE_PlayerTransformState current)
    {
        gameObject.tag = current == CSE_PlayerTransformState.Transformed ? _transformedTag : _normalTag;
    }

    // 死亡したら変身を解除する(サーバー、またはオフラインのみ)
    private void HandleDeath()
    {
        if (IsSpawned && !IsServer) return;
        if (_state.Value == CSE_PlayerTransformState.Normal) return;

        Untransform();
    }

    private bool IsPerformingSpecial()
    {
        return _specialAttack != null && _specialAttack.isPerformingSpecial;
    }

    // 各時刻の基準(オンラインはサーバー時刻で揃える)
    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
