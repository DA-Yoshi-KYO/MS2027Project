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
/// ・キャラごとに「満タンのハート Prefab」と「空のハート Prefab」を持つ
/// ・SetCharacter() で表示するキャラを切り替える
/// ・maxHp が変わったら動的に生成し直す
/// </summary>
public class CS_UIPlayerHpView : CS_BaseView<CS_UIPlayerHpPresenter>
{
    // =========================================================
    // キャラごとのハート設定
    // =========================================================

    [System.Serializable]
    public class HeartSet
    {
        [SerializeField] private CSE_CharacterType _characterType;
        [SerializeField] private GameObject _fullHeartPrefab;  // 満タンのハート
        [SerializeField] private GameObject _emptyHeartPrefab; // 空のハート

        public CSE_CharacterType characterType => _characterType;
        public GameObject fullHeartPrefab => _fullHeartPrefab;
        public GameObject emptyHeartPrefab => _emptyHeartPrefab;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("ハートアイコンの親")]
    [SerializeField] private Transform _heartsRoot;

    [Header("キャラごとのハート Prefab")]
    [SerializeField] private HeartSet[] _heartSets;

    // =========================================================
    // 内部フィールド
    // =========================================================

    // 満タンハートと空ハートを別々に管理
    private readonly List<GameObject> _fullHearts = new();
    private readonly List<GameObject> _emptyHearts = new();

    private HeartSet _currentHeartSet; // 現在表示しているキャラの設定
    private int _currentMaxHp = -1;    // 前回の maxHp（変化を検知するため）

    // =========================================================
    // CS_BaseView
    // =========================================================

    public override void SetPresenter(CS_UIPlayerHpPresenter presenter)
    {
        base.SetPresenter(presenter);
    }

    // =========================================================
    // キャラの切り替え（Presenter から呼ばれる）
    // =========================================================

    /// <summary>
    /// 表示するキャラを設定する
    /// キャラが変わったら、次の UpdateHp でハートを生成し直す
    /// </summary>
    public void SetCharacter(CSE_CharacterType characterType)
    {
        HeartSet heartSet = FindHeartSet(characterType);

        // 同じキャラなら何もしない
        if (heartSet != null && heartSet == _currentHeartSet) return;

        _currentHeartSet = heartSet;
        ClearHearts();
        _currentMaxHp = -1; // 次の UpdateHp で必ず生成し直す
    }

    /// <summary>キャラに対応するハート設定を探す</summary>
    private HeartSet FindHeartSet(CSE_CharacterType characterType)
    {
        if (_heartSets == null) return null;

        for (int i = 0; i < _heartSets.Length; i++)
        {
            if (_heartSets[i].characterType == characterType) return _heartSets[i];
        }

        Debug.LogWarning($"ハートの設定がありません: {characterType}");
        return null;
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
        // キャラが未設定なら描画できない
        if (_currentHeartSet == null) return;

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
            _fullHearts.Add(Instantiate(_currentHeartSet.fullHeartPrefab, _heartsRoot));
            _emptyHearts.Add(Instantiate(_currentHeartSet.emptyHeartPrefab, _heartsRoot));
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