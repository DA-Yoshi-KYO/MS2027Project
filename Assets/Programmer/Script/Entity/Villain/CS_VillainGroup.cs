using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * 悪人グループ1つ分の情報と、グループ単位の犯罪の進行を管理するクラス
 * CS_VillainSpawnerがグループを生成した時に作り、毎フレームTickを呼んで犯罪を進める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・犯罪はグループ単位で進む。残っているメンバー全員が犯罪中(CS_VillainCrime.isCommittingCrime)の間だけ進行する
 *   巡回している間も犯罪中の扱い。1人でも臨戦態勢・帰還中になったら、グループ全体が手を止める(それまでの進行度は保持する)
 * ・巡回パターン(スポーン位置のCS_VillainPatrolRoute)があるグループは、ここでは巡回しない(hasPatrolがfalse)
 *   悪人ごとにパターンのルートを回る(CS_VillainPatrolCrew・CS_VillainPatrol)。以下はパターンが無い時の全員での往復
 * ・巡回(スポーン位置のあるグループのみ。ルートはCS_VillainSpawnPoint.CreatePatrolPaths)
 *   メンバー全員で1つの進み具合(patrolProgress、0～1)を共有し、全員がそれぞれのルートの同じ割合の位置を回る
 *   → 進み具合は、一番長いルートを、メンバーの中で一番遅い巡回速度で歩く速さで進む(長いルートの人ほど速く歩く)
 *   → 端(0か1)に着いたら、スポーン位置のlookAroundTime秒は全員で見回してから折り返す(往復)
 *   → メンバーが臨戦態勢・帰還中でも進める。戻ってきたメンバーは、自分のルートの今の進み具合の位置へ合流する
 *   ルートの割り当て: 加わった順に、ルートの上から順に割り当てる(順番 % ルートの数)。途中で加わったメンバーも同じ
 *   同じルートのメンバー同士は、スポーン位置のpatrolSpacing(m)の間隔で並ぶ(倒れた人の分は詰める)
 *   → slot番目の人は、ルートの範囲をslot人分ずらした範囲を往復する(並んだまま往復するので、折り返しても追い越さない)
 *     短くて全員が並びきらないルートでは、間隔を詰める
 * ・エリア(スポーン位置の親のCS_CrimeArea)があるグループは、グループごとの犯罪完遂を使わない(ソロ)
 *   完遂時間・完遂の判定は行わず、犯罪の進行度(crimeProgress)はエリアの犯罪完遂率になる(メンバーへ送る値も同じ)
 *   ゲージを進めるのはCS_VillainSpawner(エリアで生き残っているグループ数を数える)
 * ・完遂までの時間は、残っているメンバーのcrimeCompleteTimeのうち一番長いもの
 * ・完遂したら
 *   1. onAnyCrimeCompleted を1回だけ呼ぶ
 *      → 最終スコアのマイナス・犯罪完遂数の加算は、スコア側がこれを購読してグループ単位で行う
 *   2. 残っているメンバー全員が逃走(フェードアウト → Despawn)する
 * ・ランダムイベント(大量発生)で生成したグループは、スポーン位置を持たない(spawnPointがnull)
 *   スポナーがTickを呼ばないので途中で犯罪は進まず、イベントの時間切れでCompleteCrimeが呼ばれて完遂する
 * ・メンバーが撃退・逃走でDestroyされるとnull扱いになる。全員いなくなったらisAliveがfalseになる
 *   撃退されて倒れている(死亡モーション中で、まだ消えていない)メンバーも、いないものとして扱う(IsActive)
 * ・同時に攻撃できるのは、グループのうち maxAttackers 人まで(攻撃枠)
 *   臨戦態勢になったメンバーは攻撃枠を取れたら攻撃し、取れなければつかず離れずで様子を見る
 *   攻撃枠は臨戦態勢が終わる(帰還する)・撃退される・逃走する時に空き、様子見のメンバーが代わりに取る
 * ・様子見のメンバーがターゲットを取り囲む時の並び順(GetSurroundOrder)も、グループの中で決める
 *   同じターゲットを取り囲んでいるメンバーの平均位置の方向を扇の中心にし、そこから見た角度(左から右)の順に並べる
 * ・グループ番号(id)と犯罪の進行度は、スキャンのUIがクライアントでも使うので、メンバー(CS_VillainCrime)のNetworkVariableへ書き込む
 *   進行度は毎フレーム送らず、前回送った値から_progressSyncStep(1%)以上変わった時と、完遂した時だけ送る
 * ・サーバー(オフライン時はその場)でのみ使う
 */
// ========================================

public class CS_VillainGroup
{
    private readonly CS_VillainSpawnPoint _spawnPoint;
    private readonly CS_CrimeArea _crimeArea;   // 担当のエリア(スポーン位置の親。無ければnull)
    private readonly List<CS_VillainCrime> _members = new List<CS_VillainCrime>();
    private readonly List<CS_VillainCombat> _combats = new List<CS_VillainCombat>();          // メンバーの反撃(_membersと同じ順)
    private readonly HashSet<CS_VillainCombat> _attackers = new HashSet<CS_VillainCombat>();   // 攻撃枠を持っているメンバー
    private readonly List<CS_VillainPatrolPath> _patrolPaths = new List<CS_VillainPatrolPath>();   // 巡回ルート(スポーン位置が無ければ空)
    private readonly List<int> _patrolRouteIndices = new List<int>();   // メンバーごとの巡回ルートの番号(_combatsと同じ順)
    private const float _progressSyncStep = 0.01f;   // 進行度をメンバーへ送る刻み(1%)
    private static int _nextId;                     // 次に作るグループの番号

    private readonly int _id;
    private readonly int _maxAttackers;
    private readonly bool _usesTimeScaling;
    private float _crimeElapsed;
    private bool _isCrimeCompleted;
    private float _syncedProgress;   // 最後にメンバーへ送った進行度
    private float _patrolProgress;          // 巡回の進み具合(0～1。全員で共有する)
    private int _patrolDirection = 1;       // 巡回の進む向き(+1: 最後の地点へ / -1: 最初の地点へ)
    private float _lookAroundRemaining;     // 端で見回している残り時間(秒。0なら歩いている)

    public int id => _id;   // グループ番号(0から。メンバーのCS_VillainCrime.groupIdと同じ)
    public CS_VillainSpawnPoint spawnPoint => _spawnPoint;
    public CS_CrimeArea crimeArea => _crimeArea;           // 担当のエリア(無ければnull)
    public bool usesAreaCrime => _crimeArea != null;      // グループごとではなく、エリアの犯罪完遂ゲージを使うか(ソロ)
    public bool isCrimeCompleted => _isCrimeCompleted;
    public IReadOnlyList<CS_VillainCrime> members => _members;
    public bool usesTimeScaling => _usesTimeScaling;   // 時間経過の段階でステータスを変えるか   // 撃退・逃走で消えたメンバーはnullになっている

    // Destroyされたメンバーはnull扱いになるので、撃退されていないメンバーが1人でも残っていれば生存
    public bool isAlive => _members.Exists(IsActive);

    public bool hasPatrol => _patrolPaths.Count > 0;            // 巡回するグループか(スポーン位置のあるグループ)
    public bool isLookingAround => _lookAroundRemaining > 0f;   // 巡回の端で、全員で見回している間か

    // 犯罪の進行度(0～1)
    // 犯罪の進行度(0～1)。エリアの担当なら、エリアの犯罪完遂率
    public float crimeProgress
    {
        get
        {
            if (usesAreaCrime) return _crimeArea.progress;

            float completeTime = GetCrimeCompleteTime();
            return completeTime <= 0f ? 1f : Math.Min(1f, _crimeElapsed / completeTime);
        }
    }

    // どのグループが犯罪を完遂しても呼ばれる(サーバーのみ)。スコア側の購読用
    public static event Action<CS_VillainGroup> onAnyCrimeCompleted;

    // Domain Reloadを切っている場合に、前回の再生の番号が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _nextId = 0;
    }

    // usesTimeScaling: 時間経過の段階でステータスを変えるか(レイドのボスなど、固定のステータスのグループはfalse)
    public CS_VillainGroup(CS_VillainSpawnPoint spawnPoint, int maxAttackers, bool usesTimeScaling = true)
    {
        _id = _nextId++;
        _spawnPoint = spawnPoint;
        _maxAttackers = maxAttackers;
        _usesTimeScaling = usesTimeScaling;

        if (spawnPoint != null)
        {
            _patrolPaths.AddRange(spawnPoint.CreatePatrolPaths());
            _crimeArea = spawnPoint.crimeArea;
        }
    }

    public void AddMember(CS_VillainCrime member)
    {
        _members.Add(member);
        member.SetGroup(_id, crimeProgress);
        member.SetCrimeArea(_crimeArea);
        CS_VillainCombat combat = member.GetComponent<CS_VillainCombat>();
        _combats.Add(combat);
        _patrolRouteIndices.Add(hasPatrol ? (_combats.Count - 1) % _patrolPaths.Count : -1);
        combat.SetGroup(this);
    }

    // ---- 巡回 ----

    // これから index 番目(0始まり)に加わるメンバーが、今の進み具合で立つ位置(生成位置に使う)
    // memberCount: 一緒に生成するメンバーの人数(同じルートの人数を数えるのに使う)
    public Vector3 GetPatrolPositionForNewMember(int index, int memberCount)
    {
        int routeIndex = (_combats.Count + index) % _patrolPaths.Count;

        // 既にいるメンバーの後ろに、一緒に生成するメンバーが加わった時の並び
        int existing = CountActiveOnRoute(routeIndex);
        int slot = existing;
        int count = existing;
        for (int i = 0; i < memberCount; i++)
        {
            if ((_combats.Count + i) % _patrolPaths.Count != routeIndex) continue;

            if (i < index) slot++;
            count++;
        }

        CS_VillainPatrolPath path = _patrolPaths[routeIndex];
        return path.Evaluate(GetSlotProgress(path, slot, count, _patrolProgress));
    }

    // メンバーが今向かう巡回の位置(サーバーのみ)。巡回しないグループ・メンバーならfalse
    // lookAhead: 歩いている間、今の位置からどれだけ先(m)を目的地にするか(目的地に追い付いて止まらないように)
    // speed: 進み具合に合わせて歩く速さ(m/秒) / forward: 進む向き(見回す時の中心の向き)
    public bool TryGetPatrolTarget(CS_VillainCombat combat, float lookAhead, out Vector3 destination, out Vector3 position, out float speed, out Vector3 forward)
    {
        destination = position = forward = Vector3.zero;
        speed = 0f;

        int index = _combats.IndexOf(combat);
        if (!hasPatrol || index < 0) return false;

        int routeIndex = _patrolRouteIndices[index];
        CS_VillainPatrolPath path = _patrolPaths[routeIndex];
        GetSlot(index, routeIndex, out int slot, out int count);

        float spacing = GetSlotSpacing(path, count);
        float range = 1f - spacing * (count - 1);   // このメンバーが往復する範囲の、ルートに対する割合
        float progress = GetSlotProgress(path, slot, count, _patrolProgress);

        position = path.Evaluate(progress);
        forward = path.GetForward(progress, _patrolDirection);
        speed = range * path.length * GetProgressSpeed();

        // 見回している間は端の位置。歩いている間は少し先の位置を目的地にする(自分の往復する範囲の中に収める)
        float aheadProgress = isLookingAround || path.length <= 0f ? 0f : lookAhead / path.length * _patrolDirection;
        float min = spacing * slot;
        float max = min + range;
        destination = path.Evaluate(Mathf.Clamp(progress + aheadProgress, min, max));
        return true;
    }

    // 巡回を進める(サーバーのみ。Tickから毎フレーム呼ばれる)。端に着いたら見回してから折り返す
    private void TickPatrol(float deltaTime)
    {
        if (!hasPatrol) return;

        if (isLookingAround)
        {
            _lookAroundRemaining -= deltaTime;
            if (_lookAroundRemaining <= 0f) _patrolDirection = -_patrolDirection;
            return;
        }

        _patrolProgress += _patrolDirection * GetProgressSpeed() * deltaTime;
        if (_patrolProgress > 0f && _patrolProgress < 1f) return;

        // 端に着いたら、全員で見回す(見回す時間が0ならすぐ折り返す)
        _patrolProgress = Mathf.Clamp01(_patrolProgress);
        _lookAroundRemaining = _spawnPoint.lookAroundTime;
        if (_lookAroundRemaining <= 0f) _patrolDirection = -_patrolDirection;
    }

    // 進み具合が1秒に進む量。一番長いルートを、メンバーの中で一番遅い巡回速度で歩く速さ
    private float GetProgressSpeed()
    {
        float longest = 0f;
        foreach (CS_VillainPatrolPath path in _patrolPaths)
        {
            longest = Math.Max(longest, path.length);
        }
        if (longest <= 0f) return 0f;

        float slowest = float.MaxValue;
        foreach (CS_VillainCombat combat in _combats)
        {
            if (combat != null && combat.enabled) slowest = Math.Min(slowest, combat.patrolSpeed);
        }
        return slowest == float.MaxValue ? 0f : slowest / longest;
    }

    // 同じルートのメンバーのうち、何番目か(slot)と、全部で何人か(count)。倒れた・逃走中のメンバーは数えない
    private void GetSlot(int index, int routeIndex, out int slot, out int count)
    {
        slot = 0;
        count = 0;
        for (int i = 0; i < _combats.Count; i++)
        {
            if (_patrolRouteIndices[i] != routeIndex || !IsPatrolling(_combats[i])) continue;

            if (i < index) slot++;
            count++;
        }
        if (!IsPatrolling(_combats[index])) count++;   // 自分自身(倒れる直前などで数えられなかった時)
    }

    private int CountActiveOnRoute(int routeIndex)
    {
        int count = 0;
        for (int i = 0; i < _combats.Count; i++)
        {
            if (_patrolRouteIndices[i] == routeIndex && IsPatrolling(_combats[i])) count++;
        }
        return count;
    }

    // 同じルートのメンバー同士の間隔(ルートに対する割合)。全員が並びきらない短いルートでは詰める
    private float GetSlotSpacing(CS_VillainPatrolPath path, int count)
    {
        if (count <= 1 || path.length <= 0f) return 0f;

        return Math.Min(_spawnPoint.patrolSpacing / path.length, 1f / (count - 1));
    }

    // slot 番目のメンバーの進み具合。ルートの範囲をslot人分ずらした範囲を、共有の進み具合の割合で進む
    private float GetSlotProgress(CS_VillainPatrolPath path, int slot, int count, float progress)
    {
        float spacing = GetSlotSpacing(path, count);
        float range = 1f - spacing * (count - 1);
        return spacing * slot + range * progress;
    }

    // 巡回に加わっているメンバーか(倒れた・逃走中のメンバーは並びに数えない)
    private static bool IsPatrolling(CS_VillainCombat combat)
    {
        return combat != null && combat.enabled;
    }

    // ---- 戦闘 ----

    // メンバーが新しく臨戦態勢になったことを、他のメンバーへ知らせる(サーバーのみ)
    public void NotifyEngaged(CS_VillainCombat source, Transform target, CS_PlayerHealth player)
    {
        foreach (CS_VillainCombat combat in _combats)
        {
            if (combat == null || combat == source) continue;

            combat.OnAllyEngaged(target, player);
        }
    }

    // 同じターゲットを取り囲んでいるメンバーの並び方を求める
    // center: ターゲットから見た、取り囲んでいるメンバーの平均位置の方向(並ぶ扇の中心)
    // index : selfが中心から見て左から何番目か / count: 全部で何人か
    public void GetSurroundOrder(CS_VillainCombat self, Transform target, out Vector3 center, out int index, out int count)
    {
        center = Vector3.zero;
        foreach (CS_VillainCombat combat in _combats)
        {
            if (!IsSurrounding(combat, self, target)) continue;

            Vector3 offset = combat.transform.position - target.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0f) center += offset.normalized;
        }
        center = center.sqrMagnitude > 0f ? center.normalized : self.transform.forward;

        float selfAngle = GetAngleFromFront(self, target, center);
        index = 0;
        count = 1;   // self
        foreach (CS_VillainCombat combat in _combats)
        {
            if (combat == self || !IsSurrounding(combat, self, target)) continue;

            count++;
            float angle = GetAngleFromFront(combat, target, center);
            // 同じ角度の時はリストの順で決める(2人が同じ番号にならないように)
            if (angle < selfAngle || (angle == selfAngle && _combats.IndexOf(combat) < _combats.IndexOf(self))) index++;
        }
    }

    // 同じターゲットを取り囲んでいるメンバーか(self自身は、まだ取り囲みに入る前でも含める)
    private static bool IsSurrounding(CS_VillainCombat combat, CS_VillainCombat self, Transform target)
    {
        if (combat == null) return false;
        if (combat == self) return true;

        return combat.isSurrounding && combat.target == target;
    }

    // 扇の中心(front)から見た、メンバーのいる方向の角度(右が+)
    private static float GetAngleFromFront(CS_VillainCombat combat, Transform target, Vector3 front)
    {
        Vector3 offset = combat.transform.position - target.position;
        offset.y = 0f;
        return Vector3.SignedAngle(front, offset, Vector3.up);
    }

    // 攻撃枠を取る。既に持っている、または空きがあればtrue
    public bool TryAcquireAttackSlot(CS_VillainCombat attacker)
    {
        if (_attackers.Contains(attacker)) return true;
        if (_attackers.Count >= _maxAttackers) return false;

        _attackers.Add(attacker);
        return true;
    }

    // 攻撃枠を空ける(持っていなければ何もしない)
    public void ReleaseAttackSlot(CS_VillainCombat attacker)
    {
        _attackers.Remove(attacker);
    }

    // 巡回と犯罪を進める。完遂時間に達したら完遂する
    // 巡回はメンバーが臨戦態勢・帰還中でも進める(犯罪は全員が犯罪中の間だけ進む)
    public void Tick(float deltaTime)
    {
        if (_isCrimeCompleted) return;
        if (!isAlive) return;

        TickPatrol(deltaTime);

        // エリアの担当は、グループでは完遂しない(進行度はエリアの完遂率をメンバーへ送るだけ)
        if (usesAreaCrime)
        {
            SyncProgress(false);
            return;
        }

        if (!IsAllMembersCommittingCrime()) return;

        _crimeElapsed += deltaTime;
        if (_crimeElapsed >= GetCrimeCompleteTime())
        {
            CompleteCrime();
            return;
        }

        SyncProgress(false);
    }

    // 犯罪の進行度をメンバーへ送る(前回から1%以上変わった時だけ。forceなら必ず送る)
    private void SyncProgress(bool force)
    {
        float progress = _isCrimeCompleted ? 1f : crimeProgress;   // ランダムイベントの時間切れで完遂した時も、完遂度は1にする
        if (!force && Math.Abs(progress - _syncedProgress) < _progressSyncStep) return;

        _syncedProgress = progress;
        foreach (CS_VillainCrime member in _members)
        {
            if (member != null) member.SetCrimeProgress(progress);
        }
    }

    // 犯罪を完遂する(Tickで完遂時間に達した時、またはランダムイベントの時間切れで呼ばれる)
    // 既に完遂済み・全員いなくなったグループでは何もしない
    public void CompleteCrime()
    {
        if (_isCrimeCompleted || !isAlive) return;

        _isCrimeCompleted = true;
        onAnyCrimeCompleted?.Invoke(this);
        SyncProgress(true);

        foreach (CS_VillainCrime member in _members)
        {
            if (IsActive(member)) member.Escape();
        }
    }

    // まだ残っていて、撃退されていない(倒れていない)メンバーか
    private static bool IsActive(CS_VillainCrime member)
    {
        return member != null && !member.isDefeated;
    }

    private bool IsAllMembersCommittingCrime()
    {
        foreach (CS_VillainCrime member in _members)
        {
            if (IsActive(member) && !member.isCommittingCrime) return false;
        }
        return true;
    }

    private float GetCrimeCompleteTime()
    {
        float completeTime = 0f;
        foreach (CS_VillainCrime member in _members)
        {
            if (IsActive(member)) completeTime = Math.Max(completeTime, member.crimeCompleteTime);
        }
        return completeTime;
    }
}
