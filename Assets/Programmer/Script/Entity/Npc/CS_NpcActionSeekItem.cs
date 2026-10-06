using UnityEngine;

/*
 * NPCの行動: 近くのアイテムを拾いに行く
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・アイテムは触れると自動で拾われる(CS_ItemBase)ので、アイテムの位置まで移動するだけ
 * ・スロットが埋まっている間は選ばない(拾えないため)
 * ・拾ったアイテムを使う判断はCS_NpcBrain(UseItemIfUseful)が行う
 */
// ========================================

public class CS_NpcActionSeekItem : CS_NpcAction
{
    public override string name => "アイテムを拾う";

    public override float Evaluate(CS_NpcBrain brain)
    {
        if (brain.itemSlot != null && brain.itemSlot.hasItem) return 0f;

        float range = brain.personality.itemSightRange;
        CS_ItemBase item = brain.sensor.FindNearestItem(brain.position, range);
        if (item == null) return 0f;

        float distance = Vector3.Distance(brain.position, item.transform.position);
        return Closeness(distance, range) * brain.personality.seekItemWeight;
    }

    public override void Tick(CS_NpcBrain brain)
    {
        CS_ItemBase item = brain.sensor.FindNearestItem(brain.position, brain.personality.itemSightRange);
        if (item == null)
        {
            brain.StopMoving();
            return;
        }

        brain.MoveTo(item.transform.position);
    }
}
