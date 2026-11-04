using System.Collections.Generic;
using UnityEngine;

/*
 * ランダムイベント「大量発生」
 * 発生地点の周りに、悪人のグループをまとめて生成する
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・groupCount 個のグループを、発生地点の範囲(radius × groupPlaceRadiusRate)の円周上に等間隔で生成する
 *   1グループの人数は membersPerGroup(0以下なら、時間経過の段階の人数)。HPなども時間経過の段階に従う
 *   生成はCS_VillainSpawner.SpawnEventGroupで行う(通常のグループ数には数えない)
 * ・イベント中は、イベントの範囲(発生地点の radius)を路地裏として扱う(CS_VillainCombat.SetEventArea)
 *   プレイヤーが範囲の外に出ると、通常の路地裏と同じく一定時間であきらめて戻る
 * ・イベントの悪人は、途中で犯罪を進めない(通常の悪人の犯罪完遂時間より、イベントの方が長いため)
 * ・終わり方
 *   全てのグループが全滅した : 時間切れを待たずに終わる(ペナルティなし)
 *   時間切れ(Duration)        : 生き残っているグループの数だけ犯罪を完遂する(1人でも残っていれば1グループ)
 *                               → 犯罪完遂の通知(CS_VillainGroup.onAnyCrimeCompleted)で、通常と同じくスコアが減る
 *                               → 完遂したグループの悪人は、通常と同じくフェードアウトして消える
 * ・シーンにCS_VillainSpawnerが無い場合は、何も生成せずにすぐ終わる
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventMassSpawn", menuName = "RandomEvent/Mass Spawn")]
public class CSO_RandomEventMassSpawn : CSO_RandomEvent
{
    [Header("大量発生")]
    [SerializeField, Min(1)]
    [Tooltip("生成するグループの数")]
    private int _groupCount = 4;

    [SerializeField, Min(0)]
    [Tooltip("1グループの人数。0なら時間経過の段階の人数")]
    private int _membersPerGroup;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("グループを並べる円の半径(発生地点の範囲に対する割合)")]
    private float _groupPlaceRadiusRate = 0.6f;

    [SerializeField, Min(0f)]
    [Tooltip("1グループのメンバーを並べる円の半径(m)")]
    private float _memberRadius = 1.5f;

    private static bool _hasWarnedNoSpawner;   // スポナーが無い警告を出したか

    public override void OnStart(CS_RandomEventContext context)
    {
        List<CS_VillainGroup> groups = new List<CS_VillainGroup>();
        context.state = groups;

        CS_VillainSpawner spawner = FindAnyObjectByType<CS_VillainSpawner>();
        if (spawner == null)
        {
            if (!_hasWarnedNoSpawner) Debug.LogWarning("CSO_RandomEventMassSpawn: シーンにCS_VillainSpawnerが無いため、悪人を生成できません");
            _hasWarnedNoSpawner = true;
            return;
        }

        Vector3 center = context.point.transform.position;
        float placeRadius = context.point.radius * _groupPlaceRadiusRate;
        for (int i = 0; i < _groupCount; i++)
        {
            Vector3 groupCenter = center;
            if (_groupCount > 1)
            {
                float angle = 360f / _groupCount * i;
                groupCenter += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * placeRadius;
            }

            CS_VillainGroup group = spawner.SpawnEventGroup(groupCenter, _memberRadius, _membersPerGroup);
            if (group == null) continue;

            SetEventArea(group, center, context.point.radius);
            groups.Add(group);
        }
    }

    // 全てのグループが全滅したら、時間切れを待たずに終わる
    public override bool IsFinished(CS_RandomEventContext context)
    {
        if (!(context.state is List<CS_VillainGroup> groups)) return true;

        return !groups.Exists(group => group.isAlive);
    }

    // 時間切れで終わった時は、生き残っているグループの数だけ犯罪を完遂する(全滅していれば何もしない)
    public override void OnEnd(CS_RandomEventContext context)
    {
        if (!(context.state is List<CS_VillainGroup> groups)) return;

        foreach (CS_VillainGroup group in groups)
        {
            group.CompleteCrime();
        }
    }

    // イベントの範囲を、グループの全員に路地裏として扱わせる
    private static void SetEventArea(CS_VillainGroup group, Vector3 center, float radius)
    {
        foreach (CS_VillainCrime member in group.members)
        {
            if (member != null && member.TryGetComponent(out CS_VillainCombat combat)) combat.SetEventArea(center, radius);
        }
    }
}
