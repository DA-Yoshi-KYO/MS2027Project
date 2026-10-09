using System;
using System.Collections.Generic;

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
 * ・サーバー(オフライン時はその場)でのみ使う
 */
// ========================================

public class CS_VillainGroup
{
    private readonly CS_VillainSpawnPoint _spawnPoint;
    private readonly List<CS_VillainCrime> _members = new List<CS_VillainCrime>();
    private readonly HashSet<CS_VillainCombat> _attackers = new HashSet<CS_VillainCombat>();   // 攻撃枠を持っているメンバー
    private readonly int _maxAttackers;
    private float _crimeElapsed;
    private bool _isCrimeCompleted;

    public CS_VillainSpawnPoint spawnPoint => _spawnPoint;
    public bool isCrimeCompleted => _isCrimeCompleted;
    public IReadOnlyList<CS_VillainCrime> members => _members;   // 撃退・逃走で消えたメンバーはnullになっている

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

    public CS_VillainGroup(CS_VillainSpawnPoint spawnPoint, int maxAttackers)
    {
        _spawnPoint = spawnPoint;
        _maxAttackers = maxAttackers;
    }

    public void AddMember(CS_VillainCrime member)
    {
        _members.Add(member);
        member.GetComponent<CS_VillainCombat>().SetGroup(this);
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
