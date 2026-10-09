using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/*
 * 悪人のプレイヤーへの反撃を行うクラス
 * プレイヤーが臨戦態勢範囲に入る、またはプレイヤーに攻撃されると臨戦態勢になり、
 * 一番近いプレイヤーを追いかけて攻撃する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・状態の流れ
 *   犯罪中(Idle) → [プレイヤーが臨戦態勢範囲に入る / 攻撃される] → 追跡(Chase) ⇔ 攻撃(Attack)
 *   追跡中に以下のどれかになったら → 帰還(Return) → 犯罪中
 *     ・ターゲットが倒れた・いなくなった
 *     ・ターゲットが路地裏の外に出てから leaveAlleyGiveUpTime 秒たった
 *     ・スポーン位置から leashRange 以上離れた(路地裏判定の取りこぼし対策)
 *   帰還中は、プレイヤーが臨戦態勢範囲に入っても追跡しない(スポーン位置に着いて犯罪中に戻ってから反応する)
 *   ただし帰還中に攻撃された場合は、反撃のため再び追跡する
 * ・グループの攻撃枠(CS_VillainGroup)
 *   追跡中は毎回攻撃枠を取りに行き、取れたら攻撃する
 *   取れなかったら、ターゲットから watchDistance 離れた位置を保って様子を見る(攻撃はしない)
 *   攻撃枠は帰還する時・無効になる時(撃退・逃走)に空ける
 *   スポナーを通さずシーンに直接置いた悪人はグループに属さないため、常に攻撃する
 * ・路地裏かどうかは、ターゲットの足元のNavMeshのAreaが alleyAreaName(既定: Alley)かで判定する
 *   ・プレイヤー側には何も必要ない。NavMeshのベイクと、Navigationの Areas に同名のAreaを追加しておくこと
 *   ・Areaが無い場合は警告を出し、路地裏判定を行わない(距離の判定だけになる)
 *   ・ジャンプ中などで足元にNavMeshが見つからない時は、直前の判定結果を使う
 *   ・出入口で行ったり来たりされても追跡と帰還が切り替わり続けないよう、外に出てから一定時間待つ
 *   ・ランダムイベントで生成された悪人は、イベントの範囲(SetEventArea)の中も路地裏として扱う
 *   ・レイドのボス(SetEventAreaのconfineがtrue)は範囲の中に留まる
 *     範囲の中の標的に反応し、範囲の外の標的は見つけない。追いかける時も範囲の外へは出ない
 * ・isEngagedがtrueの間は犯罪の手を止める(犯罪の進行側から参照する想定)
 * ・攻撃の流れ
 *   ターゲットまで attackStartDistance 以内に近づくと攻撃を始める
 *   → 攻撃を始めた時にターゲットがいた位置へ向き、chargeTime 秒溜める(この間は当たらない)
 *   → hitActiveTime 秒間、正面に攻撃判定を出す(その間に入ったプレイヤーに1回ずつ当たる)
 *   → attackInterval 秒待ってから、次の攻撃ができる
 * ・攻撃の段階(attackPhase)はNetworkVariableで全クライアントに同期し、CS_VillainAttackVisualが見た目に使う
 * ・攻撃範囲とダメージはCSO_AttackData(Attack Data)で決める(Attack DataのhitDelay・durationは使わない)
 *   判定 = 正面 hitRange 先、半径 hitRadius の球
 *   実際のダメージ = CSO_AttackData.CalculateDamage() × CS_VillainStats.attackPower
 *   → Attack DataのDamageを1にすると、攻撃力がそのままダメージになる
 * ・ダメージを与えるのはプレイヤー(CS_PlayerHealth)のみ。悪人同士では当たらない
 * ・移動速度 = プレイヤーの通常移動速度(Player Base Stats) × CS_VillainStats.moveSpeedMultiplier
 * ・移動(経路探索)はCS_VillainMove(NavMeshAgent)に任せる。このクラスは目的地を決めるだけ
 * ・どのプレイヤーに攻撃されたかは分からないため、攻撃されたら近くのプレイヤーを狙う
 * ・陽動ホログラム(CS_Hologram)
 *   臨戦態勢範囲にホログラムがあると気付き、プレイヤーより優先して標的にする(追いかけて攻撃するが、ホログラムは消えない)
 *   気付くのは犯罪中と、プレイヤーを追跡中(攻撃中は攻撃が終わってから)。帰還中は気付かない
 *   攻撃されて反撃する時は、ホログラムではなくプレイヤーを狙う
 *   ホログラムが消えたら、近くに別の標的がいればそちらを狙い、いなければ帰還する(プレイヤーが倒れた時も同じ)
 * ・煙幕(CS_SmokeScreen)の中にいるプレイヤー・煙幕越しのプレイヤーは見つけない(ホログラムも同じ)
 *   臨戦態勢範囲の確認と、攻撃された時の反撃相手探しの両方に効く(追跡中のターゲットは見失わない)
 * ・ノックバック(CS_VillainKnockback)中は、追跡・攻撃・移動などの行動を全て止める
 *   攻撃中にノックバックした場合(スーパーアーマーをオフにした時のみ)は攻撃を中断し、attackInterval 秒は次の攻撃をしない
 * ・処理はサーバー(オフライン時はその場)でのみ行う。位置はNetworkTransformで同期する
 */
// ========================================

[RequireComponent(typeof(CS_VillainMove))]
[RequireComponent(typeof(CS_VillainStats))]
[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainCombat : NetworkBehaviour
{
    private enum State
    {
        Idle,       // 犯罪中(臨戦態勢ではない)
        Chase,      // ターゲットを追いかけている
        Attack,     // 攻撃中(溜め・攻撃判定)
        Return,     // スポーン位置へ戻っている
    }

    private const float _scanInterval = 0.2f;     // 臨戦態勢範囲を確認する間隔(秒)
    private const float _arriveDistance = 0.3f;   // スポーン位置に着いたとみなす距離
    private const int _hitBufferSize = 16;        // 一度に判定できるコライダーの上限
    private const float _areaSampleRadius = 1f;   // ターゲットの足元のNavMeshを探す半径(m)
    private const float _watchTolerance = 0.5f;   // 様子見中、watchDistanceからこれ以上ずれたら位置を直す(m)
    private const float _eyeHeight = 0.6f;        // 体の中心(transform.position)から目までの高さ(m)。煙幕の視線判定に使う

    private static bool _hasWarnedNoAlleyArea;    // 路地裏Areaが無い警告を出したか(全悪人で共有)

    [Header("参照")]
    [SerializeField]
    [Tooltip("移動速度の基準にするプレイヤーのステータス(DB_PlayerStats)")]
    private CSO_PlayerStats _playerBaseStats;

    [Header("攻撃")]
    [SerializeField]
    [Tooltip("攻撃範囲とダメージのデータ。Damageを1にすると攻撃力がそのままダメージになる")]
    private CSO_AttackData _attackData;

    [SerializeField, Min(0f)]
    [Tooltip("ターゲットにこの距離(m)まで近づいたら攻撃を始める")]
    private float _attackStartDistance = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃を始めてから攻撃判定が出るまでの溜め時間(秒)")]
    private float _chargeTime = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃判定が出ている時間(秒)")]
    private float _hitActiveTime = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃が終わってから次の攻撃までの間隔(秒)")]
    private float _attackInterval = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃枠が空いていない時に、ターゲットとの間に保つ距離(m)")]
    private float _watchDistance = 3f;

    [SerializeField]
    [Tooltip("プレイヤーを探す・攻撃するレイヤー")]
    private LayerMask _targetLayers = ~0;

    [Header("索敵")]
    [SerializeField, Min(0f)]
    [Tooltip("攻撃された時に、反撃相手のプレイヤーを探す範囲(m)")]
    private float _counterSearchRange = 15f;

    [SerializeField, Min(0f)]
    [Tooltip("スポーン位置からこれ以上離れたら追跡をやめて戻る距離(m)")]
    private float _leashRange = 15f;

    [Header("路地裏")]
    [SerializeField]
    [Tooltip("路地裏として扱うNavMeshのArea名")]
    private string _alleyAreaName = "Alley";

    [SerializeField, Min(0f)]
    [Tooltip("ターゲットが路地裏の外に出てから、追跡をやめるまでの時間(秒)")]
    private float _leaveAlleyGiveUpTime = 2f;

    private CS_VillainMove _move;
    private CS_VillainStats _stats;
    private CS_VillainHealth _health;
    private CS_VillainKnockback _knockback;   // 付いていなければノックバックしない
    private CS_VillainGroup _group;       // 所属するグループ。スポナーを通さず置いた悪人はnull
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly HashSet<IDamageable> _damagedTargets = new HashSet<IDamageable>();   // 今回の攻撃で既にダメージを与えた相手

    // 攻撃の段階。書き込みはサーバーのみ(NetworkVariableのデフォルト)。見た目の切り替えに全クライアントで使う
    private readonly NetworkVariable<CSE_VillainAttackPhase> _attackPhase = new NetworkVariable<CSE_VillainAttackPhase>();

    private State _state = State.Idle;
    private Transform _target;               // 標的(プレイヤー、または陽動ホログラム)
    private CS_PlayerHealth _targetPlayer;   // 標的がプレイヤーの時のHP(倒れたかの判定に使う)。ホログラムの時はnull
    private Vector3 _homePosition;        // スポーンした位置(犯罪を行う場所)
    private Quaternion _homeRotation;
    private float _scanTimer;
    private float _attackElapsed;         // 攻撃開始からの経過時間
    private float _attackCooldown;        // 次の攻撃までの残り時間
    private Vector3 _attackAimPosition;   // 攻撃を始めた時にターゲットがいた位置(溜め中はここへ向く)
    private int _alleyAreaMask;           // 路地裏AreaのNavMeshエリアマスク。0なら路地裏判定を行わない
    private bool _isTargetInAlley;        // 直前のターゲットの路地裏判定(足元のNavMeshが見つからない時に使う)
    private float _outsideAlleyTime;      // ターゲットが路地裏の外に出てからの時間
    private bool _hasEventArea;           // ランダムイベントの範囲を路地裏として扱うか
    private Vector3 _eventAreaCenter;     // ランダムイベントの範囲の中心
    private float _eventAreaRadius;       // ランダムイベントの範囲の半径(水平方向)
    private bool _isConfinedToEventArea;  // ランダムイベントの範囲の中に留まるか(レイドのボス)

    public bool isEngaged => _state == State.Chase || _state == State.Attack;   // 臨戦態勢中か
    public bool isCommittingCrime => enabled && _state == State.Idle;             // スポーン位置で犯罪を進めているか
    public bool isAttacking => _state == State.Attack;                           // 攻撃中か(溜め・攻撃判定)
    public CSE_VillainAttackPhase attackPhase => _attackPhase.Value;              // 攻撃の段階(全クライアントで参照可)
    public float chargeTime => _chargeTime;
    public Vector3 hitCenter => transform.position + transform.forward * _attackData.hitRange;   // 攻撃判定(球)の中心
    public CSO_AttackData attackData => _attackData;
    public float counterSearchRange => _counterSearchRange;
    public float leashRange => _leashRange;
    public Vector3 homePosition => _homePosition;

    // このマシンが悪人を動かす権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private float moveSpeed => _playerBaseStats.moveSpeed * _stats.moveSpeedMultiplier;

    // 臨戦態勢になる範囲。範囲の中に留まる悪人(レイドのボス)は、イベントの範囲全体を見る
    // (範囲の外の標的はFindNearestPlayer / FindNearestHologramで外す)
    private float engageRange => _isConfinedToEventArea
        ? Vector3.Distance(transform.position, _eventAreaCenter) + _eventAreaRadius
        : _stats.engageRange;
    private bool isTargetHologram => _target != null && _targetPlayer == null;   // 陽動ホログラムを狙っているか

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
        _stats = GetComponent<CS_VillainStats>();
        _health = GetComponent<CS_VillainHealth>();
        _knockback = GetComponent<CS_VillainKnockback>();

        // スポナーはInstantiate時に位置を決めるので、Awakeの時点でスポーン位置になっている
        _homePosition = transform.position;
        _homeRotation = transform.rotation;

        // RequireComponentは後から付けたプレハブには効かないので、CS_VillainMoveの有無もここで確認する
        if (_move == null)
        {
            Debug.LogError("CS_VillainCombat: CS_VillainMove が付いていません", this);
            enabled = false;
            return;
        }

        SetUpAlleyAreaMask();

        if (_playerBaseStats != null && _attackData != null) return;

        Debug.LogError("CS_VillainCombat: Player Base Stats または Attack Data が未設定です", this);
        enabled = false;
    }

    private void OnEnable()
    {
        _health.onDamaged += HandleDamaged;
        if (_knockback != null) _knockback.onKnockbackStarted += HandleKnockbackStarted;
    }

    private void OnDisable()
    {
        _health.onDamaged -= HandleDamaged;
        if (_knockback != null) _knockback.onKnockbackStarted -= HandleKnockbackStarted;
        ReleaseAttackSlot();

        // 逃走などで無効になった時、最後の目的地へ歩き続けないようにする(破棄中は既に消えていることがある)
        if (_move != null) _move.Stop();
    }

    // 所属するグループを設定する(CS_VillainGroup.AddMemberから呼ばれる)
    public void SetGroup(CS_VillainGroup group)
    {
        _group = group;
    }

    // ランダムイベントの範囲(中心から水平に半径radius)を路地裏として扱う(イベントで生成した悪人に、生成直後に呼ぶ)
    // 範囲の中にいる標的は、路地裏(Alley)のNavMeshの上でなくても路地裏にいるとみなす
    // confine: 範囲の中に留まる(レイドのボス用)
    //   臨戦態勢範囲の代わりに、範囲の中にいる標的に反応し、範囲の外の標的は見つけない(攻撃されても追わない)
    //   追いかける時も範囲の外へは出ない
    public void SetEventArea(Vector3 center, float radius, bool confine = false)
    {
        _hasEventArea = true;
        _eventAreaCenter = center;
        _eventAreaRadius = radius;
        _isConfinedToEventArea = confine;
    }

    private void FixedUpdate()
    {
        if (!hasAuthority) return;

        _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.fixedDeltaTime);

        // ノックバック中は他の行動を止める(移動はCS_VillainKnockbackが行う)
        if (_knockback != null && _knockback.isKnockedBack) return;

        switch (_state)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
            case State.Return: UpdateReturn(); break;
        }
    }

    // 犯罪中。臨戦態勢範囲にプレイヤーが入ったら追跡を始める
    private void UpdateIdle()
    {
        _move.Stop();
        ScanEngageRange();
    }

    private void UpdateChase()
    {
        // 標的が倒れた・ホログラムが消えた時は、近くに別の標的がいればそちらを狙う
        if (!IsTargetValid() && TryEngageInRange(engageRange, true)) return;

        if (!IsTargetValid() || HasTargetLeftAlley() || IsTooFarFromHome())
        {
            ClearTarget();
            _state = State.Return;
            ReleaseAttackSlot();
            return;
        }

        // プレイヤーを追っている間に陽動ホログラムに気付いたら、ホログラムを狙う
        if (!isTargetHologram) ScanHologram();

        Vector3 toTarget = GetFlatDirection(_target.position);
        if (!TryAcquireAttackSlot())
        {
            KeepWatchDistance(toTarget);
            return;
        }

        if (toTarget.magnitude > _attackStartDistance)
        {
            _move.MoveTo(ClampToEventArea(_target.position), moveSpeed);
            return;
        }

        _move.Stop();
        _move.FaceTowards(toTarget);
        if (_attackCooldown <= 0f) StartAttack();
    }

    // 攻撃中。chargeTime秒溜めてから、hitActiveTime秒間攻撃判定を出し、追跡に戻る
    private void UpdateAttack()
    {
        _move.Stop();
        _attackElapsed += Time.fixedDeltaTime;

        // 溜め中も、攻撃を始めた時にターゲットがいた位置を向き続ける(ターゲットは追いかけない)
        _move.FaceTowards(GetFlatDirection(_attackAimPosition));

        if (_attackElapsed < _chargeTime) return;

        if (_attackPhase.Value == CSE_VillainAttackPhase.Charge)
        {
            _attackPhase.Value = CSE_VillainAttackPhase.Hit;
        }

        if (_attackElapsed < _chargeTime + _hitActiveTime)
        {
            HitPlayers();
            return;
        }

        _attackPhase.Value = CSE_VillainAttackPhase.None;
        _attackCooldown = _attackInterval;
        _state = State.Chase;
    }

    // スポーン位置へ戻る。着いたら犯罪を再開する
    // 戻っている間は、プレイヤーが臨戦態勢範囲に入っても追跡しない(攻撃された時の反撃はHandleDamagedで行う)
    private void UpdateReturn()
    {
        // 経路に沿った残りの距離で到着を判定する(曲がり角や障害物を考慮するため)
        _move.MoveTo(_homePosition, moveSpeed);
        if (!_move.IsNearDestination(_arriveDistance)) return;

        _move.Stop();
        transform.rotation = _homeRotation;
        _state = State.Idle;
    }

    // 攻撃枠が無い間、ターゲットから watchDistance 離れた位置を保ち、ターゲットの方を向く
    private void KeepWatchDistance(Vector3 toTarget)
    {
        float distance = toTarget.magnitude;
        if (Mathf.Abs(distance - _watchDistance) <= _watchTolerance)
        {
            _move.Stop();
            _move.FaceTowards(toTarget);
            return;
        }

        // ターゲットから見て今いる方向に、watchDistance 離れた位置へ移動する(近すぎれば下がり、遠すぎれば近づく)
        Vector3 fromTarget = distance > 0f ? -toTarget / distance : -transform.forward;
        _move.MoveTo(_target.position + fromTarget * _watchDistance, moveSpeed);
    }

    // グループに属していなければ、常に攻撃できる
    private bool TryAcquireAttackSlot()
    {
        return _group == null || _group.TryAcquireAttackSlot(this);
    }

    private void ReleaseAttackSlot()
    {
        _group?.ReleaseAttackSlot(this);
    }

    private void StartAttack()
    {
        _state = State.Attack;
        _attackElapsed = 0f;
        _attackAimPosition = _target.position;
        _damagedTargets.Clear();
        _attackPhase.Value = CSE_VillainAttackPhase.Charge;
    }

    // 正面の攻撃範囲(hitCenter)にいるプレイヤーのうち、今回の攻撃でまだ当たっていない相手にダメージを与える
    private void HitPlayers()
    {
        CS_AttackHitDetector.FindTargets(transform, _attackData, _targetLayers, _hitBuffer, _hitTargets);

        AttackContext context = new AttackContext(transform, 0);
        foreach (IDamageable target in _hitTargets)
        {
            if (!(target is CS_PlayerHealth)) continue;   // 悪人同士では当たらない
            if (!_damagedTargets.Add(target)) continue;   // 判定が出ている間も、同じ相手には1回だけ当てる

            float damage = _attackData.CalculateDamage(context, target) * _stats.attackPower;
            target.TakeDamage(damage);
            _attackData.OnHit(context, target);
        }
    }

    // 攻撃中にノックバックしたら、攻撃を中断して追跡に戻る(サーバーのみ呼ばれる)
    // スーパーアーマーがオンの間は攻撃中にノックバックしないので、ここで中断されるのはオフの時だけ
    private void HandleKnockbackStarted()
    {
        if (_state != State.Attack) return;

        _attackPhase.Value = CSE_VillainAttackPhase.None;
        _attackCooldown = _attackInterval;
        _state = State.Chase;
    }

    // 攻撃されたら、臨戦態勢でなければ近くのプレイヤーを狙う(サーバーのみ呼ばれる)
    private void HandleDamaged()
    {
        if (isEngaged) return;

        // 反撃なので、ホログラムではなく攻撃してきたプレイヤー(の候補)を狙う
        TryEngageInRange(_counterSearchRange, false);
    }

    // 一定間隔ごとに、臨戦態勢範囲に陽動ホログラム・プレイヤーがいないか確認する
    private bool ScanEngageRange()
    {
        if (!TickScanTimer()) return false;

        return TryEngageInRange(engageRange, true);
    }

    // 一定間隔ごとに、臨戦態勢範囲に陽動ホログラムがないか確認し、あれば標的をホログラムに切り替える
    private void ScanHologram()
    {
        if (!TickScanTimer()) return;

        Transform hologram = FindNearestHologram(engageRange);
        if (hologram != null) SetTarget(hologram, null);
    }

    // 確認する間隔(scanInterval)がたったか
    private bool TickScanTimer()
    {
        _scanTimer -= Time.fixedDeltaTime;
        if (_scanTimer > 0f) return false;

        _scanTimer = _scanInterval;
        return true;
    }

    // 指定範囲で一番近い標的を探し、見つかったら追跡を始める
    // includeHolograms: 陽動ホログラムも探すか(見つかればプレイヤーより優先する)
    private bool TryEngageInRange(float range, bool includeHolograms)
    {
        Transform hologram = includeHolograms ? FindNearestHologram(range) : null;
        if (hologram != null)
        {
            SetTarget(hologram, null);
            return true;
        }

        CS_PlayerHealth player = FindNearestPlayer(range);
        if (player == null) return false;

        SetTarget(player.transform, player);
        return true;
    }

    // 標的を設定して追跡を始める(player: 標的がプレイヤーの時のHP。ホログラムの時はnull)
    private void SetTarget(Transform target, CS_PlayerHealth player)
    {
        _target = target;
        _targetPlayer = player;
        _state = State.Chase;

        // 追跡を始めた時点では路地裏にいるものとして数え直す
        _isTargetInAlley = true;
        _outsideAlleyTime = 0f;
    }

    private void ClearTarget()
    {
        _target = null;
        _targetPlayer = null;
    }

    // 標的がまだ狙える状態か(プレイヤーは倒れていない、ホログラムは消えていない)
    private bool IsTargetValid()
    {
        if (_target == null) return false;   // Destroy(Despawn)されたホログラム・プレイヤーもnull扱いになる

        return _targetPlayer == null || IsValidTarget(_targetPlayer);
    }

    // 指定範囲で、見えている(煙幕に遮られていない)一番近い陽動ホログラムを探す
    // ホログラムはコライダーを持たないので、展開中のホログラムの一覧から探す
    private Transform FindNearestHologram(float range)
    {
        Vector3 eyePosition = transform.position + Vector3.up * _eyeHeight;
        Transform nearest = null;
        float nearestSqr = range * range;
        foreach (CS_Hologram hologram in CS_Hologram.activeHolograms)
        {
            Vector3 position = hologram.transform.position;
            float sqr = (position - transform.position).sqrMagnitude;
            if (sqr > nearestSqr) continue;
            if (IsOutsideConfinedArea(position)) continue;

            // 煙幕の中にある・煙幕越しのホログラムは見えない
            if (CS_SmokeScreen.IsLineBlocked(eyePosition, position)) continue;

            nearest = hologram.transform;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // 指定範囲で、見えている(煙幕に遮られていない)一番近いプレイヤーを探す
    private CS_PlayerHealth FindNearestPlayer(float range)
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, range, _hitBuffer, _targetLayers, QueryTriggerInteraction.Ignore);

        Vector3 eyePosition = transform.position + Vector3.up * _eyeHeight;
        CS_PlayerHealth nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _hitBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (!IsValidTarget(player)) continue;
            if (IsOutsideConfinedArea(player.transform.position)) continue;

            // 煙幕の中にいる標的・煙幕越しの標的は見えない(近くにいても気付かない)
            if (CS_SmokeScreen.IsLineBlocked(eyePosition, _hitBuffer[i].bounds.center)) continue;

            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = player;
            nearestSqr = sqr;
        }
        return nearest;
    }

    private bool IsValidTarget(CS_PlayerHealth player)
    {
        return player != null && !player.isDead;
    }

    // 路地裏のArea名からエリアマスクを求める(起動時に1回だけ)
    private void SetUpAlleyAreaMask()
    {
        int areaIndex = NavMesh.GetAreaFromName(_alleyAreaName);
        if (areaIndex < 0)
        {
            // 悪人は大量に生成されるので、警告は1回だけ出す
            if (!_hasWarnedNoAlleyArea)
            {
                Debug.LogWarning($"CS_VillainCombat: NavMeshのArea「{_alleyAreaName}」が無いため、路地裏の判定を行いません", this);
                _hasWarnedNoAlleyArea = true;
            }
            _alleyAreaMask = 0;
            return;
        }

        _alleyAreaMask = 1 << areaIndex;
    }

    // ターゲットが路地裏の外に出てから、あきらめる時間がたったか
    private bool HasTargetLeftAlley()
    {
        if (_alleyAreaMask == 0 && !_hasEventArea) return false;

        if (IsTargetInAlley())
        {
            _outsideAlleyTime = 0f;
            return false;
        }

        _outsideAlleyTime += Time.fixedDeltaTime;
        return _outsideAlleyTime >= _leaveAlleyGiveUpTime;
    }

    // ターゲットが路地裏にいるか(ランダムイベントの範囲の中、または足元のNavMeshが路地裏Area)
    private bool IsTargetInAlley()
    {
        if (IsTargetInEventArea())
        {
            _isTargetInAlley = true;
            return true;
        }
        if (_alleyAreaMask == 0) return false;

        // ジャンプ中などで足元にNavMeshが見つからない時は、直前の判定結果を使う
        if (!NavMesh.SamplePosition(_target.position, out NavMeshHit hit, _areaSampleRadius, NavMesh.AllAreas))
        {
            return _isTargetInAlley;
        }

        _isTargetInAlley = (hit.mask & _alleyAreaMask) != 0;
        return _isTargetInAlley;
    }

    private bool IsTargetInEventArea()
    {
        return _hasEventArea && IsInEventArea(_target.position);
    }

    // 位置がランダムイベントの範囲の中か(水平方向)
    private bool IsInEventArea(Vector3 position)
    {
        Vector3 offset = position - _eventAreaCenter;
        offset.y = 0f;
        return offset.sqrMagnitude <= _eventAreaRadius * _eventAreaRadius;
    }

    // 範囲の中に留まる悪人(レイドのボス)は、目的地を範囲の中に収める(範囲の外へは追わない)
    private Vector3 ClampToEventArea(Vector3 destination)
    {
        if (!_isConfinedToEventArea) return destination;

        Vector3 offset = destination - _eventAreaCenter;
        float height = offset.y;
        offset.y = 0f;
        if (offset.sqrMagnitude <= _eventAreaRadius * _eventAreaRadius) return destination;

        Vector3 clamped = _eventAreaCenter + offset.normalized * _eventAreaRadius;
        clamped.y = _eventAreaCenter.y + height;
        return clamped;
    }

    // 標的が範囲の外にいて、見つけてはいけないか(範囲の中に留まる悪人のみ)
    private bool IsOutsideConfinedArea(Vector3 position)
    {
        return _isConfinedToEventArea && !IsInEventArea(position);
    }

    private bool IsTooFarFromHome()
    {
        return (transform.position - _homePosition).sqrMagnitude > _leashRange * _leashRange;
    }

    // 目的地までの水平方向のベクトル(長さ = 距離)
    private Vector3 GetFlatDirection(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        return direction;
    }
}
