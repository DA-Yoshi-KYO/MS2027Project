using UnityEngine;

/*
 * ランダムイベントの発生地点(イベントスポット)をミニマップ(CS_MiniMapController)へ登録するクラス
 * イベントが始まったら発生地点を登録し、終わったら解除する(位置の更新はCS_MiniMapController側が行う)
 * ミニマップ側・イベント側はこのクラスの存在を知らない
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/ミニマップの使い方.md参照。発生地点(CS_RandomEventPoint)を RandomEvent として登録する
 *   開始時 : RegisterRandomEvent(id, 発生地点のtransform)
 *   終了時 : UnregisterRandomEvent(id)
 * ・CS_RandomEventManagerの onEventStarted / onEventEnded はサーバー・クライアントの両方で呼ばれるので、
 *   各クライアントがそれぞれ自分のミニマップに登録する
 * ・idは開催ごとの番号(context.id。サーバー・クライアントで同じ値)。同じ地点で2回目が起きても区別できる
 * ・CS_RandomEventManagerと同じオブジェクトに付ける(MultiGameObjects.prefab)
 * ・シーンにCS_MiniMapControllerが無ければ何もしない(ミニマップの無いテストシーンでも動く)
 * ・途中参加したクライアントには参加前に始まったイベントが通知されないため、そのイベントはミニマップに出ない
 */
// ========================================

public class CS_RandomEventMiniMapIcon : MonoBehaviour
{
    private CS_MiniMapController _miniMap;

    private void OnEnable()
    {
        CS_RandomEventManager.onEventStarted += HandleEventStarted;
        CS_RandomEventManager.onEventEnded += HandleEventEnded;
    }

    private void OnDisable()
    {
        CS_RandomEventManager.onEventStarted -= HandleEventStarted;
        CS_RandomEventManager.onEventEnded -= HandleEventEnded;
    }

    private void HandleEventStarted(CS_RandomEventContext context)
    {
        if (context.point == null) return;

        if (_miniMap == null) _miniMap = FindAnyObjectByType<CS_MiniMapController>();
        if (_miniMap == null) return;

        _miniMap.RegisterRandomEvent(GetMiniMapId(context), context.point.transform);
    }

    // 解除しないと、アイコンが残り続ける
    private void HandleEventEnded(CS_RandomEventContext context)
    {
        if (_miniMap == null) return;

        _miniMap.UnregisterRandomEvent(GetMiniMapId(context));
    }

    private static string GetMiniMapId(CS_RandomEventContext context) => $"RandomEvent_{context.id}";
}
