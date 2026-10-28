using UnityEngine;

/*
 * ParticleSystem・VFX Graph以外で動くエフェクト(仮エフェクト用のスクリプトなど)が実装するインターフェース
 * CS_EffectManagerが、ParticleSystemと同じように再生・停止・終わったかの確認をする
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・エフェクトのPrefab(またはその子)のMonoBehaviourに実装する
 *   例: CS_EffectSlash(TrailRendererで斬撃の軌跡を描く仮エフェクト)
 * ・Prefabはプールして使い回すので、Playのたびに最初の状態からやり直すこと
 */
// ========================================

public interface IEffectPlayable
{
    // 最初から再生する
    void Play();

    // 新しく出すのをやめる(出ている分は自然に消えるまで残してよい)
    void Stop();

    // 色を変える(Override Color・呼ぶ側から色を渡された時だけ呼ばれる)
    void SetColor(Color color);

    // まだ見えているか(falseになったらプールに戻す)
    bool isAlive { get; }
}
