using UnityEngine;

/*
 * 見張り役の悪人が、路地裏の端から端へ巡回し、端できょろきょろ見回すのを繰り返すクラス
 * 見張り役かどうか・巡回ルートはCS_VillainSpawnerが決め、CS_VillainCombatが犯罪中(Idle)の間だけTickを呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・流れ: 端Aへ移動 → 着いたら lookAroundTime 秒見回す → 端Bへ移動 → 見回す → …の繰り返し
 *   最初は、今いる位置から遠い方の端へ向かう
 * ・見回す: 着いた時の向きを中心に、左右 lookAroundAngle 度ずつ首を振るように体の向きを変える(lookAroundPeriod 秒で1往復)
 * ・巡回中も犯罪中(Idle)のままなので、臨戦態勢範囲にプレイヤーが入れば気付く(CS_VillainCombat側で判定)
 *   臨戦態勢 → 帰還(スポーン位置へ戻る)の後は、また遠い方の端から巡回を再開する
 * ・移動の速さは CS_VillainCombat が渡す(プレイヤーの通常移動速度 × CSO_VillainStatsの巡回時の倍率)
 * ・処理はサーバー(オフライン時はその場)でのみ行う。位置・向きはNetworkTransformで同期する
 */
// ========================================

[RequireComponent(typeof(CS_VillainMove))]
public class CS_VillainPatrol : MonoBehaviour
{
    private const float _arriveDistance = 0.3f;   // 巡回の端に着いたとみなす距離(m)

    [SerializeField, Min(0f)]
    [Tooltip("巡回の端に着いてから、見回す時間(秒)")]
    private float _lookAroundTime = 3f;

    [SerializeField, Range(0f, 180f)]
    [Tooltip("見回す時に、左右へ向きを変える角度(度)")]
    private float _lookAroundAngle = 60f;

    [SerializeField, Min(0.1f)]
    [Tooltip("見回す時に、左右を1往復する時間(秒)")]
    private float _lookAroundPeriod = 2f;

    private CS_VillainMove _move;
    private readonly Vector3[] _ends = new Vector3[2];
    private bool _hasRoute;
    private int _nextEnd;             // 次に向かう端(0 / 1)
    private bool _isLookingAround;    // 端で見回している途中か
    private float _lookTimer;
    private Vector3 _lookForward;     // 見回す時の中心の向き(着いた時の向き)

    public bool hasRoute => _hasRoute;

    private void Awake()
    {
        _move = GetComponent<CS_VillainMove>();
    }

    // 巡回ルートを設定する(生成直後にCS_VillainSpawnerから呼ばれる)
    public void SetRoute(Vector3 endA, Vector3 endB)
    {
        _ends[0] = endA;
        _ends[1] = endB;
        _hasRoute = true;
        Restart();
    }

    // 今いる位置から遠い方の端へ向かうところから始め直す(帰還して犯罪中に戻った時など)
    public void Restart()
    {
        _isLookingAround = false;
        _nextEnd = (transform.position - _ends[0]).sqrMagnitude >= (transform.position - _ends[1]).sqrMagnitude ? 0 : 1;
    }

    // 巡回を1回分進める(犯罪中の間、CS_VillainCombatが毎回呼ぶ)
    public void Tick(float speed, float deltaTime)
    {
        if (!_hasRoute) return;

        if (_isLookingAround)
        {
            LookAround(deltaTime);
            return;
        }

        _move.MoveTo(_ends[_nextEnd], speed);
        if (!_move.IsNearDestination(_arriveDistance)) return;

        // 端に着いたら、その場で見回し始める
        _move.Stop();
        _isLookingAround = true;
        _lookTimer = 0f;
        _lookForward = transform.forward;
    }

    // 着いた時の向きを中心に、左右へ首を振るように向きを変える。終わったら反対の端へ向かう
    private void LookAround(float deltaTime)
    {
        _lookTimer += deltaTime;
        if (_lookTimer >= _lookAroundTime)
        {
            _isLookingAround = false;
            _nextEnd = 1 - _nextEnd;
            return;
        }

        float yaw = Mathf.Sin(_lookTimer / _lookAroundPeriod * Mathf.PI * 2f) * _lookAroundAngle;
        _move.FaceTowards(Quaternion.Euler(0f, yaw, 0f) * _lookForward);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_hasRoute) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_ends[0], _ends[1]);
    }
}
