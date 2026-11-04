using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * プレイヤーのスキャン(周囲の悪人を、建物越しに一定時間見えるようにする)を行うクラス
 * スキャンボタン(キーボード: Q / コントローラー: LB)で使う
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「スキャン」(Issue #294)。Apexのブラッドハウンドのスキャンのようなもの
 * ・ボタンを押すと、押した位置から円状の波が_expandTime秒(既定1秒)かけて半径radiusまで広がる
 *   波が届いた悪人を、_revealDuration秒(既定10秒、仕様の仮の値)のあいだ「見えている」状態にする(建物は関係なく届く)
 *   波が広がりきった後、_fadeTime秒(既定0.3秒)で波が消えるまでは、もう一度使えない(仕様「エフェクトがなくなるまでは再使用できない」)
 * ・モーションは無く、移動しながら使える(移動・攻撃を止めない)
 * ・範囲 = 基本の範囲(_baseRadius、既定30m) + ボーナスの範囲(SetBonusRadius)
 *   暗躍ボーナスで範囲を広げるため、ゲーム中にも変えられるようにしている(SetBaseRadius / SetBonusRadius)
 * ・スキャンは自分の画面だけのもの(見えるのは使った本人だけ)。そのため自分のプレイヤー(CS_Player.isLocalPlayer)だけで動き、通信はしない
 *   (悪人の位置とHPは同期済みなので、各クライアントの手元で判定できる。悪人の一覧はCS_NpcWorld.villainsを使う)
 * ・見た目(波・悪人の目印)はCS_PlayerScanEffectが、ここの状態とイベントを見て出す(仮の見た目。差し替え・削除しやすいよう分けている)
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScan : MonoBehaviour
{
    [Header("範囲")]
    [SerializeField][Min(0f)] private float _baseRadius = 30f;      // 基本の範囲(m)

    [Header("時間")]
    [SerializeField][Min(0.01f)] private float _expandTime = 1f;    // 波が範囲いっぱいまで広がる時間(秒)
    [SerializeField][Min(0f)] private float _fadeTime = 0.3f;       // 広がりきった後、波が消えるまでの時間(秒)。この間は再使用できない
    [SerializeField][Min(0f)] private float _revealDuration = 10f;  // 波が届いた悪人が見え続ける時間(秒)

    private CS_Player _player;

    private float _bonusRadius;         // 暗躍ボーナスなどで足される範囲(m)
    private bool _isScanning;           // 波が出ている間か(広がる途中と、消える途中)
    private float _scanElapsed;         // 波が出てからの時間(秒)
    private float _scanRadius;          // 今回の波の範囲(使った時点の範囲で固定する)
    private Vector3 _scanOrigin;        // 波の中心(使った位置)

    // 見えている悪人と、見えなくなる時刻
    private readonly Dictionary<CS_VillainHealth, float> _revealEndTimes = new Dictionary<CS_VillainHealth, float>();
    private readonly List<CS_VillainHealth> _expired = new List<CS_VillainHealth>();

    public float radius => _baseRadius + _bonusRadius;      // 今の範囲(m)
    public bool isScanning => _isScanning;                  // 波が出ている間か(この間は再使用できない)
    public Vector3 scanOrigin => _scanOrigin;
    public float scanRadius => _scanRadius;
    // 波の今の半径(m)。広がりきった後は範囲のまま
    public float currentWaveRadius => _scanRadius * Mathf.Clamp01(_scanElapsed / _expandTime);
    // 波の濃さ(1 → 消える途中で0へ)。見た目用
    public float waveAlpha => _scanElapsed <= _expandTime || _fadeTime <= 0f ? 1f : Mathf.Clamp01(1f - (_scanElapsed - _expandTime) / _fadeTime);
    public IReadOnlyCollection<CS_VillainHealth> revealedVillains => _revealEndTimes.Keys;     // 今見えている悪人

    public event Action onScanStarted;      // スキャンを使った時(自分のプレイヤーのみ)
    public event Action onScanFinished;     // 波が消えて、また使えるようになった時(自分のプレイヤーのみ)

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
    }

    private void Update()
    {
        // スキャンは自分の画面だけのもの(NPC・他人のプレイヤーでは動かさない)
        if (!_player.isLocalPlayer) return;

        ReadInput();
        UpdateWave(Time.deltaTime);
        UpdateReveals();
    }

    // 基本の範囲を変える(アイテム・調整用)
    public void SetBaseRadius(float value)
    {
        _baseRadius = Mathf.Max(0f, value);
    }

    // ボーナスで足す範囲を変える(暗躍ボーナス用)
    public void SetBonusRadius(float value)
    {
        _bonusRadius = Mathf.Max(0f, value);
    }

    // この悪人が今見えているか
    public bool IsRevealed(CS_VillainHealth villain)
    {
        return villain != null && _revealEndTimes.ContainsKey(villain);
    }

    // スキャンボタンを押したら、波が出ていなければスキャンを始める
    private void ReadInput()
    {
        if (_isScanning) return;
        if (!_player.canAct) return;
        if (_player.scanAction == null || !_player.scanAction.WasPressedThisFrame()) return;

        StartScan();
    }

    private void StartScan()
    {
        _isScanning = true;
        _scanElapsed = 0f;
        _scanRadius = radius;
        _scanOrigin = transform.position;
        onScanStarted?.Invoke();
    }

    // 波を広げ、届いた悪人を見えるようにする。消えきったら再使用できるようにする
    private void UpdateWave(float deltaTime)
    {
        if (!_isScanning) return;

        _scanElapsed += deltaTime;
        RevealVillainsWithin(currentWaveRadius);

        if (_scanElapsed < _expandTime + _fadeTime) return;

        _isScanning = false;
        onScanFinished?.Invoke();
    }

    // 波の中にいる悪人を見えるようにする(すでに見えている悪人は、見える時間を延ばさない)
    private void RevealVillainsWithin(float waveRadius)
    {
        float sqrRadius = waveRadius * waveRadius;
        foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
        {
            if (!IsAlive(villain)) continue;
            if (_revealEndTimes.ContainsKey(villain)) continue;
            if ((villain.transform.position - _scanOrigin).sqrMagnitude > sqrRadius) continue;

            _revealEndTimes.Add(villain, Time.time + _revealDuration);
        }
    }

    // 見える時間が過ぎた・倒された・消えた悪人を外す
    private void UpdateReveals()
    {
        if (_revealEndTimes.Count == 0) return;

        _expired.Clear();
        foreach (KeyValuePair<CS_VillainHealth, float> pair in _revealEndTimes)
        {
            if (!IsAlive(pair.Key) || Time.time >= pair.Value) _expired.Add(pair.Key);
        }

        foreach (CS_VillainHealth villain in _expired)
        {
            _revealEndTimes.Remove(villain);
        }
    }

    // 生きていて、場に出ている悪人か(isDefeatedはサーバーでしか立たないため、同期されているHPでも見る)
    private static bool IsAlive(CS_VillainHealth villain)
    {
        return villain != null && villain.isActiveAndEnabled && !villain.isDefeated && villain.currentHp > 0f;
    }
}
