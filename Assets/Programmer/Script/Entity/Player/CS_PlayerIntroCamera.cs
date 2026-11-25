using Unity.Netcode;
using UnityEngine;

/*
 * ゲームのスタート時(自分のプレイヤーが出てきた時)に、カメラをプレイヤーの周りで下から上へ螺旋状に回すクラス
 * 回り終わると、いつものプレイヤー視点のカメラの位置・向きにぴったり重なって、そのまま操作に移る
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・自分のプレイヤー(CS_Player.isLocalPlayer)が操作を始めた時に、CS_PlayerがPlay()を呼ぶ。自分の画面だけの演出なので通信はしない
 *   NPC・他人のプレイヤーでは動かない。リスポーンでは流さない
 * ・カメラの動かし方: いつものカメラと同じく「注視点(プレイヤーの足元から高さ)の周りを、左右の角度・上下の角度・距離」で決める
 *   始め: 下から見上げる(_startPitch、マイナスが下)・近め(_startDistance)・いつもの向きから_turns回転分 手前
 *   終わり: いつものカメラ(CS_Playerのyaw・pitch・距離)
 *   この間を、ゆっくり動き出してゆっくり止まる(イーズイン・アウト)ように_duration秒で動かす。角度・高さ・距離が一緒に変わるので螺旋になる
 * ・演出中は動けない(CS_Player.canActがfalseになる)。視点の操作も受け付けない(終わりの位置がずれないように)
 * ・ゲームのスタートは演出が終わってから(_pauseGameがオンの時)
 *   ソロ(オフライン): 自分の演出中は、Time.timeScaleを0にしてゲームの時間を止める(タイマー・悪人・警察・NPCなど、ゲームの時間で動くものがすべて止まる)
 *   マルチ(オンライン): 人のプレイヤー全員の演出が終わるまでゲームを始めない(isMatchHeld)
 *     各プレイヤーの「演出が終わったか」を_hasFinished(本人が書き込む。全員が読める)で同期し、各マシンが自分で判定する(判定のための通信は足さない)
 *     始まるまでの間は
 *       ・サーバー(ホスト)はTime.timeScaleを0にする。悪人・警察・NPCはサーバーで動くので、全員の画面で止まる
 *         (クライアントのTime.timeScaleは変えない。止めるのはサーバーの世界だけでよい)
 *       ・タイマーはサーバー時刻で進むため、CS_TimerControllerがisMatchHeldの間は減らさない
 *       ・演出が先に終わったプレイヤーも、全員がそろうまで動けない(CS_Player.canAct)。視点は動かせる
 *     一度始まったら、後から接続した人の演出では止めない(s_hasMatchStarted)。全員そろわなくても、待ち始めてから_maxHoldTime秒(実時間)で始める
 *     (切断・読み込みの遅れで止まり続けないように)
 *   止めている間も自分のプレイヤーのモーションは動くよう、自分のプレイヤーのAnimatorはTime.timeScaleの影響を受けない設定にし、戻す時に元に戻す
 *   プレイヤーがいなくなったら(ゲームシーンを出たら)、止めていた時間を戻し、次の試合のために状態を消す
 * ・攻撃ボタンで早送りする(_skipSpeed倍の速さで残りを回る。カットで切ると見た目が飛ぶため)
 *   押してすぐ攻撃が出ないよう、早送りは演出の終わりまで続け、終わった後に押し直すと攻撃になる
 * ・時間はTime.timeScaleの影響を受けない時間で数える
 *   1フレームで進める時間は_maxFrameStep秒まで(ソロではゲームシーンを読み込んだ直後にプレイヤーが出るため、
 *   最初のフレームが何秒もかかって(8秒近くかかることがある)、演出が始まった瞬間に終わってしまっていた。マルチは読み込みの完了を待ってから出るので起きにくい)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerIntroCamera : NetworkBehaviour
{
    [Header("時間")]
    [SerializeField][Min(0.1f)] private float _duration = 3.5f;          // 回り終わるまでの時間(秒)
    [SerializeField][Min(1f)] private float _skipSpeed = 4f;             // 攻撃ボタンで早送りした時の速さ(倍)
    [SerializeField][Min(0.001f)] private float _maxFrameStep = 1f / 30f;   // 1フレームで進める時間の上限(秒。読み込み直後の重いフレームで飛ばないように)

    [Header("ゲームのスタート")]
    [SerializeField] private bool _pauseGame = true;                     // 演出が終わるまでゲームを始めないか
    [SerializeField][Min(0f)] private float _maxHoldTime = 15f;          // マルチで全員の演出が終わるのを待つ最長の時間(秒。実時間)

    [Header("螺旋の始め")]
    [SerializeField] private float _turns = 1.25f;                       // 回る回数(いつもの向きの何回転手前から回り始めるか。マイナスで逆回り)
    [SerializeField][Range(-89f, 89f)] private float _startPitch = -20f; // 始めの上下の角度(度。マイナスで下から見上げる)
    [SerializeField][Min(0.5f)] private float _startDistance = 2.2f;     // 始めの注視点からの距離(m)
    [SerializeField][Min(0f)] private float _startPivotHeight = 0.9f;    // 始めの注視点の高さ(足元から。m。いつもの高さへ上がっていく)

    // 演出が終わったか(本人が書き込む。全員が読める。マルチでゲームを始めてよいかの判定に使う)
    private readonly NetworkVariable<bool> _hasFinished =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // ゲームのスタートの状態(マシンごと。試合ごとに消す)
    private static int s_instanceCount;                 // 今いるこのクラスの数(0になったら試合が終わったとみなす)
    private static int s_lastUpdateFrame = -1;          // 状態を更新したフレーム(1フレームに1回だけ更新する)
    private static bool s_hasMatchStarted;              // マルチでゲームが始まったか(一度始まったら止めない)
    private static float s_holdStartTime = -1f;         // マルチで待ち始めた時刻(実時間。-1ならまだ待っていない)
    private static bool s_isPausing;                    // ゲームの時間を止めているか
    private static float s_pausedTimeScale = 1f;        // 止める前のTime.timeScale
    private static Animator[] s_animators = new Animator[0];                          // 止めている間も動かす、自分のプレイヤーのAnimator
    private static AnimatorUpdateMode[] s_animatorModes = new AnimatorUpdateMode[0];  // その元の設定

    private CS_Player _player;
    private bool _isPlaying;
    private bool _isSkipping;
    private float _elapsed;     // 演出を始めてからの時間(早送り分も含む。秒)

    public bool isPlaying => _isPlaying;    // 演出中か(この間はCS_Player.canActがfalse)
    // マルチで、人のプレイヤー全員の演出が終わるのを待っている間か(全マシン共通。この間は誰も動けず、タイマーも減らない)
    public static bool isMatchHeld => CS_PlayerNetwork.isOnline && !s_hasMatchStarted && s_holdStartTime >= 0f;

    // Domain Reloadオフ対策(再生のたびに消す)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instanceCount = 0;
        s_lastUpdateFrame = -1;
        ResetMatchState();
        s_isPausing = false;
        s_pausedTimeScale = 1f;
        s_animators = new Animator[0];
        s_animatorModes = new AnimatorUpdateMode[0];
    }

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        s_instanceCount++;
    }

    public override void OnDestroy()
    {
        // プレイヤーがいなくなったら(ゲームシーンを出たら)、時間を戻して次の試合のために状態を消す
        if (--s_instanceCount <= 0)
        {
            s_instanceCount = 0;
            SetGamePaused(false);
            ResetMatchState();
        }
        base.OnDestroy();
    }

    private void OnDisable()
    {
        if (_isPlaying) Finish();
    }

    // 演出を始める(CS_Playerが、自分のプレイヤーとして操作を始めた時に呼ぶ)
    public void Play()
    {
        if (!enabled) return;

        _isPlaying = true;
        _isSkipping = false;
        _elapsed = 0f;
    }

    private void Update()
    {
        UpdateMatchStart(_pauseGame, _maxHoldTime);

        if (!_isPlaying) return;

        if (!_isSkipping && _player.attackAction != null && _player.attackAction.WasPressedThisFrame()) _isSkipping = true;

        // 1フレームで進める時間には上限を付ける(シーンを読み込んだ直後は1フレームが何秒もかかり、演出が一瞬で終わってしまうため)
        float deltaTime = Mathf.Min(Time.unscaledDeltaTime, _maxFrameStep);
        _elapsed += deltaTime * (_isSkipping ? _skipSpeed : 1f);
    }

    // 演出中のカメラの位置・向きを求める(演出中でなければfalse)。CS_PlayerがLateUpdateでカメラを置く時に呼ぶ
    // yaw・pitch・distance・pivotHeightは、いつものカメラの値(演出の終わりの位置)
    public bool TryGetPose(float yaw, float pitch, float distance, float pivotHeight, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!_isPlaying) return false;

        if (_elapsed >= _duration)
        {
            Finish();
            return false;
        }

        float t = EaseInOut(_elapsed / _duration);
        float currentYaw = yaw - _turns * 360f * (1f - t);
        float currentPitch = Mathf.Lerp(_startPitch, pitch, t);
        float currentDistance = Mathf.Lerp(_startDistance, distance, t);
        float currentPivotHeight = Mathf.Lerp(_startPivotHeight, pivotHeight, t);

        rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * currentPivotHeight;
        position = pivot - rotation * Vector3.forward * currentDistance;
        return true;
    }

    // 演出を終える。マルチでは終わったことを全員へ知らせる
    private void Finish()
    {
        _isPlaying = false;
        if (IsSpawned && IsOwner) _hasFinished.Value = true;
    }

    // ========================================
    // ゲームのスタート(マシンごとに、1フレームに1回)
    // ========================================

    // ゲームを始めてよいかを判定し、始まるまではゲームの時間を止める
    private static void UpdateMatchStart(bool pauseGame, float maxHoldTime)
    {
        if (s_lastUpdateFrame == Time.frameCount) return;
        s_lastUpdateFrame = Time.frameCount;

        if (!CS_PlayerNetwork.isOnline)
        {
            // ソロ: 自分の演出の間だけ止める
            CS_Player local = CS_Player.localPlayer;
            bool isLocalPlaying = local != null && local.TryGetComponent(out CS_PlayerIntroCamera intro) && intro._isPlaying;
            SetGamePaused(pauseGame && isLocalPlaying);
            return;
        }

        if (!s_hasMatchStarted && HasHumanPlayer(out bool isAnyPlaying))
        {
            if (s_holdStartTime < 0f) s_holdStartTime = Time.realtimeSinceStartup;

            bool isTimeOver = Time.realtimeSinceStartup - s_holdStartTime >= maxHoldTime;
            if (!pauseGame || !isAnyPlaying || isTimeOver) s_hasMatchStarted = true;
        }

        // マルチ: 止めるのはサーバー(ホスト)の世界だけ
        SetGamePaused(isMatchHeld && NetworkManager.Singleton.IsServer);
    }

    // 人のプレイヤーがいるか。isAnyPlayingには、まだ演出が終わっていない人がいるかを返す
    private static bool HasHumanPlayer(out bool isAnyPlaying)
    {
        bool hasHuman = false;
        isAnyPlaying = false;
        foreach (CS_Player player in CS_Player.activePlayers)
        {
            if (player == null || player.isNpc) continue;
            if (!player.TryGetComponent(out CS_PlayerIntroCamera intro) || !intro.IsSpawned) continue;

            hasHuman = true;
            if (!intro._hasFinished.Value) isAnyPlaying = true;
        }
        return hasHuman;
    }

    private static void ResetMatchState()
    {
        s_hasMatchStarted = false;
        s_holdStartTime = -1f;
    }

    // ゲームの時間を止める・戻す(止めている間も、自分のプレイヤーのモーションは動かし続ける)
    private static void SetGamePaused(bool paused)
    {
        if (paused == s_isPausing) return;

        s_isPausing = paused;
        if (paused)
        {
            s_pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            CS_Player local = CS_Player.localPlayer;
            s_animators = local != null ? local.GetComponentsInChildren<Animator>(true) : new Animator[0];
            s_animatorModes = new AnimatorUpdateMode[s_animators.Length];
            for (int i = 0; i < s_animators.Length; i++)
            {
                s_animatorModes[i] = s_animators[i].updateMode;
                s_animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            return;
        }

        Time.timeScale = s_pausedTimeScale;
        for (int i = 0; i < s_animators.Length; i++)
        {
            if (s_animators[i] != null) s_animators[i].updateMode = s_animatorModes[i];
        }
        s_animators = new Animator[0];
        s_animatorModes = new AnimatorUpdateMode[0];
    }

    // ゆっくり動き出して、ゆっくり止まる(始めと終わりの速さが0。加速度も0なので、止まる時にカクッとしない)
    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
