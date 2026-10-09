/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-09 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手配度の表示を担当する View
/// ・星型 Prefab を maxLevel 分表示する
/// ・手配度が上がるごとに左から色付き星が増える（空スタート）
/// ・maxLevel が変わったら動的に生成し直す
/// </summary>
public class CS_UIWantedLevelView : CS_BaseView<CS_UIWantedLevelPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("星アイコンの親")]
    [SerializeField] private Transform _starsRoot;

    [Header("星アイコン Prefab")]
    [SerializeField] private GameObject _filledStarPrefab; // 色付き星
    [SerializeField] private GameObject _emptyStarPrefab;  // 空星

    [Header("最大手配度（Inspector で変更可能）")]
    [SerializeField] private int _defaultMaxLevel = 5;

    // =========================================================
    // 内部フィールド
    // =========================================================

    private readonly List<GameObject> _filledStars = new();
    private readonly List<GameObject> _emptyStars = new();

    private int _currentMaxLevel = -1; // 前回の maxLevel（変化を検知するため）

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIWantedLevelPresenter presenter)
    {
        base.SetPresenter(presenter);

        // 起動時にデフォルトの maxLevel で星を生成する
        RebuildStars(_defaultMaxLevel);
        _currentMaxLevel = _defaultMaxLevel;
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>
    /// 手配度を更新する
    /// maxLevel が変わったら星を生成し直す
    /// 手配度が上がるごとに左から色付き星が増える
    /// </summary>
    public void UpdateWantedLevel(int level, int max)
    {
        // maxLevel が変わったら星を生成し直す
        if (max != _currentMaxLevel)
        {
            RebuildStars(max);
            _currentMaxLevel = max;
        }

        // 手配度に応じて色付き・空星を切り替える
        for (int i = 0; i < _currentMaxLevel; i++)
        {
            bool isFilled = i < level;
            _filledStars[i].SetActive(isFilled);
            _emptyStars[i].SetActive(!isFilled);
        }
    }

    // =========================================================
    // 星の生成
    // =========================================================

    /// <summary>maxLevel 分の色付き・空星を生成する</summary>
    private void RebuildStars(int max)
    {
        ClearStars();

        for (int i = 0; i < max; i++)
        {
            var filled = Instantiate(_filledStarPrefab, _starsRoot);
            var empty = Instantiate(_emptyStarPrefab, _starsRoot);

            // 初期状態は全て空星
            filled.SetActive(false);
            empty.SetActive(true);

            _filledStars.Add(filled);
            _emptyStars.Add(empty);
        }
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void ClearStars()
    {
        foreach (var go in _filledStars)
            if (go != null) Destroy(go);
        foreach (var go in _emptyStars)
            if (go != null) Destroy(go);

        _filledStars.Clear();
        _emptyStars.Clear();
    }

    private void OnDestroy()
    {
        ClearStars();
    }
}
