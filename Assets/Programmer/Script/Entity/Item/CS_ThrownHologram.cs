/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 投げられた陽動ホログラムの弾
/// 最初に何かに当たったら(着地したら)、その下の地面にホログラム(CS_Hologram)を展開して自身を取り除く
/// </summary>
/// <remarks>
/// ・CSO_ItemEffectHologramが生成し、Launch()で初速を与える(サーバー、またはオフラインで実行される)
/// ・投げた本人とは衝突しない(手元で当たってすぐ展開するのを防ぐため)
/// ・着地の判定・展開はサーバーのみが行う。位置はNetworkTransformで同期する
///   サーバー以外では物理で動かさない(Rigidbodyをkinematicにする)
/// ・どこにも当たらず落ち続けた場合に備えて、_maxFlightTime秒経ったらその場で展開する
/// ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
/// </remarks>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class CS_ThrownHologram : NetworkBehaviour
{
    private Rigidbody _rigidbody = null;

    // 投げてからの経過時間
    private float _elapsed = 0.0f;

    // 展開済みか(当たり判定が続けて起きても1回だけ展開する)
    private bool _hasDeployed = false;

    [Header("＝＝＝ 展開 ＝＝＝")]
    [SerializeField]
    [Tooltip("着地した場所に展開するホログラムのプレハブ")]
    private CS_Hologram _hologramPrefab = null;

    [SerializeField, Min(0f)]
    [Tooltip("着地しないまま経過したら、その場で展開する時間(秒)")]
    private float _maxFlightTime = 5.0f;

    [SerializeField, Min(0f)]
    [Tooltip("地面からホログラムの中心までの高さ")]
    private float _hologramHeight = 1.0f;

    [SerializeField, Min(0f)]
    [Tooltip("展開する地面を探す距離(着地した位置から下方向)")]
    private float _groundCheckDistance = 5.0f;

    // このマシンが弾を動かす権威を持つか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (_hologramPrefab != null) return;

        Debug.LogError("CS_ThrownHologram: Hologram Prefab が未設定です", this);
    }

    public override void OnNetworkSpawn()
    {
        // 位置はNetworkTransformが更新するので、サーバー以外では物理で動かさない
        if (IsServer) return;

        _rigidbody.isKinematic = true;
    }

    /// <summary>
    /// 初速を与えて投げるメソッド(サーバー、またはオフラインで実行される)
    /// </summary>
    /// <param name="velocity">初速</param>
    /// <param name="thrower">投げた本人(衝突させない)</param>
    public void Launch(Vector3 velocity, GameObject thrower)
    {
        IgnoreCollisionWith(thrower);
        _rigidbody.linearVelocity = velocity;
    }

    private void Update()
    {
        if (!hasAuthority) return;

        _elapsed += Time.deltaTime;
        if (_elapsed >= _maxFlightTime) Deploy();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasAuthority) return;

        Deploy();
    }

    /// <summary>
    /// 着地した場所にホログラムを展開し、弾自身を取り除くメソッド
    /// </summary>
    private void Deploy()
    {
        if (_hasDeployed) return;
        _hasDeployed = true;

        if (_hologramPrefab != null)
        {
            CS_Hologram hologram = Instantiate(_hologramPrefab, GetDeployPosition(), Quaternion.identity);

            // オンライン時は、クライアントにもホログラムを出す
            if (IsSpawned) hologram.NetworkObject.Spawn();
        }

        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// ホログラムを展開する位置(着地した場所の真下の地面から、体の中心の高さ)を取得するメソッド
    /// 壁に当たった場合も、その真下の地面に展開する
    /// </summary>
    /// <returns>ホログラムを展開する位置</returns>
    private Vector3 GetDeployPosition()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return transform.position;
        }

        return hit.point + Vector3.up * _hologramHeight;
    }

    /// <summary>
    /// 投げた本人のコライダーとは衝突させないメソッド
    /// </summary>
    /// <param name="thrower">投げた本人</param>
    private void IgnoreCollisionWith(GameObject thrower)
    {
        if (thrower == null) return;

        Collider[] projectileColliders = GetComponentsInChildren<Collider>();
        foreach (Collider throwerCollider in thrower.GetComponentsInChildren<Collider>())
        {
            foreach (Collider projectileCollider in projectileColliders)
            {
                Physics.IgnoreCollision(projectileCollider, throwerCollider);
            }
        }
    }
}
