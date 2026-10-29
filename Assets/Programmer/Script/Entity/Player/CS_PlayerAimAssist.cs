using UnityEngine;

/*
 * 攻撃のエイムアシスト(近くの敵へ自動で向きを補正し、少し届かない時は踏み込む)を行うクラス
 * 通常攻撃・必殺技の開始時に相手を選び、判定が出るまでその相手の方を向き続ける
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・目的: 「誰でも遊べるように」(仕様書「プレイヤー」の操作について)、狙いが多少ずれていても攻撃が当たるようにする
 * ・流れ(攻撃している本人の手元でだけ動く。CS_PlayerAttack / CS_PlayerSpecialAttackの開始イベントは本人の手元でしか発生しない)
 *   1. 攻撃(各段)・必殺技の開始時に、_range以内・狙っている方向から左右_angle度以内の敵を探す
 *      狙っている方向は、スティック(移動入力)を_stickThreshold以上倒していればその方向、倒していなければカメラの向き
 *      近さと角度で点数を付け、一番狙いやすい1体を選ぶ(見つからなければ何もしない = いつも通りカメラの向きで攻撃する)
 *   2. 選んだ相手の方へすぐに向きを変え(CS_Player.SetAimFacing)、判定が出るまで相手を追って向き続ける
 *   3. 攻撃が届かない距離なら、判定が出るまでに届く距離まで踏み込む(CS_Player.Lunge。最大_maxLungeDistance)
 *   4. 判定が出てから_facingHoldTime秒後に補正をやめ、いつも通りカメラの向きへ戻る
 * ・判定は、攻撃した本人の手元の位置・向きをサーバーへ送って行う(CS_PlayerAttack / CS_PlayerSpecialAttack)
 *   そのため、補正した向き・踏み込んだ位置のまま当たる(オンラインでもNetworkTransformの遅れでずれない)
 * ・補正する相手は悪人を優先する。範囲内に悪人が1体でもいれば悪人の中から選び、悪人がいない時だけ他のプレイヤー(人・NPC)を補正する
 *   (フレンドリーファイアは常に有効。悪人と戦っている最中に、近くの味方の方へ勝手に向かないようにするため)
 *   _targetPlayersをオフにすると、プレイヤーは補正しない(悪人だけ)
 * ・警察は倒せない(IDamageableではない)ため、補正の対象にならない
 * ・補正が終わった後も、体は相手の方を向いたまま(移動キーを押すと進む方向へ回る。CS_Player.UpdateFacing)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerAttack))]
[RequireComponent(typeof(CS_PlayerSpecialAttack))]
public class CS_PlayerAimAssist : MonoBehaviour
{
    [Header("エイムアシスト")]
    [SerializeField] private bool _isAssistEnabled = true;          // エイムアシストを使うか
    [SerializeField][Min(0f)] private float _range = 4f;            // 補正する相手を探す距離(m)
    [SerializeField][Range(0f, 180f)] private float _angle = 75f;   // 狙っている方向(カメラの向き)から左右何度以内の相手を補正するか
    [SerializeField][Min(0f)] private float _anglePriority = 0.5f;  // 相手を選ぶ時に、近さに比べて角度をどれだけ重視するか
    [SerializeField] private bool _targetPlayers = true;            // 他のプレイヤー(人・NPC)も補正の対象にするか(悪人がいない時だけ)
    [SerializeField][Range(0f, 1f)] private float _stickThreshold = 0.3f;  // スティックをこれ以上倒していれば、その方向を狙っている方向にする

    [Header("踏み込み")]
    [SerializeField][Min(0f)] private float _maxLungeDistance = 1.5f;   // 踏み込む最大の距離(m。0で踏み込まない)
    [SerializeField][Range(0.1f, 1f)] private float _lungeTimeRate = 0.8f; // 判定が出るまでの時間のうち、踏み込みに使う割合

    [Header("向き")]
    [SerializeField][Min(0f)] private float _facingHoldTime = 0.1f;   // 判定が出た後も相手の方を向いておく時間(秒)

    private const int _bufferSize = 32;

    private CS_Player _player;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private readonly Collider[] _buffer = new Collider[_bufferSize];

    private Transform _target;       // 補正している相手
    private bool _isTracking;        // 判定が出るまで、相手を追って向いている間か
    private float _releaseTime = -1f; // 補正をやめる時刻(-1は予定なし)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
    }

    private void OnEnable()
    {
        _attack.onStepStarted += HandleStepStarted;
        _attack.onStepHitTiming += HandleHitTiming;
        _special.onSpecialStarted += HandleSpecialStarted;
        _special.onSpecialHitTiming += HandleSpecialHitTiming;
    }

    private void OnDisable()
    {
        _attack.onStepStarted -= HandleStepStarted;
        _attack.onStepHitTiming -= HandleHitTiming;
        _special.onSpecialStarted -= HandleSpecialStarted;
        _special.onSpecialHitTiming -= HandleSpecialHitTiming;
        Release();
    }

    // 今補正している相手(いなければnull)。見た目のIK(CS_PlayerIK)が、頭を向ける先に使う
    public Transform currentTarget => _target;

    private void Update()
    {
        // ダッシュ・ジャンプで攻撃が中断された時は、すぐに補正をやめる(相手の方を向いたままダッシュしないように)
        if (_target != null && (_player.isDashing || !IsActionRunning()))
        {
            Release();
            return;
        }

        if (_isTracking)
        {
            // 判定が出るまでは、動く相手を追って向き続ける(相手が倒れた・消えた時はやめる)
            if (IsValidTarget(_target) && _player.canAct)
            {
                FaceTarget();
            }
            else
            {
                Release();
            }
            return;
        }

        if (_releaseTime >= 0f && Time.time >= _releaseTime)
        {
            Release();
        }
    }

    // 通常攻撃・必殺技の最中か(判定の後、向きを保っている間も含める)
    private bool IsActionRunning()
    {
        if (_releaseTime >= 0f && Time.time < _releaseTime) return true;

        return _attack.currentStep >= 0 || _special.isPerformingSpecial;
    }

    // ========================================
    // 攻撃のイベント
    // ========================================

    private void HandleStepStarted(int stepIndex)
    {
        var steps = _attack.attackSteps;
        if (stepIndex < 0 || stepIndex >= steps.Count) return;

        Begin(steps[stepIndex]);
    }

    private void HandleSpecialStarted()
    {
        Begin(_special.specialAttackData);
    }

    private void HandleHitTiming(int stepIndex)
    {
        EndTracking();
    }

    private void HandleSpecialHitTiming()
    {
        EndTracking();
    }

    // ========================================
    // 補正
    // ========================================

    // 攻撃の開始時に相手を選び、向きを補正し、届かなければ踏み込む
    private void Begin(CSO_AttackData data)
    {
        Release();
        if (!_isAssistEnabled || data == null) return;

        _target = FindTarget();
        if (_target == null) return;

        _isTracking = true;
        FaceTarget();
        TryLunge(data);
    }

    // 判定が出たので相手を追うのをやめ、少しだけ向きを保ってから戻す
    private void EndTracking()
    {
        if (!_isTracking) return;

        _isTracking = false;
        _releaseTime = Time.time + _facingHoldTime;
    }

    private void Release()
    {
        _isTracking = false;
        _releaseTime = -1f;
        _target = null;
        if (_player != null) _player.ClearAimFacing();
    }

    private void FaceTarget()
    {
        Vector3 direction = HorizontalDirection(_target.position);
        if (direction == Vector3.zero) return;

        _player.SetAimFacing(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
    }

    // 判定が届かない距離なら、届く距離まで踏み込む(判定が出るまでに着くように)
    // 通常攻撃: 判定の球(正面hitRange先)の中心が相手に重なる距離まで / 必殺技(自分中心): 範囲の半分の距離まで
    private void TryLunge(CSO_AttackData data)
    {
        if (_maxLungeDistance <= 0f) return;

        float distance = HorizontalDistance(_target.position);
        bool isForward = data.hitRange > 0f;
        float reach = isForward ? data.hitRange + data.hitRadius * 0.5f : data.hitRadius * 0.7f;
        if (distance <= reach) return;

        float stopDistance = isForward ? data.hitRange : data.hitRadius * 0.5f;
        float lungeDistance = Mathf.Min(distance - stopDistance, _maxLungeDistance);
        float duration = Mathf.Max(Time.fixedDeltaTime, data.hitDelay * _lungeTimeRate);

        _player.Lunge(HorizontalDirection(_target.position), lungeDistance, duration);
    }

    // _range以内・狙っている方向から_angle度以内で、一番狙いやすい相手を探す(近いほど・正面に近いほど優先)
    // 悪人を優先する: 範囲内に悪人が1体でもいれば悪人の中から選び、悪人がいない時だけプレイヤーの中から選ぶ
    private Transform FindTarget()
    {
        Vector3 aim = GetAimDirection();
        int count = Physics.OverlapSphereNonAlloc(transform.position, _range, _buffer, _attack.targetLayers, QueryTriggerInteraction.Ignore);

        Transform bestVillain = null, bestOther = null;
        float bestVillainScore = float.MaxValue, bestOtherScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (_buffer[i].transform.IsChildOf(transform)) continue;   // 自分自身

            if (!(_buffer[i].GetComponentInParent<IDamageable>() is Component damageable)) continue;
            if (!IsValidTarget(damageable.transform)) continue;

            Vector3 direction = HorizontalDirection(damageable.transform.position);
            float angle = direction == Vector3.zero ? 0f : Vector3.Angle(aim, direction);
            if (angle > _angle) continue;

            float score = HorizontalDistance(damageable.transform.position) / Mathf.Max(0.01f, _range)
                        + angle / Mathf.Max(0.01f, _angle) * _anglePriority;

            if (damageable is CS_VillainHealth)
            {
                if (score >= bestVillainScore) continue;
                bestVillain = damageable.transform;
                bestVillainScore = score;
            }
            else
            {
                if (score >= bestOtherScore) continue;
                bestOther = damageable.transform;
                bestOtherScore = score;
            }
        }

        return bestVillain != null ? bestVillain : bestOther;
    }

    // 狙っている方向。スティック(移動入力)を倒していればその方向、倒していなければカメラの向き
    // (仕様「攻撃範囲内に敵が複数人いる場合、スティックを倒している方向の敵を優先して攻撃したい」)
    private Vector3 GetAimDirection()
    {
        Vector3 stick = _player.moveDirection;
        if (stick.magnitude >= _stickThreshold) return stick.normalized;

        return Quaternion.Euler(0f, _player.viewYaw, 0f) * Vector3.forward;
    }

    // 補正してよい相手か(倒れていない悪人。_targetPlayersがオンなら倒れていないプレイヤー(人・NPC)も)
    private bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        if (target.TryGetComponent(out CS_VillainHealth villain)) return !villain.isDefeated;
        if (target.TryGetComponent(out CS_PlayerHealth player)) return _targetPlayers && !player.isDead;

        return true;   // その他のIDamageable(的など)
    }

    private Vector3 HorizontalDirection(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private float HorizontalDistance(Vector3 targetPosition)
    {
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // 選択中に、補正する範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, _range);

        float yaw = Application.isPlaying && _player != null ? _player.viewYaw : transform.eulerAngles.y;
        Vector3 left = Quaternion.Euler(0f, yaw - _angle, 0f) * Vector3.forward * _range;
        Vector3 right = Quaternion.Euler(0f, yaw + _angle, 0f) * Vector3.forward * _range;
        Gizmos.DrawLine(transform.position, transform.position + left);
        Gizmos.DrawLine(transform.position, transform.position + right);
    }
}
