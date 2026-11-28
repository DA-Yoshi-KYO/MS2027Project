/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-30 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マルチ用：1プレイヤー分の結果表示 View
/// ・PlayerResultPrefab にアタッチする
/// ・順位(画像)・キャラのアイコン・称号(画像・複数)・各回数を表示する
/// ・点数は画面に出さない(順位の決定にだけ使う)
/// ・各回数の TMP に CS_UICountUpText を付けると、カウントアップで登場する
/// </summary>
public class CS_UIPlayerResultItemView : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("画像")]
    [SerializeField] private Image _rankImage;        // 順位(1〜4位)
    [SerializeField] private Image _characterImage;   // 使ったキャラのアイコン

    [Tooltip("称号の枠。称号は最大4つ(ダークヒーロー + 3つ)。先頭の枠から順に入る。使わない枠は隠れる")]
    [SerializeField] private Image[] _titleImages;    // 称号(複数)

    [Header("文字(任意)")]
    [SerializeField] private TextMeshProUGUI _playerNumberText;

    [Header("各回数")]
    [SerializeField] private TextMeshProUGUI _foundByPoliceText;   // 警察に見つかった数
    [SerializeField] private TextMeshProUGUI _surpriseAttackText;  // 奇襲の成功回数
    [SerializeField] private TextMeshProUGUI _saUseText;           // SAの使用回数

    // =========================================================
    // 描画（CS_UIResultView から呼ばれる）
    // =========================================================

    /// <summary>
    /// 1プレイヤー分の結果を表示する
    /// rank はリストの順番（Model でソート済み。同点は同順位）から渡される
    /// titleSettings は称号の条件の回数(null なら初期値で判定する)
    /// </summary>
    public void UpdateView(
        CS_ResultData data,
        int rank,
        CSO_ResultSpriteTable spriteTable,
        CSO_ResultTitleSettings titleSettings)
    {
        if (data == null) return;

        var s = data.score;

        // 順位(画像)
        CS_ResultImageUtility.SetSprite(
            _rankImage,
            spriteTable != null
                ? spriteTable.GetPlacementSprite(rank)
                : null
        );

        // キャラのアイコン
        CS_ResultImageUtility.SetSprite(
            _characterImage,
            spriteTable != null
                ? spriteTable.GetCharacterSprite(data.characterType)
                : null
        );

        // 称号(複数。条件を満たしたものを全部)
        CS_ResultImageUtility.SetTitleSprites(
            _titleImages,
            data.GetResultTitles(titleSettings),
            spriteTable
        );

        // プレイヤー番号
        if (_playerNumberText != null)
            _playerNumberText.text = $"Player{data.playerNumber}";

        // 各回数。カウントアップの準備(部品が付いていなければ、すぐ表示する)
        CS_ResultNumberUtility.SetCount(_foundByPoliceText, s.foundByPoliceCount, "回");
        CS_ResultNumberUtility.SetCount(_surpriseAttackText, s.surpriseAttackCount, "回");
        CS_ResultNumberUtility.SetCount(_saUseText, s.saUseCount, "回");
    }

    // =========================================================
    // カウントアップ
    // =========================================================

    /// <summary>
    /// 各回数のカウントアップする数字を、段ごとの一覧に足す。
    /// 変更: 段ごとに分けて渡す。始めるのは CS_UIResultView が、全員分をまとめて、段の順番に行う。
    ///   found    : 警察に見つかった回数(1段目)
    ///   surprise : 奇襲の成功回数(2段目)
    ///   sa       : SAの使用回数(3段目)
    /// </summary>
    public void CollectCountUps(
        List<CS_UICountUpText> found,
        List<CS_UICountUpText> surprise,
        List<CS_UICountUpText> sa)
    {
        AddIfExists(found, _foundByPoliceText);
        AddIfExists(surprise, _surpriseAttackText);
        AddIfExists(sa, _saUseText);
    }

    private static void AddIfExists(
        List<CS_UICountUpText> list,
        TMP_Text text)
    {
        CS_UICountUpText countUp = CS_ResultNumberUtility.GetCountUp(text);

        if (countUp != null)
        {
            list.Add(countUp);
        }
    }
}
