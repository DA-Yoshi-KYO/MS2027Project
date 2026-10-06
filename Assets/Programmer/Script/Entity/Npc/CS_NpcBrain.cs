using System.Collections.Generic;
using UnityEngine;

/*
 * NPCの頭脳。状況を見て行動を選び、その結果を「入力」としてCS_Playerへ渡す
 * 人のプレイヤーと同じプレハブ・同じ処理で動く(入力の出どころがキーボードの代わりにこのクラスになるだけ)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・LLMや機械学習は使わず、ルールベースで判断する(ユーティリティAI)
 *   一定間隔(性格と強さで変わる)で全行動(CS_NpcAction)の価値を比べ、一番高い行動を選ぶ
 *   選んだ行動は毎フレーム、移動先(MoveTo)・向き(FaceTowards)・ボタン(Press〇〇)を指示する
 * ・指示はIPlayerInputSourceとしてCS_Playerと各操作のコンポーネントへ渡る
 *   移動: 次に向かう方向へ視点(yaw)を向け、前進の入力を出す(CS_Playerが体を回してから移動する)
 *   ボタン: 押したフレームだけ「押された」になる。各コンポーネントより先に入力を決めるため、実行順を早めている
 * ・反応の遅さ: 行動を切り替えてからreactionDelay秒は、ボタンを押さない
 * ・迷い: 判断のたびに、強さに応じた確率でその間立ち止まる(強さの調整 #167)
 * ・頭脳はサーバー(またはオフライン)にだけ付ける(CS_NpcSpawnerがスポーン前に付ける)。クライアントは位置の同期を受けるだけ
 * ・Inspectorの_debugActionで、今選んでいる行動を確認できる
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[DefaultExecutionOrder(-10)] // CS_Playerや各操作のコンポーネントより先に、このフレームの入力を決めるため
public class CS_NpcBrain : MonoBehaviour, IPlayerInputSource
{
    private const float _keepActionBonus = 0.1f;    // 今の行動を続けやすくする(行動がころころ変わらないように)
    private const float _stopDistance = 0.3f;       // 目的地にこの距離まで近づいたら止まる(m)

    [Header("デバッグ")]
    [SerializeField] private string _debugAction;   // 今選んでいる行動(確認用。書き換えても意味は無い)

    private CSO_NpcPersonality _personality;
    private CS_Player _player;
    private CS_PlayerTransformation _transformation;
    private CS_PlayerSpecialGauge _gauge;
    private CS_PlayerSpecialAttack _specialAttack;
    private CS_PlayerItemSlot _itemSlot;

    private readonly CS_NpcSensor _sensor = new CS_NpcSensor();
    private readonly CS_NpcNavigator _navigator = new CS_NpcNavigator();
    private CS_NpcDifficulty _difficulty;
    private List<CS_NpcAction> _actions;
    private CS_NpcAction _currentAction;

    private float _thinkTimer;
    private float _reactionTimer;
    private float _attackTimer;
    private bool _isHesitating;

    // このフレームの入力(毎フレーム作り直す)
    private Vector2 _move;
    private Vector2 _look;
    private bool _attackPressed;
    private bool _specialPressed;
    private bool _useItemPressed;
    private bool _transformPressed;
    private bool _hasFaceTarget;
    private Vector3 _faceTarget;

    // ---- 行動(CS_NpcAction)から使う ----
    public CSO_NpcPersonality personality => _personality;
    public CS_Player player => _player;
    public CS_PlayerTransformation transformation => _transformation;
    public CS_PlayerItemSlot itemSlot => _itemSlot;
    public CS_NpcSensor sensor => _sensor;
    public CS_NpcDifficulty difficulty => _difficulty;
    public Vector3 position => transform.position;

    // ---- IPlayerInputSource ----
    public Vector2 move => _move;
    public Vector2 look => _look;
    public bool jumpPressed => false;
    public bool dashPressed => false;
    public bool dashHeld => false;
    public bool attackPressed => _attackPressed;
    public bool specialPressed => _specialPressed;
    public bool specialModifierHeld => false;
    public bool useItemPressed => _useItemPressed;
    public bool transformPressed => _transformPressed;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _transformation = GetComponent<CS_PlayerTransformation>();
        _gauge = GetComponent<CS_PlayerSpecialGauge>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();
        _itemSlot = GetComponent<CS_PlayerItemSlot>();
    }

    // 性格を決める(CS_NpcSpawnerが、スポーンする前に呼ぶ)
    public void Setup(CSO_NpcPersonality personality)
    {
        _personality = personality;
        _difficulty = new CS_NpcDifficulty(personality, GetComponent<CS_PlayerResultDataHolder>());
        _actions = CreateActions();
    }

    // NPCが選べる行動の一覧(行動を足す・外すときはここを変える)
    private static List<CS_NpcAction> CreateActions()
    {
        return new List<CS_NpcAction>
        {
            new CS_NpcActionHuntVillain(),
            new CS_NpcActionSeekItem(),
            new CS_NpcActionHarass(),
            new CS_NpcActionFleePolice(),
            new CS_NpcActionWander(),
        };
    }

    private void Update()
    {
        ClearFrameInput();

        if (_personality == null || !_player.canAct)
        {
            _navigator.Stop();
            return;
        }

        float deltaTime = Time.deltaTime;
        _sensor.Update(deltaTime);
        _difficulty.Update(deltaTime, _sensor);
        _reactionTimer -= deltaTime;
        _attackTimer -= deltaTime;

        _thinkTimer -= deltaTime;
        if (_thinkTimer <= 0f)
        {
            Think();
        }

        if (!_isHesitating)
        {
            _currentAction?.Tick(this);
            UseItemIfUseful();
        }

        UpdateSteering(deltaTime);
    }

    // ---- 行動から呼ぶ指示 ----

    // 目的地へ向かう(経路はNavMeshで求める)
    public void MoveTo(Vector3 destination)
    {
        _navigator.SetDestination(destination);
    }

    public void StopMoving()
    {
        _navigator.Stop();
    }

    // その場で止まって、指定した位置の方を向く(攻撃の前など)
    public void FaceTowards(Vector3 target)
    {
        _navigator.Stop();
        _faceTarget = target;
        _hasFaceTarget = true;
    }

    // 目的地にこの距離まで近づいたか(目的地が無ければtrue)
    public bool HasArrived(float distance)
    {
        return _navigator.HasArrived(position, distance);
    }

    // 攻撃ボタン(強さに応じた間隔で押す。変身中しか攻撃にならない)
    public void PressAttack()
    {
        if (!CanPress() || _attackTimer > 0f) return;
        if (!_transformation.isTransformed) return;

        _attackTimer = _personality.GetAttackInterval(_difficulty.strength);
        _attackPressed = true;
    }

    // 必殺技ボタン(使える時だけ押す)
    public void PressSpecial()
    {
        if (!CanPress()) return;
        if (!_transformation.isTransformed || _gauge == null || !_gauge.isFull) return;
        if (_specialAttack == null || _specialAttack.isPerformingSpecial) return;

        _specialPressed = true;
    }

    // 変身できて、近くに警察がいなければ変身ボタンを押す
    public void TryTransform()
    {
        if (!CanPress() || !_transformation.canTransform) return;
        if (_sensor.FindNearestPolice(position, _personality.policeAvoidRange) != null) return;

        _transformPressed = true;
    }

    // 変身中なら解除する(必殺技中は解除できないので待つ)
    public void TryUntransform()
    {
        if (!CanPress() || !_transformation.isTransformed) return;
        if (_specialAttack != null && _specialAttack.isPerformingSpecial) return;

        _transformPressed = true;
    }

    // ---- 内部処理 ----

    // 全行動の価値を比べて、一番高い行動を選ぶ
    private void Think()
    {
        _thinkTimer = _personality.GetThinkInterval(_difficulty.strength);

        // 迷い: 強さに応じた確率で、次の判断まで立ち止まる
        _isHesitating = Random.value < _personality.GetHesitationChance(_difficulty.strength);
        if (_isHesitating)
        {
            _navigator.Stop();
            return;
        }

        CS_NpcAction best = null;
        float bestScore = 0f;
        foreach (CS_NpcAction action in _actions)
        {
            float score = action.Evaluate(this);
            if (score <= 0f) continue;
            if (action == _currentAction) score += _keepActionBonus;
            if (score <= bestScore) continue;

            best = action;
            bestScore = score;
        }

        if (best == _currentAction) return;

        _currentAction = best;
        _reactionTimer = _personality.reactionDelay;
        _navigator.Stop();
        _currentAction?.OnEnter(this);
        _debugAction = _currentAction != null ? _currentAction.name : "";
    }

    // アイテムを持っていて、近くに悪人がいれば使う(どの行動中でも)
    private void UseItemIfUseful()
    {
        if (_itemSlot == null || !_itemSlot.hasItem || !CanPress()) return;
        if (_sensor.CountVillains(position, _personality.attackRange * 2f) <= 0) return;

        _useItemPressed = true;
    }

    // 向きたい方向へ視点を回し、移動中なら前進の入力を出す
    private void UpdateSteering(float deltaTime)
    {
        Vector3 direction;
        if (_hasFaceTarget)
        {
            direction = _faceTarget - position;
            direction.y = 0f;
            _move = Vector2.zero;
        }
        else if (_navigator.hasDestination && !_navigator.HasArrived(position, _stopDistance))
        {
            direction = _navigator.GetSteerDirection(position, deltaTime);
            _move = direction == Vector3.zero ? Vector2.zero : Vector2.up;
        }
        else
        {
            direction = Vector3.zero;
            _move = Vector2.zero;
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        _look = new Vector2(Mathf.DeltaAngle(_player.yaw, desiredYaw), 0f);
    }

    // 行動を切り替えた直後(反応の遅れ)はボタンを押さない
    private bool CanPress()
    {
        return _reactionTimer <= 0f;
    }

    private void ClearFrameInput()
    {
        _move = Vector2.zero;
        _look = Vector2.zero;
        _attackPressed = false;
        _specialPressed = false;
        _useItemPressed = false;
        _transformPressed = false;
        _hasFaceTarget = false;
    }
}
