using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/*
 * Confluenceの表とアセットの値を比べ、選んだ差分をアセットへ書き込むクラス
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・比較の流れ
 *   1. 対応表の表ごとに、Confluence側の同じ見出しの表を探す
 *   2. 2行目以降の各行について、行の書き込み先を決める(配列なら上から順に要素0,1,2...)
 *   3. 列ごとにセルの数値 × scale を、SerializedObjectで読んだ今の値と比べる
 * ・書き込みはSerializedObject経由なのでUndoできる。プレハブは書き込み後に保存する
 */
// ========================================

// 比較結果1項目分
public class CSED_SpecSyncEntry
{
    private readonly string _tableHeading;
    private readonly string _rowKey;
    private readonly string _column;
    private readonly string _specText;
    private readonly CSE_SpecSyncStatus _status;
    private readonly string _message;
    private readonly string _assetPath;
    private readonly string _component;
    private readonly string _propertyPath;
    private readonly float _currentValue;
    private readonly float _newValue;

    public string tableHeading => _tableHeading;
    public string rowKey => _rowKey;
    public string column => _column;
    public string specText => _specText;
    public CSE_SpecSyncStatus status => _status;
    public string message => _message;
    public string assetPath => _assetPath;
    public string component => _component;
    public string propertyPath => _propertyPath;
    public float currentValue => _currentValue;
    public float newValue => _newValue;
    public bool selected { get; set; }   // ウィンドウで適用するかどうか

    public CSED_SpecSyncEntry(string tableHeading, string rowKey, string column, string specText, CSE_SpecSyncStatus status, string message,
        string assetPath = null, string component = null, string propertyPath = null, float currentValue = 0f, float newValue = 0f)
    {
        _tableHeading = tableHeading;
        _rowKey = rowKey;
        _column = column;
        _specText = specText;
        _status = status;
        _message = message;
        _assetPath = assetPath;
        _component = component;
        _propertyPath = propertyPath;
        _currentValue = currentValue;
        _newValue = newValue;
        selected = status == CSE_SpecSyncStatus.Changed;
    }

    // 「DB_PlayerStats._maxHp」のような書き込み先の表示名
    public string targetName
    {
        get
        {
            string owner = string.IsNullOrEmpty(_component) ? Path.GetFileNameWithoutExtension(_assetPath) : $"{Path.GetFileNameWithoutExtension(_assetPath)}/{_component}";
            return $"{owner}.{_propertyPath}";
        }
    }
}

public static class CSED_SpecSyncComparer
{
    public static List<CSED_SpecSyncEntry> Compare(CSED_SpecSyncMapping mapping, List<CSED_SpecSyncTable> tables)
    {
        List<CSED_SpecSyncEntry> entries = new List<CSED_SpecSyncEntry>();
        Dictionary<string, SerializedObject> targetCache = new Dictionary<string, SerializedObject>();

        foreach (CSED_SpecSyncTableMapping tableMapping in mapping.tables)
        {
            CSED_SpecSyncTable table = FindTable(tables, tableMapping.heading);
            if (table == null || table.rows.Count < 2)
            {
                entries.Add(new CSED_SpecSyncEntry(tableMapping.heading, "", "", "", CSE_SpecSyncStatus.Error, "Confluenceにこの見出しの表が見つかりません"));
                continue;
            }
            CompareTable(tableMapping, table, entries, targetCache);
        }
        return entries;
    }

    // 選ばれた差分をアセットへ書き込み、書き込んだ件数を返す
    public static int Apply(IEnumerable<CSED_SpecSyncEntry> entries)
    {
        Dictionary<string, SerializedObject> targets = new Dictionary<string, SerializedObject>();
        int count = 0;

        foreach (CSED_SpecSyncEntry entry in entries)
        {
            if (!entry.selected || entry.status != CSE_SpecSyncStatus.Changed) continue;

            SerializedObject target = GetTarget(entry.assetPath, entry.component, targets, out string error);
            SerializedProperty property = target?.FindProperty(entry.propertyPath);
            if (property == null)
            {
                Debug.LogWarning($"仕様データ同期: {entry.targetName} に書き込めませんでした。{error}");
                continue;
            }

            if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Mathf.RoundToInt(entry.newValue);
            else property.floatValue = entry.newValue;
            count++;
        }

        foreach (SerializedObject target in targets.Values)
        {
            if (target == null) continue;
            target.ApplyModifiedProperties();
            EditorUtility.SetDirty(target.targetObject);

            // プレハブの中のコンポーネントはプレハブごと保存する
            if (target.targetObject is Component component && PrefabUtility.IsPartOfPrefabAsset(component))
            {
                PrefabUtility.SavePrefabAsset(component.transform.root.gameObject);
            }
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    private static void CompareTable(CSED_SpecSyncTableMapping tableMapping, CSED_SpecSyncTable table, List<CSED_SpecSyncEntry> entries, Dictionary<string, SerializedObject> targetCache)
    {
        string[] header = table.rows[0];
        CSED_SpecSyncColumnMapping[] columns = new CSED_SpecSyncColumnMapping[header.Length];
        for (int c = 0; c < header.Length; c++) columns[c] = FindColumn(tableMapping, header[c]);

        AddColumnNotes(tableMapping, header, columns, entries);

        for (int r = 1; r < table.rows.Count; r++)
        {
            string[] row = table.rows[r];
            string rowKey = row.Length > 0 ? row[0] : "";
            CSED_SpecSyncRowMapping rowMapping = FindRow(tableMapping, rowKey);

            // 行で書き込み先を分ける表なのに、対応表に無い行
            if (!tableMapping.isArray && tableMapping.rows.Length > 0 && rowMapping == null)
            {
                entries.Add(new CSED_SpecSyncEntry(tableMapping.heading, rowKey, "", "", CSE_SpecSyncStatus.Unmapped, "この行は対応表にありません"));
                continue;
            }

            for (int c = 0; c < header.Length && c < row.Length; c++)
            {
                CSED_SpecSyncColumnMapping column = columns[c];
                if (column == null || string.IsNullOrEmpty(column.property)) continue;

                entries.Add(CompareCell(tableMapping, rowMapping, column, r - 1, rowKey, header[c], row[c], targetCache));
            }
        }
    }

    // 対応表に無い列・同期しない列は、行ごとではなく表ごとに1回だけ出す
    private static void AddColumnNotes(CSED_SpecSyncTableMapping tableMapping, string[] header, CSED_SpecSyncColumnMapping[] columns, List<CSED_SpecSyncEntry> entries)
    {
        for (int c = 0; c < header.Length; c++)
        {
            // 1列目は行の名前なので、対応表に無くても問題ない
            if (columns[c] == null && c == 0) continue;

            if (columns[c] == null)
            {
                entries.Add(new CSED_SpecSyncEntry(tableMapping.heading, "", header[c], "", CSE_SpecSyncStatus.Unmapped, "この列は対応表にありません"));
            }
            else if (string.IsNullOrEmpty(columns[c].property))
            {
                entries.Add(new CSED_SpecSyncEntry(tableMapping.heading, "", header[c], "", CSE_SpecSyncStatus.Skipped, columns[c].note));
            }
        }
    }

    private static CSED_SpecSyncEntry CompareCell(CSED_SpecSyncTableMapping tableMapping, CSED_SpecSyncRowMapping rowMapping, CSED_SpecSyncColumnMapping column,
        int dataIndex, string rowKey, string columnName, string cell, Dictionary<string, SerializedObject> targetCache)
    {
        if (!CSED_SpecSyncTableParser.TryParseNumber(cell, out float specValue))
        {
            return new CSED_SpecSyncEntry(tableMapping.heading, rowKey, columnName, cell, CSE_SpecSyncStatus.NoNumber, "仕様のセルに数値がありません");
        }

        ResolveTarget(tableMapping, rowMapping, column, out string assetPath, out string componentName);
        string propertyPath = tableMapping.isArray ? $"{tableMapping.arrayProperty}.Array.data[{dataIndex}].{column.property}" : column.property;

        SerializedObject target = GetTarget(assetPath, componentName, targetCache, out string error);
        if (target == null)
        {
            return new CSED_SpecSyncEntry(tableMapping.heading, rowKey, columnName, cell, CSE_SpecSyncStatus.Error, error);
        }

        SerializedProperty property = target.FindProperty(propertyPath);
        if (property == null)
        {
            string reason = tableMapping.isArray ? "(配列の要素数が足りない可能性があります)" : "";
            return new CSED_SpecSyncEntry(tableMapping.heading, rowKey, columnName, cell, CSE_SpecSyncStatus.Error, $"{propertyPath} が見つかりません{reason}");
        }

        float currentValue;
        float newValue = specValue * column.scale;
        if (property.propertyType == SerializedPropertyType.Integer)
        {
            currentValue = property.intValue;
            newValue = Mathf.Round(newValue);
        }
        else if (property.propertyType == SerializedPropertyType.Float)
        {
            currentValue = property.floatValue;
        }
        else
        {
            return new CSED_SpecSyncEntry(tableMapping.heading, rowKey, columnName, cell, CSE_SpecSyncStatus.Error, $"{propertyPath} は数値の項目ではありません");
        }

        CSE_SpecSyncStatus status = Mathf.Approximately(currentValue, newValue) ? CSE_SpecSyncStatus.Same : CSE_SpecSyncStatus.Changed;
        return new CSED_SpecSyncEntry(tableMapping.heading, rowKey, columnName, cell, status, "",
            assetPath, componentName, propertyPath, currentValue, newValue);
    }

    // 書き込み先は「列 → 行 → 表」の順に、書いてある所を使う
    private static void ResolveTarget(CSED_SpecSyncTableMapping tableMapping, CSED_SpecSyncRowMapping rowMapping, CSED_SpecSyncColumnMapping column,
        out string assetPath, out string componentName)
    {
        if (!string.IsNullOrEmpty(column.assetPath))
        {
            assetPath = column.assetPath;
            componentName = column.component;
        }
        else if (rowMapping != null && !string.IsNullOrEmpty(rowMapping.assetPath))
        {
            assetPath = rowMapping.assetPath;
            componentName = rowMapping.component;
        }
        else
        {
            assetPath = tableMapping.assetPath;
            componentName = tableMapping.component;
        }
    }

    // アセット(またはプレハブ内のコンポーネント)のSerializedObjectを取る。同じ物は使い回す
    private static SerializedObject GetTarget(string assetPath, string componentName, Dictionary<string, SerializedObject> cache, out string error)
    {
        error = null;
        string key = assetPath + "|" + componentName;
        if (cache.TryGetValue(key, out SerializedObject cached)) return cached;

        SerializedObject target = LoadTarget(assetPath, componentName, out error);
        if (target != null) cache[key] = target;
        return target;
    }

    private static SerializedObject LoadTarget(string assetPath, string componentName, out string error)
    {
        error = null;
        Object asset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null)
        {
            error = $"アセットが見つかりません: {assetPath}";
            return null;
        }
        if (string.IsNullOrEmpty(componentName)) return new SerializedObject(asset);

        if (asset is GameObject prefab)
        {
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == componentName) return new SerializedObject(component);
            }
        }
        error = $"{Path.GetFileName(assetPath)} に {componentName} がありません";
        return null;
    }

    private static CSED_SpecSyncTable FindTable(List<CSED_SpecSyncTable> tables, string heading)
    {
        string key = CSED_SpecSyncTableParser.Normalize(heading);
        CSED_SpecSyncTable partial = null;
        foreach (CSED_SpecSyncTable table in tables)
        {
            // 見出しの候補(貼り付けの場合は複数行)のどれかと一致すればその表。一致が無ければ部分一致の表を使う
            foreach (string line in table.heading.Split('\n'))
            {
                string tableHeading = CSED_SpecSyncTableParser.Normalize(line);
                if (tableHeading == key) return table;
                if (partial == null && tableHeading.Contains(key)) partial = table;
            }
        }
        return partial;
    }

    private static CSED_SpecSyncRowMapping FindRow(CSED_SpecSyncTableMapping tableMapping, string rowKey)
    {
        string key = CSED_SpecSyncTableParser.Normalize(rowKey);
        foreach (CSED_SpecSyncRowMapping row in tableMapping.rows)
        {
            if (CSED_SpecSyncTableParser.Normalize(row.rowKey) == key) return row;
        }
        return null;
    }

    private static CSED_SpecSyncColumnMapping FindColumn(CSED_SpecSyncTableMapping tableMapping, string headerText)
    {
        string key = CSED_SpecSyncTableParser.Normalize(headerText);
        foreach (CSED_SpecSyncColumnMapping column in tableMapping.columns)
        {
            if (CSED_SpecSyncTableParser.Normalize(column.column) == key) return column;
        }
        return null;
    }
}
