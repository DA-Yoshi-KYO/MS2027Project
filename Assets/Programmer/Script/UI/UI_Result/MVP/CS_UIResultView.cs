/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リザルトシーンの View
/// ・ソロ / マルチ 両対応
/// ・画像(ランク・順位・称号・キャラ)は CSO_ResultSpriteTable から取る
/// ・称号の条件の回数は CSO_ResultTitleSettings(Inspector)から取る
/// ・ソロ  : キャラ・総合ランク・称号(複数)・総合タイム・エリアごとのタイムとランク・各回数
/// ・マルチ: プレイヤーごとに CS_UIPlayerResultItemView を並べる(順位・キャラ・称号(複数)・各回数)
///
/// 登場の演出(PlayIntro で始める。戻り値は、演出が全部終わるまでの時間)
///   ソロ
///     1. BackHeadImage・総合ランク・キャラのアイコンが、画面の上の外から、下へ動く(同時) … CS_UISlideIn
///     2. BackImage(ボードの本体)が、閉じた状態から、上から下へ開く           … CS_UIRevealMask
///     3. 称号が、画面の左の外から、右へスライドして入る(上の枠から順に)     … CS_UISlideIn
///     4. 総合クリアタイム                                                      … CS_UICountUpText
///     5. エリアごとのタイム + ランク(同時)                                    … CS_UICountUpText / CS_UIRankStamp
///     6. 警察に見つかった回数
///     7. 奇襲の成功回数
///     8. SAの使用回数
///   マルチ
///     警察に見つかった回数 → 奇襲の成功回数 → SAの使用回数(プレイヤー全員が同時)
///   部品(上の右側のクラス)が付いていない部分は、何もせず飛ばす(すでに表示されている)
/// </summary>
public class CS_UIResultView : CS_BaseView<CS_UIResultPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("共通")]
    [Tooltip("ランク・順位・称号・キャラの画像をまとめたデータ(DB_ResultSpriteTable)")]
    [SerializeField] private CSO_ResultSpriteTable _spriteTable;

    [Tooltip("称号の条件の回数(DB_ResultTitleSettings)。空なら初期値(3回 / 50回 / 50回)で判定する")]
    [SerializeField] private CSO_ResultTitleSettings _titleSettings;

    [Header("ソロ用 UI")]
    [SerializeField] private GameObject _soloPanel;
    [SerializeField] private Image _soloCharacterImage;           // 使ったキャラのアイコン
    [SerializeField] private Image _soloOverallRankImage;         // 総合ランク(S/A/B/C)
    [Tooltip("称号の枠。称号は最大4つ(ダークヒーロー + 3つ)。先頭の枠から順に入る。使わない枠は隠れる。各枠に CS_UISlideIn を付けると、左からスライドして入る")]
    [SerializeField] private Image[] _soloTitleImages;            // 称号(複数)
    [SerializeField] private TextMeshProUGUI _soloTotalTimeText;  // 総合タイム(例: 5:00)
    [SerializeField] private TextMeshProUGUI _soloFoundByPoliceText;   // 警察に見つかった数
    [SerializeField] private TextMeshProUGUI _soloSurpriseAttackText;  // 奇襲の成功回数
    [SerializeField] private TextMeshProUGUI _soloSaUseText;           // SAの使用回数
    [Tooltip("エリアごとの行。エリアの並び順(上から)で割り当てる")]
    [SerializeField] private CS_UIAreaResultRowView[] _areaRows;

    [Header("ソロの登場演出(任意。空の部分は、飛ばす)")]
    [Tooltip("BackHeadImage に付けた CS_UISlideIn(FromTop)")]
    [SerializeField] private CS_UISlideIn _soloHeadSlide;

    [Tooltip("総合ランクの Image に付けた CS_UISlideIn(FromTop)")]
    [SerializeField] private CS_UISlideIn _soloRankSlide;

    [Tooltip("キャラのアイコン(CharacterImage)に付けた CS_UISlideIn(FromTop)。BackHeadImage・総合ランクと同時に、上から下へ動く")]
    [SerializeField] private CS_UISlideIn _soloCharacterSlide;

    [Tooltip("ボードの本体(BackImage と中身をまとめた Body)に付けた CS_UIRevealMask")]
    [SerializeField] private CS_UIRevealMask _soloBodyReveal;

    [Header("マルチ用 UI")]
    [SerializeField] private GameObject _multiPanel;
    [SerializeField] private Transform _playerResultsRoot;
    [SerializeField] private GameObject _playerResultPrefab;

    [Header("登場の間隔")]

    [Tooltip("称号が1つずつ入ってくる間隔(秒)。0なら、全部同時")]
    [Min(0f)]
    [SerializeField] private float _titleInterval = 0.15f;

    [Tooltip("最後の称号が入り始めてから、数字が出始めるまでの間(秒)。称号が入りきる前に、数字を始めたい時は小さくする")]
    [Min(0f)]
    [SerializeField] private float _gapAfterTitles = 0.3f;

    [Tooltip("数字の段が、ひとつ前の段から出始めるまでの間隔(秒)。0なら、全部同時")]
    [Min(0f)]
    [SerializeField] private float _stepInterval = 0.4f;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly List<GameObject> _playerResultItems = new();

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIResultPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // ソロ用：描画
    // =========================================================

    /// <summary>ソロ用：キャラ・称号・各回数を表示する</summary>
    public void UpdateSoloResult(CS_ResultData data)
    {
        if (data == null) return;

        var s = data.score;

        // キャラのアイコン
        CS_ResultImageUtility.SetSprite(
            _soloCharacterImage,
            _spriteTable != null
                ? _spriteTable.GetCharacterSprite(data.characterType)
                : null
        );

        // 称号(複数。条件を満たしたものを全部。条件の回数は _titleSettings)
        CS_ResultImageUtility.SetTitleSprites(
            _soloTitleImages,
            data.GetResultTitles(_titleSettings),
            _spriteTable
        );

        // 各回数。カウントアップの準備(部品が付いていなければ、すぐ表示する)
        CS_ResultNumberUtility.SetCount(_soloFoundByPoliceText, s.foundByPoliceCount, "回");
        CS_ResultNumberUtility.SetCount(_soloSurpriseAttackText, s.surpriseAttackCount, "回");
        CS_ResultNumberUtility.SetCount(_soloSaUseText, s.saUseCount, "回");
    }

    /// <summary>
    /// ソロ用：総合ランク・総合タイム・エリアごとのタイムとランクを表示する
    /// エリアの数が行の数より少なければ、余った行は隠す
    /// </summary>
    public void UpdateSoloRankSummary(CS_SoloRankSummary? summary)
    {
        if (!summary.HasValue) return;

        CS_SoloRankSummary s = summary.Value;

        // 総合ランク
        CS_ResultImageUtility.SetSprite(
            _soloOverallRankImage,
            _spriteTable != null
                ? _spriteTable.GetOverallRankSprite(s.overallRank)   // 総合ランクの画像
                : null
        );

        // 総合タイム。カウントアップの準備(部品が付いていなければ、すぐ表示する)
        CS_ResultNumberUtility.SetTime(_soloTotalTimeText, s.totalSeconds);

        // エリアごとの行
        if (_areaRows == null) return;

        for (int i = 0; i < _areaRows.Length; i++)
        {
            CS_UIAreaResultRowView row = _areaRows[i];

            if (row == null) continue;

            if (s.rows != null && i < s.rows.Count)
                row.UpdateView(s.rows[i], _spriteTable);
            else
                row.Hide();
        }
    }

    // =========================================================
    // マルチ用：描画
    // =========================================================

    /// <summary>
    /// マルチ用：全プレイヤー分の結果を表示する
    /// リストはModelでスコア降順にソート済み
    /// 同じtotalScoreは同順位として表示する
    /// </summary>
    public void UpdateMultiResults(List<CS_ResultData> results)
    {
        if (results == null || results.Count == 0)
            return;

        ClearPlayerResultItems();

        for (int i = 0; i < results.Count; i++)
        {
            CS_ResultData result = results[i];

            int rank = CalculateCompetitionRank(
                results,
                result.totalScore
            );

            GameObject item = Instantiate(
                _playerResultPrefab,
                _playerResultsRoot
            );

            CS_UIPlayerResultItemView itemView =
                item.GetComponent<CS_UIPlayerResultItemView>();

            if (itemView != null)
            {
                itemView.UpdateView(
                    result,
                    rank,
                    _spriteTable,
                    _titleSettings
                );
            }

            _playerResultItems.Add(item);
        }
    }

    /// <summary>
    /// 同じスコアは同順位にする。
    /// 順位 = 自分より高いスコアの人数 + 1。
    /// </summary>
    private int CalculateCompetitionRank(
        List<CS_ResultData> results,
        int targetScore)
    {
        int higherScorePlayerCount = 0;

        foreach (CS_ResultData result in results)
        {
            if (result.totalScore > targetScore)
                higherScorePlayerCount++;
        }

        return higherScorePlayerCount + 1;
    }

    // =========================================================
    // 登場の演出
    // =========================================================

    /// <summary>
    /// 登場の演出を、決まった順番で始める。いつ呼ぶかは、Controller が決める
    /// (カットシーンが開き終わった時など)。
    /// ・今表示されているパネル(ソロ/マルチ)だけが対象
    /// ・何回呼んでも、それぞれの部品は、最初の1回しか動かない
    /// 変更: 戻り値を追加。演出が全部終わるまでの時間(秒)を返す。
    ///       リザルトの最後の流れ(ボタンの受け付け → カットシーン → タイトルへ)の開始時刻に使う。
    ///       戻り値を使わない呼び出しは、そのままコンパイルできる。
    /// </summary>
    /// <returns>演出が全部終わるまでの時間(秒)。部品が1つもなければ 0</returns>
    public float PlayIntro()
    {
        float end = 0f;

        if (IsPanelShown(_soloPanel))
        {
            end = Mathf.Max(end, PlaySoloIntro());
        }

        if (IsPanelShown(_multiPanel))
        {
            end = Mathf.Max(end, PlayMultiIntro());
        }

        return end;
    }

    /// <summary>パネルが表示されているか。パネルの設定が空なら、表示されている扱い</summary>
    private static bool IsPanelShown(GameObject panel)
    {
        return panel == null || panel.activeInHierarchy;
    }

    // ---------------------------------------------------------
    // ソロ
    // ---------------------------------------------------------

    /// <summary>
    /// ソロの演出。各部分の始まる時刻を、前の部分が終わる時刻から計算して、順番に始める。
    /// 部品が付いていない部分は、時間を使わずに飛ばす。
    /// 戻り値: 演出が全部終わるまでの時間(秒)
    /// </summary>
    private float PlaySoloIntro()
    {
        float time = 0f;
        float end = 0f;

        // 1. BackHeadImage・総合ランク・キャラのアイコンが、上から下へ動く(同時)
        float slideTime = 0f;
        slideTime = Mathf.Max(slideTime, PlaySlide(_soloHeadSlide, time));
        slideTime = Mathf.Max(slideTime, PlaySlide(_soloRankSlide, time));
        slideTime = Mathf.Max(slideTime, PlaySlide(_soloCharacterSlide, time));
        time += slideTime;
        end = Mathf.Max(end, time);

        // 2. BackImage(ボードの本体)が、上から下へ開く
        if (_soloBodyReveal != null)
        {
            _soloBodyReveal.Play(time);
            time += _soloBodyReveal.duration;
            end = Mathf.Max(end, time);
        }

        // 3. 称号が、左からスライドして入る(上の枠から順に)
        List<CS_UISlideIn> titleSlides = CollectTitleSlides();

        for (int i = 0; i < titleSlides.Count; i++)
        {
            float titleStart = time + i * _titleInterval;

            titleSlides[i].Play(titleStart);
            end = Mathf.Max(end, titleStart + titleSlides[i].duration);
        }

        if (titleSlides.Count > 0)
        {
            // 数字は、最後の称号が入り始めてから、少し後に始める
            time += (titleSlides.Count - 1) * _titleInterval + _gapAfterTitles;
        }

        // 4〜8. 数字(とエリアのランク)
        float stepsEnd = PlaySteps(BuildSoloNumberSteps(), time);

        return Mathf.Max(end, stepsEnd);
    }

    /// <summary>スライドを、待ち時間 delay で始めて、かかる時間を返す。部品が空なら 0</summary>
    private static float PlaySlide(
        CS_UISlideIn slide,
        float delay)
    {
        if (slide == null)
        {
            return 0f;
        }

        slide.Play(delay);

        return slide.duration;
    }

    /// <summary>使われている称号の枠のうち、CS_UISlideIn が付いているものを、上の枠から順に集める</summary>
    private List<CS_UISlideIn> CollectTitleSlides()
    {
        var list = new List<CS_UISlideIn>();

        if (_soloTitleImages == null)
        {
            return list;
        }

        foreach (Image slot in _soloTitleImages)
        {
            // 使わない枠は、非表示になっている(SetTitleSprites)。順番に入れない
            if (slot == null ||
                !slot.gameObject.activeInHierarchy)
            {
                continue;
            }

            CS_UISlideIn slide = slot.GetComponent<CS_UISlideIn>();

            if (slide != null)
            {
                list.Add(slide);
            }
        }

        return list;
    }

    /// <summary>
    /// ソロの数字の段を、決まった順番で集める。
    ///   1. 総合クリアタイム
    ///   2. エリアごとのタイム + ランク(同時)
    ///   3. 警察に見つかった回数
    ///   4. 奇襲の成功回数
    ///   5. SAの使用回数
    /// </summary>
    private List<(List<CS_UICountUpText> numbers, List<CS_UIRankStamp> stamps)> BuildSoloNumberSteps()
    {
        var steps = new List<(List<CS_UICountUpText> numbers, List<CS_UIRankStamp> stamps)>();

        // 1. 総合クリアタイム
        steps.Add((SingleNumber(_soloTotalTimeText), new List<CS_UIRankStamp>()));

        // 2. エリアごとのタイム + ランク(同時)
        var areaTimes = new List<CS_UICountUpText>();
        var areaRanks = new List<CS_UIRankStamp>();

        if (_areaRows != null)
        {
            foreach (CS_UIAreaResultRowView row in _areaRows)
            {
                if (row == null) continue;

                row.CollectCountUps(areaTimes);
                row.CollectRankStamps(areaRanks);
            }
        }

        steps.Add((areaTimes, areaRanks));

        // 3. 警察に見つかった回数
        steps.Add((SingleNumber(_soloFoundByPoliceText), new List<CS_UIRankStamp>()));

        // 4. 奇襲の成功回数
        steps.Add((SingleNumber(_soloSurpriseAttackText), new List<CS_UIRankStamp>()));

        // 5. SAの使用回数
        steps.Add((SingleNumber(_soloSaUseText), new List<CS_UIRankStamp>()));

        return steps;
    }

    // ---------------------------------------------------------
    // マルチ
    // ---------------------------------------------------------

    /// <summary>
    /// マルチの演出。プレイヤー全員が、同じ段で同時に出る。
    ///   警察に見つかった回数 → 奇襲の成功回数 → SAの使用回数
    /// 戻り値: 演出が全部終わるまでの時間(秒)
    /// </summary>
    private float PlayMultiIntro()
    {
        var found = new List<CS_UICountUpText>();
        var surprise = new List<CS_UICountUpText>();
        var sa = new List<CS_UICountUpText>();

        foreach (GameObject item in _playerResultItems)
        {
            if (item == null) continue;

            CS_UIPlayerResultItemView itemView =
                item.GetComponent<CS_UIPlayerResultItemView>();

            if (itemView != null)
                itemView.CollectCountUps(found, surprise, sa);
        }

        var steps = new List<(List<CS_UICountUpText> numbers, List<CS_UIRankStamp> stamps)>
        {
            (found, new List<CS_UIRankStamp>()),
            (surprise, new List<CS_UIRankStamp>()),
            (sa, new List<CS_UIRankStamp>()),
        };

        return PlaySteps(steps, 0f);
    }

    // ---------------------------------------------------------
    // 共通
    // ---------------------------------------------------------

    /// <summary>
    /// 数字(とランク画像)の段を、startDelay から、_stepInterval 秒ずつ遅らせて始める。
    /// 同じ段のものは、同時に始まる。部品が1つもない段は、待ち時間を作らずに飛ばす。
    /// 戻り値: 最後の部品が登場し終わる時刻(秒)。部品が1つもなければ startDelay
    /// </summary>
    private float PlaySteps(
        List<(List<CS_UICountUpText> numbers, List<CS_UIRankStamp> stamps)> steps,
        float startDelay)
    {
        float delay = startDelay;
        float end = startDelay;

        foreach (var step in steps)
        {
            if (step.numbers.Count == 0 &&
                step.stamps.Count == 0)
            {
                continue;
            }

            foreach (CS_UICountUpText number in step.numbers)
            {
                number.Play(delay);
                end = Mathf.Max(end, delay + number.totalSeconds);
            }

            foreach (CS_UIRankStamp stamp in step.stamps)
            {
                stamp.Play(delay);
                end = Mathf.Max(end, delay + stamp.totalSeconds);
            }

            delay += _stepInterval;
        }

        return end;
    }

    /// <summary>1つの数字だけを入れた一覧を作る。部品が付いていなければ、空の一覧</summary>
    private static List<CS_UICountUpText> SingleNumber(TMP_Text text)
    {
        var list = new List<CS_UICountUpText>(1);

        CS_UICountUpText countUp = CS_ResultNumberUtility.GetCountUp(text);

        if (countUp != null)
            list.Add(countUp);

        return list;
    }

    // =========================================================
    // ソロ / マルチ 切り替え
    // =========================================================

    public void ShowSoloPanel()
    {
        if (_soloPanel != null) _soloPanel.SetActive(true);
        if (_multiPanel != null) _multiPanel.SetActive(false);
    }

    public void ShowMultiPanel()
    {
        if (_soloPanel != null) _soloPanel.SetActive(false);
        if (_multiPanel != null) _multiPanel.SetActive(true);
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void ClearPlayerResultItems()
    {
        foreach (var item in _playerResultItems)
        {
            if (item != null) Destroy(item);
        }
        _playerResultItems.Clear();
    }

    private void OnDestroy()
    {
        ClearPlayerResultItems();
    }
}
