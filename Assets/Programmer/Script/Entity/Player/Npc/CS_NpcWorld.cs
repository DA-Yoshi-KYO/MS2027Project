using System.Collections.Generic;
using UnityEngine;

/*
 * NPCが判断に使う、フィールド上の悪人・警察・アイテムの一覧
 * 全NPCで共有し、一定間隔でだけ取り直す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・悪人・警察・アイテムには「出現中の一覧」が無いため、_refreshInterval秒ごとにFindObjectsByTypeで取り直す
 *   (コーディング規則「Findの必要性を検討」: 毎フレームは探さない。NPCが何人いても1秒に1回だけ)
 *   取り直すまでの間に消えたものは、使う側でnullチェックする(Destroyされたものは == null になる)
 * ・プレイヤーはCS_PlayerWantedLevel.activePlayers、イベント(ケアパッケージなど)はCS_InteractableRegistryを直接使う
 * ・NPCを動かすサーバー(またはオフライン)でだけ使う
 */
// ========================================

public static class CS_NpcWorld
{
    private const float _refreshInterval = 1f;

    private static readonly List<CS_VillainHealth> _villains = new List<CS_VillainHealth>();
    private static readonly List<CS_PoliceBrain> _police = new List<CS_PoliceBrain>();
    private static readonly List<CS_ItemBase> _items = new List<CS_ItemBase>();
    private static float _nextRefreshTime = float.MinValue;

    public static IReadOnlyList<CS_VillainHealth> villains { get { Refresh(); return _villains; } }
    public static IReadOnlyList<CS_PoliceBrain> police { get { Refresh(); return _police; } }
    public static IReadOnlyList<CS_ItemBase> items { get { Refresh(); return _items; } }

    // Domain Reloadを切っている場合に、前回の再生の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _villains.Clear();
        _police.Clear();
        _items.Clear();
        _nextRefreshTime = float.MinValue;
    }

    // 前回から_refreshInterval秒経っていれば取り直す
    private static void Refresh()
    {
        if (Time.time < _nextRefreshTime) return;

        _nextRefreshTime = Time.time + _refreshInterval;

        _villains.Clear();
        _villains.AddRange(Object.FindObjectsByType<CS_VillainHealth>(FindObjectsSortMode.None));
        _police.Clear();
        _police.AddRange(Object.FindObjectsByType<CS_PoliceBrain>(FindObjectsSortMode.None));
        _items.Clear();
        _items.AddRange(Object.FindObjectsByType<CS_ItemBase>(FindObjectsSortMode.None));
    }
}
