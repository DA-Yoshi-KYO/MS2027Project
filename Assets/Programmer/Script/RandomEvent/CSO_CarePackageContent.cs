using UnityEngine;

/*
 * ケアパッケージの中身1種類分の共通の形(ScriptableObject)
 * 中身(アイテムドロップ・ステータスアップなど)は、これを継承して作る
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ■ 新しい中身の作り方
 *   1. CSO_CarePackageContentを継承したクラスを作り、Openで中身の処理を書く
 *        [CreateAssetMenu(menuName = "RandomEvent/CarePackage/Heal All")]
 *        public class CSO_CarePackageHealAll : CSO_CarePackageContent
 *        {
 *            public override void Open(CS_CarePackageOpening opening) { ...opening.openerやopening.positionを使う... }
 *        }
 *   2. 右クリック → Create から中身のアセットを作り、ケアパッケージのイベント(DB_RandomEventCarePackage)の中身の候補に入れる
 *
 * ■ 呼ばれる順番(すべてサーバー、またはオフライン)
 *   箱が開封された時に Open → 終わる条件を満たすまで(IsFinished) → イベントが終わる時に OnEnd
 *   ・IsFinishedは初期状態でtrue(開けてすぐ終わる)。置いたアイテムが拾われるまで続く、などの中身はoverrideする
 *   ・イベントの終了時間(Duration)が来たら、IsFinishedに関係なく終わる
 *
 * ■ 注意
 *   アセットは共有されるので、開封ごとの状態はこのクラスのフィールドに持たない(CS_CarePackageOpening.stateに入れる)
 */
// ========================================

public abstract class CSO_CarePackageContent : ScriptableObject
{
    // 箱が開封された時の処理(サーバー、またはオフライン)
    public abstract void Open(CS_CarePackageOpening opening);

    // 中身の処理が終わったか(サーバー、またはオフライン)。初期状態は開けてすぐ終わる
    public virtual bool IsFinished(CS_CarePackageOpening opening)
    {
        return true;
    }

    // イベントが終わる時の後片付け(サーバー、またはオフライン)
    public virtual void OnEnd(CS_CarePackageOpening opening)
    {
    }
}
