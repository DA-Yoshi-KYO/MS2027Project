/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// リザルトで使う画像をまとめて持つ ScriptableObject(ソロ・マルチで共通)
/// ・総合ランク(S / A / B / C)の画像
/// ・エリアごとのランク(S / A / B / C)の画像 … 総合ランクとは別の画像セット
/// ・順位(1〜4位)の画像
/// ・称号の画像
/// ・キャラのアイコン
/// ・画像は Inspector で割り当てる。無い(空の)画像は、表示しない
/// ・Project 右クリック > Create > Result > Result Sprite Table
///   → DB_ResultSpriteTable ができる。CS_UIResultView の _spriteTable に割り当てる
/// </summary>
[CreateAssetMenu(menuName = "Result/Result Sprite Table", fileName = "DB_ResultSpriteTable")]
public sealed class CSO_ResultSpriteTable : ScriptableObject
{
    // =========================================================
    // 画像1つ分の設定
    // =========================================================

    [Serializable]
    public sealed class RankSprite
    {
        [SerializeField]
        private CSE_ResultRank _rank;

        [SerializeField]
        private Sprite _sprite;

        public CSE_ResultRank rank => _rank;
        public Sprite sprite => _sprite;
    }

    [Serializable]
    public sealed class TitleSprite
    {
        [SerializeField]
        private CSE_ResultTitle _title;

        [SerializeField]
        private Sprite _sprite;

        public CSE_ResultTitle title => _title;
        public Sprite sprite => _sprite;
    }

    [Serializable]
    public sealed class CharacterSprite
    {
        [SerializeField]
        private CSE_CharacterType _characterType;

        [SerializeField]
        private Sprite _sprite;

        public CSE_CharacterType characterType => _characterType;
        public Sprite sprite => _sprite;
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("総合ランクの画像(S / A / B / C)")]
    [SerializeField]
    private RankSprite[] _overallRankSprites;

    // 以前は _rankSprites という名前だった(1種類だけ持っていた)。
    // FormerlySerializedAs で、すでに入れてある画像は、こちら(エリアごと)に引き継がれる
    [Header("エリアごとのランクの画像(S / A / B / C)。総合ランクとは別の画像")]
    [FormerlySerializedAs("_rankSprites")]
    [SerializeField]
    private RankSprite[] _areaRankSprites;

    [Header("順位の画像(1つ目が1位、2つ目が2位…の順に入れる)")]
    [SerializeField]
    private Sprite[] _placementSprites;

    [Header("称号の画像(4種類: ダークヒーロー / 警察いらず / ワンショット / ステルス)")]
    [SerializeField]
    private TitleSprite[] _titleSprites;

    [Header("キャラのアイコン")]
    [SerializeField]
    private CharacterSprite[] _characterSprites;

    // =========================================================
    // 取得(無い画像は null を返す)
    // =========================================================

    /// <summary>総合ランクの画像を返す</summary>
    public Sprite GetOverallRankSprite(CSE_ResultRank rank)
    {
        return FindRankSprite(
            _overallRankSprites,
            rank,
            "総合ランク"
        );
    }

    /// <summary>エリアごとのランクの画像を返す</summary>
    public Sprite GetAreaRankSprite(CSE_ResultRank rank)
    {
        return FindRankSprite(
            _areaRankSprites,
            rank,
            "エリアごとのランク"
        );
    }

    /// <summary>
    /// ランクの画像を、指定した一覧から探す。
    /// 見つからなければ警告を出して null を返す(label はどちらの一覧かを警告に出すための名前)。
    /// </summary>
    private Sprite FindRankSprite(
        RankSprite[] sprites,
        CSE_ResultRank rank,
        string label)
    {
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                RankSprite entry = sprites[i];

                if (entry != null &&
                    entry.rank == rank)
                {
                    return entry.sprite;
                }
            }
        }

        Debug.LogWarning(
            $"{nameof(CSO_ResultSpriteTable)}: {label}の画像がありません: {rank}",
            this
        );

        return null;
    }

    /// <summary>順位の画像を返す。placement は1位=1。範囲外ならnull。</summary>
    public Sprite GetPlacementSprite(int placement)
    {
        int index = placement - 1;

        if (_placementSprites != null &&
            index >= 0 &&
            index < _placementSprites.Length)
        {
            return _placementSprites[index];
        }

        Debug.LogWarning(
            $"{nameof(CSO_ResultSpriteTable)}: 順位の画像がありません: {placement}位",
            this
        );

        return null;
    }

    public Sprite GetTitleSprite(CSE_ResultTitle title)
    {
        if (_titleSprites != null)
        {
            for (int i = 0; i < _titleSprites.Length; i++)
            {
                TitleSprite entry = _titleSprites[i];

                if (entry != null &&
                    entry.title == title)
                {
                    return entry.sprite;
                }
            }
        }

        // 称号なしは「画像を出さない」が普通なので、警告は出さない
        if (title != CSE_ResultTitle.None)
        {
            Debug.LogWarning(
                $"{nameof(CSO_ResultSpriteTable)}: 称号の画像がありません: {title}",
                this
            );
        }

        return null;
    }

    public Sprite GetCharacterSprite(CSE_CharacterType characterType)
    {
        if (_characterSprites != null)
        {
            for (int i = 0; i < _characterSprites.Length; i++)
            {
                CharacterSprite entry = _characterSprites[i];

                if (entry != null &&
                    entry.characterType == characterType)
                {
                    return entry.sprite;
                }
            }
        }

        Debug.LogWarning(
            $"{nameof(CSO_ResultSpriteTable)}: キャラのアイコンがありません: {characterType}",
            this
        );

        return null;
    }
}
