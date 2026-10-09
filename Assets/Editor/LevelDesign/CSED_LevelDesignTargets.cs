using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/*
 * ステータス調整ウィンドウで編集する対象をまとめたクラス
 * ・プレイヤー: 再生中のCS_PlayerStatsの値(項目ごとの読み書き方法と、保存先のDB_PlayerStatsの変数名)
 * ・データ: プロジェクト内のScriptableObject(プレイヤーの攻撃、悪人、悪人の攻撃、警察)
 *
 * 制作者：　吉田京志郎(Claude Codeで生成)
 */

// プレイヤーの項目1つ分
public class CSED_LevelDesignPlayerField
{
    private readonly string _label;
    private readonly string _property;   // DB_PlayerStats(CSO_PlayerStats)の変数名
    private readonly Func<CS_PlayerStats, float> _getter;
    private readonly Action<CS_PlayerStats, float> _setter;

    public string label => _label;
    public string property => _property;

    public CSED_LevelDesignPlayerField(string label, string property, Func<CS_PlayerStats, float> getter, Action<CS_PlayerStats, float> setter)
    {
        _label = label;
        _property = property;
        _getter = getter;
        _setter = setter;
    }

    public float Get(CS_PlayerStats stats) => _getter(stats);
    public void Set(CS_PlayerStats stats, float value) => _setter(stats, value);
}

public static class CSED_LevelDesignTargets
{
    // CS_PlayerStatsが持つ値。値の変更はCS_PlayerStatsのSetメソッドを通す(サーバー/オフラインのみ書き込める)
    private static readonly CSED_LevelDesignPlayerField[] _playerFields =
    {
        new CSED_LevelDesignPlayerField("HP上限", "_maxHp", s => s.maxHp, (s, v) => s.SetMaxHp(v)),
        new CSED_LevelDesignPlayerField("攻撃力(倍率)", "_attackPower", s => s.attackPower, (s, v) => s.SetAttackPower(v)),
        new CSED_LevelDesignPlayerField("移動速度", "_moveSpeed", s => s.moveSpeed, (s, v) => s.SetMoveSpeed(v)),
        new CSED_LevelDesignPlayerField("ジャンプ力", "_jumpPower", s => s.jumpPower, (s, v) => s.SetJumpPower(v)),
        new CSED_LevelDesignPlayerField("ダッシュ速度", "_dashSpeed", s => s.dashSpeed, (s, v) => s.SetDashSpeed(v)),
        new CSED_LevelDesignPlayerField("ダッシュ継続速度", "_sprintSpeed", s => s.sprintSpeed, (s, v) => s.SetSprintSpeed(v)),
        new CSED_LevelDesignPlayerField("必殺技ゲージ上限", "_maxGauge", s => s.maxGauge, (s, v) => s.SetMaxGauge(v)),
        new CSED_LevelDesignPlayerField("必殺技威力(倍率)", "_specialAttackPower", s => s.specialAttackPower, (s, v) => s.SetSpecialAttackPower(v)),
    };
    public static CSED_LevelDesignPlayerField[] playerFields => _playerFields;

    // タブごとに編集するデータの型
    private static readonly Type[] _villainTypes = { typeof(CSO_VillainStats), typeof(CSO_VillainTimeScaling) };
    private static readonly Type[] _policeTypes = { typeof(CSO_PoliceStatus), typeof(CSO_PoliceWantedLevelData) };

    // シーン内のプレイヤーを番号順(P1〜P4)に返す
    public static List<CS_PlayerStats> FindPlayers()
    {
        return UnityEngine.Object.FindObjectsByType<CS_PlayerStats>(FindObjectsSortMode.None)
            .OrderBy(GetPlayerNumber)
            .ToList();
    }

    // プレイヤー番号(0〜3)。CS_Playerが無ければ0
    public static int GetPlayerNumber(CS_PlayerStats stats)
    {
        CS_Player player = stats != null ? stats.GetComponent<CS_Player>() : null;
        return player != null ? player.playerNumber : 0;
    }

    // 表示する操作キャラ(自分が操作しているキャラ。オフラインなど判定できなければ最初の1人)
    public static CS_PlayerStats FindControlledPlayer(List<CS_PlayerStats> players)
    {
        foreach (CS_PlayerStats stats in players)
        {
            if (stats.IsSpawned && stats.IsOwner) return stats;
        }
        return players.Count > 0 ? players[0] : null;
    }

    // プレイヤーの値の保存先(CS_PlayerStatsに設定されているBase Stats)
    public static CSO_PlayerStats GetBaseStats(CS_PlayerStats stats)
    {
        if (stats == null) return null;
        return new SerializedObject(stats).FindProperty("_baseStats")?.objectReferenceValue as CSO_PlayerStats;
    }

    // 攻撃データ(CSO_AttackData)は全員同じ型なので、タブごとにデータ名で分ける
    private static readonly string[] _playerAttackNames = { "DB_PlayerAttack1", "DB_PlayerAttack2", "DB_PlayerAttack3", "DB_PlayerSpecial" };
    private static readonly string[] _villainAttackNames = { "DB_VillainAttack" };

    public static List<ScriptableObject> FindAssets(CSE_LevelDesignTab tab)
    {
        List<ScriptableObject> assets = new List<ScriptableObject>();
        Type[] types = tab == CSE_LevelDesignTab.Villain ? _villainTypes : tab == CSE_LevelDesignTab.Police ? _policeTypes : new Type[0];
        foreach (Type type in types) assets.AddRange(FindAssetsOfType(type));

        string[] attackNames = tab == CSE_LevelDesignTab.Player ? _playerAttackNames : tab == CSE_LevelDesignTab.Villain ? _villainAttackNames : new string[0];
        List<ScriptableObject> attacks = FindAssetsOfType(typeof(CSO_AttackData));
        foreach (string name in attackNames)
        {
            ScriptableObject attack = attacks.Find(a => a.name == name);
            if (attack != null) assets.Add(attack);
        }
        return assets;
    }

    private static List<ScriptableObject> FindAssetsOfType(Type type)
    {
        List<ScriptableObject> assets = new List<ScriptableObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
        {
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && type.IsInstanceOfType(asset)) assets.Add(asset);
        }
        return assets;
    }

    // 全タブで扱うデータ(ScriptableObject)をすべて返す
    public static List<ScriptableObject> FindAllAssets()
    {
        List<ScriptableObject> assets = FindAssets(CSE_LevelDesignTab.Player);
        assets.AddRange(FindAssets(CSE_LevelDesignTab.Villain));
        assets.AddRange(FindAssets(CSE_LevelDesignTab.Police));
        return assets;
    }
}
