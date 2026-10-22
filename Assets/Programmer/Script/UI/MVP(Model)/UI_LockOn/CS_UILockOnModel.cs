/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-21 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// ロックオンUIのModel。
/// 現在のロックオン対象と表示状態を保持する。
/// </summary>
public class CS_UILockOnModel : CS_BaseModel
{
    // =========================================================
    // 内部フィールド
    // =========================================================

    private Transform _targetTransform;
    private bool _isLockedOn;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public Transform targetTransform => _targetTransform;
    public bool isLockedOn => _isLockedOn;

    // =========================================================
    // イベント
    // =========================================================

    public event Action<bool> OnLockOnVisibilityChanged;

    // =========================================================
    // ロックオン操作
    // =========================================================

    public void SetTarget(Transform targetTransform)
    {
        _targetTransform = targetTransform;

        bool shouldShow = _targetTransform != null;

        if (_isLockedOn == shouldShow)
            return;

        _isLockedOn = shouldShow;
        OnLockOnVisibilityChanged?.Invoke(_isLockedOn);
    }

    public void ClearTarget()
    {
        _targetTransform = null;

        if (!_isLockedOn)
            return;

        _isLockedOn = false;
        OnLockOnVisibilityChanged?.Invoke(false);
    }

    public override void Dispose()
    {
        _targetTransform = null;
        _isLockedOn = false;
        OnLockOnVisibilityChanged = null;
    }
}
