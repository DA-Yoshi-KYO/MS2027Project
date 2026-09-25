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
 *   帰還中でも、プレイヤーが臨戦態勢範囲に入れば再び追跡する
 * ・路地裏かどうかは、ターゲットの足元のNavMeshのAreaが alleyAreaName(既定: Alley)かで判定する
 *   ・プレイヤー側には何も必要ない。NavMeshのベイクと、Navigationの Areas に同名のAreaを追加しておくこと
 *   ・Areaが無い場合は警告を出し、路地裏判定を行わない(距離の判定だけになる)
 *   ・ジャンプ中などで足元にNavMeshが見つからない時は、直前の判定結果を使う
 *   ・出入口で行ったり来たりされても追跡と帰還が切り替わり続けないよう、外に出てから一定時間待つ
 * ・isEngagedがtrueの間は犯罪の手を止める(犯罪の進行側から参照する想定)
 * ・攻撃のタイミング・範囲はCSO_AttackData(Attack Data)で決める
 *   実際のダメージ = CSO_AttackData.CalculateDamage() × CS_VillainStats.attackPower
 *   → Attack DataのDamageを1にすると、攻撃力がそのままダメージになる
 * ・ダメージを与えるのはプレイヤー(CS_PlayerHealth)のみ。悪人同士では当たらない
 * ・移動速度 = プレイヤーの通常移動速度(Player Base Stats) × CS_VillainStats.moveSpeedMultiplier
 * ・移動(経路探索)はCS_VillainMove(NavMeshAgent)に任せる。このクラスは目的地を決めるだけ
 * ・どのプレイヤーに攻撃されたかは分からないため、攻撃されたら近くのプレイヤーを狙う
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
        Attack,     // 攻撃モーション中
        Return,     // スポーン位置へ戻っている
    }

    private const float _scanInterval = 0.2f;     // 臨戦態勢範囲を確認する間隔(秒)
    private const float _arriveDistance = 0.3f;   // スポーン位置に着いたとみなす距離
    private const int _hitBufferSize = 16;        // 一度に判定できるコライダーの上限
    private const float _areaSampleRadius = 1f;   // ターゲットの足元のNavMeshを探す半径(m)

    private static bool _hasWarnedNoAlleyArea;    // 路地裏Areaが無い警告を出したか(全悪人で共有)

    [Header("参照")]
    [SerializeField]
    [Tooltip("移動速度の基準にするプレイヤーのステータス(DB_PlayerStats)")]
    private CSO_PlayerStats _playerBaseStats;

    [Header("攻撃")]
    [SerializeField]
    [Tooltip("攻撃1回分のデータ。Damageを1にすると攻撃力がそのままダメージになる")]
    private CSO_AttackData _attackData;

    [SerializeField, Min(0f)]
    [Tooltip("攻撃が終わってから次の攻撃までの間隔(秒)")]
    private float _attackInterval = 1f;

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
    private readonly Collider[] _hitBuffer = new Collider[_hitBufferSize];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private State _state = State.Idle;
    private CS_PlayerHealth _target;
    private Vector3 _homePosition;        // スポーンした位置(犯罪を行う場所)
    private Quaternion _homeRotation;
    private float _scanTimer;
    private float _attackElapsed;         // 攻撃開始からの経過時間
    private float _attackCooldown;        // 次の攻撃までの残り時間
    private bool _hasHit;                 // 今回の攻撃で判定を行ったか
    private int _alleyAreaMask;           // 路地裏AreaのNavMeshエリアマスク。0なら路地裏判定を行わない
    private bool _isTargetInAlley;        // 直前のターゲットの路地裏判定(足元のNavMeshが見つからない時に使う)
    private float _outsideAlleyTime;      // ターゲットが路地裏の外に出てからの時間

    public bool isEngaged => _state == State.Chase || _state == State.Attack;   // 臨戦態勢中か
    public bool isCommittingCrime => enabled && _state == State.Idle;             // スポーン位置で犯罪を進めているか
    public bool isAttacking => _state == State.Attack;                           // 攻撃モーション中か
    public CSO_AttackData attackData => _attackData;
    public float counterSearchRange => _counterSearchRange;
    public float leashRange => _leashRange;
    public Vector3 homePosition => _homePosition;

    // このマシンが悪人を動かす権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private float moveSpeed => _playerBaseStats.moveSpeed * _stats.moveSpeedMultiplier;
    private float attackReach => _attackData.hitRange + _attackData.hitRadius;   // この距離まで近づいたら攻撃する

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
        _stats = GetComponent<CS_VillainStats>();
        _health = GetComponent<CS_VillainHealth>();

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
    }

    private void OnDisable()
    {
        _health.onDamaged -= HandleDamaged;

        // 逃走などで無効になった時、最後の目的地へ歩き続けないようにする(破棄中は既に消えていることがある)
        if (_move != null) _move.Stop();
    }

    private void FixedUpdate()
    {
        if (!hasAuthority) return;

        _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.fixedDeltaTime);

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
        if (!IsValidTarget(_target) || HasTargetLeftAlley() || IsTooFarFromHome())
        {
            _target = null;
            _state = State.Return;
            return;
        }

        Vector3 toTarget = GetFlatDirection(_target.transform.position);
        if (toTarget.magnitude > attackReach)
        {
            _move.MoveTo(_target.transform.position, moveSpeed);
            return;
        }

        _move.Stop();
        _move.FaceTowards(toTarget);
        if (_attackCooldown <= 0f) StartAttack();
    }

    // 攻撃モーション中。hitDelayで判定を出し、durationで追跡に戻る
    private void UpdateAttack()
    {
        _move.Stop();
        _attackElapsed += Time.fixedDeltaTime;

        if (IsValidTarget(_target))
        {
            _move.FaceTowards(GetFlatDirection(_target.transform.position));
        }

        if (!_hasHit && _attackElapsed >= _attackData.hitDelay)
        {
            _hasHit = true;
            HitPlayers();
        }

        if (_attackElapsed < _attackData.duration) return;

        _attackCooldown = _attackInterval;
        _state = State.Chase;
    }

    // スポーン位置へ戻る。着いたら犯罪を再開する
    private void UpdateReturn()
    {
        if (ScanEngageRange()) return;

        // 経路に沿った残りの距離で到着を判定する(曲がり角や障害物を考慮するため)
        _move.MoveTo(_homePosition, moveSpeed);
        if (!_move.IsNearDestination(_arriveDistance)) return;

        _move.Stop();
        transform.rotation = _homeRotation;
        _state = State.Idle;
    }

    private void StartAttack()
    {
        _state = State.Attack;
        _attackElapsed = 0f;
        _hasHit = false;
    }

    // 正面の攻撃範囲にいるプレイヤーにダメージを与える
    private void HitPlayers()
    {
        CS_AttackHitDetector.FindTargets(transform, _attackData, _targetLayers, _hitBuffer, _hitTargets);

        AttackContext context = new AttackContext(transform, 0);
        foreach (IDamageable target in _hitTargets)
        {
            if (!(target is CS_PlayerHealth)) continue;   // 悪人同士では当たらない

            float damage = _attackData.CalculateDamage(context, target) * _stats.attackPower;
            target.TakeDamage(damage);
            _attackData.OnHit(context, target);
        }
    }

    // 攻撃されたら、臨戦態勢でなければ近くのプレイヤーを狙う(サーバーのみ呼ばれる)
    private void HandleDamaged()
    {
        if (isEngaged) return;

        TryEngageInRange(_counterSearchRange);
    }

    // 一定間隔ごとに、臨戦態勢範囲にプレイヤーがいないか確認する
    private bool ScanEngageRange()
    {
        _scanTimer -= Time.fixedDeltaTime;
        if (_scanTimer > 0f) return false;
        _scanTimer = _scanInterval;

        return TryEngageInRange(_stats.engageRange);
    }

    // 指定範囲で一番近いプレイヤーを探し、見つかったら追跡を始める
    private bool TryEngageInRange(float range)
    {
        CS_PlayerHealth nearest = FindNearestPlayer(range);
        if (nearest == null) return false;

        _target = nearest;
        _state = State.Chase;

        // 追跡を始めた時点では路地裏にいるものとして数え直す
        _isTargetInAlley = true;
        _outsideAlleyTime = 0f;
        return true;
    }

    private CS_PlayerHealth FindNearestPlayer(float range)
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, range, _hitBuffer, _targetLayers, QueryTriggerInteraction.Ignore);

        CS_PlayerHealth nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_PlayerHealth player = _hitBuffer[i].GetComponentInParent<CS_PlayerHealth>();
            if (!IsValidTarget(player)) continue;

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
        if (_alleyAreaMask == 0) return false;

        if (IsTargetInAlley())
        {
            _outsideAlleyTime = 0f;
            return false;
        }

        _outsideAlleyTime += Time.fixedDeltaTime;
        return _outsideAlleyTime >= _leaveAlleyGiveUpTime;
    }

    // ターゲットの足元のNavMeshが路地裏Areaか
    private bool IsTargetInAlley()
    {
        // ジャンプ中などで足元にNavMeshが見つからない時は、直前の判定結果を使う
        if (!NavMesh.SamplePosition(_target.transform.position, out NavMeshHit hit, _areaSampleRadius, NavMesh.AllAreas))
        {
            return _isTargetInAlley;
        }

        _isTargetInAlley = (hit.mask & _alleyAreaMask) != 0;
        return _isTargetInAlley;
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
