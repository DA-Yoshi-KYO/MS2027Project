using UnityEngine;

/*
 * 悪人1人分の巡回の動き
 * 巡回パターンの組(CS_VillainPatrolCrew)に入っていれば、先頭ならルートを回り、付いてくる人なら隊列の位置を歩く
 * 組に入っていなければ、グループ(CS_VillainGroup)が決めた、自分のルートの今の進み具合の位置を歩き、端では見回す
 * CS_VillainCombatが、犯罪中(Idle)はTick、帰還中はTickReturnを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・巡回パターン(スポーン位置にCS_VillainPatrolRouteがある時。CS_VillainSpawnerがSetCrewで組に入れる)
 *   先頭: 組の進み具合に従ってポイントを回り、ポイントでやることを実行する(CS_VillainPatrolCrew.TickLeader)
 *   付いてくる人: 先頭の位置と向きから求めた隊列の位置へ歩き、着いたら先頭と同じ向きを向く
 *     (先頭が止まる・見回すと、付いてくる人もその場で先頭と同じ向きになる)
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
    private const float _formationSpeedRate = 1.3f;        // 付いてくる人が隊列の位置へ歩く速さ(ルートの速さの倍率。遅れずに付いていくため)

    private CS_VillainMove _move;
    private CS_VillainCombat _combat;
    private CS_VillainPatrolCrew _crew;   // 巡回パターンの組(入っていなければnull)
    private bool _isLookingAround;   // 端で見回している途中か
    private float _lookTimer;

    // まだ動けるか(撃退されて倒れている・逃走中は反撃が無効になっている)。組の先頭を決めるのに使う
    public bool isActiveMember => isActiveAndEnabled && _combat != null && _combat.enabled;

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
        _combat = GetComponent<CS_VillainCombat>();
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

        Vector3 position = _crew.GetFormationPosition(leader.transform, _crew.GetFollowerIndex(this));
        float distance = GetFlatDistance(position);
        if (distance > _catchUpDistance)
        {
            _move.MoveTo(position, catchUpSpeed);
            return;
        }

        // 隊列の位置へ、先頭と同じ向きのまま歩く。着いたら先頭と同じ向きで待つ
        if (distance > _formationArriveDistance)
        {
            _move.MoveToFacing(position, _crew.speed * _formationSpeedRate, leader.transform.forward);
            return;
        }

        _move.Stop();
        _move.FaceTowards(leader.transform.forward);
    }

    // 戦った後、先頭は今向かっているポイント、付いてくる人は隊列の位置へ向かう。合流できたらtrue
    private bool TickCrewReturn(float speed)
    {
        CS_VillainPatrol leader = _crew.GetLeader();
        Vector3 position = leader == this || leader == null
            ? _crew.currentTarget
            : _crew.GetFormationPosition(leader.transform, _crew.GetFollowerIndex(this));

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
