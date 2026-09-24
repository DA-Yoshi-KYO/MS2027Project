/* ================================================
 * 　HPの状態を保持
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using R3;
using UnityEngine;

/// <summary>
/// HPの状態を保持
/// </summary>
public class CS_UIHpModel : CS_BaseModel
{
    //現在のHp
    public ReactiveProperty<int> _currentHp { get; } = new ReactiveProperty<int>(100);

    //最大Hp
    public ReactiveProperty<int> _maxHp { get; } = new ReactiveProperty<int>(100);

    //Hpを設定する
    public void SetHp(int hp)
    {
        _currentHp.Value = Mathf.Clamp(hp, 0, _maxHp.Value);
    }
}
