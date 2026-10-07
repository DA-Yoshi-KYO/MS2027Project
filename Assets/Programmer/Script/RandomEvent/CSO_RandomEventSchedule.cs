using System;
using UnityEngine;

/*
 * ランダムイベントの発生スケジュール(ScriptableObject)
 * いつ(ゲームの経過時間)イベントを起こすか と、そのタイミングで選んでよいイベントの候補を持つ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・右クリック → Create → RandomEvent → Random Event Schedule でアセットを作る
 * ・発生の回数 = タイミングの数。増やす・減らす時は、タイミングの行を追加・削除する
 * ・time はゲーム開始からの経過時間(秒)。初期値は仕様書(仮)の 2:00 / 3:00 / 4:00
 *   (発生タイミングは仕様書どうしで違いがあり、プランナーに確認中。決まったら値を変えるだけでよい)
 * ・候補(candidates)からランダムに1つ選ばれる
 *   終盤に起きても意味の無いイベント(例: 残り1分での支援物資)は、そのタイミングの候補に入れない
 * ・候補が空のタイミングでは何も起きない
 */
// ========================================

[CreateAssetMenu(fileName = "DB_RandomEventSchedule", menuName = "RandomEvent/Random Event Schedule")]
public class CSO_RandomEventSchedule : ScriptableObject
{
    // 1回分の発生タイミング
    [Serializable]
    public class Timing
    {
        [SerializeField, Min(0f)]
        [Tooltip("イベントを起こすゲームの経過時間(秒)")]
        private float _time;

        [SerializeField]
        [Tooltip("このタイミングで選んでよいイベント。ここからランダムに1つ選ばれる")]
        private CSO_RandomEvent[] _candidates = new CSO_RandomEvent[0];

        public float time => _time;
        public CSO_RandomEvent[] candidates => _candidates;

        public Timing(float time)
        {
            _time = time;
        }
    }

    // 初期値は仕様書(仮)の 2:00 / 3:00 / 4:00。候補はイベントを作った後に設定する
    [SerializeField]
    private Timing[] _timings =
    {
        new Timing(120f),
        new Timing(180f),
        new Timing(240f),
    };

    public int timingCount => _timings.Length;

    public Timing GetTiming(int index)
    {
        return _timings[index];
    }
}
