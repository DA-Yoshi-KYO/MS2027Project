/*
 * NPCの行動1つ分の基底クラス(悪人を倒す、アイテムを拾う、など)
 * CS_NpcBrainが一定間隔で全行動のEvaluateを比べ、一番大きい行動を選んで毎フレームTickを呼ぶ(ユーティリティAI)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・行動を追加するときは、このクラスを継承したクラスを作り、CS_NpcBrainの行動一覧(CreateActions)に加える
 *   行動同士は互いを知らないので、追加・削除しても他の行動に影響しない
 * ・Evaluate : 今この行動をやる価値。性格の重み(CSO_NpcPersonalityの〇〇Weight)を掛けた値を返す(0以下なら選ばれない)
 * ・Tick     : 選ばれている間、毎フレーム呼ばれる。brainのMoveTo / FaceTowards / Press〇〇で動きを指示する
 *              (ボタンはCS_NpcBrainが入力としてCS_Playerへ渡すので、人と同じ処理・同じ制限で動く)
 */
// ========================================

public abstract class CS_NpcAction
{
    // デバッグ表示用の名前
    public abstract string name { get; }

    // 今この行動をやる価値(性格の重み込み)。0以下なら選ばれない
    public abstract float Evaluate(CS_NpcBrain brain);

    // この行動に切り替わった時
    public virtual void OnEnter(CS_NpcBrain brain) { }

    // 選ばれている間、毎フレーム呼ばれる
    public abstract void Tick(CS_NpcBrain brain);

    // 距離が近いほど1に近い値(範囲の外なら0)。近くの相手ほど優先するのに使う
    protected static float Closeness(float distance, float range)
    {
        if (range <= 0f || distance > range) return 0f;

        return 0.5f + 0.5f * (1f - distance / range);
    }
}
