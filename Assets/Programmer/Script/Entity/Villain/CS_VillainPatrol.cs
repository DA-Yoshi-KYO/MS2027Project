using UnityEngine;

/*
 * 悪人1人分の巡回の動き
 * 巡回パターンの犯罪実行役なら、決めた位置で犯罪モーションをし続ける(運ぶなら貨物と車を往復する)
 * 巡回パターンの組(CS_VillainPatrolCrew)に入っていれば、先頭ならルートを回り、付いてくる人なら隊列の位置を歩く
 * 組に入っていなければ、グループ(CS_VillainGroup)が決めた、自分のルートの今の進み具合の位置を歩き、端では見回す
 * CS_VillainCombatが、犯罪中(Idle)はTick、帰還中はTickReturnを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・犯罪実行役(巡回パターンの実行役。CS_VillainSpawnerがSetExecutorで決める)
 *   決めた位置へ歩き、着いたら決めた向きで犯罪モーションをし続ける(蹴る / いじる)
 *   運ぶ: 貨物の位置で拾う(pickUpTime秒) → 車の位置まで運ぶ → 置く(putDownTime秒) → 貨物の位置へ戻る、を繰り返す
 *   今している犯罪の動作は CS_VillainCrime.SetCrimeAction で全クライアントに伝える(見た目はCS_VillainVisualがAnimatorに渡す)
 *   臨戦態勢になったら犯罪の動作をやめる(StopCrimeAction)。帰還(戦った後)は、向かっていた所へ戦闘時の速さで戻る
 * ・巡回パターン(スポーン位置にCS_VillainPatrolRouteがある時。CS_VillainSpawnerがSetCrewで組に入れる)
 *   先頭: 組の進み具合に従ってポイントを回り、ポイントでやることを実行する(CS_VillainPatrolCrew.TickLeader)
 *   付いてくる人: 先頭が通った道(足跡)の上で求めた隊列の位置へ、自分の進む向きを向いて歩く
 *     先頭が歩いている間は、隊列の位置より formationLookAhead(m) 先を目的地にして止まらずに歩き、
 *     先頭と同じ速さに、遅れている分(m)を足した速さで歩く(遅れた分を1秒で取り戻す。先に出ていれば遅くする)
 *     → 止まる・歩くを繰り返して遅れないので、先頭と1人目の間も、ほかの人の間と同じ間隔になる
 *     先頭が止まっている間は、隊列の位置ちょうどへ歩き、着いたら立っている所の道の進む向きを向いて待つ
 *     (先頭の見回し・向きを変えるは真似しない)
 *   隊列の位置から catchUpDistance 以上離れている時(戦った後など)は、戦闘時の速さで合流する
 *   帰還(戦った後): 先頭は今向かっているポイント、付いてくる人は隊列の位置へ、戦闘時の速さで向かう
 * ・巡回パターンが無い時(路地裏の両端を全員で往復)
 *   どのルートのどこを歩くか(進み具合・割り当て・端での折り返し)は、グループ全員で共有するのでCS_VillainGroupが決める
 *   このクラスは、その位置へ歩くことと、端で見回すことだけを行う
 * ・歩く: 自分のルートの今の位置より lookAhead(m) 先を目的地にして、進み具合に合う速さで歩く(目的地に追い付いて止まらないように)
 *   遅れている分だけ速く歩いて追い付く(遅れた距離(m)を1秒で取り戻す速さを足す)
 *   ルートの位置から catchUpDistance 以上離れている時(生成直後・戦った後など)は、戦闘時の速さで合流する
 * ・見回す: グループが端で見回している間、着いた時の向き(進んできた向き)を中心に、
 *   左右 lookAroundAngle 度ずつ首を振るように体の向きを変える(lookAroundPeriod 秒で1往復)
 *   まだ合流できていない時は見回さずに、端へ向かう
 * ・帰還(戦った後): ルートの今の位置へ戦闘時の速さで向かい、catchUpDistance 以内に来たら合流したとみなす
 * ・処理はサーバー(オフライン時はその場)でのみ行う。位置・向きはNetworkTransformで同期する
 */
// ========================================

[RequireComponent(typeof(CS_VillainMove))]
public class CS_VillainPatrol : MonoBehaviour
{
    private const float _lookAhead = 1f;   // 歩いている間、ルートの今の位置からどれだけ先(m)を目的地にするか

    [SerializeField, Range(0f, 180f)]
    [Tooltip("見回す時に、左右へ向きを変える角度(度)")]
    private float _lookAroundAngle = 60f;

    [SerializeField, Min(0.1f)]
    [Tooltip("見回す時に、左右を1往復する時間(秒)")]
    private float _lookAroundPeriod = 2f;

    [SerializeField, Min(0f)]
    [Tooltip("ルートの今の位置からこれ以上離れていたら、戦闘時の速さで合流する(m)。戦った後はここまで近づいたら巡回に戻る")]
    private float _catchUpDistance = 1.5f;

    private const float _formationArriveDistance = 0.3f;   // 隊列の位置に着いたとみなす距離(m)
    private const float _formationLookAhead = 1f;          // 付いてくる人が歩いている間、隊列の位置より道の上でどれだけ先(m)を目的地にするか
    private const float _minFormationSpeedRate = 0.5f;     // 付いてくる人が隊列の位置より先に出ている時の、一番遅い速さ(ルートの速さの倍率)
    private const float _executorArriveDistance = 0.3f;    // 犯罪実行役が、決めた位置に着いたとみなす距離(m)

    [Header("犯罪実行役")]
    [SerializeField, Min(0f)]
    [Tooltip("運ぶ: 貨物を拾うのにかかる時間(秒)")]
    private float _pickUpTime = 1.5f;

    [SerializeField, Min(0f)]
    [Tooltip("運ぶ: 貨物を車に置くのにかかる時間(秒)")]
    private float _putDownTime = 1.5f;

    // 運ぶ犯罪実行役の、今の段階
    private enum CarryPhase
    {
        GoToCargo,   // 貨物の位置へ向かう
        PickUp,      // 拾っている
        GoToCar,     // 車の位置へ運んでいる
        PutDown,     // 車に置いている
    }

    private CS_VillainMove _move;
    private CS_VillainCombat _combat;
    private CS_VillainCrime _crime;
    private CS_VillainPatrolCrew _crew;   // 巡回パターンの組(入っていなければnull)
    private bool _isExecutor;             // 犯罪実行役か
    private Vector3 _executorPosition;    // 実行役の位置(運ぶ時は貨物の位置)
    private Vector3 _carryPosition;       // 運ぶ時の車の位置
    private Quaternion _executorRotation; // 実行役の向き
    private CSE_VillainCrimeMotion _executorMotion;
    private CarryPhase _carryPhase;
    private float _carryTimer;
    private bool _isLookingAround;   // 端で見回している途中か
    private float _lookTimer;

    // まだ動けるか(撃退されて倒れている・逃走中は反撃が無効になっている)。組の先頭を決めるのに使う
    public bool isActiveMember => isActiveAndEnabled && _combat != null && _combat.enabled;

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
        _combat = GetComponent<CS_VillainCombat>();
        _crime = GetComponent<CS_VillainCrime>();
    }

    // 犯罪実行役にする(生成直後にCS_VillainSpawnerから呼ばれる)
    // motion: 実際に使う犯罪モーション(「エリアに合わせる」は決めてから渡す)
    public void SetExecutor(Vector3 position, Vector3 carryPosition, Quaternion rotation, CSE_VillainCrimeMotion motion)
    {
        _isExecutor = true;
        _executorPosition = position;
        _carryPosition = carryPosition;
        _executorRotation = rotation;
        _executorMotion = motion;
        _carryPhase = CarryPhase.GoToCargo;
    }

    // 犯罪の動作をやめる(臨戦態勢になった時に、CS_VillainCombatから呼ばれる)
    public void StopCrimeAction()
    {
        SetCrimeAction(CSE_VillainCrimeAction.None);
    }

    // 巡回パターンの組に入れる(生成直後にCS_VillainSpawnerから呼ばれる)
    public void SetCrew(CS_VillainPatrolCrew crew)
    {
        _crew = crew;
        crew.AddMember(this);
    }

    // 巡回を1回分進める(犯罪中の間、CS_VillainCombatが毎回呼ぶ)。巡回しないグループ・メンバーならfalse
    // catchUpSpeed: ルートから離れている時に合流する速さ(戦闘時の速さ)
    public bool Tick(CS_VillainCombat combat, CS_VillainGroup group, float catchUpSpeed, float deltaTime)
    {
        if (_isExecutor)
        {
            TickExecutor(combat.patrolSpeed, catchUpSpeed, deltaTime);
            return true;
        }

        if (_crew != null)
        {
            TickCrew(catchUpSpeed, deltaTime);
            return true;
        }

        if (!group.TryGetPatrolTarget(combat, _lookAhead, out Vector3 destination, out Vector3 position, out float speed, out Vector3 forward)) return false;

        float distance = GetFlatDistance(position);
        if (group.isLookingAround && distance <= _catchUpDistance)
        {
            LookAround(forward, deltaTime);
            return true;
        }

        // 進む向きに対して遅れている分(m)だけ速く歩いて追い付く(先に出ている時は足さない)
        Vector3 toPosition = position - transform.position;
        toPosition.y = 0f;
        float behind = Mathf.Max(0f, Vector3.Dot(toPosition, forward));

        _isLookingAround = false;
        _move.MoveTo(destination, distance > _catchUpDistance ? catchUpSpeed : speed + behind);
        return true;
    }

    // 戦った後、ルートの今の位置へ向かう(帰還中にCS_VillainCombatが毎回呼ぶ)
    // 合流できたらtrue。巡回しないグループ・メンバーならfalse(isPatrolがfalseになる)
    public bool TickReturn(CS_VillainCombat combat, CS_VillainGroup group, float speed, out bool isPatrol)
    {
        if (_isExecutor)
        {
            isPatrol = true;
            return TickExecutorReturn(speed);
        }

        if (_crew != null)
        {
            isPatrol = true;
            return TickCrewReturn(speed);
        }

        isPatrol = group.TryGetPatrolTarget(combat, _lookAhead, out _, out Vector3 position, out _, out _);
        if (!isPatrol) return false;

        _isLookingAround = false;
        _move.MoveTo(position, speed);
        return GetFlatDistance(position) <= _catchUpDistance;
    }

    // ---- 犯罪実行役 ----

    // 決めた位置で犯罪モーションをする(運ぶなら貨物と車を往復する)
    // walkSpeed: 歩く速さ(巡回時の速さ) / catchUpSpeed: 遠く離れている時の速さ(戦闘時の速さ)
    private void TickExecutor(float walkSpeed, float catchUpSpeed, float deltaTime)
    {
        if (_executorMotion == CSE_VillainCrimeMotion.Carry)
        {
            TickCarry(walkSpeed, catchUpSpeed, deltaTime);
            return;
        }

        if (!WalkTo(_executorPosition, walkSpeed, catchUpSpeed)) return;

        _move.FaceTowards(_executorRotation * Vector3.forward);
        SetCrimeAction(_executorMotion == CSE_VillainCrimeMotion.Tamper ? CSE_VillainCrimeAction.Tamper : CSE_VillainCrimeAction.Kick);
    }

    // 運ぶ: 貨物の位置で拾う → 車の位置まで運ぶ → 置く → 貨物の位置へ戻る、を繰り返す
    private void TickCarry(float walkSpeed, float catchUpSpeed, float deltaTime)
    {
        switch (_carryPhase)
        {
            case CarryPhase.GoToCargo:
                if (!WalkTo(_executorPosition, walkSpeed, catchUpSpeed)) return;
                StartCarryPhase(CarryPhase.PickUp);
                break;

            case CarryPhase.PickUp:
                _move.Stop();
                _move.FaceTowards(_executorRotation * Vector3.forward);
                SetCrimeAction(CSE_VillainCrimeAction.PickUp);
                _carryTimer += deltaTime;
                if (_carryTimer >= _pickUpTime) StartCarryPhase(CarryPhase.GoToCar);
                break;

            case CarryPhase.GoToCar:
                SetCrimeAction(CSE_VillainCrimeAction.Carry);
                if (GetFlatDistance(_carryPosition) > _executorArriveDistance && !_move.IsNearDestination(_executorArriveDistance))
                {
                    _move.MoveTo(_carryPosition, walkSpeed);
                    return;
                }
                StartCarryPhase(CarryPhase.PutDown);
                break;

            case CarryPhase.PutDown:
                _move.Stop();
                SetCrimeAction(CSE_VillainCrimeAction.PutDown);
                _carryTimer += deltaTime;
                if (_carryTimer >= _putDownTime) StartCarryPhase(CarryPhase.GoToCargo);
                break;
        }
    }

    private void StartCarryPhase(CarryPhase phase)
    {
        _carryPhase = phase;
        _carryTimer = 0f;
    }

    // 位置へ歩く(犯罪の動作はしない)。着いていればtrue(止まる)
    private bool WalkTo(Vector3 position, float walkSpeed, float catchUpSpeed)
    {
        float distance = GetFlatDistance(position);
        if (distance > _executorArriveDistance && !_move.IsNearDestination(_executorArriveDistance))
        {
            SetCrimeAction(CSE_VillainCrimeAction.None);
            _move.MoveTo(position, distance > _catchUpDistance ? catchUpSpeed : walkSpeed);
            return false;
        }

        _move.Stop();
        return true;
    }

    // 戦った後、向かっていた所(運んでいる途中なら車、それ以外は実行役の位置)へ戻る。合流できたらtrue
    private bool TickExecutorReturn(float speed)
    {
        SetCrimeAction(CSE_VillainCrimeAction.None);
        bool isCarrying = _executorMotion == CSE_VillainCrimeMotion.Carry
            && (_carryPhase == CarryPhase.GoToCar || _carryPhase == CarryPhase.PutDown);
        Vector3 position = isCarrying ? _carryPosition : _executorPosition;

        _move.MoveTo(position, speed);
        return GetFlatDistance(position) <= _catchUpDistance;
    }

    private void SetCrimeAction(CSE_VillainCrimeAction action)
    {
        if (_crime != null) _crime.SetCrimeAction(action);
    }

    // ---- 巡回パターン ----

    // 先頭ならルートを進め、付いてくる人なら隊列の位置を歩く
    private void TickCrew(float catchUpSpeed, float deltaTime)
    {
        CS_VillainPatrol leader = _crew.GetLeader();
        if (leader == this)
        {
            _crew.TickLeader(transform, _move, deltaTime);
            return;
        }
        if (leader == null) return;

        int followerIndex = _crew.GetFollowerIndex(this);
        Vector3 position = _crew.GetFormationPosition(leader.transform, followerIndex, out Vector3 forward);
        float distance = GetFlatDistance(position);
        if (distance > _catchUpDistance)
        {
            _move.MoveTo(position, catchUpSpeed);
            return;
        }

        // 先頭が止まっている間は、隊列の位置ちょうどへ歩き、着いたら立っている所の道の進む向きを向いて待つ
        if (_crew.isActing)
        {
            if (distance > _formationArriveDistance)
            {
                _move.MoveTo(position, _crew.speed);
                return;
            }

            _move.Stop();
            _move.FaceTowards(forward);
            return;
        }

        // 先頭が歩いている間は、隊列の位置より少し先を目的地にして、止まらずに歩き続ける(追い付いて止まると遅れるため)
        // 速さは先頭と同じにし、遅れている分(m)だけ速く歩いて追い付く(先に出ていれば遅くする)
        Vector3 destination = _crew.GetFormationPosition(leader.transform, followerIndex, out _, _formationLookAhead);
        Vector3 toPosition = position - transform.position;
        toPosition.y = 0f;
        float behind = Vector3.Dot(toPosition, forward);
        _move.MoveTo(destination, Mathf.Max(_crew.speed * _minFormationSpeedRate, _crew.speed + behind));
    }

    // 戦った後、先頭は今向かっているポイント、付いてくる人は隊列の位置へ向かう。合流できたらtrue
    private bool TickCrewReturn(float speed)
    {
        CS_VillainPatrol leader = _crew.GetLeader();
        Vector3 position = leader == this || leader == null
            ? _crew.currentTarget
            : _crew.GetFormationPosition(leader.transform, _crew.GetFollowerIndex(this), out _);

        _isLookingAround = false;
        _move.MoveTo(position, speed);
        return GetFlatDistance(position) <= _catchUpDistance;
    }

    // ---- 巡回パターンが無い時(全員で往復) ----

    // 着いた時の向き(進んできた向き)を中心に、左右へ首を振るように向きを変える
    private void LookAround(Vector3 forward, float deltaTime)
    {
        if (!_isLookingAround)
        {
            _isLookingAround = true;
            _lookTimer = 0f;
        }

        _move.Stop();
        _lookTimer += deltaTime;
        float yaw = Mathf.Sin(_lookTimer / _lookAroundPeriod * Mathf.PI * 2f) * _lookAroundAngle;
        _move.FaceTowards(Quaternion.Euler(0f, yaw, 0f) * forward);
    }

    // 水平方向の距離(m)
    private float GetFlatDistance(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
