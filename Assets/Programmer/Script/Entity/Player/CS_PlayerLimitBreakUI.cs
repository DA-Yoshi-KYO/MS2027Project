using UnityEngine;

/*
 * プレイヤーの暗躍ボーナス(CS_PlayerStealthBonus)を、暗躍ボーナスUI(LIMITBREAK。CS_UILimitBreakModel)へ渡すクラス
 * CS_PlayerStealthBonusのスコア倍率を%にして、UI側のModelに渡すだけ
 * UI側(Model/Presenter/View)はこのクラス・CS_Playerの存在を一切知らない
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・仕様はUI仕様書「LIMITBREAK(暗躍ボーナス)」(Issue #369)。ボーナスが付いたら表示し、20%刻みで上がる
 *   1秒ほどかけてカウントアップする演出はUI側(Presenter/View)が行う
 * ・渡す%は、スコアの倍率の上乗せ分((scoreMultiplier - 1) × 100)。5人目で20%、10人目以降で120%
 *   (仕様の「20%刻み」はスコアの倍率と同じ刻み。ステータスの倍率は10%刻みなので使わない)
 *   途切れた時は0%を渡す(UI側で0%まで下がってから消える)
 * ・暗躍ボーナスUIは自分の画面に1つだけのため、自分(isLocalPlayer)のプレイヤーだけがModelを作ってBindする
 *   isLocalPlayerはスポーン後に決まるため、毎フレーム確かめて、自分のプレイヤーになった時にBindする
 * ・連続撃破数はNetworkVariableで同期されていて、onStreakChangedは全クライアントで発生する
 * ・CS_PlayerStealthBonusのロジックはこのクラスを知らない(外しても暗躍ボーナスは動く)
 * ・オフライン(NetworkManagerが動いていない)のテストシーンでも単体で動く
 */
// ========================================

[RequireComponent(typeof(CS_Player))]
[RequireComponent(typeof(CS_PlayerStealthBonus))]
public class CS_PlayerLimitBreakUI : MonoBehaviour
{
    private CS_Player _player;
    private CS_PlayerStealthBonus _stealthBonus;
    private CS_UILimitBreakModel _limitBreakModel;

    private void Awake()
    {
        _player = GetComponent<CS_Player>();
        _stealthBonus = GetComponent<CS_PlayerStealthBonus>();
    }

    private void OnEnable()
    {
        _stealthBonus.onStreakChanged += HandleStreakChanged;
    }

    private void OnDisable()
    {
        _stealthBonus.onStreakChanged -= HandleStreakChanged;
        UnbindLimitBreakUI();
    }

    private void LateUpdate()
    {
        if (!_player.isLocalPlayer)
        {
            UnbindLimitBreakUI();   // NPC・他人のプレイヤーは表示しない
            return;
        }

        if (_limitBreakModel == null) BindLimitBreakUI();
    }

    // 連続撃破数が変わったら、今の%をUIに渡す
    private void HandleStreakChanged(int streak)
    {
        ApplyBonusPercent();
    }

    // 暗躍ボーナスUIのModelを作って公開する
    private void BindLimitBreakUI()
    {
        _limitBreakModel = new CS_UILimitBreakModel();
        ApplyBonusPercent();            // Bind前に今の値を入れておく(Bind時はアニメーションせずにすぐ出る)
        _limitBreakModel.Bind();
    }

    private void UnbindLimitBreakUI()
    {
        if (_limitBreakModel == null) return;

        _limitBreakModel.Dispose();
        _limitBreakModel = null;
    }

    // スコアの倍率の上乗せ分を%にして渡す(×1.2なら20%)
    private void ApplyBonusPercent()
    {
        if (_limitBreakModel == null) return;

        int percent = Mathf.RoundToInt((_stealthBonus.scoreMultiplier - 1f) * 100f);
        _limitBreakModel.SetBonusPercent(percent);
    }
}
