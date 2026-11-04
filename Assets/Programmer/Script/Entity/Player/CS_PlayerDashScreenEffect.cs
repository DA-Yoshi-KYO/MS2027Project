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
 * ・かける条件はCS_PlayerCameraFov(ダッシュ中に画角を広げる)と同じ。次のどれかの時にかける
 *   ブリンク中(CS_Player.isDashing) または スプリント中(isSprinting)で実際に動いている時
 *   (止まってダッシュボタンを押しているだけの時はかけない)
 *   SAでハイドポイントへ飛んでいる時(CS_PlayerHide.isHiding で、まだ張り付いていない時)
 *   奇襲の飛び込み〜当たる瞬間(CS_PlayerAmbush.isLunging)
 *   CS_PlayerHide・CS_PlayerAmbushは付いていなければ見ない
 * ・SA・奇襲は短いので、SetHeldではなく毎フレームPlayして、すぐ最大の強さで出す
 *   (SetHeldだとFade In Timeの途中で終わり、弱い強さから一瞬で消えたように見えるため)
 *   消える速さはCSV_ScreenEffectTimingのPlay Duration / Play Weight Curveで調整する
 * ・同じProfileを別のクラスからSetHeldすると、後に呼んだ方で上書きされてしまうので、条件はこのクラスにまとめる
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
    private CS_PlayerHide _hide;       // ハイド(付いていなければ見ない)
    private CS_PlayerAmbush _ambush;   // 奇襲(付いていなければ見ない)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _rigidbody = GetComponent<Rigidbody>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
        _hide = GetComponent<CS_PlayerHide>();
        _ambush = GetComponent<CS_PlayerAmbush>();
    }

    private void LateUpdate()
    {
        bool isFast = _player.isDashing || (_player.isSprinting && IsMoving());
        _screenEffect.SetHeld(_dashEffect, isFast);

        // SA・奇襲は短い(0.3〜0.6秒)ので、Fade In Timeを待たずに最大の強さで出す
        // 毎フレームPlayし直して強さ1を保ち、終わったらPlay Duration秒かけてPlay Weight Curveで消える
        if (IsHideFlying() || IsAmbushLunging()) _screenEffect.Play(_dashEffect);
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

    // SAでハイドポイントへ飛んでいるか(張り付いた後はCS_PlayerHideScreenEffectの効果に切り替わる)
    private bool IsHideFlying()
    {
        return _hide != null && _hide.isHiding && !_hide.isAttached;
    }

    // 奇襲で飛び込んでいるか(当たる瞬間まで)
    private bool IsAmbushLunging()
    {
        return _ambush != null && _ambush.isLunging;
    }
}
