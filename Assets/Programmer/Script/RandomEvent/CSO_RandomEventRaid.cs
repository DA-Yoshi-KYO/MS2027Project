using Unity.Netcode;
using UnityEngine;

/*
 * ランダムイベント「レイド」
 * 発生地点に強い個体(ボス)が出現し、倒すと貢献度に応じてスコアが配分される
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・ボスは発生地点の中心に1体だけ生成する(手下はいない)
 *   生成はCS_VillainSpawner.SpawnEventGroupで、専用のプレハブ(VillainBoss)を使う
 *   ステータスはプレハブのBase Stats(DB_VillainStatsBoss: HP30・攻撃力1)のまま(時間経過の段階では変えない)
 * ・ボスはイベントの範囲(発生地点のradius)の中に留まる
 *   範囲の中に入ったプレイヤーに反応し、範囲の外へは追わない(CS_VillainCombat.SetEventAreaのconfine)
 * ・範囲の中で警察に探知されないかは、イベント共通のBlock Police Detectionで設定する(レイドはオン)
 * ・スコアの配分はボス側(CS_VillainRaidBoss)が倒された時に行う
 * ・終わり方
 *   倒された   : 時間切れを待たずに終わる(配分はCS_VillainRaidBoss)
 *   時間切れ   : ボスが逃げる(犯罪完遂と同じく、スコアが減ってフェードアウトして消える)
 * ・シーンにCS_VillainSpawnerが無い場合は、何も生成せずにすぐ終わる
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventRaid", menuName = "RandomEvent/Raid")]
public class CSO_RandomEventRaid : CSO_RandomEvent
{
    [Header("レイド")]
    [SerializeField]
    [Tooltip("ボスのプレハブ(VillainBoss)。CS_VillainRaidBossを付けておく")]
    private NetworkObject _bossPrefab;

    private static bool _hasWarnedNoSpawner;   // スポナーが無い警告を出したか

    public override void OnStart(CS_RandomEventContext context)
    {
        CS_VillainSpawner spawner = FindAnyObjectByType<CS_VillainSpawner>();
        if (spawner == null || _bossPrefab == null)
        {
            if (!_hasWarnedNoSpawner) Debug.LogWarning($"CSO_RandomEventRaid: スポナーが無い、またはボスのプレハブが未設定のため、ボスを生成できません({name})");
            _hasWarnedNoSpawner = true;
            return;
        }

        Vector3 center = context.point.transform.position;
        CS_VillainGroup group = spawner.SpawnEventGroup(center, 0f, 1, _bossPrefab, false);
        if (group == null) return;

        context.state = group;
        foreach (CS_VillainCrime member in group.members)
        {
            if (member != null && member.TryGetComponent(out CS_VillainCombat combat)) combat.SetEventArea(center, context.point.radius, true);
        }
    }

    // ボスが倒されたら(全員いなくなったら)終わる
    public override bool IsFinished(CS_RandomEventContext context)
    {
        return !(context.state is CS_VillainGroup group) || !group.isAlive;
    }

    // 時間切れの時は、ボスが逃げる(犯罪完遂と同じ。倒されていれば何もしない)
    public override void OnEnd(CS_RandomEventContext context)
    {
        if (context.state is CS_VillainGroup group) group.CompleteCrime();
    }
}
