using UnityEngine;

/*
 * ブリンク(回避)とパリー成功のエフェクトを出すクラス
 * CS_Player / CS_PlayerParryのイベントを受けて、エフェクトを出すだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外してもブリンク・パリーは動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・出すエフェクト(見た目はDB_EffectDataBaseで決める)
 *   ブリンクを始めた時 : PlayerBlink(プレイヤーの足元に出して付いていく)
 *   パリーに成功した時 : PlayerParry(体の中心から、カメラの方へ Camera Offset だけ近づけた所に出す)
 * ・ブリンクのイベント(CS_Player.onDashStarted)は操作しているクライアント(NPCはホスト)でしか起きないので、
 *   CS_NetworkEffectPlayerで他の人の画面にも送る
 * ・パリーのイベント(CS_PlayerParry.onParried)は全クライアントで起きるので、各クライアントが自分の画面に出す(通信しない)
 * ・パリーの演出(スロー・画面端を暗くする)はCS_PlayerParryEffectが行う。こちらはエフェクトを出すだけ
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerParry))]
[RequireComponent(typeof(CS_NetworkEffectPlayer))]
public class CS_PlayerBlinkEffect : MonoBehaviour
{
    [SerializeField] private float _height = 1f;         // Colliderが無い時に、パリーのエフェクトを出す高さ(足元から。m)
    [SerializeField] private float _cameraOffset = 0.5f; // 体の中心から、カメラの方へ近づける距離(m)

    private CS_Player _player;
    private CS_PlayerParry _parry;
    private CS_NetworkEffectPlayer _effectPlayer;
    private Collider _bodyCollider;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _parry = GetComponent<CS_PlayerParry>();
        _effectPlayer = GetComponent<CS_NetworkEffectPlayer>();
        _bodyCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _player.onDashStarted += HandleDashStarted;
        _parry.onParried += HandleParried;
    }

    private void OnDisable()
    {
        _player.onDashStarted -= HandleDashStarted;
        _parry.onParried -= HandleParried;
    }

    private void HandleDashStarted()
    {
        _effectPlayer.PlayForEveryoneAttached(CSE_EffectId.PlayerBlink, Vector3.zero, Quaternion.identity);
    }

    private void HandleParried(GameObject attacker)
    {
        CS_EffectManager manager = CS_EffectManager.instance;
        if (manager == null) return;

        manager.Play(CSE_EffectId.PlayerParry, GetEffectPosition(), transform.rotation);
    }

    // 体の中心から、カメラの方へ少し近づけた位置(体に隠れないように)
    private Vector3 GetEffectPosition()
    {
        Vector3 center = GetBodyCenter();

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return center;

        Vector3 toCamera = mainCamera.transform.position - center;
        if (toCamera.sqrMagnitude < 0.0001f) return center;

        return center + toCamera.normalized * Mathf.Min(_cameraOffset, toCamera.magnitude);
    }

    private Vector3 GetBodyCenter()
    {
        if (_bodyCollider != null && _bodyCollider.enabled) return _bodyCollider.bounds.center;

        return transform.position + Vector3.up * _height;
    }
}
