using UnityEngine;

/*
 * 動いて見える距離ごとに、足音を鳴らすクラス
 * 警察・悪人など、足の動きが他のPCへ同期されないキャラ用(プレイヤーは CS_Player が同じ方式で鳴らす)
 */

// ========================================
/*
 * メモ
 * ・各クライアントが、見えている位置の動きから、自分のPCで鳴らす(通信しない)
 * ・歩きと走りは、動く速さ(_runSpeed)で分ける。ID未設定(None)の方は鳴らさない
 *   例: 警察 巡回 = PolicePatrolFootstep / 追跡 = PoliceChaseFootstep
 * ・位置が大きく飛んだ時(スポーン・テレポート)は、距離に数えない
 * ・足すだけで動く。外しても、ゲームの動きは変わらない
 */
// ========================================

public class CS_MoveFootstepSe : MonoBehaviour
{
    [Header("SE")]
    [SerializeField] private CSE_SeId _walkSeId = CSE_SeId.None;    // 歩いている時の足音
    [SerializeField] private CSE_SeId _runSeId = CSE_SeId.None;     // 走っている時の足音

    [Header("鳴らし方")]
    [SerializeField, Min(0.1f)] private float _stepDistance = 2.2f;     // 足音を鳴らす、動いた距離の間隔(m)
    [SerializeField, Min(0f)] private float _runSpeed = 4f;             // これ以上の速さなら走りの足音(m/秒)
    [SerializeField, Min(0.1f)] private float _teleportDistance = 3f;   // 1フレームでこれ以上動いたら、飛んだとみなして数えない(m)

    private const float _speedSmoothRate = 0.2f;    // 速さをなめらかにする割合

    private bool _hasBaseline;
    private Vector3 _lastPosition;
    private float _distanceRemaining;
    private float _smoothedSpeed;

    private void OnEnable()
    {
        _hasBaseline = false;
        _smoothedSpeed = 0f;
        _distanceRemaining = _stepDistance;
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
        if (moved > _teleportDistance) return;

        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, moved / deltaTime, _speedSmoothRate);
        _distanceRemaining -= moved;
        if (_distanceRemaining > 0f) return;

        _distanceRemaining = _stepDistance;
        CSE_SeId id = _smoothedSpeed >= _runSpeed ? _runSeId : _walkSeId;
        if (id == CSE_SeId.None) return;

        CS_SoundManager.instance?.PlaySe(id, position);
    }
}
