/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// リザルトの画像表示用の小さな共通処理
/// ・SetSprite       : Image に Sprite を設定する。Sprite が無い(null)時は、Image を非表示にする
/// ・SetTitleSprites : 称号の画像を、複数の枠(Image)に順番に入れる。使わない枠は隠す
/// ・SetRankSprite   : ランクの画像を設定する。CS_UIRankStamp が付いていれば、登場まで見えない状態で待たせる
/// </summary>
public static class CS_ResultImageUtility
{
    public static void SetSprite(
        Image image,
        Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    /// <summary>
    /// ランクの画像を設定する(総合ランク・エリアごとのランク用)。
    /// その Image に CS_UIRankStamp が付いていれば、登場の順番が来るまで、見えない状態で待たせる。
    /// 付いていなければ、SetSprite と同じ(すぐ表示する)。
    /// </summary>
    public static void SetRankSprite(
        Image image,
        Sprite sprite)
    {
        SetSprite(image, sprite);

        if (image == null)
        {
            return;
        }

        CS_UIRankStamp stamp = image.GetComponent<CS_UIRankStamp>();

        if (stamp != null)
        {
            stamp.PrepareHidden();
        }
    }

    /// <summary>
    /// 称号の画像を、枠(slots)に先頭から順に入れる。
    /// ・称号の数より枠が多い時、余った枠は GameObject ごと隠す
    ///   (Layout Group を使っていれば、隠した枠の分は詰められる)
    /// ・画像が無い称号の枠も隠す
    /// ・枠が称号の数より少ない時は、入りきらない分を警告に出す
    /// </summary>
    public static void SetTitleSprites(
        Image[] slots,
        IReadOnlyList<CSE_ResultTitle> titles,
        CSO_ResultSpriteTable spriteTable)
    {
        if (slots == null)
        {
            return;
        }

        int titleCount =
            titles != null
                ? titles.Count
                : 0;

        if (titleCount > slots.Length)
        {
            Debug.LogWarning(
                $"{nameof(CS_ResultImageUtility)}: " +
                $"称号の枠が足りません。称号: {titleCount}個、枠: {slots.Length}個"
            );
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Image slot = slots[i];

            if (slot == null)
            {
                continue;
            }

            Sprite sprite = null;

            if (i < titleCount &&
                spriteTable != null)
            {
                sprite = spriteTable.GetTitleSprite(titles[i]);
            }

            slot.sprite = sprite;
            slot.enabled = true;
            slot.gameObject.SetActive(sprite != null);
        }
    }
}
