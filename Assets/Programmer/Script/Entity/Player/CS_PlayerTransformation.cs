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
 * ・変身完了中は、一定間隔で現在地を警察へ知らせる
 *   信号を出すとonPoliceNotified(信号を出した位置)を発生させる。警察側がこれを購読し、
 *   警備エリア内の警察グループと、このプレイヤーの手配度で出現した増援の両方を駆け付けさせる
 *   ※ 警察側の対応が入るまでは、CS_PoliceSquad.NotifyIncidentも呼んでいる(両方あっても同じ場所へ2回指示するだけ)
 *     警察側の対応が入ったら、NotifyIncidentの呼び出しを消す(プレイヤー側から警察のスクリプトを呼ぶ所がなくなる)
 *   間隔は変身を続けるほど短くなる(仕様書「変形のリスク」、データ表「プレイヤーデータ」)
 *     間隔 = 初期信号間隔(_policeNotifyInterval、既定10秒)
 *          - 短縮時間(_policeNotifyShortenAmount、既定2秒) × (変身完了からの経過時間 ÷ 間隔短縮時間(_policeNotifyShortenTime、既定20秒) の切り捨て)
 *     例: 変身完了から0〜20秒は10秒おき → 20〜40秒は8秒おき → 40〜60秒は6秒おき…
 *   最短間隔(_policeNotifyMinInterval)より短くはしない(データ表の値だと100秒で0秒になるため。値はプランナーに確認中の仮)
 *   次の信号の間隔は、信号を出した時点の経過時間で決める。解除して再変身すると、また初期信号間隔から始まる
 *   間隔が短くなった瞬間にonPoliceNotifyIntervalShortenedを発生させる(手配度 CS_PlayerWantedLevel が+1する)
 *   (最短間隔に達した後は、それ以上短くならないので発生しない)
 *   変身が完了した瞬間そのものは、警察側(CS_PoliceTransformationWatcher)がタグの変化で検知している
 * ・状態と各時刻はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   流れ: Ownerがボタンを押す → サーバーへ依頼(RPC) → サーバーが条件を確認して確定 → 全員のタグが切り替わる
 *   変身の完了と警察への通知も、サーバーが時間を見て行う
 * ・死亡すると自動で解除される(解除扱いなのでクールタイムも始まる)
 * ・通常攻撃・必殺技を行っている間は解除できない(判定の途中で解除されて不発になるのを防ぐため)
 *   通常攻撃(CS_PlayerAttack)は変身完了中しか発動できないため、実質「攻撃できるのは変身中だけ」になる
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
    [SerializeField] private float _policeNotifyInterval = 10f;         // 初期信号間隔: 変身完了直後に現在地を警察へ知らせる間隔(秒)
    [SerializeField] private float _policeNotifyShortenTime = 20f;      // 間隔短縮時間: 変身を続けてこの秒数が経つごとに間隔を短くする(秒)
    [SerializeField] private float _policeNotifyShortenAmount = 2f;     // 短縮時間: 1回で短くする量(秒)
    [SerializeField] private float _policeNotifyMinInterval = 2f;       // 最短の間隔(秒)。プランナーに確認中の仮の値

    private const string _normalTag = "PlayerNoTransformation";
    private const string _transformedTag = "PlayerTransformation";

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _specialAttack;

    private double _nextPoliceNotifyTime;   // 次に警察へ知らせる時刻(サーバー、またはオフラインのみ使う)
    private double _transformedTime;        // 変身が完了した時刻(サーバー、またはオフラインのみ使う)
    private float _currentPoliceNotifyInterval;     // 今の信号の間隔(サーバー、またはオフラインのみ使う)

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
    public event Action onPoliceNotifyIntervalShortened;  // 変身を続けて信号の間隔が短くなった時(サーバー、またはオフラインのみ)。手配度用
    public event Action<Vector3> onPoliceNotified;        // 警察へ信号を出した時(信号を出した位置)。サーバー、またはオフラインのみ。警察側が購読する

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _attack = GetComponent<CS_PlayerAttack>();
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
            UpdatePoliceNotifyInterval();
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
                return !IsAttacking() && !IsPerformingSpecial();
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
        _transformedTime = GetCurrentTime();
        _currentPoliceNotifyInterval = GetPoliceNotifyInterval(0f);
        _nextPoliceNotifyTime = _transformedTime + GetPoliceNotifyInterval(0f);
        SetState(CSE_PlayerTransformState.Transformed);
    }

    // 変身を続けて信号の間隔が短くなったら知らせる(サーバー、またはオフラインのみ)
    private void UpdatePoliceNotifyInterval()
    {
        if (!isTransformed) return;

        float interval = GetPoliceNotifyInterval((float)(GetCurrentTime() - _transformedTime));
        if (interval >= _currentPoliceNotifyInterval) return;

        _currentPoliceNotifyInterval = interval;
        onPoliceNotifyIntervalShortened?.Invoke();
    }

    // 変身完了中は、変身を続けるほど短くなる間隔で、現在地を警察へ知らせる(サーバー、またはオフラインのみ)
    private void UpdatePoliceNotify()
    {
        if (!isTransformed) return;
        if (GetCurrentTime() < _nextPoliceNotifyTime) return;

        // 次の間隔は、この信号を出した時点の経過時間で決める
        float elapsed = (float)(_nextPoliceNotifyTime - _transformedTime);
        _nextPoliceNotifyTime += GetPoliceNotifyInterval(elapsed);
        // TODO: 警察側がonPoliceNotifiedを購読するようになったら、NotifyIncidentの呼び出しは消す
        CS_PoliceSquad.NotifyIncident(transform.position);
        onPoliceNotified?.Invoke(transform.position);
    }

    // 変身完了からの経過時間に応じた信号の間隔(間隔短縮時間ごとに短縮時間だけ短くなる。最短間隔より短くしない)
    private float GetPoliceNotifyInterval(float elapsed)
    {
        int shortenCount = _policeNotifyShortenTime > 0f ? Mathf.FloorToInt(elapsed / _policeNotifyShortenTime) : 0;
        float interval = _policeNotifyInterval - _policeNotifyShortenAmount * shortenCount;
        return Mathf.Max(_policeNotifyMinInterval, interval);
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

    private bool IsAttacking()
    {
        return _attack != null && _attack.isAttacking;
    }

    private bool IsPerformingSpecial()
    {
        return _specialAttack != null && _specialAttack.isPerformingSpecial;
    }

    // Inspectorで調整した値が不正にならないようにする(最短間隔が0以下だと、毎フレーム信号を出してしまうため)
    private void OnValidate()
    {
        _policeNotifyMinInterval = Mathf.Max(0.1f, _policeNotifyMinInterval);
        _policeNotifyInterval = Mathf.Max(_policeNotifyMinInterval, _policeNotifyInterval);
        _policeNotifyShortenTime = Mathf.Max(0f, _policeNotifyShortenTime);
        _policeNotifyShortenAmount = Mathf.Max(0f, _policeNotifyShortenAmount);
    }

    // 各時刻の基準(オンラインはサーバー時刻で揃える)
    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
