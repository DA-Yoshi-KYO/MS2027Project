using UnityEngine;

/*
 * プレイヤーが攻撃を受けた時に、体の位置へエフェクトを出すクラス
 * CS_PlayerHealthのHPが減ったのを見て、CS_EffectManagerで自分の画面に出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても動きは変わらない)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト: PlayerDamage(見た目はDB_EffectDataBaseで決める)
 *   悪人・警察・防犯装置など、何に攻撃されても同じものを出す
 * ・HPは全員に同期されている(NetworkVariable)ので、各クライアントが自分の画面に出す(通信しない)
 * ・HPが減ったかは毎フレーム前のHPと比べて調べる
 *   (オフラインやスポーン時の同期では変更の通知が来ないことがあるため。回復・復活では出さない)
 * ・出す位置は体の中心(Colliderがあればその中心、無ければ足元から Height の高さ)
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerDamageEffect : MonoBehaviour
{
    [SerializeField] private float _height = 1f;   // Colliderが無い時に、エフェクトを出す高さ(足元から。m)

    private CS_PlayerHealth _health;
    private Collider _bodyCollider;
    private float _lastHp;

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
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

        manager.Play(CSE_EffectId.PlayerDamage, GetBodyCenter(), transform.rotation);
    }

    private Vector3 GetBodyCenter()
    {
        if (_bodyCollider != null && _bodyCollider.enabled) return _bodyCollider.bounds.center;

        return transform.position + Vector3.up * _height;
    }
}
