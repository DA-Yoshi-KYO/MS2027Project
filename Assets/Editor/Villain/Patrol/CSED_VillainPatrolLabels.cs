/*
 * 巡回エディタで表示する日本語の名前(やること・回り方・隊列の形・犯罪モーション)
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

    // CSE_VillainCrimeMotionの並びと同じ順
    public static readonly string[] crimeMotionNames = { "エリアに合わせる", "蹴る", "運ぶ", "いじる" };

    public static string GetRouteName(int routeIndex) => $"悪人{routeIndex + 1}";

    public static string GetExecutorName(int executorIndex) => $"実行役{executorIndex + 1}";

    public static string GetCrimeMotionName(CSE_VillainCrimeMotion motion)
    {
        int index = (int)motion;
        return index >= 0 && index < crimeMotionNames.Length ? crimeMotionNames[index] : motion.ToString();
    }

    public static string GetCrimeActionName(CSE_VillainCrimeAction action)
    {
        switch (action)
        {
            case CSE_VillainCrimeAction.Kick: return "蹴る";
            case CSE_VillainCrimeAction.Tamper: return "いじる";
            case CSE_VillainCrimeAction.PickUp: return "拾う";
            case CSE_VillainCrimeAction.Carry: return "運ぶ";
            case CSE_VillainCrimeAction.PutDown: return "置く";
            default: return "";
        }
    }
}
