using Unity.Netcode;
using UnityEngine;

/*
 * NPC(4人に満たない枠を埋める、人の代わりのプレイヤー)の判断を行うクラス
 * 状況を見て行動を選び、人と同じ入力(移動・向き・ボタン)としてCS_PlayerNpcInputへ書き込む
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様: 仕様書「NPC(他プレイヤー)」、データ表「NPC(他プレイヤー)思考／行動指針データ」、Issue #165 / #166
 *   LLMや機械学習は使わず、行動ごとに点数を付けて一番高いものを選ぶ(ユーティリティAI)
 *   点数 = 性格(CSO_NpcPersonality)の重み × 近さ。性格の違いは重みと数値で表す
 * ・NPCはプレイヤーと同じ部品(移動・攻撃・変身・必殺技・アイテム・インタラクト・手配度・スコア)をそのまま使う
 *   ここは「ボタンを押す・スティックを倒す」だけを行い、数値やルールは人と全く同じ
 * ・動くのはNPCを操作するマシン(サーバー、またはオフライン)だけ(CS_Player.npcInputがある時)
 * ・各部品のUpdateより先にボタンを押す必要があるため、実行順を早めている
 * ・選べる行動
 *   悪人を倒す / アイテムを拾う / イベント(ケアパッケージなど)に参加する / 他のプレイヤーを邪魔する /
 *   追ってくる警察から逃げる / うろつく
 * ・行動ごとの細かい動き
 *   悪人・邪魔する相手: 近づき、変身距離に入ったら変身、届く距離なら攻撃。囲めていれば必殺技
 *   逃げる: 変身を解き(仕様「変形を解けばバレない」)、警察と反対側へダッシュで逃げる
 *   イベント: 操作できる距離まで近づき、通常攻撃ボタン(=インタラクト)を押し続ける
 *   変身は、手配度が高くなりすぎた時・標的が無い状態が続いた時に解く
 * ・引っかかって進めない時は、ジャンプして経路を計算し直す。何度も続く時は、その行動をあきらめる
 * ・プレイヤーのスコアを見て強さを変える調整(Issue #167)はクローズ済みのため入れていない
 */
// ========================================

[DefaultExecutionOrder(-50)] // ボタンを押す処理を、各部品(CS_PlayerAttackなど)のUpdateより先に行う
[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
[RequireComponent(typeof(CS_PlayerTransformation))]
[RequireComponent(typeof(CS_PlayerWantedLevel))]
[RequireComponent(typeof(CS_PlayerSpecialGauge))]
[RequireComponent(typeof(CS_PlayerItemSlot))]
public class CS_PlayerNpcBrain : NetworkBehaviour
{
    private enum GoalType { None, Villain, Item, Event, Harass, Flee, Wander }

    [Header("性格")]
    [SerializeField] private CSO_NpcPersonality _defaultPersonality;   // スポナーから渡されなかった時に使う

    [Header("距離")]
    [SerializeField] private float _arriveDistance = 1f;         // 目的地に着いたとみなす距離(m)
    [SerializeField] private float _sprintDistance = 8f;         // これより遠い目標へはダッシュ・スプリントで向かう(m)
    [SerializeField] private float _waitDistance = 3.5f;         // 変身できない間、標的からこの距離で待つ(m)
    [SerializeField] private float _itemUseDistance = 8f;        // 標的がこの距離以内ならアイテムを使う(m)
    [SerializeField] private float _fleeDistance = 15f;          // 逃げる時に、警察から離れる距離(m)

    private const float _transformTimeEstimate = 5f;   // 変身にかかるおおよその秒数(間に合うかの見積もり用)
    private const float _stuckCheckInterval = 1f;      // 引っかかっていないか確かめる間隔(秒)
    private const float _stuckMoveDistance = 0.5f;     // この間隔で、これしか進んでいなければ引っかかっている(m)
    private const int _stuckGiveUpCount = 3;           // 引っかかりがこの回数続いたら、その行動をあきらめる
    private const float _maxAimOffset = 60f;           // 攻撃の向きがずれる時の最大角度(度)
    private const float _interactRetryInterval = 2f;   // 操作が始まらない時に、ボタンを押し直す間隔(秒)
    private const float _keepGoalBonus = 1.2f;         // 今の標的を選び続ける時の点数の上乗せ(似た点数の標的の間で、行ったり来たりしないように)

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerTransformation _transformation;
    private CS_PlayerWantedLevel _wantedLevel;
    private CS_PlayerSpecialGauge _gauge;
    private CS_PlayerItemSlot _itemSlot;
    private CS_PlayerAttack _attack;
    private CS_PlayerSpecialAttack _special;
    private CS_PlayerStats _stats;
    private CSO_NpcPersonality _personality;
    private readonly CS_NpcNavigator _navigator = new CS_NpcNavigator();

    // 今の行動
    private GoalType _goalType;
    private Transform _goalTarget;          // 悪人・アイテム・邪魔する相手
    private IInteractable _goalInteractable;
    private Vector3 _goalPosition;          // 逃げ先・うろつき先
    private float _actStartTime;            // 反応の遅れが明けて動き出す時刻

    private float _nextDecisionTime;
    private float _nextAttackTime;
    private float _aimOffset;               // 今の攻撃の向きのずれ(度)
    private float _lastHasTargetTime;       // 最後に標的(悪人・邪魔する相手)がいた時刻
    private bool _useItemPending;           // 次のフレームでアイテムを使う
    private float _nextInteractRetryTime;
    private bool _isHoldingInteract;        // インタラクトのボタンを押し続けている途中か

    private Vector3 _stuckCheckPosition;
    private float _nextStuckCheckTime;
    private int _stuckCount;
    private bool _isMoving;

    // このフレームで押すボタン(毎フレーム決め直し、最後にまとめて反映する)
    private bool _wantAttack, _wantSpecial, _wantDash, _wantUseItem, _wantTransform, _wantJump;

    public CSO_NpcPersonality personality => _personality;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _transformation = GetComponent<CS_PlayerTransformation>();
        _wantedLevel = GetComponent<CS_PlayerWantedLevel>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
        _itemSlot = GetComponent<CS_PlayerItemSlot>();
        _attack = GetComponent<CS_PlayerAttack>();
        _special = GetComponent<CS_PlayerSpecialAttack>();
        _stats = GetComponent<CS_PlayerStats>();
    }

    // 性格を決める(CS_PlayerSpawnerが、スポーンする前に呼ぶ)
    public void Setup(CSO_NpcPersonality personality)
    {
        _personality = personality;
    }

    private void Update()
    {
        CS_PlayerNpcInput input = _player.npcInput;
        if (input == null) return;   // NPCを操作するマシン以外では何もしない

        if (_personality == null) _personality = _defaultPersonality;
        if (_personality == null || !_player.canAct)
        {
            input.Clear();
            return;
        }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + _personality.decisionInterval;
            Decide();
        }

        ClearWants();
        _isMoving = false;
        input.move = Vector2.zero;

        if (Time.time >= _actStartTime)
        {
            Act(input);
            UpdateTransformation();
        }

        UpdateStuck();
        ApplyWants(input);
    }

    // ========================================
    // 行動を選ぶ
    // ========================================

    // 行動ごとに点数を付けて、一番高いものを選ぶ
    private void Decide()
    {
        Vector3 position = transform.position;
        bool isLowHp = _health.currentHp <= _health.maxHp * _personality.fleeHpRatio;

        GoalType bestType = GoalType.Wander;
        float bestScore = _personality.wanderWeight;
        Transform bestTarget = null;
        IInteractable bestInteractable = null;

        ConsiderVillains(position, isLowHp, ref bestType, ref bestScore, ref bestTarget);
        ConsiderItems(position, isLowHp, ref bestType, ref bestScore, ref bestTarget);
        ConsiderEvents(position, ref bestType, ref bestScore, ref bestInteractable);
        ConsiderHarass(position, ref bestType, ref bestScore, ref bestTarget);
        bool shouldFlee = ConsiderFlee(position, isLowHp, ref bestType, ref bestScore, out Vector3 fleePosition);

        SetGoal(bestType, bestTarget, bestInteractable, shouldFlee && bestType == GoalType.Flee ? fleePosition : _goalPosition);
        DecideItemUse(position);
    }

    private void ConsiderVillains(Vector3 position, bool isLowHp, ref GoalType bestType, ref float bestScore, ref Transform bestTarget)
    {
        if (_personality.villainWeight <= 0f) return;

        float weight = _personality.villainWeight * (isLowHp ? 0.5f : 1f);
        foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
        {
            if (villain == null || villain.isDefeated) continue;

            float distance = Vector3.Distance(position, villain.transform.position);
            if (distance > _personality.senseRange) continue;
            if (_personality.skipsLateCrimes && IsTooLate(villain, distance)) continue;

            float score = weight * (0.5f + 0.5f * Closeness(distance, _personality.senseRange));
            if (villain.transform == _goalTarget) score *= _keepGoalBonus;
            if (score <= bestScore) continue;

            bestType = GoalType.Villain;
            bestScore = score;
            bestTarget = villain.transform;
        }
    }

    // 向かっても犯罪の完遂に間に合わないか(データ表 Lv.3「間に合わないであろう悪人には向かいません」)
    private bool IsTooLate(CS_VillainHealth villain, float distance)
    {
        CS_VillainCombat combat = villain.GetComponent<CS_VillainCombat>();
        CS_VillainCrime crime = villain.GetComponent<CS_VillainCrime>();
        if (combat == null || crime == null || combat.group == null) return false;

        float remaining = (1f - combat.group.crimeProgress) * crime.crimeCompleteTime;
        float travel = distance / Mathf.Max(0.1f, _stats.moveSpeed) + (_transformation.isTransformed ? 0f : _transformTimeEstimate);
        return travel > remaining;
    }

    private void ConsiderItems(Vector3 position, bool isLowHp, ref GoalType bestType, ref float bestScore, ref Transform bestTarget)
    {
        if (_personality.itemWeight <= 0f) return;

        foreach (CS_ItemBase item in CS_NpcWorld.items)
        {
            if (item == null || item.itemData == null) continue;

            // 携帯型はスロットが空いている時だけ拾える。即時型(回復など)はいつでも拾える
            bool isCarriable = item.itemData is CSO_ItemDataCarriable;
            if (isCarriable && _itemSlot.hasItem) continue;

            float distance = Vector3.Distance(position, item.transform.position);
            if (distance > _personality.senseRange) continue;

            float weight = _personality.itemWeight * (!isCarriable && isLowHp ? 2f : 1f);
            float score = weight * Closeness(distance, _personality.senseRange);
            if (item.transform == _goalTarget) score *= _keepGoalBonus;
            if (score <= bestScore) continue;

            bestType = GoalType.Item;
            bestScore = score;
            bestTarget = item.transform;
        }
    }

    private void ConsiderEvents(Vector3 position, ref GoalType bestType, ref float bestScore, ref IInteractable bestInteractable)
    {
        if (_personality.eventWeight <= 0f) return;

        foreach (IInteractable interactable in CS_InteractableRegistry.interactables)
        {
            if (interactable == null || !interactable.CanInteract(gameObject)) continue;

            float distance = Vector3.Distance(position, interactable.interactPosition);
            if (distance > _personality.senseRange) continue;

            float score = _personality.eventWeight * (0.5f + 0.5f * Closeness(distance, _personality.senseRange));
            if (interactable == _goalInteractable) score *= _keepGoalBonus;
            if (score <= bestScore) continue;

            bestType = GoalType.Event;
            bestScore = score;
            bestInteractable = interactable;
        }
    }

    // 他のプレイヤーを邪魔しに行く(データ表 Lv.2「近くにプレイヤーを検知した場合、妨害行為に移ります」)
    private void ConsiderHarass(Vector3 position, ref GoalType bestType, ref float bestScore, ref Transform bestTarget)
    {
        if (_personality.harassWeight <= 0f) return;

        foreach (CS_PlayerWantedLevel other in CS_PlayerWantedLevel.activePlayers)
        {
            if (other == null || other.gameObject == gameObject) continue;

            CS_PlayerHealth otherHealth = other.GetComponent<CS_PlayerHealth>();
            if (otherHealth == null || otherHealth.isDead) continue;

            float distance = Vector3.Distance(position, other.transform.position);
            if (distance > _personality.harassRange) continue;

            // 邪魔したいのは人のプレイヤー。NPC同士はあまり狙わない
            CS_Player otherPlayer = other.GetComponent<CS_Player>();
            float weight = _personality.harassWeight * (otherPlayer != null && otherPlayer.isNpc ? 0.5f : 1f);
            float score = weight * Closeness(distance, _personality.harassRange);
            if (other.transform == _goalTarget) score *= _keepGoalBonus;
            if (score <= bestScore) continue;

            bestType = GoalType.Harass;
            bestScore = score;
            bestTarget = other.transform;
        }
    }

    // 追ってくる警察から逃げる。逃げ先(警察と反対側)も求める
    private bool ConsiderFlee(Vector3 position, bool isLowHp, ref GoalType bestType, ref float bestScore, out Vector3 fleePosition)
    {
        fleePosition = position;
        if (_personality.fleeWeight <= 0f) return false;

        CS_PoliceBrain chaser = FindNearestChasingPolice(position, out float distance);
        if (chaser == null) return false;

        float score = _personality.fleeWeight * Closeness(distance, _personality.fleeRange) * (isLowHp ? 2f : 1f);
        if (score <= bestScore) return false;

        Vector3 away = position - chaser.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        fleePosition = CS_NpcNavigator.RandomPoint(position + away.normalized * _fleeDistance, 3f);

        bestType = GoalType.Flee;
        bestScore = score;
        return true;
    }

    // 追跡中の警察のうち、逃げ始める距離以内で一番近いもの
    private CS_PoliceBrain FindNearestChasingPolice(Vector3 position, out float nearestDistance)
    {
        CS_PoliceBrain nearest = null;
        nearestDistance = _personality.fleeRange;

        foreach (CS_PoliceBrain police in CS_NpcWorld.police)
        {
            if (police == null || police.state != CSE_PoliceMoveState.Chase) continue;

            float distance = Vector3.Distance(position, police.transform.position);
            if (distance > nearestDistance) continue;

            nearest = police;
            nearestDistance = distance;
        }

        return nearest;
    }

    // 行動を切り替える(同じ行動・同じ相手なら続ける)
    private void SetGoal(GoalType type, Transform target, IInteractable interactable, Vector3 position)
    {
        bool isSame = type == _goalType && target == _goalTarget && interactable == _goalInteractable;
        if (type == GoalType.Wander && _goalType == GoalType.Wander) return;   // うろつき先は着くまで変えない

        if (type == GoalType.Wander)
        {
            position = CS_NpcNavigator.RandomPoint(transform.position, _personality.senseRange * 0.5f);
        }

        _goalType = type;
        _goalTarget = target;
        _goalInteractable = interactable;
        _goalPosition = position;

        if (isSame) return;

        // 行動を変えた時は、反応の遅れの分だけ待ってから動き出す
        _actStartTime = Time.time + _personality.reactionDelay;
        _stuckCount = 0;
        _navigator.ForceRepath();
    }

    // アイテムの使いどころなら、確率で使う(逃げている時・標的が近い時)
    private void DecideItemUse(Vector3 position)
    {
        if (!_itemSlot.hasItem) return;
        if (Random.value > _personality.itemUseChance) return;

        bool isTargetNear = _goalTarget != null
            && Vector3.Distance(position, _goalTarget.position) <= _itemUseDistance
            && (_goalType == GoalType.Villain || (_goalType == GoalType.Harass && _personality.usesItemsOnPlayers));

        if (_goalType == GoalType.Flee || isTargetNear)
        {
            _useItemPending = true;
        }
    }

    // ========================================
    // 選んだ行動を行う
    // ========================================

    private void Act(CS_PlayerNpcInput input)
    {
        if (_useItemPending)
        {
            _useItemPending = false;
            _wantUseItem = true;
        }

        switch (_goalType)
        {
            case GoalType.Villain:
            case GoalType.Harass:
                ActOnTarget(input);
                break;
            case GoalType.Item:
                ActOnItem(input);
                break;
            case GoalType.Event:
                ActOnEvent(input);
                break;
            case GoalType.Flee:
                ActFlee(input);
                break;
            case GoalType.Wander:
                if (!MoveTo(input, _goalPosition, false)) EndGoal();
                break;
        }
    }

    // 悪人・邪魔する相手に近づいて戦う
    private void ActOnTarget(CS_PlayerNpcInput input)
    {
        if (!IsTargetValid())
        {
            EndGoal();
            return;
        }

        _lastHasTargetTime = Time.time;
        Vector3 targetPosition = _goalTarget.position;
        float distance = Vector3.Distance(transform.position, targetPosition);

        // 変身距離に入ったら変身する(攻撃は変身中しかできない)
        if (_transformation.canTransform && distance <= _personality.transformDistance)
        {
            _wantTransform = true;
        }

        if (_transformation.isTransformed && distance <= GetAttackReach())
        {
            Face(input, targetPosition, _aimOffset);
            TryAttack();
            TrySpecial();
            return;
        }

        // 変身できない間は、少し離れて待つ(近づきすぎると一方的に攻撃される)
        if (!_transformation.isTransformed && distance <= _waitDistance)
        {
            Face(input, targetPosition, 0f);
            return;
        }

        MoveTo(input, targetPosition, distance > _sprintDistance);
    }

    private bool IsTargetValid()
    {
        if (_goalTarget == null) return false;

        if (_goalType == GoalType.Villain)
        {
            CS_VillainHealth villain = _goalTarget.GetComponent<CS_VillainHealth>();
            return villain != null && !villain.isDefeated;
        }

        CS_PlayerHealth other = _goalTarget.GetComponent<CS_PlayerHealth>();
        return other != null && !other.isDead;
    }

    // 攻撃ボタンを一定間隔で押す(弱い性格は、たまに向きがずれる)
    private void TryAttack()
    {
        if (Time.time < _nextAttackTime) return;

        _nextAttackTime = Time.time + _personality.attackInterval;
        _wantAttack = true;

        // 次の攻撃の向きのずれを決める
        _aimOffset = Random.value < _personality.attackMissChance ? Random.Range(-_maxAimOffset, _maxAimOffset) : 0f;
    }

    // 必殺技の範囲に十分な数の相手がいれば使う
    private void TrySpecial()
    {
        if (!_transformation.isTransformed || !_gauge.isFull) return;

        float radius = GetSpecialRadius();
        int villains = _personality.savesSpecialForPlayers ? 0 : CountVillainsWithin(radius);
        int players = _goalType == GoalType.Harass || _personality.savesSpecialForPlayers ? CountPlayersWithin(radius) : 0;

        // 他のプレイヤーは1人でも当てる価値がある(妨害)
        if (villains >= _personality.specialMinTargets || players >= 1)
        {
            _wantSpecial = true;
        }
    }

    private void ActOnItem(CS_PlayerNpcInput input)
    {
        if (_goalTarget == null)
        {
            EndGoal();
            return;
        }

        // アイテムには触れるだけで拾える(CS_ItemBaseのトリガー)
        float distance = Vector3.Distance(transform.position, _goalTarget.position);
        if (!MoveTo(input, _goalTarget.position, distance > _sprintDistance, 0.2f)) EndGoal();
    }

    // イベント(ケアパッケージなど)に近づき、通常攻撃ボタン(インタラクト)を押し続ける
    private void ActOnEvent(CS_PlayerNpcInput input)
    {
        IInteractable target = _goalInteractable;
        if (target == null || (target is Object unityObject && unityObject == null))
        {
            EndGoal();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.interactPosition);
        float reach = target.interactRange * 0.7f;
        if (distance > reach)
        {
            MoveTo(input, target.interactPosition, distance > _sprintDistance, reach);
            return;
        }

        // 押し始めたら、操作が始まるまで少し待つ(開始の依頼が届くまでに少しかかる)
        if (!_isHoldingInteract)
        {
            _isHoldingInteract = true;
            _nextInteractRetryTime = Time.time + _interactRetryInterval;
        }

        // 待っても操作が始まらない・中断された時は、一度離して押し直す(CS_PlayerInteractorは押し直さないと再開しないため)
        if (!target.IsInteracting(gameObject) && Time.time >= _nextInteractRetryTime)
        {
            _isHoldingInteract = false;
            return;
        }

        _wantAttack = true;
    }

    // 変身を解いて、警察と反対側へ逃げる
    private void ActFlee(CS_PlayerNpcInput input)
    {
        if (_transformation.isTransformed)
        {
            _wantTransform = true;   // 変形を解けばバレない(仕様書「変形のリスク」)
        }

        if (!MoveTo(input, _goalPosition, true)) EndGoal();
    }

    // 手配度が高くなりすぎた・標的が無い状態が続いた時は、変身を解く
    private void UpdateTransformation()
    {
        if (!_transformation.isTransformed) return;

        bool isWantedTooHigh = _wantedLevel.level >= _personality.maxWantedLevel;
        bool isIdleTooLong = Time.time - _lastHasTargetTime >= _personality.untransformIdleTime;
        if (isWantedTooHigh || isIdleTooLong)
        {
            _wantTransform = true;
        }
    }

    // 今の行動を終わらせ、すぐ次を選ぶ
    private void EndGoal()
    {
        _goalType = GoalType.None;
        _goalTarget = null;
        _goalInteractable = null;
        _nextDecisionTime = 0f;
    }

    // ========================================
    // 移動・向き
    // ========================================

    // 目的地へ向かう。着いていればfalseを返す
    private bool MoveTo(CS_PlayerNpcInput input, Vector3 destination, bool sprint, float arriveDistance = -1f)
    {
        if (arriveDistance < 0f) arriveDistance = _arriveDistance;

        _navigator.SetDestination(transform.position, destination);
        Vector3 direction = _navigator.GetDirection(transform.position, arriveDistance);
        if (direction == Vector3.zero) return false;

        input.yaw = YawOf(direction);
        input.move = Vector2.up;   // 向き(yaw)の前方へ進む
        _isMoving = true;

        if (sprint && _personality.usesSprint)
        {
            _wantDash = true;   // ダッシュ(ブリンク)の後、押し続けてスプリント
        }
        return true;
    }

    // 立ち止まって相手の方を向く
    private void Face(CS_PlayerNpcInput input, Vector3 targetPosition, float offset)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        input.yaw = YawOf(direction) + offset;
    }

    // 引っかかって進めていなければ、ジャンプして経路を計算し直す。続く時はその行動をあきらめる
    private void UpdateStuck()
    {
        if (Time.time < _nextStuckCheckTime) return;

        _nextStuckCheckTime = Time.time + _stuckCheckInterval;
        float moved = Vector3.Distance(transform.position, _stuckCheckPosition);
        _stuckCheckPosition = transform.position;

        if (!_isMoving || moved >= _stuckMoveDistance)
        {
            _stuckCount = 0;
            return;
        }

        _stuckCount++;
        _wantJump = true;
        _navigator.ForceRepath();

        if (_stuckCount < _stuckGiveUpCount) return;

        // 別の場所へうろつきに行く
        _goalType = GoalType.Wander;
        _goalTarget = null;
        _goalInteractable = null;
        _goalPosition = CS_NpcNavigator.RandomPoint(transform.position, _personality.senseRange * 0.5f);
        _stuckCount = 0;
    }

    // ========================================
    // ボタン
    // ========================================

    private void ClearWants()
    {
        _wantAttack = _wantSpecial = _wantDash = _wantUseItem = _wantTransform = _wantJump = false;
    }

    // このフレームで押すボタンを反映する
    // 攻撃(インタラクトの長押しに使う)とダッシュ(スプリントに使う)は、押し続けられるようそのまま反映する
    // それ以外は「押した瞬間」だけが意味を持つため、押しっぱなしにせず、押した次のフレームで一度離す
    // (変身を解けない攻撃中に押した時など、1回目が受け付けられなくても押し直せるように)
    private void ApplyWants(CS_PlayerNpcInput input)
    {
        if (!_wantAttack) _isHoldingInteract = false;

        input.attack.SetHeld(_wantAttack);
        input.dash.SetHeld(_wantDash);
        Tap(input.special, _wantSpecial);
        Tap(input.useItem, _wantUseItem);
        Tap(input.transformation, _wantTransform);
        Tap(input.jump, _wantJump);
    }

    private static void Tap(CS_PlayerButton button, bool want)
    {
        button.SetHeld(want && !button.IsPressed());
    }

    // ========================================
    // 補助
    // ========================================

    // 通常攻撃(1段目)が届く距離
    private float GetAttackReach()
    {
        if (_attack == null || _attack.attackSteps.Count == 0) return 1.5f;

        CSO_AttackData step = _attack.attackSteps[0];
        return step.hitRange + step.hitRadius;
    }

    // 必殺技の範囲(自分中心)
    private float GetSpecialRadius()
    {
        if (_special == null || _special.specialAttackData == null) return 3f;

        return _special.specialAttackData.hitRadius;
    }

    private int CountVillainsWithin(float radius)
    {
        int count = 0;
        foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
        {
            if (villain == null || villain.isDefeated) continue;
            if (Vector3.Distance(transform.position, villain.transform.position) <= radius) count++;
        }
        return count;
    }

    private int CountPlayersWithin(float radius)
    {
        int count = 0;
        foreach (CS_PlayerWantedLevel other in CS_PlayerWantedLevel.activePlayers)
        {
            if (other == null || other.gameObject == gameObject) continue;
            if (Vector3.Distance(transform.position, other.transform.position) <= radius) count++;
        }
        return count;
    }

    // 近さ(0〜1。0mで1、range以上で0)
    private static float Closeness(float distance, float range)
    {
        return Mathf.Clamp01(1f - distance / range);
    }

    private static float YawOf(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }
}
