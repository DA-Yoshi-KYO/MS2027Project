/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 光学迷彩の実体
/// 使用者が透明になっている間だけ存在し、警察・悪人などのAIから見つけられなくなり、他のプレイヤーの画面でも見えなくなる
/// </summary>
/// <remarks>
/// ・CSO_ItemEffectOpticalCamoが生成する(サーバー、またはオフラインで実行される)
/// ・AIは、標的を探す時に IsConcealed で問い合わせる(光学迷彩側からAIへは通知しない。煙幕のIsLineBlockedと同じ形)
/// ・透明になる時間が過ぎるか、使用者が死亡したら終わる。終わりの判定と消去はサーバー(またはオフライン)で行う
/// ・使用者は、全員のマシンで同じ使用者を見つけられるよう、使用者のNetworkObjectIdをNetworkVariableで持つ
/// ・見た目は、使用者以外の人の画面で、使用者のモデルを隠す(自分の画面の自分は隠さない。NPCの場合は全員から隠す)
///   Renderer.enabledは触らず、forceRenderingOffで隠す(他の処理が設定した表示状態を壊さないため)
/// ・NetworkPrefabsList(DefaultNetworkPrefabs)にこのプレハブを登録しておくこと
/// </remarks>
[RequireComponent(typeof(NetworkObject))]
public class CS_OpticalCamo : NetworkBehaviour
{
    // 出ている光学迷彩(AIからの問い合わせに使う)
    private static readonly List<CS_OpticalCamo> _activeCamos = new List<CS_OpticalCamo>();

    // 使用者のNetworkObjectId(書き込みはサーバーのみ。0なら使用者が決まっていない)
    private readonly NetworkVariable<ulong> _userObjectId = new NetworkVariable<ulong>();

    // 透明になる使用者(サーバー・オフラインでは使用者そのもの、クライアントではIdから見つけたもの)
    private CS_Player _user = null;

    // 使用者のモデル(使用者を見つけた時に控える)
    private Renderer[] _renderers = new Renderer[0];

    // 透明になっている残り時間(サーバー、またはオフラインだけで数える)
    private float _remainingTime = 0.0f;

    // 使用者が決まっているか(Beginが呼ばれたか。サーバー、またはオフラインだけ)
    private bool _isArmed = false;

    // 使用者のモデルを隠しているか
    private bool _isHiding = false;

    // このマシンが寿命を管理するか(オフライン、またはサーバー)
    private bool hasAuthority => !IsSpawned || IsServer;

    /// <summary>
    /// 標的のプレイヤーが、光学迷彩で透明になっているかを判定するメソッド
    /// </summary>
    /// <param name="targetRoot">標的の本体(プレイヤー)</param>
    /// <returns>透明になっていればtrue</returns>
    public static bool IsConcealed(Transform targetRoot)
    {
        if (targetRoot == null) return false;

        foreach (CS_OpticalCamo camo in _activeCamos)
        {
            if (camo._user != null && targetRoot.IsChildOf(camo._user.transform)) return true;
        }
        return false;
    }

    /// <summary>
    /// すでに透明になっている使用者の、透明になる時間を延ばし直すメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="user">使用者</param>
    /// <param name="duration">延ばし直した後の、透明になる残り時間(秒)</param>
    /// <returns>すでに透明になっていて、時間を延ばし直したらtrue</returns>
    public static bool TryRefresh(CS_Player user, float duration)
    {
        foreach (CS_OpticalCamo camo in _activeCamos)
        {
            if (camo._user != user || !camo._isArmed) continue;

            camo._remainingTime = duration;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 透明になり始めるメソッド(サーバー、またはオフラインで呼ぶこと)
    /// </summary>
    /// <param name="user">透明になる使用者</param>
    /// <param name="duration">透明になる時間(秒)</param>
    public void Begin(CS_Player user, float duration)
    {
        _user = user;
        _remainingTime = duration;
        _isArmed = true;

        if (IsSpawned) _userObjectId.Value = user.NetworkObjectId;
        CacheRenderers();
    }

    private void OnEnable()
    {
        if (!_activeCamos.Contains(this)) _activeCamos.Add(this);
    }

    private void OnDisable()
    {
        _activeCamos.Remove(this);
        ShowRenderers();
    }

    private void Update()
    {
        // クライアントは、使用者がまだ見つかっていなければ探す(NetworkVariableが届いても、使用者の出現が遅れることがある)
        if (_user == null && !hasAuthority) FindUserFromId();

        UpdateVisual();

        if (!hasAuthority || !_isArmed) return;

        // 使用者がいなくなった・死亡した・時間が過ぎたら終わる
        _remainingTime -= Time.deltaTime;
        if (_remainingTime > 0.0f && IsAlive()) return;

        Remove();
    }

    /// <summary>
    /// 使用者が生きているかを判定するメソッド
    /// </summary>
    private bool IsAlive()
    {
        return _user != null && _user.TryGetComponent(out CS_PlayerHealth health) && !health.isDead;
    }

    /// <summary>
    /// クライアントで、同期された使用者のIdから、使用者を見つけるメソッド
    /// </summary>
    private void FindUserFromId()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (_userObjectId.Value == 0 || manager == null) return;
        if (!manager.SpawnManager.SpawnedObjects.TryGetValue(_userObjectId.Value, out NetworkObject userObject)) return;
        if (!userObject.TryGetComponent(out CS_Player player)) return;

        _user = player;
        CacheRenderers();
    }

    /// <summary>
    /// 使用者のモデル(Renderer)を控えるメソッド
    /// </summary>
    private void CacheRenderers()
    {
        _renderers = _user != null ? _user.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    /// <summary>
    /// 使用者のモデルを、使用者以外の人の画面で隠すメソッド
    /// </summary>
    private void UpdateVisual()
    {
        bool shouldHide = _user != null && !_user.isLocalPlayer;
        if (shouldHide == _isHiding) return;

        _isHiding = shouldHide;
        SetRenderersOff(shouldHide);
    }

    /// <summary>
    /// 使用者のモデルを元の表示に戻すメソッド
    /// </summary>
    private void ShowRenderers()
    {
        if (!_isHiding) return;

        _isHiding = false;
        SetRenderersOff(false);
    }

    private void SetRenderersOff(bool isOff)
    {
        foreach (Renderer target in _renderers)
        {
            if (target != null) target.forceRenderingOff = isOff;
        }
    }

    /// <summary>
    /// このオブジェクトを消すメソッド(オンラインならネットワーク越しにDespawn、オフラインならDestroy)
    /// </summary>
    private void Remove()
    {
        _isArmed = false;
        if (IsSpawned)
        {
            NetworkObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }
}
