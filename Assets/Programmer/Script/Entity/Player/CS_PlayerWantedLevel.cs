using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーごとの手配度(0〜_maxLevel)を管理するクラス
 * 手配度が上がるほど警察の警備が厳しくなる(警察側の変化は #159 / #160 で、onLevelChangedを購読して行う)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は警察仕様書「手配度」(Issue #158)。手配度はプレイヤーごとに持ち、全員で共有しない
 * ・上がる条件
 *   変身を続けて、警察への信号の間隔が短くなるたびに+1(CS_PlayerTransformation.onPoliceNotifyIntervalShortened)
 *     データ表の値だと: 変身完了 → 0 / 20秒後に8秒間隔 → 1 / 40秒後に6秒間隔 → 2 …
 *   どこかの悪人グループが犯罪を完遂した時、全プレイヤーが_crimeCompletedIncrease(仮で1。プランナーに確認中)上がる
 * ・下がる条件
 *   変身を解く・死亡すると、_decreaseInterval秒(既定2秒)ごとに1つ下がる(リスポーンの10秒で必ず0になる)
 *   下がっている途中でもう一度変身を始めたら、下がるのを止める
 * ・手配度はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   増減の判断はサーバーだけが行う(変身状態・犯罪完遂の判定がサーバーで行われるため)
 * ・onLevelChangedは全クライアントで発生する(警察の変化・追加スポーン・UI用)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_PlayerTransformation))]
[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerWantedLevel : NetworkBehaviour
{
    [Header("手配度")]
    [SerializeField] private int _maxLevel = 5;                 // 手配度の上限(仮で5段階)
    [SerializeField] private int _crimeCompletedIncrease = 1;   // 悪人が犯罪を完遂した時に上がる量(プランナーに確認中の仮の値)
    [SerializeField] private float _decreaseInterval = 2f;      // 変身を解いた・死亡した後、1つ下がるまでの時間(秒)

    private CS_PlayerTransformation _transformation;
    private CS_PlayerHealth _health;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<int> _level = new NetworkVariable<int>();

    private bool _isDecreasing;     // 下がっている途中か(サーバー、またはオフラインのみ使う)
    private float _decreaseTimer;

    public int level => _level.Value;
    public int maxLevel => _maxLevel;

    public event Action<int> onLevelChanged;    // (今の手配度)全クライアントで発生する

    private void Awake()
    {
        _transformation = GetComponent<CS_PlayerTransformation>();
        _health = GetComponent<CS_PlayerHealth>();

        _transformation.onPoliceNotifyIntervalShortened += HandleIntervalShortened;
        _transformation.onStateChanged += HandleTransformStateChanged;
        _health.onDeath += HandleDeath;
        CS_VillainGroup.onAnyCrimeCompleted += HandleCrimeCompleted;
    }

    public override void OnNetworkSpawn()
    {
        _level.OnValueChanged += HandleLevelChanged;
    }

    public override void OnNetworkDespawn()
    {
        _level.OnValueChanged -= HandleLevelChanged;
    }

    public override void OnDestroy()
    {
        if (_transformation != null)
        {
            _transformation.onPoliceNotifyIntervalShortened -= HandleIntervalShortened;
            _transformation.onStateChanged -= HandleTransformStateChanged;
        }

        if (_health != null) _health.onDeath -= HandleDeath;
        CS_VillainGroup.onAnyCrimeCompleted -= HandleCrimeCompleted;

        base.OnDestroy();
    }

    private void Update()
    {
        if (!HasAuthority()) return;

        UpdateDecrease(Time.deltaTime);
    }

    // 下がっている途中なら、_decreaseInterval秒ごとに1つ下げる(0になったら止める)
    private void UpdateDecrease(float deltaTime)
    {
        if (!_isDecreasing) return;

        if (_level.Value <= 0)
        {
            _isDecreasing = false;
            return;
        }

        _decreaseTimer -= deltaTime;
        if (_decreaseTimer > 0f) return;

        _decreaseTimer = _decreaseInterval;
        AddLevel(-1);
    }

    // 変身を続けて信号の間隔が短くなったら+1
    private void HandleIntervalShortened()
    {
        if (!HasAuthority()) return;

        AddLevel(1);
    }

    // どこかの悪人グループが犯罪を完遂したら、全プレイヤーが上がる(各プレイヤーが自分の分を上げる)
    private void HandleCrimeCompleted(CS_VillainGroup group)
    {
        if (!HasAuthority()) return;

        AddLevel(_crimeCompletedIncrease);
    }

    // 変身を解いたら下がり始め、変身を始めたら下がるのを止める
    private void HandleTransformStateChanged(CSE_PlayerTransformState state)
    {
        if (!HasAuthority()) return;

        if (state == CSE_PlayerTransformState.Normal)
        {
            StartDecrease();
            return;
        }

        _isDecreasing = false;
    }

    // 死亡したら下がり始める(変身中に死んだ場合は変身の解除でも呼ばれるが、同じ処理なので問題ない)
    private void HandleDeath()
    {
        if (!HasAuthority()) return;

        StartDecrease();
    }

    private void StartDecrease()
    {
        if (_isDecreasing) return;

        _isDecreasing = true;
        _decreaseTimer = _decreaseInterval;
    }

    private void AddLevel(int amount)
    {
        int previous = _level.Value;
        int next = Mathf.Clamp(previous + amount, 0, _maxLevel);
        if (next == previous) return;

        _level.Value = next;

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接通知する
        if (IsSpawned) return;

        HandleLevelChanged(previous, next);
    }

    private void HandleLevelChanged(int previous, int current)
    {
        onLevelChanged?.Invoke(current);
    }

    // 手配度を増減してよいか(サーバー、またはオフラインのみ)
    private bool HasAuthority()
    {
        return !IsSpawned || IsServer;
    }

    private void OnValidate()
    {
        _maxLevel = Mathf.Max(1, _maxLevel);
        _crimeCompletedIncrease = Mathf.Max(0, _crimeCompletedIncrease);
        _decreaseInterval = Mathf.Max(0.1f, _decreaseInterval);
    }
}
