using System.Collections.Generic;
using UnityEngine;

/*
 * NPCが周りの状況(悪人・アイテム・警察・他のプレイヤー)を把握するクラス
 * CS_NpcBrainが持ち、各行動(CS_NpcAction)が問い合わせる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・場にいるものの一覧は_refreshInterval秒ごとにまとめて取り直す(毎フレーム探すと重いため)
 *   一覧を取ってから倒された・拾われたものは、問い合わせの時に除外する
 * ・「見える範囲」は性格(CSO_NpcPersonality)ごとに違う。問い合わせ側が範囲を渡す
 *   (今は距離だけで判定しており、壁越しでも分かる。必要になったら視線の判定を足す)
 * ・悪人・警察・アイテムのコードには手を入れず、各コンポーネントを探して読むだけにしている
 */
// ========================================

public class CS_NpcSensor
{
    private const float _refreshInterval = 1f;

    private readonly List<CS_VillainHealth> _villains = new List<CS_VillainHealth>();
    private readonly List<CS_ItemBase> _items = new List<CS_ItemBase>();
    private readonly List<CS_PoliceBrain> _police = new List<CS_PoliceBrain>();
    private readonly List<CS_Player> _players = new List<CS_Player>();

    private float _refreshTimer;

    public IReadOnlyList<CS_Player> players => _players;

    // 一定間隔で、場にいるものの一覧を取り直す
    public void Update(float deltaTime)
    {
        _refreshTimer -= deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = _refreshInterval;

        Refresh(_villains);
        Refresh(_items);
        Refresh(_police);
        Refresh(_players);
    }

    // 範囲内で一番近い、生きている悪人
    public CS_VillainHealth FindNearestVillain(Vector3 position, float range)
    {
        return FindNearest(_villains, position, range, villain => !villain.isDefeated);
    }

    // 範囲内にいる、生きている悪人の数
    public int CountVillains(Vector3 position, float range)
    {
        int count = 0;
        float sqrRange = range * range;
        foreach (CS_VillainHealth villain in _villains)
        {
            if (villain == null || villain.isDefeated) continue;
            if ((villain.transform.position - position).sqrMagnitude <= sqrRange) count++;
        }

        return count;
    }

    // 範囲内で一番近いアイテム
    public CS_ItemBase FindNearestItem(Vector3 position, float range)
    {
        return FindNearest(_items, position, range, null);
    }

    // 範囲内で一番近い警察
    public CS_PoliceBrain FindNearestPolice(Vector3 position, float range)
    {
        return FindNearest(_police, position, range, null);
    }

    // 範囲内で一番近い、生きている他のプレイヤー(自分を除く)
    public CS_Player FindNearestOtherPlayer(CS_Player self, float range)
    {
        return FindNearest(_players, self.transform.position, range,
            player => player != self && !player.GetComponent<CS_PlayerHealth>().isDead);
    }

    private static void Refresh<T>(List<T> list) where T : Object
    {
        list.Clear();
        list.AddRange(Object.FindObjectsByType<T>(FindObjectsSortMode.None));
    }

    private static T FindNearest<T>(List<T> list, Vector3 position, float range, System.Predicate<T> filter) where T : Component
    {
        T nearest = null;
        float nearestSqr = range * range;

        foreach (T candidate in list)
        {
            if (candidate == null) continue;   // 一覧を取ってから消えたもの
            if (filter != null && !filter(candidate)) continue;

            float sqr = (candidate.transform.position - position).sqrMagnitude;
            if (sqr > nearestSqr) continue;

            nearest = candidate;
            nearestSqr = sqr;
        }

        return nearest;
    }
}
