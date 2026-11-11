/* ================================================
 *
 * ================================================
 * 制作者：KR
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * 2026-10-30 | 狙った相手へ向かって曲がるホーミングを追加
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/*
 * 投げられた爆弾の実体
 * 投げてから_fuseTime秒後に爆発し、自身を破棄(Despawn)する(着弾していなくても爆発する)
 *
 * 制作者：　KR
 */

// ========================================
/*
 * メモ
 * ・CSO_ItemEffectThrowが生成し、Launch()で初速を与える(サーバー、またはオフラインで実行される)
 * ・投げた本人とは衝突しない(手元で当たって即着弾するのを防ぐため)
 *   爆発の範囲に本人がいればダメージは受ける(フレンドリーファイアは常に有効)
 * ・爆発の処理はCSO_ItemEffectExplodeに任せる(爆弾の位置を中心に範囲ダメージ)
 * ・着弾の判定・爆発はサーバーのみが行う。位置はNetworkTransformで同期する
 *   サーバー以外では物理で動かさない(Rigidbodyをkinematicにする)
 *   ※ NetworkRigidbodyはオフライン時にもkinematicにしてしまい投げられなくなるため使わない
 * ・ホーミング: Launch()で狙う相手を渡されると、爆発するまで、重力と回転を切って、相手の体の高さ(_homingAimHeight)へ
 *   向かって、_homingSpeedの速さで飛び続ける(進む向きは_homingTurnSpeed度/秒で相手へ曲げる。誘導ミサイルのような動き)
 *   転がって見えると追尾に見えないので、爆弾は回転させず、進む向きへ向ける
 *   相手がいなくなったら、重力と回転を元に戻して、そのまま落ちる
 * ・爆発は、着弾を待たずに、投げた瞬間から_fuseTime秒後に起きる(どこにも当たらず落ち続けても爆発する)
 * ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
 */
// ========================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class CS_ThrownBomb : NetworkBehaviour
{
    [SerializeField][Tooltip("投げてから爆発するまでの時間(秒)。空中でも、この時間が来たら爆発する")][Min(0f)] private float _fuseTime = 2f;
    [SerializeField][Tooltip("爆発の効果")] private CSO_ItemEffectExplode _explodeEffect;
    [SerializeField][Tooltip("ホーミングで狙う高さ(相手の足元からの高さ。相手の体の真ん中あたり)")][Min(0f)] private float _homingAimHeight = 1f;

    private Rigidbody _rigidbody;
    private float _elapsed;         // 投げてからの時間
    private Transform _homingTarget;    // ホーミングで狙っている相手(いなければnull)
    private float _homingTurnSpeed;     // ホーミングで曲がる速さ(度/秒)
    private float _homingSpeed;         // ホーミング中の速さ(m/秒)
    private bool _isHoming;             // ホーミングで飛んでいるか(重力と回転を切っているか)

    // このマシンが爆弾を動かす権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (_explodeEffect != null) return;

        Debug.LogError("CS_ThrownBomb: Explode Effect が未設定です", this);
    }

    public override void OnNetworkSpawn()
    {
        // 位置はNetworkTransformが更新するので、サーバー以外では物理で動かさない
        if (IsServer) return;

        _rigidbody.isKinematic = true;
    }

    // 初速を与えて投げる(サーバー、またはオフラインで実行される)
    // homingTargetを渡すと、爆発するまで、その相手へ向かって飛び続ける(nullなら、山なりに飛ぶ)
    public void Launch(Vector3 velocity, GameObject thrower, Transform homingTarget = null, float homingTurnSpeed = 0f, float homingSpeed = 0f)
    {
        IgnoreCollisionWith(thrower);
        _rigidbody.linearVelocity = velocity;
        _homingTarget = homingTarget;
        _homingTurnSpeed = homingTurnSpeed;
        _homingSpeed = homingSpeed;

        // ホーミングする時は、重力と回転を切って、誘導ミサイルのように飛ばす
        if (homingTarget != null && homingTurnSpeed > 0f && homingSpeed > 0f) SetHoming(true);
    }

    private void FixedUpdate()
    {
        if (!hasAuthority || !_isHoming) return;

        // 狙っている相手がいなくなったら、ホーミングをやめて、そのまま落ちる
        if (_homingTarget == null)
        {
            SetHoming(false);
            return;
        }

        SteerToTarget();
    }

    // ホーミング中は、重力と回転を切る(やめる時は元に戻す)
    private void SetHoming(bool isHoming)
    {
        _isHoming = isHoming;
        _rigidbody.useGravity = !isHoming;
        _rigidbody.freezeRotation = isHoming;
        if (isHoming) _rigidbody.angularVelocity = Vector3.zero;
    }

    // 進む向きを、狙っている相手の体の高さへ向けて曲げ、一定の速さで飛ばし、進む向きへ向ける
    private void SteerToTarget()
    {
        Vector3 toTarget = _homingTarget.position + Vector3.up * _homingAimHeight - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 velocity = _rigidbody.linearVelocity;
        float maxRadians = _homingTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector3 direction = velocity.sqrMagnitude > 0.0001f
            ? Vector3.RotateTowards(velocity.normalized, toTarget.normalized, maxRadians, 0f)
            : toTarget.normalized;

        _rigidbody.linearVelocity = direction * _homingSpeed;
        _rigidbody.MoveRotation(Quaternion.LookRotation(direction));
    }

    private void Update()
    {
        if (!hasAuthority) return;

        // 投げた瞬間から数え、空中でも着弾後でも、時間が来たら爆発する
        _elapsed += Time.deltaTime;
        if (_elapsed >= _fuseTime)
        {
            Explode();
        }
    }

    // 範囲ダメージを与えて、自身を取り除く
    private void Explode()
    {
        if (_explodeEffect != null)
        {
            _explodeEffect.Apply(gameObject);
        }

        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }

    // 投げた本人のコライダーとは衝突させない
    private void IgnoreCollisionWith(GameObject thrower)
    {
        if (thrower == null) return;

        Collider[] bombColliders = GetComponentsInChildren<Collider>();
        foreach (Collider throwerCollider in thrower.GetComponentsInChildren<Collider>())
        {
            foreach (Collider bombCollider in bombColliders)
            {
                Physics.IgnoreCollision(bombCollider, throwerCollider);
            }
        }
    }
}
