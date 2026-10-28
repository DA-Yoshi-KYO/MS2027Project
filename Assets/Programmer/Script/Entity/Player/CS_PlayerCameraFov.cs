using Unity.Cinemachine;
using UnityEngine;

/*
 * ダッシュ中にカメラの画角(FOV)を広げ、スピード感を出すクラス
 * ブリンク(ダッシュボタンを押した瞬間の高速移動)とスプリント(押し続けている間の速い移動)は同じ広さにする
 * (ゲームロジック側はこのクラスの存在を知らない。外しても移動は動く)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・広さは「元のFOV(CinemachineCameraのLensに設定されている値) + 足す量」
 *   ブリンク中・スプリント中: _dashFovAdd(既定+15°)。ブリンクは短い間(0.2秒)なので、_widenSpeedで素早く広げる
 *                            スプリントは動いている時だけ(止まってボタンを押しているだけでは広げない)
 *   それ以外               : 元のFOVへ_returnSpeedでゆっくり戻す
 *   ブリンク → スプリントと続けると、広がったままボタンを離すまで保つ
 *   SAでハイドポイントへ飛んでいる時(CS_PlayerHide)・奇襲の飛び込み〜当たる瞬間(CS_PlayerAmbush.isLunging)も、ブリンクと同じ広さにする
 *   (CS_PlayerHide・CS_PlayerAmbushは付いていなければ見ない)
 * ・変化は指数的になめらかにする(速さは「1秒あたりに目標へ近づく割合」の目安。大きいほど速い)
 * ・自分の画面のプレイヤー(CS_Player.isLocalPlayer)のカメラだけを変える
 *   (他人のプレイヤー・NPCのカメラは無効化されている。ブリンク中かどうかも操作しているマシンでしか分からない)
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(Rigidbody))]
public class CS_PlayerCameraFov : MonoBehaviour
{
    [Header("広げる量(度)")]
    [SerializeField] private float _dashFovAdd = 15f;      // ブリンク中・スプリント中に足す量(同じ広さ)

    [Header("変わる速さ(大きいほど速い)")]
    [SerializeField][Min(0.1f)] private float _widenSpeed = 20f;     // 広がる速さ
    [SerializeField][Min(0.1f)] private float _returnSpeed = 5f;     // 元に戻る(狭まる)速さ

    [Header("スプリントとみなす速さ")]
    [SerializeField][Min(0f)] private float _minSprintSpeed = 0.5f;  // 水平方向にこれ以上の速さで動いている時だけスプリントの広さにする(m/秒)

    private CS_Player _player;
    private Rigidbody _rigidbody;
    private CinemachineCamera _camera;
    private CS_PlayerHide _hide;       // ハイド(付いていなければ見ない)
    private CS_PlayerAmbush _ambush;   // 奇襲(付いていなければ見ない)
    private float _baseFov;
    private float _currentFov;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _rigidbody = GetComponent<Rigidbody>();
        _hide = GetComponent<CS_PlayerHide>();
        _ambush = GetComponent<CS_PlayerAmbush>();
        _camera = GetComponentInChildren<CinemachineCamera>(true);

        if (_camera == null) return;

        _baseFov = _camera.Lens.FieldOfView;
        _currentFov = _baseFov;
    }

    private void LateUpdate()
    {
        if (_camera == null || !_player.isLocalPlayer) return;

        float target = _baseFov;
        float speed = _returnSpeed;

        // ブリンク中・スプリント中(動いている時)・SAで飛んでいる時・奇襲で飛び込んでいる時は同じ広さにする
        if (_player.isDashing || (_player.isSprinting && IsMoving()) || IsHideFlying() || IsAmbushLunging())
        {
            target += _dashFovAdd;
            speed = _widenSpeed;
        }

        // 指数的に目標へ近づける(フレームレートに依存しない)
        _currentFov = Mathf.Lerp(_currentFov, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        _camera.Lens.FieldOfView = _currentFov;
    }

    private void OnDisable()
    {
        // 外した時・無効にした時は元の広さに戻す
        if (_camera == null) return;

        _currentFov = _baseFov;
        _camera.Lens.FieldOfView = _baseFov;
    }

    // 水平方向に動いているか(止まってダッシュボタンを押しているだけの時は広げない)
    private bool IsMoving()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.sqrMagnitude >= _minSprintSpeed * _minSprintSpeed;
    }

    // SAでハイドポイントへ飛んでいるか(張り付いたら戻す)
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
