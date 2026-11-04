/*
 * プレイヤーの警戒状態(CS_PlayerAlertが使う)
 *
 * 制作者：　秋野翔太
 */

public enum CSE_PlayerAlertState
{
    None,       // なし(警察陣営に見つかっていない)
    Detected,   // 発見中(警察陣営に今まさに見られている)
    Searching,  // 捜索中(見失われてから解除されるまでの間)
}
