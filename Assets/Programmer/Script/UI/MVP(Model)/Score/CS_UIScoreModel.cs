/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーごとのスコア状態を保持
/// Bind(番号) で「何番のプレイヤーのスコアか」を公開し、UI側がそれを拾って表示する
/// ※ ModelはUIのクラスを一切参照しない(UI → Model の一方向)
/// </summary>
public class CS_UIScoreModel : CS_BaseModel
{
    // ---------------- 番号付きで公開されたModelの一覧 ----------------

    private static readonly Dictionary<int, CS_UIScoreModel> _boundModels = new();

    //Bindされた時の通知(番号, Model)
    public static event Action<int, CS_UIScoreModel> OnBound;

    //Bindが外れた時の通知(番号)
    public static event Action<int> OnUnbound;

    //指定番号のModelを取得する(UI側が後から生成された場合に使う)
    public static bool TryGet(int playerNumber, out CS_UIScoreModel model)
        => _boundModels.TryGetValue(playerNumber, out model);

    //Domain Reload対策
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _boundModels.Clear();
        OnBound = null;
        OnUnbound = null;
    }

    // ---------------- 状態 ----------------

    private readonly ReactiveProperty<int> _score;
    private int _playerNumber = -1;   //-1 = 未Bind

    //外部からは読み取り専用
    public ReadOnlyReactiveProperty<int> score => _score;

    public CS_UIScoreModel(int initialScore = 0)
    {
        _score = new ReactiveProperty<int>(initialScore);
    }

    // ---------------- 公開 ----------------

    //このModelを何番のプレイヤーのスコアとして公開するか
    public void Bind(int playerNumber)
    {
        Unbind();

        _playerNumber = playerNumber;
        _boundModels[playerNumber] = this;
        OnBound?.Invoke(playerNumber, this);
    }

    //公開をやめる
    public void Unbind()
    {
        if (_playerNumber < 0) return;

        //別のModelで上書きされていなければ外す
        if (_boundModels.TryGetValue(_playerNumber, out var current) && current == this)
        {
            _boundModels.Remove(_playerNumber);
            OnUnbound?.Invoke(_playerNumber);
        }
        _playerNumber = -1;
    }

    // ---------------- 値の変更 ----------------

    //スコアを加算する
    public void AddScore(int value)
    {
        _score.Value += value;
    }

    //スコアを設定する
    public void SetScore(int value)
    {
        _score.Value = value;
    }

    public override void Dispose()
    {
        Unbind();
        _score.Dispose();
    }
}

