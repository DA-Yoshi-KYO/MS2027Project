using UnityEngine;

/*
 * 悪人が攻撃を受けた時に、体の位置へエフェクト(火花など)を出すクラス
 * CS_VillainHealthのHPが減ったのを見て、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: VillainDamage(見た目はDB_EffectDataBaseで決める)
 *   通常攻撃・必殺技・奇襲など、何で攻撃されても同じものを出す
 * ・HPは全員に同期されている(NetworkVariable)ので、各クライアントが自分の画面に出す(通信しない)
 * ・HPが減ったかは毎フレーム前のHPと比べて調べる
 *   (オフラインでは変更の通知が来ないため。スポーン時にHPが入った時は増えるだけなので出ない)
 * ・出す位置は体の中心(Colliderがあればその中心、無ければ足元から Height の高さ)
 *   倒れるとColliderが無効になるので、その時は Height の高さに出す
 */
// ========================================

[RequireComponent(typeof(CS_VillainHealth))]
public class CS_VillainDamageEffect : MonoBehaviour
{
    [SerializeField] private float _height = 1f;   // Colliderが無い時に、エフェクトを出す高さ(足元から。m)

    private CS_VillainHealth _health;
    private Collider _bodyCollider;
    private float _lastHp;

    private void Awake()
    {
        _health = GetComponent<CS_VillainHealth>();
        _bodyCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _lastHp = _health.currentHp;
    }

    private void LateUpdate()
    {
        float currentHp = _health.currentHp;
        bool isDamaged = currentHp < _lastHp;
        _lastHp = currentHp;

        if (!isDamaged) return;

        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        manager.Play(CSE_EffectId.VillainDamage, GetBodyCenter(), transform.rotation);
    }

    private Vector3 GetBodyCenter()
    {
        if (_bodyCollider != null && _bodyCollider.enabled) return _bodyCollider.bounds.center;

        return transform.position + Vector3.up * _height;
    }
}
