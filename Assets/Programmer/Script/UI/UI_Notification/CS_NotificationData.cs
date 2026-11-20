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
/// 通知の種類
/// </summary>
public enum CSE_NotificationType
{
    Event,
    TimeAlert,
    Kill,
    Countdown,
}

/// <summary>
/// 通知データ1件分
/// ・イベント、残り時間、カウントダウンは文章を保持する
/// ・キル通知は倒されたプレイヤー名を保持し、表示時に文章を生成する
/// </summary>
public class CS_NotificationData
{
    private readonly CSE_NotificationType _type;
    private string _message;
    private readonly List<string> _killedPlayerNames = new();

    // 追加: 時間通知で使う帯画像(未指定ならnull)
    private Sprite _backSprite;

    public CSE_NotificationType type => _type;

    /// <summary>追加: 時間通知で指定された帯画像。未指定ならnull</summary>
    public Sprite backSprite => _backSprite;

    public string message
    {
        get
        {
            if (_type == CSE_NotificationType.Kill)
                return $"{string.Join("・", _killedPlayerNames)}が倒された";

            return _message;
        }
    }

    public CS_NotificationData(
        CSE_NotificationType type,
        string message)
    {
        _type = type;
        _message = message;
    }

    /// <summary>カウントダウン表示の文章を更新する。</summary>
    public void SetMessage(string message)
    {
        _message = message;
    }

    /// <summary>
    /// 追加: 時間通知を作る。帯画像を指定すると、その画像で表示される。
    /// nullなら従来のTimeAlert用の画像になる。
    /// </summary>
    public static CS_NotificationData CreateTimeAlert(
        string message,
        Sprite backSprite)
    {
        var data = new CS_NotificationData(
            CSE_NotificationType.TimeAlert,
            message
        );

        data._backSprite = backSprite;
        return data;
    }

    public static CS_NotificationData CreateKill(string playerName)
    {
        var data = new CS_NotificationData(
            CSE_NotificationType.Kill,
            string.Empty
        );

        data.AddKilledPlayer(playerName);
        return data;
    }

    public void AddKilledPlayer(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return;

        if (_killedPlayerNames.Contains(playerName))
            return;

        _killedPlayerNames.Add(playerName);
    }

    public void MergeKill(CS_NotificationData other)
    {
        if (other == null ||
            other.type != CSE_NotificationType.Kill)
            return;

        foreach (string playerName in other._killedPlayerNames)
            AddKilledPlayer(playerName);
    }
}