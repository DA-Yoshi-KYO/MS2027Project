using System.Collections.Generic;
using TMPro;
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
 * ・全体の狙い: 押した瞬間に溜め、飛び込みで一気に加速し、命中で止めて弾けさせる。足す時間は溜め(0.1秒)とヒットストップ(CS_PlayerAmbushの_hitStopTime。0.1秒)だけ
 * ・場面1 押した瞬間(溜め。onAmbushWindupStarted。溜めの秒数はCS_PlayerAmbushの_windupTime)
 *   下の画面効果・Animatorのスローは、_useWindupEffectsがオンの時だけかける(今はオフ。星マークはいつも出す)
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
 *                   ヒットストップ(CS_PlayerAmbush.hitStopTime秒。自分のAnimatorを止める。対象の悪人のAnimatorは、villainHitStopTimeが0より大きい時だけ止める)
 *                   (動きはCS_PlayerAmbush・CS_VillainKnockbackが止める)
 * ・カメラ揺れはCS_PlayerCameraShakeと同じCinemachineImpulseSourceを使う(揺れ方の形はそちらのImpulse Definition)
 *   奇襲のカメラ揺れは、_useCameraShakeがオンの時だけかける(今はオフ)
 *   それとは別に、命中の瞬間だけ短く小さい揺れを一瞬かける(_useHitMicroShake。連続奇襲でも見やすいように)
 *   この揺れは専用のCinemachineImpulseSourceを実行時に作って使う(長さ・形を、他の揺れの設定と分けて決めるため)
 *   Cinemachineの揺れはゲームの時間で進むので、スロー中は長さをTime.timeScale倍に縮めて出し、実時間で_hitMicroShakeTime秒にする
 * ・自分の画面のプレイヤーかどうかはCS_Player.isLocalPlayerで判定する(NPCはホストで動くため、NPCの奇襲でホストの画面が変わらないようにする)
 *   奇襲のイベントは操作しているクライアント(NPCはホスト)でしか起きないので、全員に出す物はCS_NetworkEffectPlayerで送る
 * ・Animatorを止めるのは見た目だけ(悪人・プレイヤーの動き(ロジック)は変えない)。時間はTime.timeScaleの影響を受けない時間で数える
 * ・画面効果のVolume Profileは Assets/Programmer/Volume/Postprocess/VO_Ambush〜。出し入れの速さはProfileのCSV_ScreenEffectTimingで変える
 * ・場面4 連続奇襲のスロー(CS_PlayerAmbush.isChainTime。スロー自体はCS_PlayerAmbushが行う)の間の見た目(自分の画面だけ)
 *   溜めと同じ画面効果(_windupScreenEffect。モノクロ・ビネット)をかける(_useChainSlowVisualsがオンの時)
 *   視点の操作を重くする(回転量を_chainSlowLookScale倍、慣性_chainSlowLookSmoothTime秒。CS_Player.SetLookWeight)
 *   ズーム(視野角を狭める)はCS_PlayerCameraFovが isChainSlowing・chainSlowStrength を見て行う
 *   _fadeWithWaitTimeがオンなら、待機時間(CS_PlayerAmbush.chainTimeRemainingRate)が減るのに合わせて、モノクロ・ビネット・ズームを弱める
 *   (奇襲中は最大の強さ。当て終わると_fadeCurveの形で弱まり、待機時間が0の時に_fadeEndStrength倍になる。次の奇襲を始めると最大に戻る)
 *   カメラを次の奇襲対象(FindAmbushTarget。毎フレーム探し直す)へ向ける(_lookAtNextTarget。CS_Player.SetLookTarget)
 *   飛び込んでいる間(当てるまで)は向け直さない。スローが終わったら向けるのをやめる
 * ・コンボ数の表示(自分の画面だけ): 連続奇襲のコンボ数(CS_PlayerAmbush.chainCount)が_comboMinCount以上の間、
 *   大きな数字を画面に出す(CS_PlayerAmbushComboHud)。コンボ数が増えるほど大きくし(_comboGrowPerCount。_comboMaxGrow倍まで)、
 *   増えた瞬間に揺らす(揺れもコンボ数に応じて強くする。_comboShakeGrowPerCount。_comboMaxShakeGrow倍まで)。
 *   連続奇襲が終わってコンボ数が確定した時に、色を変えて大きく弾ませ・強く揺らして強調し、少し見せてから消す(_comboConfirmEffect)。連続奇襲が続いている間(isInChainWindow)出し、終わったら消す
 * ・連続奇襲の盛り上げ(自分の画面だけ): 連続数(CS_PlayerAmbush.chainCount)が増えるほど、命中のカメラ揺れを強くする
 *   (1体ごとに_chainShakeStep倍ずつ。_maxChainShakeScale倍まで)
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
    [SerializeField] private bool _useWindupEffects = false;               // 溜めの間の演出(モノクロ・ビネット、自分と対象のモーションのスロー)をかけるか
    [SerializeField][Min(0f)] private float _windupAnimatorSpeed = 0.2f;   // 溜めの間のAnimatorの速さ(倍。スローモーション)

    [Header("カメラ揺れ")]
    [SerializeField] private bool _useCameraShake = false;                 // 奇襲のカメラ揺れ(飛び込み中・命中・フィニッシュ)をかけるか
    [SerializeField] private bool _useHitMicroShake = true;                // 命中の瞬間だけ、細かい揺れを一瞬かけるか(上の揺れとは別。連続奇襲でも見やすいよう短く小さく)
    [SerializeField][Min(0f)] private float _hitMicroShakeForce = 0.35f;   // 命中の細かい揺れの強さ
    [SerializeField][Min(0.01f)] private float _hitMicroShakeTime = 0.08f; // 命中の細かい揺れの長さ(秒。実時間。スロー中も同じ長さ)
    [SerializeField] private CinemachineImpulseDefinition.ImpulseShapes _hitMicroShakeShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;   // 命中の細かい揺れの形

    [Header("飛び込み")]
    [SerializeField][Min(0f)] private float _lungeShakeForce = 0.08f;       // 飛び込み中の細かいカメラ揺れの強さ(0で揺れない)
    [SerializeField][Min(0.01f)] private float _lungeShakeInterval = 0.04f; // 細かい揺れを起こす間隔(秒)

    [Header("命中")]
    [SerializeField][Min(0f)] private float _finishHitShakeForce = 1.6f;    // グループの最後の1人に命中した時(フィニッシュカメラ)のカメラ揺れの強さ(通常より強く)
    [SerializeField][Min(0f)] private float _hitShakeForce = 0.8f;          // 命中の時のカメラ揺れの強さ(0で揺れない)
    [SerializeField][Min(0f)] private float _hitStopAnimatorSpeed = 0f;     // ヒットストップ中のAnimatorの速さ(倍)
    [SerializeField] private Vector3 _burstOffset = new Vector3(0f, 1f, 0f); // 命中エフェクトを出す、悪人の足元からのずらし幅(m)

    [Header("コンボ数の表示")]
    [SerializeField] private bool _showComboHud = true;                    // コンボ数を画面に出すか
    [SerializeField][Min(1)] private int _comboMinCount = 2;               // このコンボ数から出す
    [SerializeField] private Vector2 _comboAnchor = new Vector2(0.2f, 0.62f);   // 画面の中の位置(割合。左下(0,0)〜右上(1,1))
    [SerializeField][Min(1f)] private float _comboNumberSize = 420f;       // 大きな数字の大きさ(コンボ数が_comboMinCountの時)
    [SerializeField][Min(0f)] private float _comboGrowPerCount = 0.08f;    // コンボ数が1増えるごとに大きくする割合(倍)
    [SerializeField][Min(1f)] private float _comboMaxGrow = 1.6f;          // 大きくする上限(倍)
    [SerializeField] private TMP_FontAsset _comboFont;                     // 文字のフォント(空ならTextMeshProの既定のフォント)
    [SerializeField] private bool _comboItalic = true;                     // 文字を斜体にするか
    [SerializeField][Min(0f)] private float _comboShakeAmplitude = 16f;    // コンボ数が増えた瞬間に揺らす幅(ピクセル)
    [SerializeField][Min(0f)] private float _comboShakeTime = 0.12f;       // 揺らす時間(秒)
    [SerializeField][Min(0f)] private float _comboShakeGrowPerCount = 0.25f;   // コンボ数が1増えるごとに、揺れを強くする割合(倍)
    [SerializeField][Min(1f)] private float _comboMaxShakeGrow = 3f;       // 揺れを強くする上限(倍)
    [SerializeField] private bool _comboConfirmEffect = true;              // 連続奇襲が終わってコンボ数が確定した時に、強調するか
    [SerializeField] private Color _comboConfirmColor = new Color(1f, 0.8f, 0.15f, 1f);   // 確定した時の数字の色
    [SerializeField][Min(1f)] private float _comboConfirmPunchScale = 1.5f;    // 確定した時に大きくする倍率
    [SerializeField][Min(0f)] private float _comboConfirmPunchTime = 0.25f;    // 確定した時に、元の大きさに戻るまでの時間(秒)
    [SerializeField][Min(0f)] private float _comboConfirmShake = 28f;          // 確定した時に揺らす幅(ピクセル)
    [SerializeField][Min(0f)] private float _comboConfirmHoldTime = 0.7f;      // 確定してから、消え始めるまでの時間(秒)
    [SerializeField] private Color _comboNumberColor = new Color(1f, 1f, 1f, 0.85f);   // 大きな数字の色(アルファで透けさせる)
    [SerializeField] private Color _comboNumberOutlineColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    [SerializeField][Range(0f, 1f)] private float _comboNumberOutlineWidth = 0.2f;     // 大きな数字の縁取りの太さ
    [SerializeField] private string _comboLabel = "COMBO";                 // 数字の下の小さなラベル(空なら出さない)
    [SerializeField][Min(1f)] private float _comboLabelSize = 36f;
    [SerializeField] private Color _comboLabelColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField][Min(1f)] private float _comboPunchScale = 1.3f;       // コンボ数が増えた時に大きくする倍率
    [SerializeField][Min(0f)] private float _comboPunchTime = 0.15f;       // 元の大きさに戻るまでの時間(秒)
    [SerializeField][Min(0f)] private float _comboFadeTime = 0.3f;         // 連続奇襲が終わってから消えるまでの時間(秒)
    [SerializeField] private int _comboSortingOrder = 100;                 // 表示順(大きいほど手前)

    [Header("連続奇襲の盛り上げ")]
    [SerializeField][Min(0f)] private float _chainShakeStep = 0.15f;        // 連続数が1増えるごとに、命中のカメラ揺れに足す倍率
    [SerializeField][Min(1f)] private float _maxChainShakeScale = 2f;       // 命中のカメラ揺れの倍率の上限

    [Header("連続奇襲のスローの見た目(スローの速さ・使うかはCS_PlayerAmbush)")]
    [SerializeField] private bool _useChainSlowVisuals = true;   // スローの間に、画面効果(モノクロ)・ズームをかけるか
    [SerializeField][Range(0f, 1f)] private float _chainSlowLookScale = 0.4f;      // スローの間の視点の回転量(倍。小さいほど重い。1で普段どおり)
    [SerializeField][Min(0f)] private float _chainSlowLookSmoothTime = 0.12f;      // スローの間、視点の回転に付ける慣性(秒。0で慣性なし)
    [SerializeField] private bool _lookAtNextTarget = false;                       // スローの間、カメラを次の奇襲対象へ向けるか(ズームの注視点を次の対象にする)
    [SerializeField][Min(0f)] private float _lookAtSpeed = 6f;                     // 次の奇襲対象へカメラを向ける速さ(大きいほど速い)
    [SerializeField] private float _lookAtHeight = 1f;                             // カメラを向ける、次の奇襲対象の足元からの高さ(m)
    [SerializeField] private float _lookAtPitchOffset = 10f;                       // 次の奇襲対象より、これだけ見下ろす(度。自分が画面から外れないように)
    [SerializeField] private bool _fadeWithWaitTime = true;                        // 待機時間が減るのに合わせて、モノクロ・ビネット・ズームを弱めるか
    [SerializeField][Range(0f, 1f)] private float _fadeEndStrength = 0f;           // 待機時間が0になった時の強さ(倍。0で消えきる)
    [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);   // 待機時間の残り(横軸0〜1)に対する強さの変わり方(縦軸0〜1)

    private CS_Player _player;
    private CS_PlayerAmbush _ambush;
    private CS_PlayerScreenEffect _screenEffect;
    private CS_NetworkEffectPlayer _effectPlayer;
    private CinemachineImpulseSource _impulseSource;   // 付いていなければ揺らさない
    private CS_PlayerAmbushComboHud _comboHud;         // コンボ数の表示(使う時に作る)
    private bool _wasComboActive;                      // 前のフレームでコンボ数を出していたか(確定の演出のきっかけ)
    private CinemachineImpulseSource _microImpulse;    // 命中の細かい揺れ専用(使う時に実行時に作る。他の揺れの設定と分けるため)
    private CS_PlayerAmbushFinishCamera _finishCamera; // フィニッシュカメラ(付いていなければ見ない)
    private CS_UIAmbushMarkModel _markModel;           // 星マーク(自分の画面だけ。使う時に作る)

    private readonly Dictionary<Animator, float> _frozenAnimators = new Dictionary<Animator, float>();   // 止めているAnimatorと元の速さ
    private float _unfreezeTime = -1f;   // Animatorを元に戻す時刻(Time.unscaledTime)
    private float _nextShakeTime;        // 次に細かい揺れを起こす時刻(Time.unscaledTime)

    private bool _isLookWeighted;        // 連続奇襲のスローの間、視点を重くしているか

    public bool isChainSlowing => _useChainSlowVisuals && _ambush.isChainTime;   // 連続奇襲のスローの見た目をかけているか(CS_PlayerCameraFovがズームするのに使う)

    // 連続奇襲のスローの見た目の強さ(0〜1)。待機時間が減るのに合わせて弱まる(CS_PlayerCameraFovがズームの量に使う)。スロー中でなければ0
    public float chainSlowStrength
    {
        get
        {
            if (!isChainSlowing) return 0f;
            if (!_fadeWithWaitTime) return 1f;
            float curve = _fadeCurve != null && _fadeCurve.length > 0 ? Mathf.Clamp01(_fadeCurve.Evaluate(_ambush.chainTimeRemainingRate)) : _ambush.chainTimeRemainingRate;
            return Mathf.Lerp(_fadeEndStrength, 1f, curve);
        }
    }

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

        SetLookWeighted(false);
        _player.ClearLookTarget();
        _comboHud?.Hide();
        RestoreAnimators();
        _screenEffect.SetHeld(_windupScreenEffect, false);
        _screenEffect.SetHeld(_lungeScreenEffect, false);
        _markModel?.ClearTarget();
    }

    private void OnDestroy()
    {
        _markModel?.Dispose();
        _markModel = null;
        _comboHud?.Dispose();
        _comboHud = null;
    }

    private void Update()
    {
        if (!_player.isLocalPlayer) return;

        // 連続奇襲のスローの間は、視点を重くする
        SetLookWeighted(_ambush.isChainTime);
        UpdateLookAtNextTarget();
        UpdateComboHud();

        // 溜めの間と、連続奇襲のスローの間だけモノクロにする(終わる・奇襲が中止されると、すぐ戻る)
        bool isWindupEffect = _useWindupEffects && _ambush.isWindingUp;
        _screenEffect.SetHeld(_windupScreenEffect, isWindupEffect || isChainSlowing);
        // 連続奇襲のスローの間は、待機時間が減るのに合わせて弱める(溜めの間はいつもの強さ)
        _screenEffect.SetHeldScale(_windupScreenEffect, !isWindupEffect && isChainSlowing ? chainSlowStrength : 1f);
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
        if (_useWindupEffects) FreezeAnimators(villain, windupTime, _windupAnimatorSpeed);
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

        // 連続数が増えるほど、揺れを強くする
        bool isFinish = _finishCamera != null && _finishCamera.isFinishing;
        float chainScale = Mathf.Min(_maxChainShakeScale, 1f + _chainShakeStep * Mathf.Max(0, _ambush.chainCount - 1));
        Shake((isFinish ? _finishHitShakeForce : _hitShakeForce) * chainScale);
        MicroShake();   // 命中の瞬間だけの細かい揺れ

        // 悪人のヒットストップが0なら、悪人のモーションは止めない(自分だけ止める)
        if (_ambush.hitStopTime > 0f) FreezeAnimators(_ambush.villainHitStopTime > 0f ? villain : null, _ambush.hitStopTime, _hitStopAnimatorSpeed);
    }

    // ========================================
    // 場面4: 連続奇襲のスロー(視点の重さ)
    // ========================================

    // 視点を重くする・戻す(変わった時だけCS_Playerへ伝える)
    private void SetLookWeighted(bool weighted)
    {
        if (_isLookWeighted == weighted) return;

        _isLookWeighted = weighted;
        if (weighted) _player.SetLookWeight(_chainSlowLookScale, _chainSlowLookSmoothTime);
        else _player.ClearLookWeight();
    }

    // スローの間、カメラを次の奇襲対象へ向ける(毎フレーム探し直す。いなくなったら、最後に向けた対象のまま)
    // スローが終わったら向けるのをやめる
    private void UpdateLookAtNextTarget()
    {
        if (!_lookAtNextTarget || !_ambush.isChainTime)
        {
            _player.ClearLookTarget();
            return;
        }

        // 奇襲で飛び込んでいる間は、今の対象を見る(次の対象を探すのは、当てた後〜次の奇襲を始めるまで)
        if (_ambush.isAmbushing && !_ambush.hasStruck) return;

        CS_VillainHealth next = _ambush.FindAmbushTarget();
        if (next != null) _player.SetLookTarget(next.transform, _lookAtSpeed, _lookAtHeight, _lookAtPitchOffset);
    }

    // ========================================
    // コンボ数の表示
    // ========================================

    private void UpdateComboHud()
    {
        bool isActive = _showComboHud && _ambush.isInChainWindow && _ambush.chainCount >= _comboMinCount;
        if (!isActive && _comboHud == null) return;

        CS_PlayerAmbushComboHud.Settings settings = new CS_PlayerAmbushComboHud.Settings
        {
            anchor = _comboAnchor,
            numberFontSize = _comboNumberSize,
            numberColor = _comboNumberColor,
            numberOutlineColor = _comboNumberOutlineColor,
            numberOutlineWidth = _comboNumberOutlineWidth,
            label = _comboLabel,
            labelFontSize = _comboLabelSize,
            labelColor = _comboLabelColor,
            font = _comboFont,
            italic = _comboItalic,
            shakeAmplitude = _comboShakeAmplitude,
            shakeTime = _comboShakeTime,
            punchScale = _comboPunchScale,
            punchTime = _comboPunchTime,
            fadeTime = _comboFadeTime,
            confirmColor = _comboConfirmColor,
            confirmPunchScale = _comboConfirmPunchScale,
            confirmPunchTime = _comboConfirmPunchTime,
            confirmShakeAmplitude = _comboConfirmShake,
            confirmHoldTime = _comboConfirmHoldTime,
            sortingOrder = _comboSortingOrder,
        };
        if (_comboHud == null) _comboHud = new CS_PlayerAmbushComboHud(settings);
        else _comboHud.Apply(settings);

        // コンボ数が増えるほど大きくする(上限あり)
        int over = Mathf.Max(0, _ambush.chainCount - _comboMinCount);
        float sizeScale = Mathf.Min(_comboMaxGrow, 1f + _comboGrowPerCount * over);
        float shakeScale = Mathf.Min(_comboMaxShakeGrow, 1f + _comboShakeGrowPerCount * over);   // 揺れもコンボ数に応じて強くする
        if (isActive) _comboHud.Show(_ambush.chainCount, sizeScale, shakeScale);
        else if (_wasComboActive && _comboConfirmEffect) _comboHud.Confirm();   // 連続奇襲が終わった: 確定の強調
        else _comboHud.Hide();
        _wasComboActive = isActive;
        _comboHud.Tick();
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

    // 命中の瞬間だけ、短く小さい揺れを一瞬かける(_useCameraShakeとは別に動く)
    private void MicroShake()
    {
        if (!_useHitMicroShake || _hitMicroShakeForce <= 0f) return;

        if (_microImpulse == null)
        {
            _microImpulse = gameObject.AddComponent<CinemachineImpulseSource>();
            // いつもの揺れと同じチャンネルで、カメラ側(CinemachineImpulseListener)に届くようにする
            if (_impulseSource != null) _microImpulse.ImpulseDefinition.ImpulseChannel = _impulseSource.ImpulseDefinition.ImpulseChannel;
            _microImpulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        }

        // 再生中にInspectorで変えても反映されるよう、毎回設定する
        _microImpulse.ImpulseDefinition.ImpulseShape = _hitMicroShakeShape;
        // 揺れはゲームの時間で進むので、スロー中(Time.timeScale < 1)はその分だけ縮めて、実時間で_hitMicroShakeTime秒にする
        float timeScale = Time.timeScale > 0f ? Mathf.Min(1f, Time.timeScale) : 1f;
        _microImpulse.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.0001f, _hitMicroShakeTime * timeScale);
        _microImpulse.GenerateImpulseWithVelocity(UnityEngine.Random.onUnitSphere * _hitMicroShakeForce);
    }

    private void Shake(float force)
    {
        if (!_useCameraShake || force <= 0f || _impulseSource == null) return;

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
