using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

/*
 * 奇襲(CS_PlayerAmbush)の演出を行うクラス(ペルソナ5の奇襲のような緩急をつける)
 * CS_PlayerAmbushのイベントとisLungingを見て、画面効果・星マーク・エフェクト・カメラ揺れ・Animatorの停止をかけるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても奇襲は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・全体の狙い: 押した瞬間に溜め、飛び込みで一気に加速し、命中で止めて弾けさせる。足す時間は溜め(0.1秒)とヒットストップ(0.06秒)だけ
 * ・場面1 押した瞬間(溜め。onAmbushWindupStarted。溜めの秒数はCS_PlayerAmbushの_windupTime)
 *   自分の画面だけ: 画面効果(_windupScreenEffect。完全なモノクロ・ビネットを強く)を、溜めている間(CS_PlayerAmbush.isWindingUp)だけかけ続ける
 *                   溜めが終わる(飛び込み開始)・中止される・無効にされると、すぐ戻る(戻る速さはProfileのCSV_ScreenEffectTimingのFade In / Fade Out。0.02〜0.03秒)
 *                   対象の悪人の位置に星マークを出す(CS_UIAmbushMarkModel。奇襲が終わるまで出し続ける)
 *                   自分と対象のAnimatorを溜めの間だけスローにする(_windupAnimatorSpeed倍。Time.timeScaleは触らない。マルチで全員止まるため)
 *   溜めが0秒の時はこのイベントが出ないので、星マークは飛び込み開始(onAmbushStarted)で出す
 * ・場面2 飛び込み中(onAmbushStarted〜isLunging)
 *   全員の画面に: 体に付いていく軌跡(PlayerAmbushTrail)・体の残像(PlayerAmbushAfterimage)。CS_NetworkEffectPlayerで送る
 *   自分の画面だけ: 画面効果(_lungeScreenEffect。濃い赤系の集中線・ビネット・周りのぼかし)をisLungingの間かけ続ける
 *                   細かいカメラ揺れ(_lungeShakeForce。_lungeShakeInterval秒ごと)
 *   ※ ダッシュの画面効果(CS_PlayerDashScreenEffect)は、奇襲の飛び込み中にはかけない(こちらの専用の効果に移した)
 * ・場面3 命中(onAmbushHitTiming)
 *   全員の画面に: 対象の位置にBurst(PlayerAmbushBurst。衝撃波・火花・星の破片)
 *   自分の画面だけ: 画面効果(_hitScreenEffect。モーションブラーを強く・色収差を一瞬)を1回かける
 *                   星マークを弾けさせる
 *                   大きめのカメラ揺れ(_hitShakeForce。グループの最後の1人でフィニッシュカメラ(CS_PlayerAmbushFinishCamera)の時は、もっと強い_finishHitShakeForce)
 *                   ヒットストップ(_hitStopTime秒。自分と対象のAnimatorを止める)
 * ・カメラ揺れはCS_PlayerCameraShakeと同じCinemachineImpulseSourceを使う(揺れ方の形はそちらのImpulse Definition)
 * ・自分の画面のプレイヤーかどうかはCS_Player.isLocalPlayerで判定する(NPCはホストで動くため、NPCの奇襲でホストの画面が変わらないようにする)
 *   奇襲のイベントは操作しているクライアント(NPCはホスト)でしか起きないので、全員に出す物はCS_NetworkEffectPlayerで送る
 * ・Animatorを止めるのは見た目だけ(悪人・プレイヤーの動き(ロジック)は変えない)。時間はTime.timeScaleの影響を受けない時間で数える
 * ・画面効果のVolume Profileは Assets/Programmer/Volume/Postprocess/VO_Ambush〜。出し入れの速さはProfileのCSV_ScreenEffectTimingで変える
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerAmbush))]
[RequireComponent(typeof(CS_PlayerScreenEffect))]
[RequireComponent(typeof(CS_NetworkEffectPlayer))]
public class CS_PlayerAmbushEffect : MonoBehaviour
{
    [Header("画面効果(自分の画面だけ)")]
    [SerializeField] private VolumeProfile _windupScreenEffect;   // 溜めの間かける効果(モノクロ・ビネット)
    [SerializeField] private VolumeProfile _lungeScreenEffect;    // 飛び込み中にかける効果(集中線・ビネット・周りのぼかし)
    [SerializeField] private VolumeProfile _hitScreenEffect;      // 命中の時にかける効果(モーションブラー・色収差)

    [Header("溜め")]
    [FormerlySerializedAs("_freezeAnimatorSpeed")]
    [SerializeField][Min(0f)] private float _windupAnimatorSpeed = 0.2f;   // 溜めの間のAnimatorの速さ(倍。スローモーション)

    [Header("飛び込み")]
    [SerializeField][Min(0f)] private float _lungeShakeForce = 0.08f;       // 飛び込み中の細かいカメラ揺れの強さ(0で揺れない)
    [SerializeField][Min(0.01f)] private float _lungeShakeInterval = 0.04f; // 細かい揺れを起こす間隔(秒)

    [Header("命中")]
    [SerializeField][Min(0f)] private float _finishHitShakeForce = 1.6f;    // グループの最後の1人に命中した時(フィニッシュカメラ)のカメラ揺れの強さ(通常より強く)
    [SerializeField][Min(0f)] private float _hitShakeForce = 0.8f;          // 命中の時のカメラ揺れの強さ(0で揺れない)
    [SerializeField][Min(0f)] private float _hitStopTime = 0.06f;           // ヒットストップの時間(秒。0で止めない。0.05〜0.08がおすすめ)
    [SerializeField][Min(0f)] private float _hitStopAnimatorSpeed = 0f;     // ヒットストップ中のAnimatorの速さ(倍)
    [SerializeField] private Vector3 _burstOffset = new Vector3(0f, 1f, 0f); // 命中エフェクトを出す、悪人の足元からのずらし幅(m)

    private CS_Player _player;
    private CS_PlayerAmbush _ambush;
    private CS_PlayerScreenEffect _screenEffect;
    private CS_NetworkEffectPlayer _effectPlayer;
    private CinemachineImpulseSource _impulseSource;   // 付いていなければ揺らさない
    private CS_PlayerAmbushFinishCamera _finishCamera; // フィニッシュカメラ(付いていなければ見ない)
    private CS_UIAmbushMarkModel _markModel;           // 星マーク(自分の画面だけ。使う時に作る)

    private readonly Dictionary<Animator, float> _frozenAnimators = new Dictionary<Animator, float>();   // 止めているAnimatorと元の速さ
    private float _unfreezeTime = -1f;   // Animatorを元に戻す時刻(Time.unscaledTime)
    private float _nextShakeTime;        // 次に細かい揺れを起こす時刻(Time.unscaledTime)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _ambush = GetComponent<CS_PlayerAmbush>();
        _screenEffect = GetComponent<CS_PlayerScreenEffect>();
        _effectPlayer = GetComponent<CS_NetworkEffectPlayer>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _finishCamera = GetComponent<CS_PlayerAmbushFinishCamera>();
    }

    private void OnEnable()
    {
        _ambush.onAmbushWindupStarted += HandleWindupStarted;
        _ambush.onAmbushStarted += HandleStarted;
        _ambush.onAmbushHitTiming += HandleHitTiming;
    }

    private void OnDisable()
    {
        _ambush.onAmbushWindupStarted -= HandleWindupStarted;
        _ambush.onAmbushStarted -= HandleStarted;
        _ambush.onAmbushHitTiming -= HandleHitTiming;

        RestoreAnimators();
        _screenEffect.SetHeld(_windupScreenEffect, false);
        _screenEffect.SetHeld(_lungeScreenEffect, false);
        _markModel?.ClearTarget();
    }

    private void OnDestroy()
    {
        _markModel?.Dispose();
        _markModel = null;
    }

    private void Update()
    {
        if (!_player.isLocalPlayer) return;

        // 溜めの間だけモノクロにする(溜めが終わる・奇襲が中止されると、すぐ戻る)
        _screenEffect.SetHeld(_windupScreenEffect, _ambush.isWindingUp);
        UpdateLunge();

        // 止めたAnimatorを、時間が来たら(または奇襲が中止されて動かなくなったら)元に戻す
        if (_frozenAnimators.Count > 0 && (Time.unscaledTime >= _unfreezeTime || !_ambush.isAmbushing))
        {
            RestoreAnimators();
        }

        // 奇襲が当たらずに終わった(中止された)時は、星マークをすぐ消す
        if (_markModel != null && _markModel.hasTarget && !_ambush.isAmbushing)
        {
            _markModel.ClearTarget();
        }
    }

    // ========================================
    // 場面1: 押した瞬間(溜め)
    // ========================================

    private void HandleWindupStarted(CS_VillainHealth villain, float windupTime)
    {
        if (!_player.isLocalPlayer) return;

        ShowMark(villain);
        FreezeAnimators(villain, windupTime, _windupAnimatorSpeed);
    }

    // ========================================
    // 場面2: 飛び込み中
    // ========================================

    private void HandleStarted(CS_VillainHealth villain)
    {
        // 軌跡と残像は全員の画面に出す(体に付いていく)
        _effectPlayer.PlayForEveryoneAttached(CSE_EffectId.PlayerAmbushTrail, Vector3.zero, Quaternion.identity);
        _effectPlayer.PlayForEveryoneAttached(CSE_EffectId.PlayerAmbushAfterimage, Vector3.zero, Quaternion.identity);

        if (!_player.isLocalPlayer) return;

        // 溜めが無い時はここで星マークを出す(溜めがあれば出ているので何もしない)
        ShowMark(villain);

        // 溜めが終わったので、止めていたAnimatorを動かす
        RestoreAnimators();
    }

    // 飛び込んでいる間は画面効果をかけ続け、細かくカメラを揺らす
    private void UpdateLunge()
    {
        bool isLunging = _ambush.isLunging;
        _screenEffect.SetHeld(_lungeScreenEffect, isLunging);

        if (!isLunging || Time.unscaledTime < _nextShakeTime) return;

        _nextShakeTime = Time.unscaledTime + _lungeShakeInterval;
        Shake(_lungeShakeForce);
    }

    // ========================================
    // 場面3: 命中
    // ========================================

    private void HandleHitTiming(CS_VillainHealth villain)
    {
        // 衝撃は全員の画面に出す(対象の位置。倒れて消えている時は自分の前)
        Vector3 position = villain != null
            ? villain.transform.position + _burstOffset
            : transform.position + transform.forward + _burstOffset;
        _effectPlayer.PlayForEveryone(CSE_EffectId.PlayerAmbushBurst, position, transform.rotation);

        if (!_player.isLocalPlayer) return;

        _markModel?.Burst();
        _screenEffect.Play(_hitScreenEffect);
        Shake(_finishCamera != null && _finishCamera.isFinishing ? _finishHitShakeForce : _hitShakeForce);

        if (_hitStopTime > 0f) FreezeAnimators(villain, _hitStopTime, _hitStopAnimatorSpeed);
    }

    // ========================================
    // 星マーク
    // ========================================

    // 対象の悪人の位置に星マークを出す(初めて使う時にModelを作ってBindする)
    private void ShowMark(CS_VillainHealth villain)
    {
        if (villain == null) return;

        if (_markModel == null)
        {
            // 自分の番号を先に登録しておく(Bindの通知を受けた時点でPresenterが自分の分だと判断できるように)
            CS_UIAmbushMarkModel.SetLocalPlayerNumber(_player.playerNumber);
            _markModel = new CS_UIAmbushMarkModel();
            _markModel.Bind(_player.playerNumber);
        }

        _markModel.SetTarget(villain.transform);
    }

    // ========================================
    // カメラ揺れ
    // ========================================

    private void Shake(float force)
    {
        if (force <= 0f || _impulseSource == null) return;

        _impulseSource.GenerateImpulseWithForce(force);
    }

    // ========================================
    // Animatorを止める(見た目だけ)
    // ========================================

    // 自分と対象のAnimatorを、duration秒だけspeed倍の速さにする(秒数はTime.timeScaleの影響を受けない)
    private void FreezeAnimators(CS_VillainHealth villain, float duration, float speed)
    {
        if (duration <= 0f) return;

        FreezeAnimators(gameObject, speed);
        if (villain != null) FreezeAnimators(villain.gameObject, speed);

        _unfreezeTime = Time.unscaledTime + duration;
    }

    private void FreezeAnimators(GameObject root, float speed)
    {
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            // すでに止めているものは、元の速さを上書きしない
            if (!_frozenAnimators.ContainsKey(animator)) _frozenAnimators.Add(animator, animator.speed);
            animator.speed = speed;
        }
    }

    // 止めていたAnimatorを元の速さに戻す(消えたものは飛ばす)
    private void RestoreAnimators()
    {
        foreach (KeyValuePair<Animator, float> pair in _frozenAnimators)
        {
            if (pair.Key != null) pair.Key.speed = pair.Value;
        }
        _frozenAnimators.Clear();
    }
}
