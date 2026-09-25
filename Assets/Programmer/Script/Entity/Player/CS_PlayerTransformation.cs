using System;
using Unity.Netcode;
using UnityEngine;

/*
 * プレイヤーの変身を管理するクラス
 * 変身中だけ必殺技(CS_PlayerSpecialAttack)を使用できる
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・変身ボタン(キーボード: Q / コントローラー: Rスティック押し込み)で変身、変身中にもう一度押すと解除
 * ・変身・解除は移動を止めない(移動しながら変身できる)
 * ・解除してから_cooldown秒(既定5秒)経つまで再変身できない。変身そのものにはクールタイムは無い
 * ・変身状態に合わせてタグを切り替える
 *   通常: PlayerNoTransformation / 変身中: PlayerTransformation
 * ・変身状態とクールタイム終了時刻はNetworkVariableで持つ(書き込みはサーバーのみ、読み取りは全員可)
 *   流れ: Ownerがボタンを押す → サーバーへ依頼(RPC) → サーバーが条件を確認して確定 → 全員のタグが切り替わる
 * ・死亡すると自動で解除される(解除扱いなのでクールタイムも始まる)
 * ・必殺技を行っている間は解除できない(判定の途中で解除されて不発になるのを防ぐため)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerHealth))]
public class CS_PlayerTransformation : NetworkBehaviour
{
    [Header("変身")]
    [SerializeField] private float _cooldown = 5f;  // 解除してから再変身できるまでの時間(秒)

    private const string _normalTag = "PlayerNoTransformation";
    private const string _transformedTag = "PlayerTransformation";

    private CS_Player _player;
    private CS_PlayerHealth _health;
    private CS_PlayerSpecialAttack _specialAttack;

    // 書き込みはサーバーのみ(NetworkVariableのデフォルト)。読み取りは全員可
    private readonly NetworkVariable<bool> _isTransformed = new NetworkVariable<bool>();
    private readonly NetworkVariable<double> _cooldownEndTime = new NetworkVariable<double>();  // 再変身できるようになる時刻

    public bool isTransformed => _isTransformed.Value;
    public float cooldown => _cooldown;
    public float cooldownRemaining => Mathf.Max(0f, (float)(_cooldownEndTime.Value - GetCurrentTime()));  // HUD用
    public bool canTransform => !_isTransformed.Value && cooldownRemaining <= 0f;

    public event Action<bool> onTransformChanged;   // (isTransformed) 見た目・HUD用

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _health = GetComponent<CS_PlayerHealth>();
        _specialAttack = GetComponent<CS_PlayerSpecialAttack>();

        _health.onDeath += HandleDeath;
    }

    // オフライン(NetworkManagerが動いていない)のテストシーン用
    private void Start()
    {
        if (IsSpawned) return;

        ApplyTag(_isTransformed.Value);
    }

    public override void OnNetworkSpawn()
    {
        _isTransformed.OnValueChanged += HandleTransformChanged;

        // 途中参加したクライアントでも、現在の状態のタグに合わせる
        ApplyTag(_isTransformed.Value);
    }

    public override void OnNetworkDespawn()
    {
        _isTransformed.OnValueChanged -= HandleTransformChanged;
    }

    public override void OnDestroy()
    {
        if (_health != null)
        {
            _health.onDeath -= HandleDeath;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        if (!_player.canAct) return;
        if (!_player.transformationAction.WasPressedThisFrame()) return;

        // 必殺技中の解除、クールタイム中の変身はここで弾く(最終的な確認はサーバーで行う)
        if (_isTransformed.Value && IsPerformingSpecial()) return;
        if (!_isTransformed.Value && !canTransform) return;

        RequestToggle();
    }

    // 変身/解除を依頼する(確定はサーバーで行う)
    private void RequestToggle()
    {
        // オフライン(テストシーン)では、その場で切り替える
        if (!IsSpawned)
        {
            ExecuteToggle();
            return;
        }

        ToggleRpc();
    }

    // Ownerからサーバーへ、変身/解除を依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ToggleRpc()
    {
        ExecuteToggle();
    }

    // 条件を確認して変身/解除を切り替える(サーバー、またはオフラインで実行される)
    private void ExecuteToggle()
    {
        if (_health.isDead) return;

        if (_isTransformed.Value)
        {
            if (IsPerformingSpecial()) return;

            Untransform();
            return;
        }

        if (!canTransform) return;

        SetTransformed(true);
    }

    // 変身を解除し、クールタイムを開始する(サーバー、またはオフラインで実行される)
    private void Untransform()
    {
        _cooldownEndTime.Value = GetCurrentTime() + _cooldown;
        SetTransformed(false);
    }

    private void SetTransformed(bool transformed)
    {
        _isTransformed.Value = transformed;

        // オフライン時はNetworkVariableの変更通知が届かないため、ここで直接反映する
        if (IsSpawned) return;

        HandleTransformChanged(!transformed, transformed);
    }

    // 変身状態が変わったとき、全クライアントでタグを切り替えて通知する
    private void HandleTransformChanged(bool previous, bool current)
    {
        ApplyTag(current);
        onTransformChanged?.Invoke(current);
    }

    private void ApplyTag(bool transformed)
    {
        gameObject.tag = transformed ? _transformedTag : _normalTag;
    }

    // 死亡したら変身を解除する(サーバー、またはオフラインのみ)
    private void HandleDeath()
    {
        if (IsSpawned && !IsServer) return;
        if (!_isTransformed.Value) return;

        Untransform();
    }

    private bool IsPerformingSpecial()
    {
        return _specialAttack != null && _specialAttack.isPerformingSpecial;
    }

    // クールタイムの基準時刻(オンラインはサーバー時刻で揃える)
    private double GetCurrentTime()
    {
        return IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }
}
