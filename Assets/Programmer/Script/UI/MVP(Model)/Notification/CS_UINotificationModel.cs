/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-10 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;

/// <summary>
/// 通知の状態を管理するModel
/// ・表示スロットは3つ固定
/// ・全スロット使用中の通知は待機列へ入れる
/// ・キル通知は表示時間内なら名前を結合する
/// ・カウントダウンは1スロットの文章を更新する
/// </summary>
public class CS_UINotificationModel : CS_BaseModel
{
    public const int slotCount = 3;
    public const float displayDuration = 2f;
    public const float killMergeDuration = 2f;

    public event Action<int, CS_NotificationData> OnSlotUpdated;
    public event Action<int> OnSlotCleared;

    private readonly CS_NotificationData[] _slots =
        new CS_NotificationData[slotCount];

    private readonly float[] _slotTimers =
        new float[slotCount];

    private readonly Queue<CS_NotificationData> _waitQueue = new();

    private float _killMergeTimer;
    private int _killSlotIndex = -1;
    private int _countdownSlotIndex = -1;

    public void AddNotification(CS_NotificationData data)
    {
        if (data == null)
            return;

        if (data.type == CSE_NotificationType.Kill &&
            _killSlotIndex >= 0 &&
            _killMergeTimer > 0f &&
            _slots[_killSlotIndex] != null)
        {
            _slots[_killSlotIndex].MergeKill(data);
            _slotTimers[_killSlotIndex] = displayDuration;
            _killMergeTimer = killMergeDuration;

            OnSlotUpdated?.Invoke(
                _killSlotIndex,
                _slots[_killSlotIndex]
            );
            return;
        }

        int emptySlot = FindEmptySlot();

        if (emptySlot >= 0)
            SetSlot(emptySlot, data);
        else
            _waitQueue.Enqueue(data);
    }

    /// <summary>
    /// カウントダウンを表示または更新する。
    /// 表示中なら同じスロットを使い、新しい通知を増やさない。
    /// </summary>
    public void ShowCountdown(string message)
    {
        if (_countdownSlotIndex >= 0 &&
            _slots[_countdownSlotIndex] != null &&
            _slots[_countdownSlotIndex].type == CSE_NotificationType.Countdown)
        {
            _slots[_countdownSlotIndex].SetMessage(message);
            _slotTimers[_countdownSlotIndex] = displayDuration;

            OnSlotUpdated?.Invoke(
                _countdownSlotIndex,
                _slots[_countdownSlotIndex]
            );
            return;
        }

        var countdownData = new CS_NotificationData(
            CSE_NotificationType.Countdown,
            message
        );

        int emptySlot = FindEmptySlot();

        if (emptySlot >= 0)
        {
            SetSlot(emptySlot, countdownData);
            _countdownSlotIndex = emptySlot;
        }
        else
        {
            // カウントダウン開始時に空きがない場合は待機せず、
            // 最も残り時間が少ないスロットを使用する。
            int replaceSlot = FindOldestSlot();
            ClearSlot(replaceSlot);
            SetSlot(replaceSlot, countdownData);
            _countdownSlotIndex = replaceSlot;
        }
    }

    /// <summary>カウントダウン通知を終了する。</summary>
    public void HideCountdown()
    {
        if (_countdownSlotIndex < 0)
            return;

        int slotIndex = _countdownSlotIndex;
        _countdownSlotIndex = -1;
        ClearSlot(slotIndex);

        if (_waitQueue.Count > 0)
            SetSlot(slotIndex, _waitQueue.Dequeue());
    }

    public void Tick(float deltaTime)
    {
        if (_killMergeTimer > 0f)
            _killMergeTimer -= deltaTime;

        for (int i = 0; i < slotCount; i++)
        {
            if (_slots[i] == null)
                continue;

            // カウントダウンはTimerControllerが終了させる。
            if (_slots[i].type == CSE_NotificationType.Countdown)
                continue;

            _slotTimers[i] -= deltaTime;

            if (_slotTimers[i] > 0f)
                continue;

            ClearSlot(i);

            if (_waitQueue.Count > 0)
                SetSlot(i, _waitQueue.Dequeue());
        }
    }

    private void SetSlot(int index, CS_NotificationData data)
    {
        _slots[index] = data;
        _slotTimers[index] = displayDuration;

        if (data.type == CSE_NotificationType.Kill)
        {
            _killSlotIndex = index;
            _killMergeTimer = killMergeDuration;
        }

        if (data.type == CSE_NotificationType.Countdown)
            _countdownSlotIndex = index;

        OnSlotUpdated?.Invoke(index, data);
    }

    private void ClearSlot(int index)
    {
        if (index < 0 || index >= slotCount)
            return;

        _slots[index] = null;
        _slotTimers[index] = 0f;

        if (_killSlotIndex == index)
        {
            _killSlotIndex = -1;
            _killMergeTimer = 0f;
        }

        if (_countdownSlotIndex == index)
            _countdownSlotIndex = -1;

        OnSlotCleared?.Invoke(index);
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (_slots[i] == null)
                return i;
        }

        return -1;
    }

    private int FindOldestSlot()
    {
        int result = 0;
        float shortestTimer = float.MaxValue;

        for (int i = 0; i < slotCount; i++)
        {
            if (_slotTimers[i] < shortestTimer)
            {
                shortestTimer = _slotTimers[i];
                result = i;
            }
        }

        return result;
    }

    public float GetSlotTimer(int index)
    {
        if (index < 0 || index >= slotCount)
            return 0f;

        if (_slots[index] != null &&
            _slots[index].type == CSE_NotificationType.Countdown)
            return displayDuration;

        return _slotTimers[index];
    }

    public override void Dispose()
    {
        OnSlotUpdated = null;
        OnSlotCleared = null;
        _waitQueue.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            _slots[i] = null;
            _slotTimers[i] = 0f;
        }

        _killSlotIndex = -1;
        _countdownSlotIndex = -1;
        _killMergeTimer = 0f;
    }
}
