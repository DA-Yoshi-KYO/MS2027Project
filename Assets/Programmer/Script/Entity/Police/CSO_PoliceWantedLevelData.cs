/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-10-05 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// 手配度ごとに出現する警察(増援)の人数・強さを管理するScriptableObject
/// 要素の0番目が手配度1、最後の要素が最大の手配度になる(手配度0は増援なし)
/// </summary>
[CreateAssetMenu(menuName = "Police/Wanted Level Data")]
public class CSO_PoliceWantedLevelData : ScriptableObject
{
    /// <summary>
    /// 手配度1段階分のデータ
    /// </summary>
    [Serializable]
    public struct LevelData
    {
        [SerializeField, Min(0)]
        [Tooltip("この手配度で出現している警察の人数(手配度を上げたプレイヤー1人あたり)")]
        private int _policeCount;
        public int policeCount => _policeCount;

        [SerializeField, Min(0f)]
        [Tooltip("追跡速度倍率(警察のステータスの追跡速度倍率に、さらに掛ける)")]
        private float _chaseSpeedMultiplier;
        public float chaseSpeedMultiplier => _chaseSpeedMultiplier;

        [SerializeField, Min(0f)]
        [Tooltip("視野距離(警察のステータスの視野距離の代わりに使う)")]
        private float _viewDistance;
        public float viewDistance => _viewDistance;
    }

    [SerializeField]
    [Tooltip("手配度ごとのデータ(0番目が手配度1)")]
    private LevelData[] _levels = new LevelData[0];

    // 最大の手配度
    public int maxLevel => _levels.Length;

    /// <summary>
    /// 手配度に応じたデータを取得するメソッド
    /// </summary>
    /// <param name="level">手配度(1〜最大の手配度。範囲外は近い方に丸める)</param>
    /// <returns>手配度のデータ</returns>
    public LevelData GetLevel(int level)
    {
        int index = Mathf.Clamp(level, 1, _levels.Length) - 1;
        return _levels[index];
    }

    /// <summary>
    /// 手配度に応じた警察の人数を取得するメソッド
    /// </summary>
    /// <param name="level">手配度(0以下なら0人)</param>
    /// <returns>警察の人数</returns>
    public int GetPoliceCount(int level)
    {
        if (level <= 0 || _levels.Length == 0) return 0;

        return GetLevel(level).policeCount;
    }
}
