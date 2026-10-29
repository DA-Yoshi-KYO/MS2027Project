using UnityEngine;

/*
 * プレイヤーが出てきた時(スポーン)と、気絶から復活した時(リスポーン)に、足元へエフェクトを出すクラス
 * CS_PlayerHealthの生死の状態を見て、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト(見た目はDB_EffectDataBaseで決める)
 *   スポーン   : PlayerSpawn(最初のフレームに出す)
 *   リスポーン : PlayerRespawn(気絶している状態から、気絶していない状態に戻った時に出す)
 *   どちらもプレイヤーの足元に出して付いていく
 * ・スポーン・生死の状態は全員に同期されている(NetworkVariable)ので、各クライアントが自分の画面に出す(通信しない)
 * ・生死の状態は毎フレーム前の状態と比べて調べる(オフラインでは変更の通知が来ないため)
 *   最初のフレームは、同期された状態を覚えるだけにする(途中から参加した時に、気絶中のプレイヤーで出ないように)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerSpawnEffect : MonoBehaviour
{
    private CS_PlayerHealth _health;
    private bool _hasChecked;
    private bool _wasDead;

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
    }

    private void Start()
    {
        Play(CSE_EffectId.PlayerSpawn);
    }

    private void LateUpdate()
    {
        bool isDead = _health.isDead;
        bool isRevived = _hasChecked && _wasDead && !isDead;
        _wasDead = isDead;
        _hasChecked = true;

        if (isRevived) Play(CSE_EffectId.PlayerRespawn);
    }

    private void Play(CSE_EffectId id)
    {
        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        manager.Play(id, transform.position, transform.rotation, transform);
    }
}
