/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 陽動ホログラムの実体
/// 展開された位置に一定時間残り、気付いた警察・悪人の標的になる(攻撃を受けても消えない)
/// 警察・悪人などのAIは、標的を探す時に activeHolograms から見えるものを探す(ホログラム側からAIへは通知しない)
/// </summary>
/// <remarks>
/// ・CS_ThrownHologramが着地した場所に生成する(サーバー、またはオフラインで実行される)
/// ・見た目を全員に見せるためNetworkObjectでSpawnする。寿命の管理と消去はサーバー(またはオフライン)で行う
/// ・transformの位置は、プレイヤーと同じく体の中心(地面から約1m)にする
/// ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
/// </remarks>
[RequireComponent(typeof(NetworkObject))]
public class CS_Hologram : NetworkBehaviour
{
    // 展開中のホログラム(AIからの問い合わせに使う)
    private static readonly List<CS_Hologram> _activeHolograms = new List<CS_Hologram>();

    // ホログラムが消えるまでの残り時間
    private float _remainingTime = 0.0f;

    [Header("＝＝＝ ホログラム ＝＝＝")]
    [SerializeField, Min(0.1f)]
    [Tooltip("ホログラムが残る時間(秒)")]
    private float _duration = 10.0f;

    // 展開中のホログラム
    public static IReadOnlyList<CS_Hologram> activeHolograms => _activeHolograms;

    // このマシンがホログラムの寿命を管理するか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    private void Awake()
    {
        _remainingTime = _duration;
    }

    private void OnEnable()
    {
        _activeHolograms.Add(this);
    }

    private void OnDisable()
    {
        _activeHolograms.Remove(this);
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
}
