using System;
using UnityEngine;

/*
 * 悪人のエリア(生活区域・インフラ区域・エネルギー区域)と、エリアごとの犯罪完遂ゲージ(ソロ)
 * エリアの下に置いたスポーン位置(CS_VillainSpawnPoint)の悪人グループが、このエリアの担当になる
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・置き方(Field_Solo1)
 *   Villain
 *   └ VillainSpawnPoints
 *      ├ CrimeArea_生活区域      ← このコンポーネント(Lv.1 / 完遂ライン1000)
 *      │  ├ VillainSpawnPoint_01   ← この下のスポーン位置が、このエリアの担当
 *      │  └ …
 *      ├ CrimeArea_インフラ区域  ← (Lv.2 / 2000)
 *      └ CrimeArea_エネルギー区域 ← (Lv.3 / 3000)
 *   スポーン位置の親を変えるだけで、担当のエリアを変えられる(エリアの範囲は決めなくてよい)
 * ・犯罪完遂ゲージ(ソロ時の仕様)
 *   毎秒「活動中(生き残っている)の悪人グループ数 × 特殊倍率」をゲージに足す(CS_VillainSpawnerがTickを呼ぶ)
 *   特殊倍率はランダムイベントで変わる(CS_RandomEventManager.GetCrimeRateMultiplier。通常1.0)
 *   ゲージが完遂ライン(completeLine)に届いたら完遂。ゲージはラインで止め、onCompleted / onAnyAreaCompleted を呼ぶ
 *   → 完遂した時に何が起きるか(スコア・悪人の逃走など)は未定。購読する側で決める
 * ・エリアがあるスポーン位置のグループは、グループごとの犯罪完遂(完遂時間・ゲージ)を使わない(CS_VillainGroup)
 *   エリアが無いスポーン位置(マルチ)は、今までどおりグループごとに完遂する
 * ・ソロは通信せずに遊ぶので、ゲージはネットワークで同期しない
 */
// ========================================

public class CS_CrimeArea : MonoBehaviour
{
    [SerializeField]
    [Tooltip("エリアの名前(生活区域 / インフラ区域 / エネルギー区域)")]
    private string _areaName = "生活区域";

    [SerializeField, Min(1)]
    [Tooltip("エリアのレベル(Lv.1 生活区域 / Lv.2 インフラ区域 / Lv.3 エネルギー区域)")]
    private int _level = 1;

    [SerializeField, Min(1f)]
    [Tooltip("犯罪完遂ライン(点)。ゲージがここに届いたら完遂(Lv.1: 1000 / Lv.2: 2000 / Lv.3: 3000)")]
    private float _completeLine = 1000f;

    private float _points;
    private bool _isCompleted;

    public string areaName => _areaName;
    public int level => _level;
    public float completeLine => _completeLine;
    public float points => _points;                                  // 今のゲージ(点)
    public float progress => Mathf.Clamp01(_points / _completeLine); // 犯罪完遂率(0〜1)
    public bool isCompleted => _isCompleted;

    public event Action onCompleted;                         // このエリアが完遂した時
    public static event Action<CS_CrimeArea> onAnyAreaCompleted;   // どのエリアが完遂しても呼ばれる(スコア側などの購読用)

    // ゲージを進める(CS_VillainSpawnerから毎フレーム呼ばれる)
    // activeGroupCount: このエリアで活動中(生き残っている)の悪人グループ数 / multiplier: 特殊倍率
    public void Tick(int activeGroupCount, float multiplier, float deltaTime)
    {
        if (_isCompleted) return;

        _points += activeGroupCount * multiplier * deltaTime;
        if (_points < _completeLine) return;

        _points = _completeLine;
        _isCompleted = true;
        onCompleted?.Invoke();
        onAnyAreaCompleted?.Invoke(this);
    }
}
