using UnityEngine;

/*
 * ダッシュ中に集中線などの画面効果をかけるクラス
 * CS_Playerのダッシュ状態を見て、CS_PlayerScreenEffectへ効果のオン・オフを伝えるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても移動は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・かける条件はCS_PlayerCameraFov(ダッシュ中に画角を広げる)と同じ
 *   ブリンク中(CS_Player.isDashing) または スプリント中(isSprinting)で実際に動いている時
 *   (止まってダッシュボタンを押しているだけの時はかけない)
 * ・見た目(線の本数・太さなど)は_speedLinesのVolume Profile(CSV_SpeedLines)で、
 *   出し入れの速さは_speedLines(CSO_ScreenEffectData)のFade In / Fade Outで調整する
 * ・自分の画面のプレイヤーかどうかの判定はCS_PlayerScreenEffect側で行う
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
public class CS_PlayerDashScreenEffect : MonoBehaviour
{
    [Header("かける効果")]
    [SerializeField] private CSO_ScreenEffectData _speedLines;   // ダッシュ中にかける効果

    [Header("スプリントとみなす速さ")]
    [SerializeField][Min(0f)] private float _minSprintSpeed = 0.5f;  // 水平方向にこれ以上の速さで動いている時だけかける(m/秒)

    private CS_Player _player;
    private Rigidbody _rigidbody;
    private CS_PlayerScreenEffect _screenEffect;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _rigidbody = GetComponent<Rigidbody>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
    }

    private void LateUpdate()
    {
        bool isFast = _player.isDashing || (_player.isSprinting && IsMoving());
        _screenEffect.SetHeld(_speedLines, isFast);
    }

    private void OnDisable()
    {
        if (_screenEffect == null) return;
        _screenEffect.SetHeld(_speedLines, false);
    }

    // 水平方向に動いているか(止まってダッシュボタンを押しているだけの時はかけない)
    private bool IsMoving()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.sqrMagnitude >= _minSprintSpeed * _minSprintSpeed;
    }
}
