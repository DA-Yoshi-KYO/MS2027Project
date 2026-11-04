using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/*
 * 巡回エディタのプレビュー(プレイせずにSceneビューで悪人を動かす)と、ルートの道の計算
 * ルート1本分の1周の流れ(移動・やること)を作り、時刻から悪人の位置と向きを求める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・ポイントの間の道は、NavMeshで実際に通る道(NavMesh.CalculatePath)。通れない区間は直線で、isBlockedになる
 *   → Sceneビューでは通れない区間を赤い線で表示する
 * ・1周の流れはゲーム中(CS_VillainPatrolCrew)と同じ
 *   ポイント1から、回り方の順にポイントへ歩き(ルートの速さ)、着いたらやることを上から順に実行する
 *   見回し: 左右を1往復 / 向きを変える: CSO_VillainPatrolPattern.turnDuration秒かけて回る
 * ・1周が終わったら最初から繰り返す
 * ・付いてくる人は、先頭の位置と向きから隊列の位置を求める(壁などは考えない)
 */
// ========================================

public static class CSED_VillainPatrolPreview
{
    // ポイントの間の道1本分
    public class Leg
    {
        public Vector3[] corners;   // 通る点(最初のポイントから次のポイントまで)
        public bool isBlocked;      // NavMeshで通れない(直線で結んでいる)
        public float length;
    }

    // ある時刻の悪人の様子
    public struct Pose
    {
        public Vector3 position;
        public float yaw;        // 向き(度)
        public string label;     // していること(移動 / 停止 / 見回し / 向きを変える)
    }

    private class Step
    {
        public float start;
        public float duration;
        public Leg leg;                                  // 移動の時
        public CSO_VillainPatrolPattern.Action action;   // やることの時
        public Vector3 position;                         // やることをする位置
        public float yaw;                                // やることを始めた時の向き
    }

    // fromからtoまでの道(NavMeshで通れなければ直線)
    public static Leg CalculateLeg(Vector3 from, Vector3 to)
    {
        Leg leg = new Leg();
        NavMeshPath path = new NavMeshPath();
        bool found = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        leg.corners = found ? path.corners : new[] { from, to };
        leg.isBlocked = !found;
        for (int i = 1; i < leg.corners.Length; i++)
        {
            leg.length += Vector3.Distance(leg.corners[i - 1], leg.corners[i]);
        }
        return leg;
    }

    // ポイントのワールド座標(NavMeshの上)
    public static Vector3[] GetPointPositions(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern.Route route)
    {
        Vector3[] positions = new Vector3[route.points.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = owner.GetPointWorldPosition(route, i);
        }
        return positions;
    }

    // 時刻time(秒)の先頭の様子
    public static Pose Evaluate(CS_VillainPatrolRoute owner, CSO_VillainPatrolPattern.Route route, float time)
    {
        Vector3[] points = GetPointPositions(owner, route);
        float startYaw = owner.GetStartRotation(route).eulerAngles.y;
        if (points.Length == 0) return new Pose { position = owner.transform.position, yaw = startYaw, label = "" };

        List<Step> steps = BuildSteps(route, points, startYaw, out float total);
        if (total < 0.01f) return new Pose { position = points[0], yaw = startYaw, label = "" };

        float t = time % total;
        foreach (Step step in steps)
        {
            if (t > step.start + step.duration) continue;

            float rate = step.duration > 0f ? Mathf.Clamp01((t - step.start) / step.duration) : 1f;
            return step.leg != null ? EvaluateMove(step, rate) : EvaluateAction(step, rate);
        }

        Step last = steps[steps.Count - 1];
        return last.leg != null ? EvaluateMove(last, 1f) : EvaluateAction(last, 1f);
    }

    // 先頭の様子から、隊列のfollowerIndex番目の人の位置
    public static Vector3 GetFollowerPosition(CSO_VillainPatrolPattern.Route route, Pose leader, int followerIndex)
    {
        Vector3 offset = CSO_VillainPatrolPattern.GetFormationOffset(route.formation.shape, followerIndex, route.formation.spacing);
        return leader.position + Quaternion.Euler(0f, leader.yaw, 0f) * offset;
    }

    // 1周分の流れを作る
    private static List<Step> BuildSteps(CSO_VillainPatrolPattern.Route route, Vector3[] points, float startYaw, out float total)
    {
        List<Step> steps = new List<Step>();
        Vector3 position = points[0];
        float yaw = startYaw;
        total = 0f;

        foreach (int index in route.GetOrder())
        {
            Leg leg = CalculateLeg(position, points[index]);
            if (leg.length > 0.01f)
            {
                Step move = new Step { start = total, duration = leg.length / route.speed, leg = leg };
                steps.Add(move);
                total += move.duration;
                yaw = GetEndYaw(leg, yaw);
            }
            position = points[index];

            foreach (CSO_VillainPatrolPattern.Action action in route.points[index].actions)
            {
                Step act = new Step { start = total, duration = action.duration, action = action, position = position, yaw = yaw };
                steps.Add(act);
                total += act.duration;
                if (action.type == CSE_VillainPatrolActionType.Turn) yaw += action.angle;
            }
        }
        return steps;
    }

    private static Pose EvaluateMove(Step step, float rate)
    {
        Vector3[] corners = step.leg.corners;
        float distance = step.leg.length * rate;
        for (int i = 1; i < corners.Length; i++)
        {
            float segment = Vector3.Distance(corners[i - 1], corners[i]);
            if (distance > segment && i < corners.Length - 1)
            {
                distance -= segment;
                continue;
            }

            float r = segment > 0f ? Mathf.Clamp01(distance / segment) : 1f;
            return new Pose
            {
                position = Vector3.Lerp(corners[i - 1], corners[i], r),
                yaw = GetYaw(corners[i] - corners[i - 1]),
                label = "移動",
            };
        }
        return new Pose { position = corners[corners.Length - 1], yaw = 0f, label = "移動" };
    }

    private static Pose EvaluateAction(Step step, float rate)
    {
        Pose pose = new Pose { position = step.position, yaw = step.yaw };
        switch (step.action.type)
        {
            case CSE_VillainPatrolActionType.Look:
                pose.yaw += Mathf.Sin(rate * Mathf.PI * 2f) * step.action.angle;
                pose.label = "見回し";
                break;
            case CSE_VillainPatrolActionType.Turn:
                pose.yaw += step.action.angle * rate;
                pose.label = "向きを変える";
                break;
            default:
                pose.label = "停止";
                break;
        }
        return pose;
    }

    // 道の最後の向き(道が無ければ元の向き)
    private static float GetEndYaw(Leg leg, float fallback)
    {
        Vector3[] corners = leg.corners;
        for (int i = corners.Length - 1; i > 0; i--)
        {
            Vector3 direction = corners[i] - corners[i - 1];
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) return GetYaw(direction);
        }
        return fallback;
    }

    private static float GetYaw(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction).eulerAngles.y : 0f;
    }
}
