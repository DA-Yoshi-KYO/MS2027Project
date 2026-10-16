using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/*
 * ステータス調整ウィンドウに出す項目名と単位をまとめたクラス
 * 項目名と単位はConfluence(仕様書)の書き方にそろえる
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// ========================================
/*
 * メモ
 * ・項目名は次の順で決める
 *   1. 仕様データ同期ツールの対応表(SpecSyncMapping.json)にある項目は、Confluenceの列名
 *   2. Confluenceに無い項目は、下の _extraLabels の日本語名
 *   3. どちらにも無い項目は、Unityの表示名(項目を足しても表示が抜けないようにするため)
 * ・Confluenceと単位が違う項目(移動速度は5倍、時間は分と秒など)は、対応表の _scale を使い、
 *   ウィンドウではConfluenceの単位で表示・入力して、データには「仕様の数値 × scale」を入れる
 * ・対応表を直せば、仕様データ同期ツールとこのウィンドウの両方の表示が変わる
 */
// ========================================

// 項目1つ分の表示名と単位
public struct CSED_LevelDesignLabel
{
    public string text;    // 表示名
    public float scale;    // データの値 = 表示する値 × scale

    public CSED_LevelDesignLabel(string text, float scale)
    {
        this.text = text;
        this.scale = scale;
    }

    public float ToDisplay(float value) => value / scale;
    public float ToData(float value) => value * scale;
}

public static class CSED_LevelDesignLabels
{
    // Confluenceに無い項目の日本語名(データの型名 → 変数名 → 表示名)
    private static readonly Dictionary<string, Dictionary<string, string>> _extraLabels = new Dictionary<string, Dictionary<string, string>>
    {
        { nameof(CSO_PlayerStats), new Dictionary<string, string>
            {
                { "_maxHp", "HP" }, { "_attackPower", "攻撃力" }, { "_moveSpeed", "通常移動速度" }, { "_jumpPower", "ジャンプ力" },
                { "_dashSpeed", "ダッシュ時速度" }, { "_sprintSpeed", "ダッシュ継続速度" }, { "_maxGauge", "必殺技ゲージ" }, { "_specialAttackPower", "必殺技威力" },
            }
        },
        { nameof(CSO_VillainTimeScaling), new Dictionary<string, string> { { "_stages", "時間経過による変化" }, { "_attackPower", "攻撃力" } } },
        { nameof(CSO_PoliceStatus), new Dictionary<string, string> { { "_viewAngle", "視野角" }, { "_attackPower", "攻撃力" } } },
        { nameof(CSO_PoliceWantedLevelData), new Dictionary<string, string> { { "_levels", "手配度" } } },
        { nameof(CSO_AttackData), new Dictionary<string, string>
            {
                { "_damage", "威力" }, { "_hitDelay", "判定が出るまでの時間(秒)" }, { "_duration", "攻撃モーションの長さ(秒)" },
                { "_comboWindow", "次の段への受付時間(秒)" }, { "_hitRange", "判定の距離" }, { "_hitRadius", "判定の半径" },
                { "_gaugeGain", "必殺技ゲージの増加量" }, { "_cameraShakeForce", "画面の揺れの強さ" },
            }
        },
    };

    // Confluenceに無いデータの名前(アセット名 → 表示名)
    private static readonly Dictionary<string, string> _extraTitles = new Dictionary<string, string>
    {
        { "DB_PlayerAttack1", "通常攻撃 1段目" }, { "DB_PlayerAttack2", "通常攻撃 2段目" }, { "DB_PlayerAttack3", "通常攻撃 3段目" },
        { "DB_PlayerSpecial", "必殺技" }, { "DB_VillainAttack", "悪人の攻撃" },
    };

    // 対応表から作った「アセットのパス|変数名 → Confluenceの列名と単位」と「アセットのパス → 表の見出し(+行)」
    private static Dictionary<string, CSED_LevelDesignLabel> _specLabels;
    private static Dictionary<string, string> _specTitles;

    private static readonly Regex _arrayElement = new Regex(@"\.Array\.data\[(\d+)\]$");

    // 対応表を読み直す(対応表を書き換えた時用)
    public static void Reload()
    {
        _specLabels = null;
        _specTitles = null;
    }

    // データ(アセット)の表示名。例: 「悪人データ A」。見つからなければアセット名
    public static string GetTitle(ScriptableObject asset)
    {
        GetSpecLabels();
        if (_specTitles.TryGetValue(AssetDatabase.GetAssetPath(asset), out string title)) return title;
        return _extraTitles.TryGetValue(asset.name, out title) ? title : asset.name;
    }

    // データの項目の表示名と単位
    public static CSED_LevelDesignLabel Get(ScriptableObject asset, SerializedProperty property)
    {
        // 配列の要素(「Element 0」)は「手配度 1」のように親の名前 + 番号にする
        Match element = _arrayElement.Match(property.propertyPath);
        if (element.Success)
        {
            string arrayName = property.propertyPath.Substring(0, element.Index);
            string parent = Get(asset, LastName(arrayName)).text;
            return new CSED_LevelDesignLabel($"{parent} {int.Parse(element.Groups[1].Value) + 1}", 1f);
        }

        CSED_LevelDesignLabel label = Get(asset, property.name);
        return label.text != null ? label : new CSED_LevelDesignLabel(property.displayName, 1f);
    }

    // プレイヤーの項目(DB_PlayerStatsの変数名)の表示名と単位
    public static CSED_LevelDesignLabel GetPlayer(CSO_PlayerStats baseStats, string propertyName, string fallback)
    {
        CSED_LevelDesignLabel label = baseStats != null ? Get(baseStats, propertyName) : Get(nameof(CSO_PlayerStats), null, propertyName);
        return label.text != null ? label : new CSED_LevelDesignLabel(fallback, 1f);
    }

    private static CSED_LevelDesignLabel Get(ScriptableObject asset, string propertyName)
    {
        return Get(asset.GetType().Name, AssetDatabase.GetAssetPath(asset), propertyName);
    }

    private static CSED_LevelDesignLabel Get(string typeName, string assetPath, string propertyName)
    {
        if (assetPath != null && GetSpecLabels().TryGetValue(assetPath + "|" + propertyName, out CSED_LevelDesignLabel spec)) return spec;
        if (_extraLabels.TryGetValue(typeName, out Dictionary<string, string> extra) && extra.TryGetValue(propertyName, out string text))
        {
            return new CSED_LevelDesignLabel(text, 1f);
        }
        return new CSED_LevelDesignLabel(null, 1f);
    }

    private static Dictionary<string, CSED_LevelDesignLabel> GetSpecLabels()
    {
        if (_specLabels != null) return _specLabels;

        _specLabels = new Dictionary<string, CSED_LevelDesignLabel>();
        _specTitles = new Dictionary<string, string>();
        CSED_SpecSyncMapping mapping = CSED_SpecSyncMapping.Load(out string error);
        if (mapping == null)
        {
            Debug.LogWarning("レベルデザイン: 仕様データ同期の対応表が読めないため、項目名は日本語名かUnityの表示名になります: " + error);
            return _specLabels;
        }

        foreach (CSED_SpecSyncTableMapping table in mapping.tables)
        {
            if (!string.IsNullOrEmpty(table.assetPath) && string.IsNullOrEmpty(table.component)) _specTitles[table.assetPath] = table.heading;
            foreach (CSED_SpecSyncRowMapping row in table.rows)
            {
                if (!string.IsNullOrEmpty(row.assetPath)) _specTitles[row.assetPath] = $"{table.heading} {row.rowKey}";
            }

            foreach (CSED_SpecSyncColumnMapping column in table.columns)
            {
                // プレハブへの書き込み(コンポーネント指定)と、同期しない列は対象外
                if (string.IsNullOrEmpty(column.property) || !string.IsNullOrEmpty(column.component)) continue;

                List<string> paths = new List<string>();
                if (!string.IsNullOrEmpty(column.assetPath)) paths.Add(column.assetPath);
                else
                {
                    foreach (CSED_SpecSyncRowMapping row in table.rows)
                    {
                        if (!string.IsNullOrEmpty(row.assetPath)) paths.Add(row.assetPath);
                    }
                    if (paths.Count == 0 && !string.IsNullOrEmpty(table.assetPath)) paths.Add(table.assetPath);
                }

                foreach (string path in paths)
                {
                    _specLabels[path + "|" + column.property] = new CSED_LevelDesignLabel(column.column, column.scale);
                }
            }
        }
        return _specLabels;
    }

    private static string LastName(string propertyPath)
    {
        int dot = propertyPath.LastIndexOf('.');
        return dot >= 0 ? propertyPath.Substring(dot + 1) : propertyPath;
    }
}
