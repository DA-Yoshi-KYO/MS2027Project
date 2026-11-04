using UnityEngine;

/*
 * 悪人1人分の巡回の動き
 * グループ(CS_VillainGroup)が決めた、自分のルートの今の進み具合の位置を歩き、端では見回す
 * CS_VillainCombatが、犯罪中(Idle)はTick、帰還中はTickReturnを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・どのルートのどこを歩くか(進み具合・割り当て・端での折り返し)は、グループ全員で共有するのでCS_VillainGroupが決める
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

    private CS_VillainMove _move;
    private bool _isLookingAround;   // 端で見回している途中か
    private float _lookTimer;

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
    }

    // 巡回を1回分進める(犯罪中の間、CS_VillainCombatが毎回呼ぶ)。巡回しないグループ・メンバーならfalse
    // catchUpSpeed: ルートから離れている時に合流する速さ(戦闘時の速さ)
    public bool Tick(CS_VillainCombat combat, CS_VillainGroup group, float catchUpSpeed, float deltaTime)
    {
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
        isPatrol = group.TryGetPatrolTarget(combat, _lookAhead, out _, out Vector3 position, out _, out _);
        if (!isPatrol) return false;

        _isLookingAround = false;
        _move.MoveTo(position, speed);
        return GetFlatDistance(position) <= _catchUpDistance;
    }

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
