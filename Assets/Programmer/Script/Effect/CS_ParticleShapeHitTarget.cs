using UnityEngine;

/*
 * CS_ParticleShapeHitSpawner のエフェクトを出す対象にする目印のクラス
 * (これが付いているオブジェクトだけが、爆発の範囲に入った時にエフェクトを付けられる)
 *
 * 制作者：　吉本竜
 */

// ========================================
/*
 * メモ
 * ・対象にしたいオブジェクトの一番上(キャラならルート)に付ける
 * ・範囲の判定にはColliderを使うので、このオブジェクトか子にColliderが必要
 *   子のColliderが範囲に入った時も、これが付いているオブジェクト1つとして扱う(部位ごとに何個も出ない)
 *   子に別のこれが付いている時は、近い方(Colliderから親をたどって最初に見つかった方)の対象になる
 * ・Spawn Point
 *   空    : 範囲に入ったColliderの中心(Spawner側で Use Bounds Center をオフにするとこのオブジェクトの位置)に出す
 *   指定  : 指定したTransformの位置に出す(胸や腰のボーンなど)。Attach To Target がオンならそのTransformに付く
 * ・Receive Effect をオフにすると、一時的に対象から外せる
 */
// ========================================

public class CS_ParticleShapeHitTarget : MonoBehaviour
{
    [Header("出す位置")]
    [SerializeField] private Transform _spawnPoint;           // エフェクトを出す・付ける位置(空ならColliderの中心)

    [Header("対象にするか")]
    [SerializeField] private bool _receiveEffect = true;      // オフなら範囲に入ってもエフェクトを出さない

    public Transform SpawnPoint => _spawnPoint;
    public bool ReceiveEffect
    {
        get => _receiveEffect && isActiveAndEnabled;
        set => _receiveEffect = value;
    }
}
