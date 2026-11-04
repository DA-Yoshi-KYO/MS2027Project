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
 *   1人でも臨戦態勢・帰還中になったら、グループ全体が手を止める(それまでの進行度は保持する)
 *   実行役(見張り役ではないメンバー)が全員倒されて見張り役だけになった時も、見張り役が引き継ぐまで止める
 * ・完遂までの時間は、残っているメンバーのcrimeCompleteTimeのうち一番長いもの
 * ・完遂したら
 *   1. onAnyCrimeCompleted を1回だけ呼ぶ
 *      → 最終スコアのマイナス・犯罪完遂数の加算は、スコア側がこれを購読してグループ単位で行う
 *   2. 残っているメンバー全員が逃走(フェードアウト → Despawn)する
 * ・ランダムイベント(大量発生)で生成したグループは、スポーン位置を持たない(spawnPointがnull)
 *   スポナーがTickを呼ばないので途中で犯罪は進まず、イベントの時間切れでCompleteCrimeが呼ばれて完遂する
 * ・メンバーが撃退・逃走でDestroyされるとnull扱いになる。全員いなくなったらisAliveがfalseになる
 * ・同時に攻撃できるのは、グループのうち maxAttackers 人まで(攻撃枠)
 *   臨戦態勢になったメンバーは攻撃枠を取れたら攻撃し、取れなければつかず離れずで様子を見る
 *   攻撃枠は臨戦態勢が終わる(帰還する)・撃退される・逃走する時に空き、様子見のメンバーが代わりに取る
 * ・様子見のメンバーがターゲットを取り囲む時の並び順(GetSurroundOrder)も、グループの中で決める
 *   同じターゲットを取り囲んでいるメンバーの平均位置の方向を扇の中心にし、そこから見た角度(左から右)の順に並べる
 * ・サーバー(オフライン時はその場)でのみ使う
 */
// ========================================

public class CS_VillainGroup
{
    private readonly CS_VillainSpawnPoint _spawnPoint;
    private readonly List<CS_VillainCrime> _members = new List<CS_VillainCrime>();
    private readonly List<CS_VillainCombat> _combats = new List<CS_VillainCombat>();          // メンバーの反撃(_membersと同じ順)
    private readonly HashSet<CS_VillainCombat> _attackers = new HashSet<CS_VillainCombat>();   // 攻撃枠を持っているメンバー
    private readonly int _maxAttackers;
    private readonly bool _usesTimeScaling;
    private float _crimeElapsed;
    private bool _isCrimeCompleted;

    public CS_VillainSpawnPoint spawnPoint => _spawnPoint;
    public bool isCrimeCompleted => _isCrimeCompleted;
    public IReadOnlyList<CS_VillainCrime> members => _members;
    public bool usesTimeScaling => _usesTimeScaling;   // 時間経過の段階でステータスを変えるか   // 撃退・逃走で消えたメンバーはnullになっている

    // Destroyされたメンバーはnull扱いになるので、1人でも残っていれば生存
    public bool isAlive => _members.Exists(member => member != null);

    // 犯罪の進行度(0～1)
    public float crimeProgress
    {
        get
        {
            float completeTime = GetCrimeCompleteTime();
            return completeTime <= 0f ? 1f : Math.Min(1f, _crimeElapsed / completeTime);
        }
    }

    // どのグループが犯罪を完遂しても呼ばれる(サーバーのみ)。スコア側の購読用
    public static event Action<CS_VillainGroup> onAnyCrimeCompleted;

    // usesTimeScaling: 時間経過の段階でステータスを変えるか(レイドのボスなど、固定のステータスのグループはfalse)
    public CS_VillainGroup(CS_VillainSpawnPoint spawnPoint, int maxAttackers, bool usesTimeScaling = true)
    {
        _spawnPoint = spawnPoint;
        _maxAttackers = maxAttackers;
        _usesTimeScaling = usesTimeScaling;
    }

    public void AddMember(CS_VillainCrime member)
    {
        _members.Add(member);
        CS_VillainCombat combat = member.GetComponent<CS_VillainCombat>();
        _combats.Add(combat);
        combat.SetGroup(this);
    }

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

    // 犯罪を進める。完遂時間に達したら完遂する
    public void Tick(float deltaTime)
    {
        if (_isCrimeCompleted) return;
        if (!isAlive) return;
        if (!IsAllMembersCommittingCrime()) return;
        if (!HasExecutor()) return;   // 見張り役だけ残った時は、見張り役が引き継ぐまで進めない

        _crimeElapsed += deltaTime;
        if (_crimeElapsed >= GetCrimeCompleteTime())
        {
            CompleteCrime();
        }
    }

    // 犯罪を完遂する(Tickで完遂時間に達した時、またはランダムイベントの時間切れで呼ばれる)
    // 既に完遂済み・全員いなくなったグループでは何もしない
    public void CompleteCrime()
    {
        if (_isCrimeCompleted || !isAlive) return;

        _isCrimeCompleted = true;
        onAnyCrimeCompleted?.Invoke(this);

        foreach (CS_VillainCrime member in _members)
        {
            if (member != null) member.Escape();
        }
    }

    // 犯罪の実行役(見張り役ではないメンバー)が1人でも残っているか
    public bool HasExecutor()
    {
        foreach (CS_VillainCombat combat in _combats)
        {
            if (combat != null && !combat.isWatcher) return true;
        }
        return false;
    }

    private bool IsAllMembersCommittingCrime()
    {
        foreach (CS_VillainCrime member in _members)
        {
            if (member != null && !member.isCommittingCrime) return false;
        }
        return true;
    }

    private float GetCrimeCompleteTime()
    {
        float completeTime = 0f;
        foreach (CS_VillainCrime member in _members)
        {
            if (member != null) completeTime = Math.Max(completeTime, member.crimeCompleteTime);
        }
        return completeTime;
    }
}
