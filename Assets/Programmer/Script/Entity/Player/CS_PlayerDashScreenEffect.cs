using UnityEngine;
using UnityEngine.Rendering;

/*
 * ダッシュ中に集中線・周りの円形ぼかしなどの画面効果をかけるクラス
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
 * ・ダッシュ中の効果は1つのVolume Profile(_dashEffect)にまとめ、Playerの子のVolume1つでかける
 *   見た目も出し入れの速さも、このProfileで調整する
 *   集中線: CSV_SpeedLines
 *   周りのぼかし: CSV_EdgeBlur(画面の中心はくっきり残し、Inner Radiusより外を円形にぼかす)
 *   出し入れの速さ: CSV_ScreenEffectTimingのFade In Time / Fade Out Time(全部の効果で共通)
 * ・自分の画面のプレイヤーかどうかの判定はCS_PlayerScreenEffect側で行う
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
public class CS_PlayerDashScreenEffect : MonoBehaviour
{
    [Header("かける効果")]
    [SerializeField] private VolumeProfile _dashEffect;   // ダッシュ中にかける効果(集中線・周りのぼかし)

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
        _screenEffect.SetHeld(_dashEffect, isFast);
    }

    private void OnDisable()
    {
        if (_screenEffect == null) return;
        _screenEffect.SetHeld(_dashEffect, false);
    }

    // 水平方向に動いているか(止まってダッシュボタンを押しているだけの時はかけない)
    private bool IsMoving()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.sqrMagnitude >= _minSprintSpeed * _minSprintSpeed;
    }
}
