/*
 * プレイヤーの変身状態(CS_PlayerTransformationが使う)
 *
 * 制作者：　秋野翔太
 */

public enum CSE_PlayerTransformState
{
    Normal,         // 通常(変身していない)
    Transforming,   // 変身途中(無敵)
    Transformed,    // 変身完了
}
