using Unity.Cinemachine;
using UnityEngine;

/*
 * 悪人のグループの最後の1人を奇襲した時に、悪人の向こう側からプレイヤーの全身を映すカメラ(フィニッシュカメラ)に切り替えるクラス
 * CS_PlayerAmbushのイベントを見て、CinemachineCameraを切り替えるだけ
 * (ゲームロジック側はこのクラスの存在を知らない。外しても奇襲は動く)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・流れ(奇襲した本人の画面(CS_Player.isLocalPlayer)だけ)
 *   1. 溜めの開始(onAmbushWindupStarted。溜めが0秒の時は飛び込み開始のonAmbushStarted)
 *      狙った悪人が「グループの最後の1人」なら、フィニッシュ用のCinemachineCameraへカットで切り替える
 *      溜め〜飛び込みの間、カメラは動かさない
 *   2. 命中(onAmbushHitTiming): そのカメラのまま_holdTime秒(既定0.25秒)保つ(ゲームは止めない。揺れはCS_PlayerAmbushEffectが起こす)
 *   3. _returnTime秒(既定0.25秒)かけて、なめらか(イーズイン・アウト)にいつものプレイヤー視点のカメラへ戻る
 *   時間はTime.timeScaleの影響を受けない時間で数える
 * ・「グループの最後の1人」の判定は本人の手元で行う(サーバー専用のCS_VillainGroupは使わない。通信は足さない)
 *   狙った悪人と同じ groupId(CS_VillainCrime。同期済み)で、まだ倒れていない悪人が他にいなければ最後の1人
 *   倒れているかはCS_VillainHealth(isDefeated・currentHp。同期済み)で見る
 *   groupId が -1(グループに属さない悪人)は、_includeUngroupedがオンなら最後の1人として扱う
 *   数えるのは溜めの開始時に1回だけ(FindObjectsByType)
 *   ※ 奇襲の巻き込み(CS_PlayerAmbushの_splashRadius)で一緒に倒れる悪人は数えない(今いるものは「生きている」として数える)
 * ・カメラの位置: プレイヤー→悪人の水平方向の延長上で、悪人の向こう側(悪人から離れた所、悪人の足元から_cameraHeightの高さ)
 *   プレイヤーの方を向く(注視点はプレイヤーの足元から_lookHeight)。悪人が手前、プレイヤーが奥に映る
 *   悪人からの距離は、飛び込みで悪人の_arrivalDistance手前まで近づいた時(命中の瞬間)に、プレイヤーの全身(当たり判定の高さ × _bodyMargin)が
 *   画面に収まる距離をFOVから計算し、_minBehindDistance〜_maxBehindDistanceで挟んで決める(近づいても頭から足まで収まる)
 *   壁(建物)でカメラを手前へ寄せない(建物は透過する)。地面より下には行かないよう、カメラの高さを地面の上(_groundClearance)に保つ
 * ・建物の透過: 切り替えている間、CS_PlayerCameraOcclusion.SetExtraTargetで、カメラ→プレイヤー・カメラ→悪人の線を太め(_occlusionRadius)に調べて、
 *   間をさえぎるBuilding / Propを透過する(画面に映る建物をすべて消すことはしない)。切り替えた瞬間から透過して見えるよう、
 *   判定に使うカメラの位置にはフィニッシュカメラの位置を直接渡す(実際のカメラはBrainの更新後に動くため、1フレーム遅れる)
 *   戻りのブレンドの間は実際のカメラの位置から調べる。戻り終わり・中止・無効化でやめる
 * ・フィニッシュ用のCinemachineCameraは、自分のプレイヤーの時だけ、使う時に実行時に作る(プレイヤーの子にしない)
 *   (CS_Playerは他人のCinemachineCameraを消す・CS_PlayerCameraFovはプレイヤーの子のCinemachineCameraを探す、ため影響させない)
 *   LensとCinemachineImpulseListenerの設定は、プレイヤーのCinemachineCameraと同じにする(揺れが乗るように)
 * ・切り替え: フィニッシュ用カメラのPriorityを上げ下げする(下げる時もカメラは有効のままなので、Brainがブレンドで戻る)
 *   Brainの既定のブレンド(DefaultBlend)は、切り替えの間だけコードで差し替え、終わったら元に戻す(Prefabの値は書き換えない)
 *   切り替え(カット)・中止の時はCut、戻る時だけイーズイン・アウトの_returnTime秒
 * ・中止: 当たる前に奇襲が中止された時・動けなくなった(死亡・スタン)時・無効にされた時は、すぐに(カットで)元のカメラへ戻す
 * ・連続奇襲: 最後の1人の時だけ切り替わる。保つ・戻る間に次の奇襲が始まっても、その悪人が最後の1人でなければそのまま続ける
 *   最後の1人なら、すぐカットでそちらへ切り替え直す
 * ・見た目の演出用なので通信はしない
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerAmbush))]
public class CS_PlayerAmbushFinishCamera : MonoBehaviour
{
    [Header("最後の1人の判定")]
    [SerializeField] private bool _includeUngrouped = true;   // グループに属さない悪人(groupIdが-1)も最後の1人として扱うか

    [Header("カメラの位置(悪人の向こう側)")]
    [SerializeField][Min(0f)] private float _minBehindDistance = 4f;     // 悪人からカメラまでの最小の距離(m)
    [SerializeField][Min(0f)] private float _maxBehindDistance = 12f;    // 悪人からカメラまでの最大の距離(m)
    [SerializeField][Min(0f)] private float _arrivalDistance = 1.2f;     // 飛び込みで着いた時の、プレイヤーと悪人の水平距離(m。CS_PlayerAmbushの_strikeDistanceと同じ値にする)
    [SerializeField][Min(0f)] private float _cameraHeight = 1.4f;        // カメラの高さ(悪人の足元から。m)
    [SerializeField][Min(0f)] private float _lookHeight = 0.9f;          // 注視点の高さ(プレイヤーの足元から。m)
    [SerializeField][Range(10f, 90f)] private float _fieldOfView = 40f;  // 画角(度。縦)
    [SerializeField][Min(1f)] private float _bodyMargin = 2.2f;          // 全身を映す時の余白(プレイヤーの高さの何倍を画面の縦に収めるか)
    [SerializeField][Min(0f)] private float _fallbackBodyHeight = 1.8f;  // 当たり判定が無い時のプレイヤーの高さ(m)

    [Header("地面")]
    [SerializeField] private LayerMask _groundLayers = ~0;               // 地面のレイヤー(カメラが地面の下に行かないようにする)
    [SerializeField][Min(0f)] private float _groundClearance = 0.5f;     // カメラを地面から離す高さ(m)

    [Header("建物の透過")]
    [SerializeField][Min(0f)] private float _occlusionRadius = 1.0f;     // 切り替えている間、カメラ→プレイヤー・悪人の線を調べる太さ(m)

    [Header("時間")]
    [SerializeField][Min(0f)] private float _holdTime = 0.25f;           // 命中してから、そのカメラのまま保つ時間(秒)
    [SerializeField][Min(0.01f)] private float _returnTime = 0.25f;      // プレイヤー視点のカメラへ戻る時間(秒)

    [Header("切り替え")]
    [SerializeField] private int _activePriority = 100;                  // フィニッシュカメラを使っている間のPriority(プレイヤーのカメラより大きく)
    [SerializeField] private int _idlePriority = -100;                   // 使っていない間のPriority(プレイヤーのカメラより小さく)

    private enum State
    {
        Idle,       // フィニッシュカメラを使っていない
        Active,     // 切り替えて、命中を待っている
        Holding,    // 命中して、そのカメラのまま保っている
        Returning,  // プレイヤー視点のカメラへ戻っている
    }

    private const int _maxHitCount = 16;               // 地面を調べる時の当たりの上限
    private const float _groundCastHeight = 3f;        // 地面を調べる時、カメラの何m上から下へ調べるか
    private const float _groundCastDistance = 30f;     // 地面を調べる距離(m)
    private const int _restoreFrames = 2;              // Brainのブレンドを元に戻すまで待つフレーム数(Brainが切り替えを処理し終えるまで)
    private const float _returnMargin = 0.1f;          // 戻りが終わったとみなすまでの余裕(秒)

    private readonly RaycastHit[] _hits = new RaycastHit[_maxHitCount];

    private CS_Player _player;
    private CS_PlayerAmbush _ambush;
    private CS_PlayerCameraOcclusion _occlusion;        // 建物の透過(付いていなければ透過しない)
    private CinemachineBrain _brain;                    // 自分のカメラのBrain(無ければ何もしない)
    private CinemachineCamera _playerCamera;            // プレイヤーのCinemachineCamera(Lens・揺れの設定の元)
    private CinemachineCamera _finishCamera;            // フィニッシュ用のCinemachineCamera(使う時に作る)
    private State _state = State.Idle;
    private float _timer;                               // 保つ・戻る残り時間(秒)
    private bool _isBlendOverridden;                    // Brainの既定のブレンドを差し替えているか
    private CinemachineBlendDefinition _originalBlend;  // 差し替える前のBrainの既定のブレンド
    private int _restoreCountdown;                      // 0より大きい間は、Brainのブレンドを元に戻すのを待っている(フレーム数)
    private CS_VillainHealth _decidedVillain;           // 今の奇襲で、最後の1人かを判定済みの悪人(溜めの開始と飛び込み開始で、2回数えないため)

    public bool isFinishing => _state != State.Idle;    // フィニッシュカメラを使っている間か(本人の手元。CS_PlayerAmbushEffectが揺れの強さに使う)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _ambush = GetComponent<CS_PlayerAmbush>();
        _occlusion = GetComponent<CS_PlayerCameraOcclusion>();
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

        // 無効にされた時は、すぐ元のカメラへ戻し、Brainの設定も元に戻す
        Cancel();
        RestoreBlend();
        if (_occlusion != null) _occlusion.ClearExtraTarget();
    }

    private void OnDestroy()
    {
        if (_finishCamera != null) Destroy(_finishCamera.gameObject);
    }

    private void Update()
    {
        // 奇襲が終わったら、判定済みの悪人を忘れる
        if (_decidedVillain != null && !_ambush.isAmbushing) _decidedVillain = null;

        // 切り替え(カット)・中止のあと、Brainが処理し終えたらブレンドを元に戻す
        if (_restoreCountdown > 0 && --_restoreCountdown == 0 && _state != State.Returning) RestoreBlend();

        switch (_state)
        {
            case State.Active:
                // 当たる前に奇襲が中止された時(動けなくなった時も含む)は、すぐ戻す
                if (!_ambush.isAmbushing) Cancel();
                break;
            case State.Holding:
                UpdateHolding();
                break;
            case State.Returning:
                UpdateReturning();
                break;
        }
    }

    // ========================================
    // 奇襲のイベント
    // ========================================

    private void HandleWindupStarted(CS_VillainHealth villain, float windupTime)
    {
        TryBegin(villain);
    }

    // 溜めが無い時は、飛び込み開始で切り替える(溜めの開始で判定済みなら何もしない)
    private void HandleStarted(CS_VillainHealth villain)
    {
        if (_decidedVillain == villain) return;

        TryBegin(villain);
    }

    private void HandleHitTiming(CS_VillainHealth villain)
    {
        if (_state != State.Active) return;

        _state = State.Holding;
        _timer = _holdTime;
    }

    // ========================================
    // 切り替え
    // ========================================

    // 最後の1人なら、フィニッシュカメラへカットで切り替える
    private void TryBegin(CS_VillainHealth villain)
    {
        if (!_player.isLocalPlayer || villain == null) return;

        _decidedVillain = villain;
        if (!IsLastOfGroup(villain)) return;
        if (!EnsureCameras()) return;

        Vector3 cameraPosition = PlaceCamera(villain);

        // 切り替えている間、カメラ→プレイヤー・カメラ→悪人の間の建物を透過する(判定にはフィニッシュカメラの位置を直接渡す)
        if (_occlusion != null) _occlusion.SetExtraTarget(villain.transform, _occlusionRadius, cameraPosition);

        // カットで切り替える(Brainの既定のブレンドはCutにして、少ししたら元に戻す)
        SetBlend(new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f));
        _restoreCountdown = _restoreFrames;
        _finishCamera.Priority = _activePriority;
        _state = State.Active;
    }

    // 命中のあと、_holdTime秒たったら戻り始める
    private void UpdateHolding()
    {
        // 保っている間に動けなくなった(死亡・スタン)時は、すぐ戻す
        if (!_player.canAct)
        {
            Cancel();
            return;
        }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;

        // プレイヤー視点のカメラへ、_returnTime秒かけてなめらかに戻る
        SetBlend(new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _returnTime));
        _finishCamera.Priority = _idlePriority;
        _state = State.Returning;
        _timer = _returnTime + _returnMargin;

        // 戻りのブレンドの間は、実際のカメラの位置から調べる(透過の対象は、そのまま続ける)
        if (_occlusion != null) _occlusion.SetCameraPositionOverride(null);
    }

    // ブレンドが終わったら、Brainの既定のブレンドを元に戻す
    private void UpdateReturning()
    {
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        if (_brain != null && _brain.IsBlending) return;

        RestoreBlend();
        if (_occlusion != null) _occlusion.ClearExtraTarget();
        _state = State.Idle;
    }

    // すぐ(カットで)元のカメラへ戻す
    private void Cancel()
    {
        if (_state == State.Idle) return;

        SetBlend(new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f));
        _restoreCountdown = _restoreFrames;
        if (_finishCamera != null) _finishCamera.Priority = _idlePriority;
        if (_occlusion != null) _occlusion.ClearExtraTarget();
        _state = State.Idle;
    }

    // Brainの既定のブレンドを差し替える(元の値は最初の1回だけ控える)
    private void SetBlend(CinemachineBlendDefinition blend)
    {
        if (_brain == null) return;

        if (!_isBlendOverridden)
        {
            _originalBlend = _brain.DefaultBlend;
            _isBlendOverridden = true;
        }
        _brain.DefaultBlend = blend;
    }

    private void RestoreBlend()
    {
        if (!_isBlendOverridden) return;

        _isBlendOverridden = false;
        if (_brain != null) _brain.DefaultBlend = _originalBlend;
    }

    // ========================================
    // 最後の1人の判定
    // ========================================

    // 狙った悪人のほかに、同じグループで倒れていない悪人がいないか(溜めの開始時に1回だけ数える)
    private bool IsLastOfGroup(CS_VillainHealth target)
    {
        CS_VillainCrime targetCrime = target.GetComponent<CS_VillainCrime>();
        if (targetCrime == null || targetCrime.groupId < 0) return _includeUngrouped;

        CS_VillainCrime[] villains = FindObjectsByType<CS_VillainCrime>(FindObjectsSortMode.None);
        foreach (CS_VillainCrime villain in villains)
        {
            if (villain == targetCrime || villain.groupId != targetCrime.groupId) continue;

            CS_VillainHealth health = villain.GetComponent<CS_VillainHealth>();
            if (health == null || health.isDefeated || health.currentHp <= 0f) continue;

            return false;   // 同じグループに、まだ倒れていない悪人がいる
        }
        return true;
    }

    // ========================================
    // カメラ
    // ========================================

    // フィニッシュ用のCinemachineCameraを用意する(初めて使う時に1回だけ作る)。使えない時はfalse
    private bool EnsureCameras()
    {
        if (_brain == null) _brain = GetComponentInChildren<CinemachineBrain>(true);
        if (_playerCamera == null) _playerCamera = GetComponentInChildren<CinemachineCamera>(true);
        if (_brain == null || _playerCamera == null) return false;
        if (_finishCamera != null) return true;

        // プレイヤーの子にはしない(シーンの直下に置く)
        GameObject cameraObject = new GameObject("AmbushFinishCamera");
        _finishCamera = cameraObject.AddComponent<CinemachineCamera>();
        _finishCamera.Lens = _playerCamera.Lens;
        _finishCamera.Priority = _idlePriority;
        _finishCamera.OutputChannel = _playerCamera.OutputChannel;
        _finishCamera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;

        // 揺れが乗るよう、プレイヤーのカメラと同じ設定のImpulse Listenerを付ける
        CinemachineImpulseListener source = _playerCamera.GetComponent<CinemachineImpulseListener>();
        if (source != null)
        {
            CinemachineImpulseListener listener = cameraObject.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = source.ApplyAfter;
            listener.ChannelMask = source.ChannelMask;
            listener.Gain = source.Gain;
            listener.Use2DDistance = source.Use2DDistance;
            listener.UseCameraSpace = source.UseCameraSpace;
            listener.SignalCombinationMode = source.SignalCombinationMode;
            listener.ReactionSettings = source.ReactionSettings;
        }
        return true;
    }

    // 悪人の向こう側に置いて、プレイヤーの方へ向ける(置いた位置を返す)
    private Vector3 PlaceCamera(CS_VillainHealth villain)
    {
        Vector3 playerPosition = transform.position;
        Vector3 villainPosition = villain.transform.position;

        Vector3 direction = villainPosition - playerPosition;
        direction.y = 0f;
        float playerDistance = direction.magnitude;
        direction = playerDistance > 0.0001f ? direction / playerDistance : transform.forward;

        // プレイヤーの全身が縦に収まる距離を、FOVから求める
        // プレイヤーは飛び込みで悪人の_arrivalDistance手前まで近づくので、その時に収まるよう、
        // 必要な距離から_arrivalDistanceを引いたものを、悪人からカメラまでの距離にする
        float bodyHeight = GetBodyHeight() * _bodyMargin;
        float neededDistance = bodyHeight / (2f * Mathf.Tan(_fieldOfView * 0.5f * Mathf.Deg2Rad));
        float behindDistance = Mathf.Clamp(neededDistance - _arrivalDistance, _minBehindDistance, Mathf.Max(_minBehindDistance, _maxBehindDistance));

        // 壁(建物)では寄せない。地面より下には行かないようにだけする
        Vector3 cameraPosition = villainPosition + Vector3.up * _cameraHeight + direction * behindDistance;
        cameraPosition.y = Mathf.Max(cameraPosition.y, GetGroundHeight(cameraPosition) + _groundClearance);

        Vector3 lookTarget = playerPosition + Vector3.up * _lookHeight;

        LensSettings lens = _finishCamera.Lens;
        lens.FieldOfView = _fieldOfView;
        _finishCamera.Lens = lens;

        Transform cameraTransform = _finishCamera.transform;
        cameraTransform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up));
        return cameraPosition;
    }

    // プレイヤーの高さ(当たり判定の高さ。無ければ既定値)
    private float GetBodyHeight()
    {
        Collider body = GetComponent<Collider>();
        return body != null ? body.bounds.size.y : _fallbackBodyHeight;
    }

    // positionの真下の地面の高さ(Building・Propと、キャラクターは地面として扱わない。見つからなければ-無限大)
    private float GetGroundHeight(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * _groundCastHeight;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, _groundCastDistance, _groundLayers, QueryTriggerInteraction.Ignore);

        float ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i].collider;
            if (hit.GetComponentInParent<IDamageable>() != null) continue;
            if (hit.CompareTag("Building") || hit.CompareTag("Prop")) continue;

            ground = Mathf.Max(ground, _hits[i].point.y);
        }
        return ground;
    }
}
