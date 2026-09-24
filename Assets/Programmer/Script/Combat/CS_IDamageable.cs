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
 */
// ========================================

public interface IDamageable
{
    // ダメージを受ける
    void TakeDamage(float damage);
}
