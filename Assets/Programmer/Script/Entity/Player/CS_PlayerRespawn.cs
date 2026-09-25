using System.Collections;
using Unity.Netcode;
using UnityEngine;

/*
 * 死亡したプレイヤーを、一定時間後にその場で復活させるクラス
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・死亡(CS_PlayerHealth.onDeath)から_respawnDelay秒(既定10秒)後に、CS_PlayerHealth.Revive()でHP満タンにして復活させる
 * ・復活する場所は死んだ場所(位置は動かさない)。空中で死んだ場合は、落下して止まった位置で復活する
 *   (死亡中の移動の扱いはCS_Player側で行っている)
 * ・復活の処理はサーバーで行う(Revive()がサーバー専用のため)
 * ・復活予定時刻はNetworkVariableで持ち、HUD用に復活までの残り秒数(respawnRemaining)を全クライアントへ公開する
 * ・復活してから_invincibleDuration秒(既定3秒)は無敵(CS_PlayerHealth.SetInvincible)
 *   無敵の終了時刻もNetworkVariableで持ち、全クライアントで無敵中か(isRespawnInvincible)を判定できる
 *   (見た目の点滅はCS_PlayerVisualがこれを見て行う)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerRespawn : NetworkBehaviour
{
    [Header("復活")]
    [SerializeField] private float _respawnDelay = 10f;  // 死亡してから復活するまでの時間(秒)
    [SerializeField] private float _invincibleDuration = 3f;    // 復活してから無敵が続く時間(秒)

    private CS_PlayerHealth _health;
    private Coroutine _respawnCoroutine;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<double> _respawnTime = new NetworkVariable<double>();  // 復活予定時刻
    private readonly NetworkVariable<double> _invincibleEndTime = new NetworkVariable<double>();    // 復活後の無敵が終わる時刻

    public float respawnDelay => _respawnDelay;
    public float respawnRemaining => _health.isDead ? Mathf.Max(0f, (float)(_respawnTime.Value - GetCurrentTime())) : 0f;  // HUD用
    public bool isRespawnInvincible => !_health.isDead && GetCurrentTime() < _invincibleEndTime.Value;   // 復活直後の無敵中か(見た目用)

    private void Awake()
    {
        _health = GetComponent<CS_PlayerHealth>();
        _health.onDeath += HandleDeath;
    }

    public override void OnDestroy()
    {
        if (_health != null)
        {
            _health.onDeath -= HandleDeath;
        }

        base.OnDestroy();
    }

    // 死亡したら復活のカウントを始める(サーバー、またはオフラインのみ)
    private void HandleDeath()
    {
        if (IsSpawned && !IsServer) return;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
        }

        // 復活後の無敵中に死亡した場合(無敵を無視するダメージなど)に備えて、無敵を確実に終わらせる
        EndInvincible();

        _respawnTime.Value = GetCurrentTime() + _respawnDelay;
        _respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    // _respawnDelay秒待ってから、その場で復活させ、_invincibleDuration秒だけ無敵にする
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(_respawnDelay);

        // 待っている間に他の処理で復活済みなら、HPを満タンにし直さない
        if (!_health.isDead)
        {
            _respawnCoroutine = null;
            yield break;
        }

        _health.Revive();

        _health.SetInvincible(this, true);
        _invincibleEndTime.Value = GetCurrentTime() + _invincibleDuration;
        yield return new WaitForSeconds(_invincibleDuration);

        _respawnCoroutine = null;
        EndInvincible();
    }

    // 復活後の無敵を終わらせる(サーバー、またはオフラインのみ)
    private void EndInvincible()
    {
        _health.SetInvincible(this, false);
        _invincibleEndTime.Value = 0;
    }

    // 復活の基準時刻(オンラインはサーバー時刻で揃える)
    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
