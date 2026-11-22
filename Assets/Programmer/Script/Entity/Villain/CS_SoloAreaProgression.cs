using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * ソロの、エリアの攻略順と「今のエリア」・エリアごとの制限時間・倒した率を持つクラス
 * 悪人・警察・防犯ドローンは、今のエリアの分だけが有効になる
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・置き方: Field_Solo1 > Villain などに1つだけ置く。マルチ(Field_Solo1が無効)では存在しないので、instanceはnullのまま
 * ・攻略順(areas)をInspectorで決める(仕様: 生活 → エネルギー → インフラ)
 * ・エリアごとに制限時間(秒)がある(timeLimits。初期値は2分)。エリアが始まった時から数える
 * ・エリアが決着する条件(判定はCS_VillainSpawnerが行い、AdvanceAreaを呼ぶ)
 *   ・今のエリアの悪人を全員倒した → 守れた
 *   ・制限時間が切れた時、倒した率(倒した悪人 ÷ 出した悪人)が clearRate 以上 → 守れた(一旦クリア)
 *   ・制限時間が切れた時、倒した率が clearRate 未満 → 守れなかった(次のエリアへ進む)
 * ・全エリアが決着したら onAllAreasResolved を呼ぶ(終了の判定はCS_SoloGameJudge)
 * ・倒した率: エリアごと(GetDefeatedRate)と全体(totalDefeatedRate = 倒した合計 ÷ 出した合計)
 *   悪人の数はCS_VillainSpawnerが SetAreaCounts で報告する。犯罪完遂ゲージUIのバーにも、この倒した率を出す
 * ・タワーへ運ぶ演出は別(#538)。その演出は onAreaCleared を購読して行う
 * ・IsActiveAt(位置)で、その位置が今のエリアの扇形に入るかを調べられる(エリア分けはCS_CrimeAreaLayout)
 *   instanceが無い(マルチ)時は常にtrue
 */
// ========================================

public class CS_SoloAreaProgression : MonoBehaviour
{
    [SerializeField]
    [Tooltip("エリアの攻略順(上から順に攻略。生活区域 → エネルギー区域 → インフラ区域)")]
    private CS_CrimeArea[] _areas = new CS_CrimeArea[0];

    [SerializeField]
    [Tooltip("エリアごとの制限時間(秒)。Areasと同じ順。足りない・0以下のエリアは、下の初期値を使う")]
    private float[] _timeLimits = new float[0];

    [SerializeField, Min(1f)]
    [Tooltip("制限時間の初期値(秒)。2分")]
    private float _defaultTimeLimit = 120f;

    [SerializeField, Range(0.5f, 1f)]
    [Tooltip("時間切れの時に、倒した率がこれ以上なら、そのエリアを守れた(一旦クリア)ことにする(50〜100%)")]
    private float _clearRate = 0.8f;

    private static CS_SoloAreaProgression _instance;

    private int _index;
    private int _protectedCount;   // 守れたエリアの数
    private float _areaElapsed;    // 今のエリアが始まってからの時間(秒)
    private int[] _spawnedCounts = new int[0];    // エリアごとの、出した悪人の数
    private int[] _defeatedCounts = new int[0];   // エリアごとの、倒した悪人の数

    public static CS_SoloAreaProgression instance => _instance;

    public IReadOnlyList<CS_CrimeArea> areas => _areas;
    public int areaCount => _areas.Length;
    public int clearedCount => _protectedCount;                                  // 守れた(クリアした)エリアの数
    public bool isAllResolved => _index >= _areas.Length;                        // 全エリアが決着したか(守れたか守れなかったかは問わない)
    public CS_CrimeArea currentArea => isAllResolved ? null : _areas[_index];    // 今のエリア(全て決着済みならnull)
    public float clearRate => _clearRate;

    public float areaTimeLimit => GetTimeLimit(_index);                          // 今のエリアの制限時間(秒)
    public float areaRemainingTime => Mathf.Max(0f, areaTimeLimit - _areaElapsed);
    public bool isAreaTimeUp => !isAllResolved && _areaElapsed >= areaTimeLimit;

    public event Action<CS_CrimeArea> onAreaCleared;    // エリアを守れた時(そのエリア)
    public event Action<CS_CrimeArea> onAreaChanged;    // 今のエリアが変わった時(次のエリア。全て決着済みならnull)
    public event Action onAllAreasResolved;             // 全エリアが決着した時

    private void Awake()
    {
        _spawnedCounts = new int[_areas.Length];
        _defeatedCounts = new int[_areas.Length];
    }

    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!isAllResolved) _areaElapsed += Time.deltaTime;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    // エリアの制限時間(秒)
    private float GetTimeLimit(int index)
    {
        if (index >= 0 && index < _timeLimits.Length && _timeLimits[index] > 0f) return _timeLimits[index];
        return _defaultTimeLimit;
    }

    // 今のエリアを決着させて、次のエリアへ進む(CS_VillainSpawnerから呼ばれる)
    // isProtected: 守れたならtrue(悪人を全員倒した、または時間切れで倒した率が基準以上)
    public void AdvanceArea(bool isProtected)
    {
        if (isAllResolved) return;

        CS_CrimeArea resolved = _areas[_index];
        _index++;
        _areaElapsed = 0f;
        if (isProtected)
        {
            _protectedCount++;
            onAreaCleared?.Invoke(resolved);
        }
        onAreaChanged?.Invoke(currentArea);
        if (isAllResolved) onAllAreasResolved?.Invoke();
    }

    // エリアの悪人の数(出した数・倒した数)を報告する(CS_VillainSpawnerから呼ばれる)
    public void SetAreaCounts(CS_CrimeArea area, int spawned, int defeated)
    {
        int index = Array.IndexOf(_areas, area);
        if (index < 0) return;

        _spawnedCounts[index] = spawned;
        _defeatedCounts[index] = defeated;
    }

    // エリアの倒した率(0〜1)。まだ悪人を出していないエリアは0、悪人が0人のまま決着したエリアは1
    public float GetDefeatedRate(CS_CrimeArea area)
    {
        int index = Array.IndexOf(_areas, area);
        if (index < 0) return 0f;
        if (_spawnedCounts[index] <= 0) return index < _index ? 1f : 0f;

        return Mathf.Clamp01((float)_defeatedCounts[index] / _spawnedCounts[index]);
    }

    // 全体の倒した率(0〜1) = 倒した合計 ÷ 出した合計(まだ出していないエリアの悪人は含まない)
    public float totalDefeatedRate
    {
        get
        {
            int spawned = 0;
            int defeated = 0;
            for (int i = 0; i < _spawnedCounts.Length; i++)
            {
                spawned += _spawnedCounts[i];
                defeated += _defeatedCounts[i];
            }
            return spawned <= 0 ? 0f : Mathf.Clamp01((float)defeated / spawned);
        }
    }

    // 位置が、今のエリアの扇形に入るか。ソロでない(instanceが無い)時は常にtrue
    public static bool IsActiveAt(Vector3 position)
    {
        if (_instance == null) return true;

        CS_CrimeArea area = _instance.currentArea;
        return area != null && CS_CrimeAreaLayout.FindArea(position) == area;
    }
}
