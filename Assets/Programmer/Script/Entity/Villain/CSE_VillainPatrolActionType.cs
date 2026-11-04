/*
 * 巡回のポイントで「やること」の種類
 * CSO_VillainPatrolPatternが持ち、CS_VillainPatrolと巡回エディタのプレビューが使う
 *
 * 制作者：　中出峻輔
 */

public enum CSE_VillainPatrolActionType : byte
{
    Stop,   // 停止: seconds秒その場で止まる
    Look,   // 見回し: seconds秒かけて、左右にangle度ずつ首を振る
    Turn,   // 向きを変える: angle度向きを変える(＋で右回り)
}
