using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * プレイヤーのスキャン(周囲の悪人グループ・警察・巡回ドローンを、建物越しに一定時間見えるようにする)を行うクラス
 * スキャンボタン(キーボード: Q / コントローラー: LB)で使う
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様は仕様書「スキャン」(Issue #294)。Apexのブラッドハウンドのスキャンのようなもの
 * ・ボタンを押すと、押した位置から円状の波が_expandTime秒(既定1秒)かけて半径radiusまで広がる
 *   範囲は円柱型: 横方向の距離が波の半径以内、かつ高さの差が上下それぞれ_height以内(既定15m)の相手に届く
 *   波が届いた悪人グループ・警察・巡回ドローンを、_revealDuration秒(既定10秒、仕様の仮の値)のあいだ「見えている」状態にする(建物は関係なく届く)
 * ・悪人はグループ単位で見えるようにする(仕様 10/31更新。UIでグループごとに1アイコンを出すため)
 *   グループの誰か1人に波が届いたら、そのグループ全員が見えるようになる
 *   グループはCS_VillainCrime.groupId(サーバーから同期)で見分ける。グループに属さない悪人(-1)は、1人で1グループとして扱う
 *   見えているグループは revealedVillainGroups で取れる(メンバー・中心の位置・犯罪の進行度)
 * ・警察(CS_PoliceBrain)・巡回ドローン(CS_SecurityDrone)は1人(1機)ずつ。revealedPolice / revealedDrones で取れる
 *   波が広がりきった後、_fadeTime秒(既定0.3秒)で波が消えるまでは、もう一度使えない(仕様「エフェクトがなくなるまでは再使用できない」)
 * ・モーションは無く、移動しながら使える(移動・攻撃を止めない)
 * ・範囲 = 基本の範囲(_baseRadius、既定30m) + ボーナスの範囲(SetBonusRadius)
 *   暗躍ボーナスで範囲を広げるため、ゲーム中にも変えられるようにしている(SetBaseRadius / SetBonusRadius)
 * ・スキャンは自分の画面だけのもの(見えるのは使った本人だけ)。そのため自分のプレイヤー(CS_Player.isLocalPlayer)だけで動き、通信はしない
 *   (位置・HP・グループ番号は同期済みなので、各クライアントの手元で判定できる。一覧はCS_NpcWorldのvillains / police / securityDronesを使う)
 * ・波の見た目はCS_PlayerScanEffectが、見えている相手の目印(UI)はCS_PlayerScanMarkerUIが、ここの状態を見て出す
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
public class CS_PlayerScan : MonoBehaviour
{
    [Header("範囲")]
    [SerializeField][Min(0f)] private float _baseRadius = 30f;      // 基本の範囲(m)
    [SerializeField][Min(0f)] private float _height = 15f;          // 範囲の高さ(使った位置から上下それぞれ、m)

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

    // 見えている相手と、見えなくなる時刻
    private readonly Dictionary<int, float> _groupRevealEndTimes = new Dictionary<int, float>();                          // 悪人グループ(グループ番号)
    private readonly Dictionary<CS_VillainHealth, float> _loneVillainRevealEndTimes = new Dictionary<CS_VillainHealth, float>();  // グループに属さない悪人
    private readonly Dictionary<CS_PoliceBrain, float> _policeRevealEndTimes = new Dictionary<CS_PoliceBrain, float>();
    private readonly Dictionary<CS_SecurityDrone, float> _droneRevealEndTimes = new Dictionary<CS_SecurityDrone, float>();

    // 見えている悪人グループの一覧(毎フレーム作り直す。中身は使い回す)
    private readonly List<RevealedVillainGroup> _revealedGroups = new List<RevealedVillainGroup>();
    private readonly List<RevealedVillainGroup> _groupPool = new List<RevealedVillainGroup>();
    private readonly List<int> _expiredGroupIds = new List<int>();

    public float radius => _baseRadius + _bonusRadius;      // 今の範囲(m)
    public bool isScanning => _isScanning;                  // 波が出ている間か(この間は再使用できない)
    public Vector3 scanOrigin => _scanOrigin;
    public float scanRadius => _scanRadius;
    // 波の今の半径(m)。広がりきった後は範囲のまま
    public float currentWaveRadius => _scanRadius * Mathf.Clamp01(_scanElapsed / _expandTime);
    // 波の濃さ(1 → 消える途中で0へ)。見た目用
    public float waveAlpha => _scanElapsed <= _expandTime || _fadeTime <= 0f ? 1f : Mathf.Clamp01(1f - (_scanElapsed - _expandTime) / _fadeTime);
    public IReadOnlyList<RevealedVillainGroup> revealedVillainGroups => _revealedGroups;       // 今見えている悪人グループ
    public IReadOnlyCollection<CS_PoliceBrain> revealedPolice => _policeRevealEndTimes.Keys;   // 今見えている警察
    public IReadOnlyCollection<CS_SecurityDrone> revealedDrones => _droneRevealEndTimes.Keys;  // 今見えている巡回ドローン

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
        RebuildRevealedGroups();
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

    // この悪人が今見えているか(グループの誰かに波が届いていれば、全員見えている)
    public bool IsRevealed(CS_VillainHealth villain)
    {
        if (villain == null) return false;

        int groupId = GetGroupId(villain);
        return groupId >= 0 ? _groupRevealEndTimes.ContainsKey(groupId) : _loneVillainRevealEndTimes.ContainsKey(villain);
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
        RevealWithin(currentWaveRadius);

        if (_scanElapsed < _expandTime + _fadeTime) return;

        _isScanning = false;
        onScanFinished?.Invoke();
    }

    // 波の中(円柱型)にいる悪人グループ・警察・巡回ドローンを見えるようにする(すでに見えている相手は、見える時間を延ばさない)
    private void RevealWithin(float waveRadius)
    {
        float sqrRadius = waveRadius * waveRadius;
        float endTime = Time.time + _revealDuration;

        foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
        {
            if (!IsAlive(villain)) continue;
            if (!IsWithin(villain.transform, sqrRadius)) continue;

            // グループの誰かに届いたら、グループごと見えるようにする
            int groupId = GetGroupId(villain);
            if (groupId >= 0) _groupRevealEndTimes.TryAdd(groupId, endTime);
            else _loneVillainRevealEndTimes.TryAdd(villain, endTime);
        }

        foreach (CS_PoliceBrain police in CS_NpcWorld.police)
        {
            if (!IsAlive(police)) continue;
            if (!IsWithin(police.transform, sqrRadius)) continue;

            _policeRevealEndTimes.TryAdd(police, endTime);
        }

        foreach (CS_SecurityDrone drone in CS_NpcWorld.securityDrones)
        {
            if (!IsAlive(drone)) continue;
            if (!IsWithin(drone.transform, sqrRadius)) continue;

            _droneRevealEndTimes.TryAdd(drone, endTime);
        }
    }

    // 波の中(円柱型)にいるか: 高さの差が上下それぞれ_height以内で、横方向の距離が波の半径以内
    private bool IsWithin(Transform target, float sqrRadius)
    {
        Vector3 offset = target.position - _scanOrigin;
        if (Mathf.Abs(offset.y) > _height) return false;

        offset.y = 0f;
        return offset.sqrMagnitude <= sqrRadius;
    }

    // 見える時間が過ぎた・倒された・消えた相手を外す
    // (悪人グループは、メンバーが全員いなくなった時もRebuildRevealedGroupsで外す)
    private void UpdateReveals()
    {
        float now = Time.time;
        RemoveExpired(_loneVillainRevealEndTimes, now, IsAlive);
        RemoveExpired(_policeRevealEndTimes, now, IsAlive);
        RemoveExpired(_droneRevealEndTimes, now, IsAlive);

        _expiredGroupIds.Clear();
        foreach (KeyValuePair<int, float> pair in _groupRevealEndTimes)
        {
            if (now >= pair.Value) _expiredGroupIds.Add(pair.Key);
        }
        foreach (int groupId in _expiredGroupIds)
        {
            _groupRevealEndTimes.Remove(groupId);
        }
    }

    private static void RemoveExpired<T>(Dictionary<T, float> revealEndTimes, float now, Func<T, bool> isAlive)
    {
        if (revealEndTimes.Count == 0) return;

        List<T> expired = null;
        foreach (KeyValuePair<T, float> pair in revealEndTimes)
        {
            if (isAlive(pair.Key) && now < pair.Value) continue;

            expired ??= new List<T>();
            expired.Add(pair.Key);
        }

        if (expired == null) return;
        foreach (T key in expired)
        {
            revealEndTimes.Remove(key);
        }
    }

    // 見えている悪人グループの一覧を作り直す(メンバーは動き・減るので毎フレーム集め直す)
    private void RebuildRevealedGroups()
    {
        _groupPool.AddRange(_revealedGroups);
        _revealedGroups.Clear();

        if (_groupRevealEndTimes.Count > 0)
        {
            foreach (CS_VillainHealth villain in CS_NpcWorld.villains)
            {
                if (!IsAlive(villain)) continue;

                int groupId = GetGroupId(villain);
                if (groupId < 0 || !_groupRevealEndTimes.ContainsKey(groupId)) continue;

                RevealedVillainGroup group = FindRevealedGroup(groupId) ?? AddRevealedGroup(groupId);
                group.AddMember(villain);
            }

            // メンバーが全員いなくなったグループは外す
            _expiredGroupIds.Clear();
            foreach (int groupId in _groupRevealEndTimes.Keys)
            {
                if (FindRevealedGroup(groupId) == null) _expiredGroupIds.Add(groupId);
            }
            foreach (int groupId in _expiredGroupIds)
            {
                _groupRevealEndTimes.Remove(groupId);
            }
        }

        // グループに属さない悪人は、1人で1グループとして扱う
        foreach (CS_VillainHealth villain in _loneVillainRevealEndTimes.Keys)
        {
            AddRevealedGroup(-1).AddMember(villain);
        }
    }

    private RevealedVillainGroup FindRevealedGroup(int groupId)
    {
        foreach (RevealedVillainGroup group in _revealedGroups)
        {
            if (group.groupId == groupId) return group;
        }
        return null;
    }

    private RevealedVillainGroup AddRevealedGroup(int groupId)
    {
        RevealedVillainGroup group;
        if (_groupPool.Count > 0)
        {
            group = _groupPool[_groupPool.Count - 1];
            _groupPool.RemoveAt(_groupPool.Count - 1);
        }
        else
        {
            group = new RevealedVillainGroup();
        }

        group.Reset(groupId);
        _revealedGroups.Add(group);
        return group;
    }

    // 所属するグループの番号(グループに属さなければ-1)
    private static int GetGroupId(CS_VillainHealth villain)
    {
        return villain.TryGetComponent(out CS_VillainCrime crime) ? crime.groupId : -1;
    }

    // 生きていて、場に出ている悪人か(isDefeatedはサーバーでしか立たないため、同期されているHPでも見る)
    private static bool IsAlive(CS_VillainHealth villain)
    {
        return villain != null && villain.isActiveAndEnabled && !villain.isDefeated && villain.currentHp > 0f;
    }

    // 場に出ている警察か(クライアントではCS_PoliceBrain自体は無効なので、GameObjectで見る)
    private static bool IsAlive(CS_PoliceBrain police)
    {
        return police != null && police.gameObject.activeInHierarchy;
    }

    // 場に出ている巡回ドローンか
    private static bool IsAlive(CS_SecurityDrone drone)
    {
        return drone != null && drone.gameObject.activeInHierarchy;
    }

    // ========================================
    // 見えている悪人グループ1つ分
    // ========================================
    public class RevealedVillainGroup
    {
        private readonly List<CS_VillainHealth> _members = new List<CS_VillainHealth>();
        private int _groupId;
        private float _crimeProgress;

        public int groupId => _groupId;                             // グループの番号(グループに属さない悪人は-1)
        public IReadOnlyList<CS_VillainHealth> members => _members;  // 生きているメンバー
        public float crimeProgress => _crimeProgress;               // 犯罪の進行度(0〜1)

        // 生きているメンバーの中心の位置
        public Vector3 center
        {
            get
            {
                if (_members.Count == 0) return Vector3.zero;

                Vector3 sum = Vector3.zero;
                foreach (CS_VillainHealth member in _members)
                {
                    sum += member.transform.position;
                }
                return sum / _members.Count;
            }
        }

        internal void Reset(int groupId)
        {
            _groupId = groupId;
            _crimeProgress = 0f;
            _members.Clear();
        }

        internal void AddMember(CS_VillainHealth villain)
        {
            _members.Add(villain);

            // 進行度はグループ全員に同じ値が入っているので、一番進んでいる値を使う(同期の途中でずれていても戻らないように)
            if (villain.TryGetComponent(out CS_VillainCrime crime))
                _crimeProgress = Mathf.Max(_crimeProgress, crime.crimeProgress);
        }
    }
}
