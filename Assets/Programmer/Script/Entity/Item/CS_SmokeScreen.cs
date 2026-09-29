/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-29 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 煙幕の実体
/// 生成された位置に一定時間残り、煙の中にいるキャラクターを隠し、煙の向こう側への視線も遮る
/// 警察・悪人などのAIは、標的を探す時に IsConcealed / IsLineBlocked で問い合わせる(煙幕側からAIへは通知しない)
/// </summary>
/// <remarks>
/// ・CSO_ItemEffectSmokeが生成する(サーバー、またはオフラインで実行される)
/// ・見た目を全員に見せるためNetworkObjectでSpawnする。寿命の管理と消去はサーバー(またはオフライン)で行う
/// ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
/// ・煙の中にいても攻撃は当たる(隠れるのは「見つからない」ことだけ)
/// </remarks>
[RequireComponent(typeof(NetworkObject))]
public class CS_SmokeScreen : NetworkBehaviour
{
    // 出ている煙幕(AIからの問い合わせに使う)
    private static readonly List<CS_SmokeScreen> _activeSmokes = new List<CS_SmokeScreen>();

    // 煙が消えるまでの残り時間
    private float _remainingTime = 0.0f;

    [Header("＝＝＝ 煙幕 ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("煙の半径")]
    private float _radius = 3.0f;

    [SerializeField, Min(0.1f)]
    [Tooltip("煙が残る時間(秒)")]
    private float _duration = 5.0f;

    [SerializeField]
    [Tooltip("煙の見た目(半径に合わせて大きさを変える)")]
    private Transform _visual = null;

    // このマシンが煙幕の寿命を管理するか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    /// <summary>
    /// 指定した位置が、いずれかの煙幕の中にあるかを判定するメソッド
    /// </summary>
    /// <param name="position">判定する位置</param>
    /// <returns>煙の中ならtrue</returns>
    public static bool IsConcealed(Vector3 position)
    {
        foreach (CS_SmokeScreen smoke in _activeSmokes)
        {
            if ((position - smoke.transform.position).sqrMagnitude <= smoke._radius * smoke._radius) return true;
        }
        return false;
    }

    /// <summary>
    /// 2点を結ぶ視線が、いずれかの煙幕を通るかを判定するメソッド
    /// どちらかの点が煙の中にある場合も通るとみなす(煙の中からも、煙の中へも見えない)
    /// </summary>
    /// <param name="from">視線の始点(目の位置など)</param>
    /// <param name="to">視線の終点(標的の位置など)</param>
    /// <returns>煙幕に遮られていればtrue</returns>
    public static bool IsLineBlocked(Vector3 from, Vector3 to)
    {
        foreach (CS_SmokeScreen smoke in _activeSmokes)
        {
            if (IsSegmentThroughSphere(from, to, smoke.transform.position, smoke._radius)) return true;
        }
        return false;
    }

    private void Awake()
    {
        _remainingTime = _duration;

        // 見た目の球(直径1)を煙の範囲の大きさに合わせる(サーバー・クライアントの両方で行う)
        if (_visual != null) _visual.localScale = Vector3.one * (_radius * 2.0f);
    }

    private void OnEnable()
    {
        _activeSmokes.Add(this);
    }

    private void OnDisable()
    {
        _activeSmokes.Remove(this);
    }

    private void Update()
    {
        if (!hasAuthority) return;

        _remainingTime -= Time.deltaTime;
        if (_remainingTime > 0.0f) return;

        // 時間が来たら消す(オンラインならクライアント側も含めて消す)
        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 線分が球を通るかを判定するメソッド
    /// </summary>
    /// <param name="from">線分の始点</param>
    /// <param name="to">線分の終点</param>
    /// <param name="center">球の中心</param>
    /// <param name="radius">球の半径</param>
    /// <returns>通っていればtrue</returns>
    private static bool IsSegmentThroughSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        // 線分上で球の中心に一番近い点を求め、その点が球の中にあれば通っている
        Vector3 segment = to - from;
        float sqrLength = segment.sqrMagnitude;
        float rate = sqrLength > 0.0f ? Mathf.Clamp01(Vector3.Dot(center - from, segment) / sqrLength) : 0.0f;
        Vector3 closestPoint = from + segment * rate;
        return (center - closestPoint).sqrMagnitude <= radius * radius;
    }

    private void OnDrawGizmos()
    {
        // Sceneビューで煙の範囲を確認できるようにする
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
