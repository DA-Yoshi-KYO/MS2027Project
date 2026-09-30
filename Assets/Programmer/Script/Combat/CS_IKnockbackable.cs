using UnityEngine;

/*
 * ノックバックを受けられるもの(敵など)が実装するインターフェース
 * プレイヤーの攻撃・警察・爆発アイテムなどは、このインターフェースを持つものにノックバックを呼び出す
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・呼び出し側の例(攻撃が当たった相手に、ダメージの後で呼ぶ)
 *     if (target is IKnockbackable knockbackable)
 *     {
 *         knockbackable.Knockback(transform.position);        // 標準の距離だけ下がる
 *         knockbackable.Knockback(explosionCenter, 3f);        // 爆発など強い攻撃は倍率を上げる
 *     }
 * ・どれだけ下がるか(標準の距離・時間)は受ける側が決める。呼び出し側は倍率だけ指定する
 * ・受ける側の都合(攻撃中のスーパーアーマーなど)で、呼んでもノックバックしないことがある
 * ※ ネットワーク対戦の場合、Knockbackはサーバーで呼ぶ(IDamageable.TakeDamageと同じ)
 */
// ========================================

public interface IKnockbackable
{
    // sourcePosition(攻撃した位置)から離れる方向へノックバックする
    // power: 受ける側の標準の距離に掛ける倍率(1で標準)
    void Knockback(Vector3 sourcePosition, float power = 1f);
}
