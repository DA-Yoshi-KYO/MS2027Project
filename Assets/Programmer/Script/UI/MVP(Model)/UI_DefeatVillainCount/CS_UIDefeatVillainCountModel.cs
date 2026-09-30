/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// 倒した悪人の数を保持する Model
/// ・倒した数（currentCount）と目標数（maxCount）を管理する
/// ・最大値は可変対応
/// ・評価システムは未定のため後から追加できる設計
/// </summary>
public class CS_UIDefeatVillainCountModel : CS_BaseModel
{
    // =========================================================
    // バッキングフィールド
    // =========================================================

    private readonly ReactiveProperty<int> _currentCount; // 現在の撃破数
    private readonly ReactiveProperty<int> _maxCount;     // 目標撃破数（可変）

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public ReadOnlyReactiveProperty<int> currentCount => _currentCount;
    public ReadOnlyReactiveProperty<int> maxCount => _maxCount;

    // =========================================================
    // コンストラクタ
    // =========================================================

    /// <summary>0 からスタート</summary>
    public CS_UIDefeatVillainCountModel(int maxCount) : this(maxCount, 0) { }

    public CS_UIDefeatVillainCountModel(int maxCount, int currentCount)
    {
        _maxCount = new ReactiveProperty<int>(Mathf.Max(1, maxCount));
        _currentCount = new ReactiveProperty<int>(Mathf.Clamp(currentCount, 0, _maxCount.Value));
    }

    // =========================================================
    // 値の変更
    // =========================================================

    /// <summary>撃破数を1増やす</summary>
    public void IncrementCount()
    {
        SetCount(_currentCount.Value + 1);
    }

    /// <summary>撃破数を直接セットする</summary>
    public void SetCount(int count)
    {
        _currentCount.Value = Mathf.Clamp(count, 0, _maxCount.Value);
    }

    /// <summary>目標撃破数を変更する（可変対応）</summary>
    public void SetMaxCount(int maxCount)
    {
        _maxCount.Value = Mathf.Max(1, maxCount);
        SetCount(_currentCount.Value); // 現在値をクランプし直す
    }

    /// <summary>カウントをリセットする</summary>
    public void Reset()
    {
        _currentCount.Value = 0;
    }

    /// <summary>目標達成したか</summary>
    public bool IsCompleted => _currentCount.Value >= _maxCount.Value;

    // =========================================================
    // IDisposable
    // =========================================================

    public override void Dispose()
    {
        _currentCount.Dispose();
        _maxCount.Dispose();
    }

}
