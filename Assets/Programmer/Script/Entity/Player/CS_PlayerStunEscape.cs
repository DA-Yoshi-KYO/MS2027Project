using System;
using Unity.Netcode;
using UnityEngine;

/*
 * スタン中のコマンド入力(連打・スティック回転)で、スタンを短くするクラス
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「スタン状態について」。巡回ドローンのテーザーなどで10秒スタンした時、コマンド入力で早く抜け出せる
 *   キーボード     : 左(A)と右(D)を交互に連打する。A → D(またはD → A)の1往復で1周
 *   コントローラー : 左スティックを回す。1回転で1周
 *   1周ごとに_shortenPerCycle秒(既定0.5秒。データ表「コマンドによるスタン短縮時間」、仕様の仮の値)短くなる
 * ・入力は移動入力(CS_Player.moveInput)をそのまま使う(スタン中は移動しないので、移動と取り合わない)
 *   入力の向きが一瞬で大きく変わった(_snapAngle度以上)時は「反対側のキーを押した」とみなして半周と数え、
 *   少しずつ変わった時は「スティックを回している」とみなして回した角度を足していく
 *   (スティックを回すと左右も交互に通るため、両方で数えると2倍になってしまうのを防ぐ)
 * ・どのスタンでも使える(パリーされた時の短いスタンも含む)
 * ・入力は操作している本人の手元で読み、短くする量だけサーバーへ送る(スタンの時刻はサーバーが持つため)
 *   1回に送れる周回数は_maxCyclesPerRequestまでにしている(不正な値で一気に解かれないように)
 * ・NPCはコマンド入力をしない(スタンが自然に解けるのを待つ)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerStun))]
public class CS_PlayerStunEscape : NetworkBehaviour
{
    [Header("コマンド入力")]
    [SerializeField][Min(0f)] private float _shortenPerCycle = 0.5f;           // 1周ごとに短くなる秒数
    [SerializeField][Range(0.1f, 1f)] private float _inputThreshold = 0.5f;    // これ以上倒した入力だけを数える
    [SerializeField][Range(90f, 180f)] private float _snapAngle = 150f;        // 1フレームでこれ以上向きが変わったら、反対側のキーを押したとみなす(度)

    private const int _maxCyclesPerRequest = 4;     // 1回のRPCで受け付ける周回数の上限

    private CS_Player _player;
    private CS_PlayerStun _stun;

    private bool _hasLastAngle;     // 前のフレームの入力の向きを覚えているか
    private float _lastAngle;       // 前に倒していた入力の向き(度)
    private int _halfCycles;        // 反対側のキーを押した回数(2回で1周)
    private float _rotation;        // スティックを回した角度の合計(度。符号付き)

    public float shortenPerCycle => _shortenPerCycle;

    public event Action onShortened;   // コマンド入力でスタンが短くなった時(操作している本人の手元のみ。見た目・UI用)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stun = GetComponent<CS_PlayerStun>();
    }

    private void Update()
    {
        // 入力を読むのは、人が操作している自分のプレイヤーだけ
        if (!_player.isLocalPlayer || !_player.isControlled) return;

        if (!_stun.isStunned)
        {
            ResetTracking();
            return;
        }

        int cycles = ReadCycles(_player.moveInput);
        if (cycles <= 0) return;

        RequestShorten(cycles);
        onShortened?.Invoke();
    }

    // 入力から、このフレームで何周したかを数える
    private int ReadCycles(Vector2 input)
    {
        if (input.magnitude < _inputThreshold) return 0;   // 離している間は、前の向きを覚えたまま待つ

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (!_hasLastAngle)
        {
            _hasLastAngle = true;
            _lastAngle = angle;
            return 0;
        }

        float delta = Mathf.DeltaAngle(_lastAngle, angle);
        _lastAngle = angle;

        // 一瞬で反対側へ変わった → 反対側のキーを押した(半周)
        if (Mathf.Abs(delta) >= _snapAngle)
        {
            _halfCycles++;
            int keyCycles = _halfCycles / 2;
            _halfCycles %= 2;
            return keyCycles;
        }

        // 少しずつ変わった → スティックを回している(回した向きに関係なく、1回転で1周)
        _rotation += delta;
        int stickCycles = Mathf.FloorToInt(Mathf.Abs(_rotation) / 360f);
        if (stickCycles > 0) _rotation -= Mathf.Sign(_rotation) * 360f * stickCycles;
        return stickCycles;
    }

    private void ResetTracking()
    {
        _hasLastAngle = false;
        _halfCycles = 0;
        _rotation = 0f;
    }

    // 短くする量をサーバーへ送る(オフラインはその場で短くする)
    private void RequestShorten(int cycles)
    {
        if (!IsSpawned)
        {
            _stun.Shorten(_shortenPerCycle * cycles);
            return;
        }

        ShortenRpc(cycles);
    }

    // 操作している本人からサーバーへ、スタンを短くするよう依頼する
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ShortenRpc(int cycles)
    {
        int accepted = Mathf.Clamp(cycles, 0, _maxCyclesPerRequest);
        _stun.Shorten(_shortenPerCycle * accepted);
    }
}
