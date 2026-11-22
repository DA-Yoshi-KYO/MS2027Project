/*
 * パスをたどる動き方(CS_PathFollowerの動き方)
 *
 * 制作者：　吉田京志郎
 */

public enum CSE_PathFollowMode
{
    Once,       // 一方向: 最初の点から最後の点まで行って、そこで止まる
    Loop,       // 周回: 最後の点から最初の点へ戻る曲線もつなげて、ぐるぐる回る
    PingPong,   // 往復: 最後の点まで行ったら、同じ道を通って最初の点へ戻る
}
