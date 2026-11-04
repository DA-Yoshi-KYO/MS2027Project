using System.Collections.Generic;
using UnityEngine;

/*
 * 巡回パターンのルート1本を回る組(先頭 + 隊列で付いてくる人)
 * ルートの進み具合(どのポイントへ向かっているか・ポイントで何をしているか)を持ち、先頭のCS_VillainPatrolが進める
 * CS_VillainSpawnerがグループを生成した時に、巡回パターンのルートごとに作る
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・先頭は、組に加わった順で一番前の、まだ動ける(撃退・逃走していない)メンバー
 *   先頭が倒されたら次の人が先頭になり、進み具合はそのまま続きから回る(進み具合は組が持つので引き継げる)
 *   付いてくる人の隊列の番号も、倒れた人の分だけ前へ詰める
 * ・先頭の動き(TickLeader)
 *   今向かっているポイントへ歩き(ルートの速さ)、着いたら、やることを上から順に実行してから次のポイントへ向かう
 *   停止: その場で止まる / 見回し: 着いた時の向きを中心に左右へ首を振る / 向きを変える: turnDuration秒かけて回る
 * ・付いてくる人は、先頭の位置と向きから隊列の位置を求めて、そこへ歩く(CS_VillainPatrol)
 * ・ポイントの位置は生成時にワールド座標(NavMeshの上)へ変換して持つ(途中でスポーン位置を動かすことは無いため)
 * ・サーバー(オフライン時はその場)でのみ使う
 */
// ========================================

public class CS_VillainPatrolCrew
{
    private const float _arriveDistance = 0.3f;   // ポイントに着いたとみなす距離(m)

    private readonly CSO_VillainPatrolPattern.Route _route;
    private readonly Vector3[] _pointPositions;   // ポイントのワールド座標(NavMeshの上)
    private readonly List<int> _order;            // 1周で回るポイントの順番
    private readonly List<CS_VillainPatrol> _members = new List<CS_VillainPatrol>();   // 加わった順(最初が先頭)

    private int _orderIndex;      // 今向かっている(やることを実行している)ポイントの、_orderの中の番号
    private bool _isActing;       // ポイントに着いて、やることを実行している途中か
    private int _actionIndex;     // 実行しているやることの番号
    private float _actionTimer;   // 実行しているやることの経過時間(秒)
    private float _baseYaw;       // 見回し・向きを変える時の基準の向き(度)

    public CSO_VillainPatrolPattern.Route route => _route;
    public float speed => _route.speed;

    // 今向かっているポイント(やることの実行中は、そのポイント)の位置
    public Vector3 currentTarget => _pointPositions[_order[_orderIndex]];

    public CS_VillainPatrolCrew(CS_VillainPatrolRoute owner, int routeIndex)
    {
        _route = owner.pattern.routes[routeIndex];
        _pointPositions = new Vector3[_route.points.Count];
        for (int i = 0; i < _pointPositions.Length; i++)
        {
            _pointPositions[i] = owner.GetPointWorldPosition(_route, i);
        }
        _order = _route.GetOrder();
    }

    public void AddMember(CS_VillainPatrol member)
    {
        _members.Add(member);
    }

    // 今の先頭(まだ動ける一番前のメンバー)。全員いなければnull
    public CS_VillainPatrol GetLeader()
    {
        foreach (CS_VillainPatrol member in _members)
        {
            if (IsActive(member)) return member;
        }
        return null;
    }

    // memberが隊列の何番目か(0始まり。倒れた人の分は詰める)。先頭なら-1
    public int GetFollowerIndex(CS_VillainPatrol member)
    {
        int index = -1;
        foreach (CS_VillainPatrol other in _members)
        {
            if (!IsActive(other)) continue;
            if (other == member) return index;
            index++;
        }
        return index;
    }

    // 先頭の向きと位置から、隊列のfollowerIndex番目の人が並ぶ位置
    public Vector3 GetFormationPosition(Transform leader, int followerIndex)
    {
        Vector3 offset = CSO_VillainPatrolPattern.GetFormationOffset(_route.formation.shape, followerIndex, _route.formation.spacing);
        Quaternion yaw = Quaternion.Euler(0f, leader.eulerAngles.y, 0f);
        return leader.position + yaw * offset;
    }

    // 先頭が巡回を1回分進める(先頭のCS_VillainPatrolから、犯罪中の間毎回呼ばれる)
    public void TickLeader(Transform leader, CS_VillainMove move, float deltaTime)
    {
        if (_isActing)
        {
            Act(leader, move, deltaTime);
            return;
        }

        Vector3 target = currentTarget;
        Vector3 offset = target - leader.position;
        offset.y = 0f;
        if (offset.magnitude > _arriveDistance && !move.IsNearDestination(_arriveDistance))
        {
            move.MoveTo(target, speed);
            return;
        }

        // 着いたら、やることを上から順に実行する(何も無ければ止まらずに次へ)
        move.Stop();
        _isActing = true;
        _actionIndex = 0;
        _actionTimer = 0f;
        _baseYaw = leader.eulerAngles.y;
        Act(leader, move, 0f);
    }

    // ポイントでやることを実行する。全部終わったら次のポイントへ向かう
    private void Act(Transform leader, CS_VillainMove move, float deltaTime)
    {
        List<CSO_VillainPatrolPattern.Action> actions = _route.points[_order[_orderIndex]].actions;
        _actionTimer += deltaTime;

        while (_actionIndex < actions.Count)
        {
            CSO_VillainPatrolPattern.Action action = actions[_actionIndex];
            float duration = action.duration;
            if (_actionTimer < duration)
            {
                Perform(action, _actionTimer / duration, move);
                return;
            }

            // 終わった。向きを変えたら、その向きを次の基準にする
            if (action.type == CSE_VillainPatrolActionType.Turn) _baseYaw += action.angle;
            Perform(action, 1f, move);
            _actionTimer -= duration;
            _actionIndex++;
        }

        _isActing = false;
        _orderIndex = (_orderIndex + 1) % _order.Count;
    }

    // やることを、進み具合rate(0～1)の分だけ行う
    private void Perform(CSO_VillainPatrolPattern.Action action, float rate, CS_VillainMove move)
    {
        move.Stop();
        float yaw = _baseYaw;
        switch (action.type)
        {
            case CSE_VillainPatrolActionType.Look:
                yaw += Mathf.Sin(rate * Mathf.PI * 2f) * action.angle;   // 左右を1往復する
                break;
            case CSE_VillainPatrolActionType.Turn:
                yaw += action.angle * Mathf.Clamp01(rate);
                break;
        }
        move.FaceTowards(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
    }

    // まだ動けるメンバーか(撃退されて倒れている・逃走中は無効になっている)
    private static bool IsActive(CS_VillainPatrol member)
    {
        return member != null && member.isActiveMember;
    }
}
