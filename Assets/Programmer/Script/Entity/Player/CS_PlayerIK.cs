using UnityEngine;

/*
 * プレイヤーの見た目のIK(足を地面に合わせる・頭を見たい方へ向ける)を行うクラス
 * Animatorと同じオブジェクト(Player.prefabの子のModel)に、CS_PlayerVisualが起動時に付ける
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・見た目だけの処理(当たり判定・移動には関わらない)。全クライアントで、それぞれの手元で動く
 * ・HumanoidのAnimatorのIK(OnAnimatorIK)を使う。Animator ControllerのレイヤーのIK Passをオンにしておくこと
 *   (Tools/Player/アニメーションを組み込む がオンにする)
 * ・足のIK(段差・坂で足が浮いたり埋まったりしないように)
 *   モーションの足首の位置から真下へ線を飛ばし、当たった地面の高さ・傾きへ足を合わせる
 *   片足が下がる時は、その分だけ腰も下げる(届かない足が伸びきらないように)
 *   止まっている時ほど強く効かせ、移動中は弱める(走りの足の上げ下げを消さないように)。空中では効かせない
 * ・頭のIK(原神のように、近くの相手やカメラの向きへ自然に顔を向ける)
 *   向ける先: 攻撃の補正をしている相手(CS_PlayerAimAssist) → 奇襲している悪人(CS_PlayerAmbush)
 *             → 自分の画面のプレイヤーならカメラの見ている先 → それ以外は体の正面
 *   体の正面から_maxLookAngle度より後ろを見る時は向けない(首が回りすぎないように)
 * ・死亡中は使わない
 */
// ========================================

[RequireComponent(typeof(Animator))]
public class CS_PlayerIK : MonoBehaviour
{
    [Header("足")]
    [SerializeField] private bool _useFootIK = true;
    [SerializeField][Min(0f)] private float _footRayUp = 0.5f;         // 足首の上の、線を飛ばし始める高さ(m)
    [SerializeField][Min(0f)] private float _footRayDown = 0.6f;       // 足首から下へ、地面を探す長さ(m)
    [SerializeField][Min(0f)] private float _footHeight = 0.09f;       // 地面から足首までの高さ(m)
    [SerializeField][Min(0f)] private float _maxFootOffset = 0.4f;     // 足を上下させる最大の量(m)
    [SerializeField][Range(0f, 1f)] private float _movingFootWeight = 0.25f;   // 移動中(Speed = 1)の足のIKの強さ
    [SerializeField][Min(0f)] private float _pelvisSmoothing = 12f;    // 腰の上下のなめらかさ(大きいほどすぐ合わせる)

    [Header("頭")]
    [SerializeField] private bool _useLookIK = true;
    [SerializeField][Range(0f, 1f)] private float _lookWeight = 0.8f;   // 全体の強さ
    [SerializeField][Range(0f, 1f)] private float _lookBodyWeight = 0.15f;
    [SerializeField][Range(0f, 1f)] private float _lookHeadWeight = 0.7f;
    [SerializeField][Range(0f, 1f)] private float _lookClamp = 0.6f;    // 向ける量の制限(1に近いほど回らない)
    [SerializeField][Range(0f, 180f)] private float _maxLookAngle = 100f;
    [SerializeField][Min(0f)] private float _lookSmoothing = 6f;
    [SerializeField][Min(0f)] private float _cameraLookDistance = 15f; // カメラの見ている先として、カメラの前方何mを見るか
    [SerializeField] private float _targetLookHeight = 1.5f;           // 相手を見る時、相手の足元から何m上を見るか

    private Animator _animator;
    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerAimAssist _aimAssist;
    private CS_PlayerAmbush _ambush;
    private int _groundMask;

    private float _pelvisOffset;
    private float _currentLookWeight;
    private Vector3 _lookPosition;

    // CS_PlayerVisualが、付けた直後に呼ぶ
    public void Setup(CS_Player player)
    {
        _player = player;
        _health = player.GetComponent<CS_PlayerHealth>();
        _aimAssist = player.GetComponent<CS_PlayerAimAssist>();
        _ambush = player.GetComponent<CS_PlayerAmbush>();

        // 自分のコライダー(プレイヤーのレイヤー)には当たらないようにする
        _groundMask = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        _lookPosition = transform.position + transform.forward * _cameraLookDistance;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_player == null || !_animator.isHuman) return;

        bool isDead = _health != null && _health.isDead;
        if (_useFootIK) SolveFeet(isDead);
        if (_useLookIK) SolveLook(isDead);
    }

    // ========================================
    // 足
    // ========================================

    private void SolveFeet(bool isDead)
    {
        float weight = 0f;
        if (!isDead && _player.isGrounded)
        {
            float speed = Mathf.Clamp01(_animator.GetFloat(CS_PlayerAnimatorParams.speedHash));
            weight = Mathf.Lerp(1f, _movingFootWeight, speed);
        }

        float leftOffset = SolveFoot(AvatarIKGoal.LeftFoot, weight);
        float rightOffset = SolveFoot(AvatarIKGoal.RightFoot, weight);

        // 下がる方の足に合わせて腰を下げる(上がる時は下げない)
        float targetPelvis = weight > 0f ? Mathf.Min(0f, Mathf.Min(leftOffset, rightOffset)) * weight : 0f;
        _pelvisOffset = Mathf.Lerp(_pelvisOffset, targetPelvis, 1f - Mathf.Exp(-_pelvisSmoothing * Time.deltaTime));
        _animator.bodyPosition += Vector3.up * _pelvisOffset;
    }

    // 足を地面に合わせる。地面までの上下のずれ(m)を返す(地面が無ければ0)
    private float SolveFoot(AvatarIKGoal goal, float weight)
    {
        _animator.SetIKPositionWeight(goal, weight);
        _animator.SetIKRotationWeight(goal, weight);
        if (weight <= 0f) return 0f;

        Vector3 foot = _animator.GetIKPosition(goal);
        Vector3 origin = foot + Vector3.up * _footRayUp;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _footRayUp + _footRayDown, _groundMask, QueryTriggerInteraction.Ignore))
        {
            return 0f;
        }

        float offset = Mathf.Clamp(hit.point.y + _footHeight - foot.y, -_maxFootOffset, _maxFootOffset);
        _animator.SetIKPosition(goal, foot + Vector3.up * offset);
        _animator.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, hit.normal) * _animator.GetIKRotation(goal));
        return offset;
    }

    // ========================================
    // 頭
    // ========================================

    private void SolveLook(bool isDead)
    {
        bool hasTarget = TryGetLookPosition(out Vector3 target);
        if (hasTarget) _lookPosition = target;

        float targetWeight = !isDead && hasTarget && IsInLookRange(_lookPosition) ? _lookWeight : 0f;
        float blend = 1f - Mathf.Exp(-_lookSmoothing * Time.deltaTime);
        _currentLookWeight = Mathf.Lerp(_currentLookWeight, targetWeight, blend);

        _animator.SetLookAtWeight(_currentLookWeight, _lookBodyWeight, _lookHeadWeight, 1f, _lookClamp);
        _animator.SetLookAtPosition(_lookPosition);
    }

    // 頭を向ける先(相手 → カメラの見ている先 → 体の正面)
    private bool TryGetLookPosition(out Vector3 position)
    {
        Transform target = _aimAssist != null ? _aimAssist.currentTarget : null;
        if (target == null && _ambush != null) target = _ambush.currentTarget != null ? _ambush.currentTarget.transform : null;

        if (target != null)
        {
            position = target.position + Vector3.up * _targetLookHeight;
            return true;
        }

        Transform view = _player.isLocalPlayer ? _player.viewTransform : null;
        if (view != null)
        {
            position = view.position + view.forward * _cameraLookDistance;
            return true;
        }

        position = _animator.bodyPosition + transform.forward * _cameraLookDistance;
        return true;
    }

    // 体の正面から見て、首が回りすぎない範囲か
    private bool IsInLookRange(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;

        return Vector3.Angle(transform.forward, direction) <= _maxLookAngle;
    }
}
