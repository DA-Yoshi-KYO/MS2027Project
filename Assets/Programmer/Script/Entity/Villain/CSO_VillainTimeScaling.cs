using System;
using UnityEngine;

/*
 * ゲームの経過時間による悪人の変化(ScriptableObject)
 * 経過時間ごとに、新しく生成するグループの人数・HP・犯罪完遂時間・攻撃力を決める
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・右クリック → Create → Villain → Villain Time Scaling でアセットを作る(初期値は悪人のデータ表の値)
 * ・CS_VillainSpawnerのTime Scalingに設定すると、グループの生成時に、その時点の経過時間の段階の値を使う
 *   既にフィールドにいる悪人の値は変わらない(新しく生成した悪人だけ)
 * ・段階は startTime(ゲーム開始からの秒数)が小さい順に並べる
 *   経過時間がstartTime以上の段階のうち、一番後ろのものが使われる
 * ・maxHp / crimeCompleteTime / attackPower は0以下にすると、悪人のBase Statsの値のまま変えない
 */
// ========================================

[CreateAssetMenu(fileName = "DB_VillainTimeScaling", menuName = "Villain/Villain Time Scaling")]
public class CSO_VillainTimeScaling : ScriptableObject
{
    // 1段階分の値
    [Serializable]
    public class Stage
    {
        [SerializeField, Min(0f)]
        [Tooltip("この段階が始まる経過時間(秒)")]
        private float _startTime;

        [SerializeField, Min(1)]
        [Tooltip("1グループの人数")]
        private int _memberCount = 2;

        [SerializeField]
        [Tooltip("HP上限(0以下ならBase Statsのまま)")]
        private float _maxHp;

        [SerializeField]
        [Tooltip("犯罪完遂までの時間(秒)(0以下ならBase Statsのまま)")]
        private float _crimeCompleteTime;

        [SerializeField]
        [Tooltip("攻撃力(0以下ならBase Statsのまま)")]
        private float _attackPower;

        public float startTime => _startTime;
        public int memberCount => _memberCount;
        public float maxHp => _maxHp;
        public float crimeCompleteTime => _crimeCompleteTime;
        public float attackPower => _attackPower;

        public Stage(float startTime, int memberCount, float maxHp, float crimeCompleteTime)
        {
            _startTime = startTime;
            _memberCount = memberCount;
            _maxHp = maxHp;
            _crimeCompleteTime = crimeCompleteTime;
        }
    }

    // 初期値は悪人のデータ表(悪人の時間経過による変化データ)の値。攻撃力はデータ表に無いので変えない
    [SerializeField]
    private Stage[] _stages =
    {
        new Stage(0f, 2, 1f, 40f),     // 0分〜1分
        new Stage(60f, 3, 2f, 35f),    // 1分〜2分
        new Stage(120f, 4, 3f, 35f),   // 2分〜3分
        new Stage(180f, 5, 3f, 30f),   // 3分〜4分
        new Stage(240f, 6, 4f, 30f),   // 4分〜5分
    };

    // 経過時間(秒)に対応する段階を返す(段階が無ければnull)
    public Stage GetStage(float elapsedTime)
    {
        Stage current = null;
        foreach (Stage stage in _stages)
        {
            if (stage.startTime > elapsedTime) continue;
            if (current != null && stage.startTime < current.startTime) continue;

            current = stage;
        }

        // 一番早い段階より前(マイナスの時間など)は、一番早い段階を使う
        return current ?? GetEarliestStage();
    }

    private Stage GetEarliestStage()
    {
        Stage earliest = null;
        foreach (Stage stage in _stages)
        {
            if (earliest == null || stage.startTime < earliest.startTime) earliest = stage;
        }
        return earliest;
    }
}
