using UnityEngine;

/*
 * キャラが動いて見える間、足音を鳴らすクラス
 * 警察・悪人など、足の動きが他のPCへ同期されないキャラ用(プレイヤーは CS_Player が1歩ずつ鳴らす)
 */

// ========================================
/*
 * メモ
 * ・各クライアントが、見えている位置の動きから、自分のPCで鳴らす(通信しない)
 * ・歩きと走りは、動く速さ(_runSpeed)で分ける。ID未設定(None)の方は鳴らさない
 *   例: 警察 巡回 = PolicePatrolFootstep / 追跡 = PoliceChaseFootstep
 * ・位置が大きく飛んだ時(スポーン・テレポート)は、速さ・距離に数えない
 * ・足すだけで動く。外しても、ゲームの動きは変わらない
 *
 * ・鳴らし方は2通り(_useLoop)
 *   ループ(オン・既定): 動いている間(速さが_moveSpeed以上)、足音の音素材をループで流し続ける。止まったら_fadeOutTime秒で消す
 *     何歩分も入った長い音素材用。このキャラが持つAudioSource(実行時に子として作る)で鳴らす
 *     設定(音量・3D・出力先)はSEのデータベースのものを使う(CS_SoundManager.TrySetupLoopSe)
 *     歩き⇔走りが切り替わったら、今の足音を_fadeOutTime秒で消してから、もう一方を流す
 *     流し始めの位置は、音素材の中からランダムに選ぶ(何人も同時に歩き出しても、足音がそろわない)
 *   1歩ずつ(オフ): 動いた距離_stepDistanceごとに、足音を1回鳴らす(1歩分の短い音素材用)
 */
// ========================================

public class CS_MoveFootstepSe : MonoBehaviour
{
    [Header("SE")]
    [SerializeField] private CSE_SeId _walkSeId = CSE_SeId.None;    // 歩いている時の足音
    [SerializeField] private CSE_SeId _runSeId = CSE_SeId.None;     // 走っている時の足音

    [Header("鳴らし方")]
    [SerializeField] private bool _useLoop = true;                      // 動いている間ループで流すか(オフなら1歩ずつ鳴らす)
    [SerializeField, Min(0f)] private float _runSpeed = 4f;             // これ以上の速さなら走りの足音(m/秒)
    [SerializeField, Min(0.1f)] private float _teleportDistance = 3f;   // 1フレームでこれ以上動いたら、飛んだとみなして数えない(m)

    [Header("ループ")]
    [SerializeField, Min(0f)] private float _moveSpeed = 0.5f;          // これ以上の速さなら、動いているとみなして流す(m/秒)
    [SerializeField, Min(0f)] private float _fadeInTime = 0.05f;        // 流し始めに、音量を上げきるまでの時間(秒)
    [SerializeField, Min(0f)] private float _fadeOutTime = 0.15f;       // 止まった時に、消えるまでの時間(秒)
    [SerializeField] private bool _randomStartTime = true;              // 流し始めの位置を、音素材の中からランダムに選ぶか

    [Header("1歩ずつ")]
    [SerializeField, Min(0.1f)] private float _stepDistance = 2.2f;     // 足音を鳴らす、動いた距離の間隔(m)

    private const float _speedSmoothRate = 0.2f;    // 速さをなめらかにする割合

    private bool _hasBaseline;
    private Vector3 _lastPosition;
    private float _distanceRemaining;
    private float _smoothedSpeed;

    // ループ
    private AudioSource _loopSource;                    // ループで流すAudioSource(使う時に作る)
    private CSE_SeId _loopSeId = CSE_SeId.None;         // 今流している足音
    private float _loopBaseVolume = 1f;                 // データベースの音量
    private float _loopFade;                            // 今の音量の割合(0〜1)

    private void OnEnable()
    {
        _hasBaseline = false;
        _smoothedSpeed = 0f;
        _distanceRemaining = _stepDistance;
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void OnDestroy()
    {
        if (_loopSource != null) Destroy(_loopSource.gameObject);
    }

    private void Update()
    {
        Vector3 position = transform.position;
        if (!_hasBaseline)
        {
            _hasBaseline = true;
            _lastPosition = position;
            return;
        }

        Vector3 delta = position - _lastPosition;
        delta.y = 0f;
        _lastPosition = position;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        float moved = delta.magnitude;
        if (moved <= _teleportDistance)
        {
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, moved / deltaTime, _speedSmoothRate);
        }
        else
        {
            moved = 0f;   // 飛んだ分は数えない
        }

        if (_useLoop)
        {
            UpdateLoop();
            return;
        }

        StopLoop();
        UpdateStep(moved, position);
    }

    // 走り・歩き・止まっている(None)のどれの足音を流すか
    private CSE_SeId GetCurrentSeId()
    {
        if (_smoothedSpeed < _moveSpeed) return CSE_SeId.None;
        return _smoothedSpeed >= _runSpeed ? _runSeId : _walkSeId;
    }

    // ループ: 動いている間流し、止まったら・歩き⇔走りが変わったらフェードで消す
    private void UpdateLoop()
    {
        CSE_SeId wanted = GetCurrentSeId();
        float deltaTime = Time.unscaledDeltaTime;

        // 今の足音と違う(止まった・切り替わった)なら、まず消す
        if (_loopSeId != CSE_SeId.None && _loopSeId != wanted)
        {
            _loopFade = _fadeOutTime > 0f ? Mathf.MoveTowards(_loopFade, 0f, deltaTime / _fadeOutTime) : 0f;
            ApplyLoopVolume();
            if (_loopFade > 0f) return;
            StopLoop();
        }

        if (wanted == CSE_SeId.None) return;

        // 流し始める
        if (_loopSeId == CSE_SeId.None && !StartLoop(wanted)) return;

        _loopFade = _fadeInTime > 0f ? Mathf.MoveTowards(_loopFade, 1f, deltaTime / _fadeInTime) : 1f;
        ApplyLoopVolume();
    }

    private bool StartLoop(CSE_SeId id)
    {
        CS_SoundManager manager = CS_SoundManager.instance;
        if (manager == null) return false;

        if (_loopSource == null)
        {
            GameObject sourceObject = new GameObject("FootstepLoopSe");
            sourceObject.transform.SetParent(transform, false);
            _loopSource = sourceObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
        }

        if (!manager.TrySetupLoopSe(id, _loopSource)) return false;

        _loopSeId = id;
        _loopBaseVolume = _loopSource.volume;
        _loopFade = 0f;
        ApplyLoopVolume();
        _loopSource.Play();
        if (_randomStartTime && _loopSource.clip != null && _loopSource.clip.length > 0f)
        {
            _loopSource.time = Random.Range(0f, _loopSource.clip.length * 0.99f);
        }
        return true;
    }

    private void ApplyLoopVolume()
    {
        if (_loopSource != null) _loopSource.volume = _loopBaseVolume * _loopFade;
    }

    private void StopLoop()
    {
        if (_loopSource != null)
        {
            _loopSource.Stop();
            _loopSource.clip = null;
        }
        _loopSeId = CSE_SeId.None;
        _loopFade = 0f;
    }

    // 1歩ずつ: 動いた距離_stepDistanceごとに1回鳴らす
    private void UpdateStep(float moved, Vector3 position)
    {
        _distanceRemaining -= moved;
        if (_distanceRemaining > 0f) return;

        _distanceRemaining = _stepDistance;
        CSE_SeId id = _smoothedSpeed >= _runSpeed ? _runSeId : _walkSeId;
        if (id == CSE_SeId.None) return;

        CS_SoundManager.instance?.PlaySe(id, position);
    }
}
