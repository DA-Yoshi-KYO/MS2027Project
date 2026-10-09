using System;
using System.IO;
using UnityEngine;

/*
 * 仕様データ同期ツールの対応表
 * Confluenceのデータ表の「どの表・どの行・どの列」を「どのアセットのどの項目」に入れるかを持つ
 * 中身は SpecSyncMapping.json に書き、JsonUtilityで読み込む(キー名は変数名と同じ)
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・書き込み先は「列 → 行 → 表」の順に、_assetPath が書いてある所を使う
 *   (プレイヤーの信号間隔のように、1つの表の中で一部の列だけ別アセットに入れたい時は列に書く)
 * ・_component を書くと、プレハブの中からその名前のコンポーネントを探して書き込む
 * ・_arrayProperty を書いた表は、データ行の上から順に配列の0番目,1番目...へ入れる(手配度など)
 * ・_scale は「アセットの値 = 仕様の数値 × _scale」。倍率で書かれている移動速度などの換算に使う
 * ・_property を空にした列は、_note を表示して同期しない(意図的に仕様と違う値にしている項目など)
 */
// ========================================

[Serializable]
public class CSED_SpecSyncMapping
{
    [SerializeField] private string _site;     // 例: xxx.atlassian.net
    [SerializeField] private string _pageId;   // データ表ページのID
    [SerializeField] private CSED_SpecSyncTableMapping[] _tables = new CSED_SpecSyncTableMapping[0];

    public string site => _site;
    public string pageId => _pageId;
    public CSED_SpecSyncTableMapping[] tables => _tables;

    public const string mappingPath = "Assets/Editor/SpecSync/SpecSyncMapping.json";

    // 対応表を読み込む(失敗時はnullとエラー文を返す)
    public static CSED_SpecSyncMapping Load(out string error)
    {
        error = null;
        if (!File.Exists(mappingPath))
        {
            error = "対応表が見つかりません: " + mappingPath;
            return null;
        }

        try
        {
            CSED_SpecSyncMapping mapping = JsonUtility.FromJson<CSED_SpecSyncMapping>(File.ReadAllText(mappingPath));
            if (mapping == null || mapping._tables == null) error = "対応表の中身が空です: " + mappingPath;
            return error == null ? mapping : null;
        }
        catch (Exception e)
        {
            error = "対応表のJSONが読めません: " + e.Message;
            return null;
        }
    }
}

// 表1つ分の対応
[Serializable]
public class CSED_SpecSyncTableMapping
{
    [SerializeField] private string _heading;         // 表の直前の見出し(例: プレイヤーデータ)
    [SerializeField] private string _assetPath;       // 表全体の書き込み先
    [SerializeField] private string _component;       // プレハブの場合のコンポーネント名
    [SerializeField] private string _arrayProperty;   // 行を配列の要素に対応させる場合の配列名
    [SerializeField] private CSED_SpecSyncRowMapping[] _rows = new CSED_SpecSyncRowMapping[0];
    [SerializeField] private CSED_SpecSyncColumnMapping[] _columns = new CSED_SpecSyncColumnMapping[0];

    public string heading => _heading;
    public string assetPath => _assetPath;
    public string component => _component;
    public string arrayProperty => _arrayProperty;
    public CSED_SpecSyncRowMapping[] rows => _rows ?? new CSED_SpecSyncRowMapping[0];
    public CSED_SpecSyncColumnMapping[] columns => _columns ?? new CSED_SpecSyncColumnMapping[0];
    public bool isArray => !string.IsNullOrEmpty(_arrayProperty);
}

// 行1つ分の対応(1列目の値で行を見分ける)
[Serializable]
public class CSED_SpecSyncRowMapping
{
    [SerializeField] private string _rowKey;      // 1列目の値(例: A)
    [SerializeField] private string _assetPath;   // この行の書き込み先(空なら表の書き込み先)
    [SerializeField] private string _component;

    public string rowKey => _rowKey;
    public string assetPath => _assetPath;
    public string component => _component;
}

// 列1つ分の対応
[Serializable]
public class CSED_SpecSyncColumnMapping
{
    [SerializeField] private string _column;      // 見出し行の文字(空白・全角半角の違いは無視して比べる)
    [SerializeField] private string _property;    // 書き込む変数名(空なら同期しない)
    [SerializeField] private float _scale = 1f;   // アセットの値 = 仕様の数値 × scale
    [SerializeField] private string _assetPath;   // この列だけ別の書き込み先にする場合
    [SerializeField] private string _component;
    [SerializeField] private string _note;        // 同期しない理由など

    public string column => _column;
    public string property => _property;
    public float scale => _scale > 0f ? _scale : 1f;   // 書き忘れ(0)は等倍として扱う
    public string assetPath => _assetPath;
    public string component => _component;
    public string note => _note;
}
