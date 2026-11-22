/*
 * 往復の帰り道の動き方(CS_PathFollowerPointの、区間の帰りの進み方の選び方)
 *
 * 制作者：　吉田京志郎
 */

public enum CSE_PathFollowEaseMode
{
    Copy,       // 行きと同じカーブを、帰りの進む向きにそのまま使う(行きの出発がゆっくりなら、帰りも出発がゆっくり)
    Inverse,    // 行きの動きの逆再生(行きの出発がゆっくりなら、帰りは到着がゆっくり)
    Custom,     // 帰り専用のカーブを使う
}
