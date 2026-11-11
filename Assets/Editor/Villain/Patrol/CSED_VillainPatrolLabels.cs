/*
 * 巡回エディタで表示する日本語の名前(やること・回り方・隊列の形)
 *
 * 制作者：　中出峻輔
 */

public static class CSED_VillainPatrolLabels
{
    // CSE_VillainFormationShapeの並びと同じ順
    public static readonly string[] formationShapeNames = { "横一列", "縦一列", "斜め右", "斜め左", "V字" };

    public static string GetActionName(CSE_VillainPatrolActionType type)
    {
        switch (type)
        {
            case CSE_VillainPatrolActionType.Look: return "見回し";
            case CSE_VillainPatrolActionType.Turn: return "向きを変える";
            default: return "停止";
        }
    }

    public static string GetRouteName(int routeIndex) => $"悪人{routeIndex + 1}";
}
