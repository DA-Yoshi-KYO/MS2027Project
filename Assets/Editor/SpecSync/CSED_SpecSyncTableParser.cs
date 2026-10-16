using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

/*
 * Confluenceのページから表を取り出すクラス
 * ・ParseHtml: REST APIで取った本文(storage形式のHTML)を読む
 * ・ParseText: ブラウザからコピーした表(タブ区切り)やMarkdownの表を読む
 * どちらも「表の直前の見出し」と「行ごとのセルの文字」を返す
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// 読み込んだ表1つ分
public class CSED_SpecSyncTable
{
    private readonly string _heading;        // 表の直前の見出し(貼り付けの場合は、見出しの候補を改行区切りで持つ)
    private readonly List<string[]> _rows;   // 0行目が見出し行

    public string heading => _heading;
    public IReadOnlyList<string[]> rows => _rows;

    public CSED_SpecSyncTable(string heading, List<string[]> rows)
    {
        _heading = heading;
        _rows = rows;
    }
}

public static class CSED_SpecSyncTableParser
{
    // 見出し(h1〜h6)と表を、ページの上から順に拾う
    private static readonly Regex _blockRegex = new Regex(@"<h([1-6])[^>]*>(.*?)</h\1>|<table[^>]*>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex _rowRegex = new Regex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex _cellRegex = new Regex(@"<t[hd](?:\s[^>]*)?>(.*?)</t[hd]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex _breakRegex = new Regex(@"<br\s*/?>|</p>", RegexOptions.IgnoreCase);
    private static readonly Regex _tagRegex = new Regex(@"<[^>]+>");
    private static readonly Regex _spaceRegex = new Regex(@"\s+");
    private static readonly Regex _numberRegex = new Regex(@"-?\d+(?:\.\d+)?");
    private static readonly Regex _separatorCellRegex = new Regex(@"^:?-+:?$");

    public static List<CSED_SpecSyncTable> ParseHtml(string html)
    {
        List<CSED_SpecSyncTable> tables = new List<CSED_SpecSyncTable>();
        string heading = "";

        foreach (Match block in _blockRegex.Matches(html))
        {
            // 見出しなら覚えておき、次の表の名前にする
            if (block.Groups[1].Success)
            {
                heading = ToPlainText(block.Groups[2].Value);
                continue;
            }

            List<string[]> rows = new List<string[]>();
            foreach (Match row in _rowRegex.Matches(block.Groups[3].Value))
            {
                List<string> cells = new List<string>();
                foreach (Match cell in _cellRegex.Matches(row.Groups[1].Value)) cells.Add(ToPlainText(cell.Groups[1].Value));
                if (cells.Count > 0) rows.Add(cells.ToArray());
            }
            if (rows.Count > 0) tables.Add(new CSED_SpecSyncTable(heading, rows));
        }
        return tables;
    }

    public static List<CSED_SpecSyncTable> ParseText(string text)
    {
        List<CSED_SpecSyncTable> tables = new List<CSED_SpecSyncTable>();
        List<string> headingLines = new List<string>();   // 前の表からこの表までにあった文の行(見出し+説明文)
        List<string[]> current = null;

        foreach (string rawLine in text.Replace("\r", "").Split('\n'))
        {
            string[] cells = SplitTableLine(rawLine);
            if (cells != null)
            {
                // Markdownの区切り行(|---|---|)は読み飛ばす
                if (cells.Length == 0) continue;
                if (current == null) current = new List<string[]>();
                current.Add(cells);
                continue;
            }

            // 表ではない行が来たら表の終わり
            if (current != null)
            {
                tables.Add(new CSED_SpecSyncTable(string.Join("\n", headingLines), current));
                headingLines.Clear();
                current = null;
            }

            // コピーだと見出しと説明文の区別がつかないので、文の行は全部見出しの候補として持っておく
            string line = rawLine.Trim();
            if (line.Length > 0) headingLines.Add(line.TrimStart('#').Trim());
        }
        if (current != null) tables.Add(new CSED_SpecSyncTable(string.Join("\n", headingLines), current));
        return tables;
    }

    // 比較用に文字をそろえる(全角→半角、空白と太字の*を消す)
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string normalized = text.Normalize(NormalizationForm.FormKC).Replace("*", "");
        return _spaceRegex.Replace(normalized, "");
    }

    // セルの中の最初の数値を取り出す(例: "10秒"→10, "×0.6"→0.6, "２秒"→2)
    public static bool TryParseNumber(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(text)) return false;

        Match match = _numberRegex.Match(text.Normalize(NormalizationForm.FormKC));
        return match.Success && float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // 表の行ならセルの配列、区切り行なら空配列、表の行でなければnullを返す
    private static string[] SplitTableLine(string line)
    {
        if (line.Contains("\t"))
        {
            string[] tabCells = line.Split('\t');
            for (int i = 0; i < tabCells.Length; i++) tabCells[i] = tabCells[i].Trim();
            return tabCells;
        }

        string trimmed = line.Trim();
        if (!trimmed.StartsWith("|")) return null;

        string[] cells = trimmed.Trim('|').Split('|');
        bool isSeparator = true;
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = cells[i].Replace("**", "").Trim();
            if (!_separatorCellRegex.IsMatch(cells[i])) isSeparator = false;
        }
        return isSeparator ? new string[0] : cells;
    }

    // HTMLのタグを消して普通の文字にする
    private static string ToPlainText(string html)
    {
        string text = _breakRegex.Replace(html, " ");
        text = WebUtility.HtmlDecode(_tagRegex.Replace(text, ""));
        return _spaceRegex.Replace(text, " ").Trim();
    }
}
