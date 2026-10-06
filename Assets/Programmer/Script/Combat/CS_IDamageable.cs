/*
 * ダメージを受けられるもの(敵など)が実装するインターフェース
 * 攻撃判定はこのインターフェースを持つものにだけダメージ処理を呼び出す
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * 敵側は、このインターフェースを実装するだけで攻撃を受けられる
 *   public class CS_Enemy : MonoBehaviour, IDamageable
 *   {
 *       public void TakeDamage(float damage) { ... }
 *   }
 * ※ ネットワーク対戦の場合、TakeDamageはサーバーで呼ばれる
 *    (HPをNetworkVariableで持つ場合は、サーバーが値を変更する)
 * ・攻撃者付きのTakeDamage(damage, attacker)もある
 *   誰の攻撃かを知りたい受け手(プレイヤーのHP: 倒された時のスコア計算に使う)だけがoverrideする
 *   overrideしない受け手は、攻撃者を無視して通常のTakeDamage(damage)が呼ばれるので、実装しなくてよい
 */
// ========================================

public interface IDamageable
{
    // ダメージを受ける
    void TakeDamage(float damage);

    // 攻撃者付きでダメージを受ける(既定では攻撃者を無視して上のTakeDamageを呼ぶ)
    void TakeDamage(float damage, UnityEngine.GameObject attacker) => TakeDamage(damage);
}
