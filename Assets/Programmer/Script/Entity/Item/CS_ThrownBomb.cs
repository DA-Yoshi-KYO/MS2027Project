/* ================================================
 *
 * ================================================
 * 制作者：KR
 * ------------------------------------------------
 * 2026-09-28 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/*
 * 投げられた爆弾の実体
 * 着弾(最初に何かに当たる)してから_fuseTime秒後に爆発し、自身を破棄(Despawn)する
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
 * ・どこにも当たらず落ち続けた場合に備えて、_maxFlightTime秒経ったら着弾扱いにする
 * ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
 */
// ========================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class CS_ThrownBomb : NetworkBehaviour
{
    [SerializeField][Tooltip("着弾してから爆発するまでの時間(秒)")][Min(0f)] private float _fuseTime = 2f;
    [SerializeField][Tooltip("着弾しないまま経過したら着弾扱いにする時間(秒)")][Min(0f)] private float _maxFlightTime = 5f;
    [SerializeField][Tooltip("爆発の効果")] private CSO_ItemEffectExplode _explodeEffect;

    private Rigidbody _rigidbody;
    private bool _hasLanded;        // 着弾したか
    private float _elapsed;         // 着弾前は投げてからの時間、着弾後は着弾してからの時間

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
    public void Launch(Vector3 velocity, GameObject thrower)
    {
        IgnoreCollisionWith(thrower);
        _rigidbody.linearVelocity = velocity;
    }

    private void Update()
    {
        if (!hasAuthority) return;

        _elapsed += Time.deltaTime;

        if (!_hasLanded)
        {
            if (_elapsed >= _maxFlightTime) Land();
            return;
        }

        if (_elapsed >= _fuseTime)
        {
            Explode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasAuthority) return;
        if (_hasLanded) return;

        Land();
    }

    // 着弾した。ここから爆発までのカウントを始める
    private void Land()
    {
        _hasLanded = true;
        _elapsed = 0f;
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
