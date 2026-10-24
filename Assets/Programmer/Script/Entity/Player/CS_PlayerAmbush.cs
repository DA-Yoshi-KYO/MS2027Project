using System;
using Unity.Netcode;
using UnityEngine;

/*
 * 奇襲(気づかれていない悪人の背後・上から攻撃すると、一撃で倒せる)を行うクラス
 * 通常攻撃(5段コンボ、CS_PlayerAttack)とは別の攻撃。待機中に攻撃ボタンを押した時、CS_PlayerAttackがTryStartAmbush()を呼ぶ
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「奇襲」(Issue #275)。次の3つがそろった時に攻撃ボタンを押すと、奇襲になる
 *   ① 悪人がこちらに気づいていない: 臨戦態勢(CS_VillainCombat.isEngaged)でない、または自分以外を狙っている
 *   ② 一定距離まで近づいた: 悪人との水平距離が_range(既定3m)以内
 *   ③ 背後か上を取っている
 *      背後: 悪人の正面から見て、自分のいる方向が_backAngle度(既定120度)以上うしろ(真後ろを中心に左右60度ずつ)
 *      上  : 自分が悪人より_aboveHeight(既定1.5m)以上高い位置にいる
 * ・奇襲の流れ(攻撃している本人の手元で進める)
 *   1. 待機中に攻撃ボタン → 奇襲できる悪人(FindAmbushTarget)がいれば奇襲を始める(いなければCS_PlayerAttackが1段目を出す)
 *   2. 飛び込み: 悪人の今の位置を毎フレーム追いながら、_dashSpeedで_strikeDistanceの距離まで近づく(CS_Player.Lunge)
 *      _maxDashTime秒以内に着けなかった(何かに引っかかった)時や、動けなくなった・悪人が倒れた時は中止する
 *   3. 攻撃: 着いてから_strikeDelay秒後に、その悪人1体だけへ奇襲を当てる(範囲の当たり判定は使わないので空振りしない)
 *   4. _strikeDuration秒で奇襲が終わり、動けるようになる。この後に攻撃ボタンを押すと、通常攻撃の1段目から始まる
 * ・奇襲になった時は、その悪人の残りHP以上のダメージを与えて一撃で倒す(仕様「戦闘をすぐに終わらせる」)
 * ・ダメージはサーバー(またはオフライン)で確定させる。サーバーは、送られてきた位置から奇襲の条件がそろっているかを確かめてから倒す
 *   (悪人の臨戦態勢・標的はサーバーでしか分からないため。飛び込んだ直後はサーバー上の位置が追いついていないので、位置のずれは_range分多めに許す)
 * ・奇襲が決まった時はonAmbush(倒した悪人)で知らせる(サーバー、またはオフラインのみ)
 *   見た目用に、onAmbushStarted(飛び込み開始)・onAmbushHitTiming(当たる瞬間)を本人の手元で出す。演出は未実装
 * ・壁越しの悪人は奇襲できない: プレイヤーの体の中心から悪人の体の中心までの間に、_obstacleLayersのもの(壁など)があれば奇襲にならない
 *   キャラクター(IDamageableを持つもの)は遮るものとして扱わない。飛び込みもこの線の上を進むので、壁をすり抜けて奇襲することもない
 * ・今奇襲できる悪人はFindAmbushTarget()で探せる(全クライアントで呼べる)。ロックオンUI(CS_PlayerLockOnUI)が使う
 * ・_rangeを広げると、遠くから奇襲できるようになる(既定はPlayer.prefabで18m)
 *   悪人に気づかれているかは、CS_VillainCombat.IsTargeting(同期された標的)で判定するので、クライアントでも分かる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerAmbush : NetworkBehaviour
{
    [Header("奇襲の条件")]
    [SerializeField][Min(0f)] private float _range = 3f;                   // 悪人との水平距離がこれ以内(m)
    [SerializeField][Range(0f, 180f)] private float _backAngle = 120f;     // 悪人の正面から見て、これ以上うしろなら背後(度)
    [SerializeField][Min(0f)] private float _aboveHeight = 1.5f;           // 悪人よりこれ以上高ければ上を取っている(m)

    [Header("奇襲できる悪人を探す(ロックオン表示用)")]
    [SerializeField] private LayerMask _villainLayers = ~0;               // 悪人を探すレイヤー
    [SerializeField] private LayerMask _obstacleLayers = ~0;              // 悪人との間にあると奇襲できなくなるもの(壁など)のレイヤー

    [Header("奇襲攻撃")]
    [SerializeField][Min(0.1f)] private float _dashSpeed = 30f;           // 飛び込む速さ(m/秒)
    [SerializeField][Min(0f)] private float _maxDashTime = 1f;            // これ以上かかっても着かなければ奇襲をやめる(秒)
    [SerializeField][Min(0f)] private float _strikeDistance = 1.2f;       // 悪人との水平距離がこれ以内になったら着いたとみなす(m)
    [SerializeField][Min(0f)] private float _strikeDelay = 0.1f;          // 着いてから奇襲が当たるまでの時間(秒)
    [SerializeField][Min(0f)] private float _strikeDuration = 0.4f;       // 着いてから奇襲が終わる(動けるようになる)までの時間(秒)

    private enum Phase
    {
        None,     // 奇襲していない
        Dash,     // 悪人へ飛び込んでいる
        Strike,   // 着いて、攻撃している
    }

    private const int _searchBufferSize = 32;   // 一度に調べるコライダーの上限
    private readonly Collider[] _searchBuffer = new Collider[_searchBufferSize];
    private readonly RaycastHit[] _obstacleBuffer = new RaycastHit[_searchBufferSize];

    private CS_Player _player;
    private CS_PlayerScoring _scoring;       // 付いていればスコアを通してダメージを与える
    private Phase _phase = Phase.None;
    private CS_VillainHealth _target;        // 奇襲している悪人
    private float _elapsed;                  // 今の段階(飛び込み・攻撃)が始まってからの時間
    private bool _hasStruck;                 // 奇襲を当てる処理を行ったか

    public float range => _range;                   // 奇襲できる距離(m)
    public bool isAmbushing => _phase != Phase.None;   // 奇襲中か(CS_PlayerAttackが、通常攻撃・必殺技を止めるのに使う)

    public event Action<CS_VillainHealth> onAmbush;     // 奇襲が決まった時(サーバー、またはオフラインのみ)
    public event Action<CS_VillainHealth> onAmbushStarted;     // 飛び込みを始めた時(本人の手元のみ)。見た目用
    public event Action<CS_VillainHealth> onAmbushHitTiming;   // 奇襲が当たる瞬間(本人の手元のみ)。見た目・カメラの揺れ用

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _scoring = GetComponent<CS_PlayerScoring>();
    }

    private void Update()
    {
        if (_phase == Phase.None) return;

        // 動けなくなった(死亡・スタン)・悪人が倒れた・消えた時はやめる
        if (!_player.canAct || !IsTargetAlive())
        {
            EndAmbush();
            return;
        }

        _elapsed += Time.deltaTime;
        if (_phase == Phase.Dash) UpdateDash();
        else UpdateStrike();
    }

    // ========================================
    // 奇襲攻撃(攻撃している本人の手元で進める)
    // ========================================

    // 奇襲できる悪人がいれば奇襲を始める(始めたらtrue)。CS_PlayerAttackが、待機中に攻撃ボタンが押された時に呼ぶ
    public bool TryStartAmbush()
    {
        if (isAmbushing) return true;

        CS_VillainHealth villain = FindAmbushTarget();
        if (villain == null) return false;

        _target = villain;
        _phase = Phase.Dash;
        _elapsed = 0f;
        _hasStruck = false;
        FaceTarget();
        onAmbushStarted?.Invoke(villain);
        return true;
    }

    // 悪人の今の位置を追いながら、_strikeDistanceまで飛び込む
    private void UpdateDash()
    {
        FaceTarget();

        float distance = HorizontalDistance(_target.transform.position);
        if (distance <= _strikeDistance)
        {
            _phase = Phase.Strike;
            _elapsed = 0f;
            return;
        }

        // 何かに引っかかって着かない時はやめる
        if (_elapsed >= _maxDashTime)
        {
            EndAmbush();
            return;
        }

        // 毎フレーム向きと距離を決め直す(悪人が動いても追いかける)
        float moveDistance = distance - _strikeDistance;
        _player.Lunge(HorizontalDirection(_target.transform.position), moveDistance, moveDistance / _dashSpeed);
    }

    // 着いてから_strikeDelay秒後に当て、_strikeDuration秒で終わる
    private void UpdateStrike()
    {
        FaceTarget();

        if (!_hasStruck && _elapsed >= _strikeDelay)
        {
            _hasStruck = true;
            RequestStrike(_target);
            onAmbushHitTiming?.Invoke(_target);
        }

        if (_elapsed >= Mathf.Max(_strikeDelay, _strikeDuration)) EndAmbush();
    }

    private void EndAmbush()
    {
        _phase = Phase.None;
        _target = null;
        _elapsed = 0f;
        _hasStruck = false;
        _player.ClearAimFacing();
    }

    // 奇襲を当てる処理を依頼する(ダメージはサーバーで確定させる)
    private void RequestStrike(CS_VillainHealth villain)
    {
        Vector3 position = transform.position;

        // オフライン(テストシーン)では、その場で判定する
        if (!IsSpawned)
        {
            ExecuteStrike(villain, position);
            return;
        }

        StrikeRpc(new NetworkObjectReference(villain.NetworkObject), position);
    }

    // Ownerからサーバーへ、奇襲の実行を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void StrikeRpc(NetworkObjectReference villainReference, Vector3 position)
    {
        if (!villainReference.TryGet(out NetworkObject villainObject)) return;
        if (!villainObject.TryGetComponent(out CS_VillainHealth villain)) return;

        ExecuteStrike(villain, position);
    }

    // 奇襲の条件がそろっているか確かめてから、残りHP分のダメージで倒す(サーバー、またはオフラインで実行される)
    private void ExecuteStrike(CS_VillainHealth villain, Vector3 position)
    {
        // 送られてきた位置が、サーバー上の位置から大きく離れていないか確かめてから使う
        // 飛び込んだ直後はサーバー上の位置が追いついていないので、奇襲できる距離の分だけ多めに許す
        Vector3 forward = transform.forward;
        CS_AttackHitDetector.ResolvePose(transform, ref position, ref forward, _range);

        if (!CanAmbush(villain, position, out CS_VillainHealth ambushed)) return;

        DealDamage(ambushed, ambushed.currentHp);
        CS_AttackHitDetector.TryKnockback(ambushed, position);
        NotifyAmbush(ambushed);
    }

    // ダメージを与える。スコア(CS_PlayerScoring)があればそれを通し、誰が倒したかをスコアに反映する
    private void DealDamage(IDamageable target, float damage)
    {
        if (_scoring != null)
        {
            _scoring.DealDamage(target, damage);
            return;
        }

        target.TakeDamage(damage, gameObject);
    }

    private bool IsTargetAlive()
    {
        return _target != null && !_target.isDefeated && _target.currentHp > 0f;
    }

    private void FaceTarget()
    {
        Vector3 direction = HorizontalDirection(_target.transform.position);
        if (direction == Vector3.zero) return;

        _player.SetAimFacing(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
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

    // ========================================
    // 奇襲の条件
    // ========================================

    // この相手に、attackerPositionから攻撃した時に奇襲になるか(サーバー、またはオフラインで呼ぶ)
    public bool CanAmbush(IDamageable target, Vector3 attackerPosition, out CS_VillainHealth villain)
    {
        villain = null;

        CS_VillainHealth candidate = target as CS_VillainHealth;
        if (candidate == null || candidate.isDefeated) return false;
        if (!MeetsAmbushCondition(candidate, attackerPosition)) return false;

        villain = candidate;
        return true;
    }

    // 今の位置から奇襲できる悪人のうち、一番近い悪人を探す(いなければnull。全クライアントで呼べる)
    public CS_VillainHealth FindAmbushTarget()
    {
        Vector3 position = transform.position;
        int count = Physics.OverlapSphereNonAlloc(position, _range, _searchBuffer, _villainLayers, QueryTriggerInteraction.Ignore);

        CS_VillainHealth nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            CS_VillainHealth villain = _searchBuffer[i].GetComponentInParent<CS_VillainHealth>();
            if (villain == null || villain == nearest) continue;

            // isDefeatedはサーバーのみなので、クライアントでも分かるHPでも見る
            if (villain.isDefeated || villain.currentHp <= 0f) continue;
            if (!MeetsAmbushCondition(villain, position)) continue;

            float sqr = (villain.transform.position - position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearest = villain;
            nearestSqr = sqr;
        }
        return nearest;
    }

    // 奇襲が決まったことを知らせる(一撃で倒すダメージを与えた後に呼ぶ)
    private void NotifyAmbush(CS_VillainHealth villain)
    {
        onAmbush?.Invoke(villain);
    }

    // ②距離 ①気づかれていない ③背後か上 ④間に壁が無い、がそろっているか
    private bool MeetsAmbushCondition(CS_VillainHealth villain, Vector3 attackerPosition)
    {
        Transform villainTransform = villain.transform;
        Vector3 toAttacker = attackerPosition - villainTransform.position;
        Vector3 flat = new Vector3(toAttacker.x, 0f, toAttacker.z);

        if (flat.magnitude > _range) return false;
        if (IsNoticed(villain)) return false;
        if (!IsAbove(toAttacker.y) && !IsBehind(villainTransform.forward, flat)) return false;
        return IsClearLine(attackerPosition, villainTransform);
    }

    // fromから悪人の体の中心までの間に、壁など(_obstacleLayers)が無いか
    // 悪人自身・プレイヤー・他の悪人などのキャラクター(IDamageableを持つもの)は遮るものとして扱わない
    private bool IsClearLine(Vector3 from, Transform villain)
    {
        Vector3 toVillain = villain.position - from;
        float distance = toVillain.magnitude;
        if (distance <= 0.0001f) return true;

        int count = Physics.RaycastNonAlloc(from, toVillain / distance, _obstacleBuffer, distance, _obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _obstacleBuffer[i].collider;
            if (hit.transform.IsChildOf(villain) || hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<IDamageable>() != null) continue;

            return false;
        }
        return true;
    }

    // 悪人がこちらに気づいているか(臨戦態勢で、自分を狙っている)
    private bool IsNoticed(CS_VillainHealth villain)
    {
        CS_VillainCombat combat = villain.GetComponent<CS_VillainCombat>();
        if (combat == null) return false;

        return combat.IsTargeting(transform.root);
    }

    private bool IsAbove(float heightDifference)
    {
        return heightDifference >= _aboveHeight;
    }

    // 悪人の正面から見て、自分が背後の範囲にいるか
    private bool IsBehind(Vector3 villainForward, Vector3 flatToAttacker)
    {
        villainForward.y = 0f;
        if (villainForward.sqrMagnitude < 0.0001f || flatToAttacker.sqrMagnitude < 0.0001f) return true;   // 重なっている時は背後扱い

        return Vector3.Angle(villainForward, flatToAttacker) >= _backAngle;
    }

    // 選択中に、奇襲できる範囲をシーンビューへ表示する(調整用)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
