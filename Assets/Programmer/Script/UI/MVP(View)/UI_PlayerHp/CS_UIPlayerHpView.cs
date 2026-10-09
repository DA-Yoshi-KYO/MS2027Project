/* ================================================
 * Hpの描画の処理
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hpの描画の処理
/// ・満タンのハート Prefab と空のハート Prefab の2種類を使う
/// ・maxHp が変わったら動的に生成し直す
/// </summary>
public class CS_UIPlayerHpView : CS_BaseView<CS_UIPlayerHpPresenter>
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("ハートアイコンの親")]
    [SerializeField] private Transform _heartsRoot;

    [Header("ハートアイコン Prefab")]
    [SerializeField] private GameObject _fullHeartPrefab;  // 満タンのハート
    [SerializeField] private GameObject _emptyHeartPrefab; // 空のハート

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 満タンハートと空ハートを別々に管理
    private readonly List<GameObject> _fullHearts = new();
    private readonly List<GameObject> _emptyHearts = new();

    private int _currentMaxHp = -1; // 前回の maxHp（変化を検知するため）

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIPlayerHpPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // 描画（Presenter から呼ばれる）
    // =========================================================

    /// <summary>
    /// HP を更新する
    /// maxHp が変わったらハートを生成し直す
    /// </summary>
    public void UpdateHp(int hp, int max)
    {
        // maxHp が変わったらハートを生成し直す
        if (max != _currentMaxHp)
        {
            RebuildHearts(max);
            _currentMaxHp = max;
        }

        // HP に応じて満タン・空ハートを切り替える
        for (int i = 0; i < _currentMaxHp; i++)
        {
            bool isFull = i < hp;
            _fullHearts[i].SetActive(isFull);
            _emptyHearts[i].SetActive(!isFull);
        }
    }

    // =========================================================
    // ハートの生成
    // =========================================================

    /// <summary>maxHp 分の満タン・空ハートを生成する</summary>
    private void RebuildHearts(int max)
    {
        // 既存のハートを削除
        ClearHearts();

        // maxHp 分の満タン・空ハートを生成
        for (int i = 0; i < max; i++)
        {
            // 満タンハートと空ハートを同じ位置に重ねて生成
            // HP に応じて SetActive で切り替える
            _fullHearts.Add(Instantiate(_fullHeartPrefab, _heartsRoot));
            _emptyHearts.Add(Instantiate(_emptyHeartPrefab, _heartsRoot));
        }
    }

    // =========================================================
    // クリーンアップ
    // =========================================================

    private void ClearHearts()
    {
        foreach (var go in _fullHearts)
            if (go != null) Destroy(go);
        foreach (var go in _emptyHearts)
            if (go != null) Destroy(go);

        _fullHearts.Clear();
        _emptyHearts.Clear();
    }

    private void OnDestroy()
    {
        ClearHearts();
    }
}
