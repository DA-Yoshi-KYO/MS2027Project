/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エリアごとのランクのしきい値(クリアタイム)を入れておく ScriptableObject
/// ・エリアごとに「何秒以内ならS / A / B」を決められる。それより遅ければC
/// ・エリアは「並んでいる順番(0から)」で指定する。エリアの順番と、下のリストの順番を合わせる
/// ・リストにないエリア番号は、_defaultThreshold を使う
/// ・Project 右クリック > Create > Result > Result Rank Settings
///   → DB_ResultRankSettings ができる
/// </summary>
[CreateAssetMenu(menuName = "Result/Result Rank Settings", fileName = "DB_ResultRankSettings")]
public sealed class CSO_ResultRankSettings : ScriptableObject
{
    // =========================================================
    // エリア1つ分のしきい値
    // =========================================================

    /// <summary>
    /// エリア1つ分の、ランクのしきい値(秒)。
    /// クリアタイムが sSeconds 以下ならS、aSeconds 以下ならA、bSeconds 以下ならB、それより遅ければC。
    /// </summary>
    [Serializable]
    public sealed class AreaThreshold
    {
        [Tooltip("Inspectorで見分けるための名前(コードでは使わない)")]
        [SerializeField]
        private string _label = "Area";

        [Tooltip("この秒数以内でクリアしたらS")]
        [Min(0f)]
        [SerializeField]
        private float _sSeconds = 60f;

        [Tooltip("この秒数以内でクリアしたらA")]
        [Min(0f)]
        [SerializeField]
        private float _aSeconds = 90f;

        [Tooltip("この秒数以内でクリアしたらB。これより遅ければC")]
        [Min(0f)]
        [SerializeField]
        private float _bSeconds = 120f;

        public string label => _label;
        public float sSeconds => _sSeconds;
        public float aSeconds => _aSeconds;
        public float bSeconds => _bSeconds;

        /// <summary>S ≦ A ≦ B の順になるように直す(Sのしきい値が一番短くなければ矛盾するため)</summary>
        public void Sanitize()
        {
            _sSeconds = Mathf.Max(0f, _sSeconds);
            _aSeconds = Mathf.Max(_sSeconds, _aSeconds);
            _bSeconds = Mathf.Max(_aSeconds, _bSeconds);
        }
    }

    // =========================================================
    // Inspector
    // =========================================================

    [Header("エリアごとのしきい値(エリアの並び順と同じ順番にする)")]
    [SerializeField]
    private List<AreaThreshold> _areaThresholds = new List<AreaThreshold>();

    [Header("リストにないエリアに使うしきい値")]
    [SerializeField]
    private AreaThreshold _defaultThreshold = new AreaThreshold();

    // =========================================================
    // 取得
    // =========================================================

    /// <summary>
    /// 指定したエリア番号のしきい値を返す。
    /// リストにない番号(範囲外・空の要素)なら、既定のしきい値を返す。
    /// </summary>
    public AreaThreshold GetThreshold(int areaIndex)
    {
        if (areaIndex >= 0 &&
            areaIndex < _areaThresholds.Count &&
            _areaThresholds[areaIndex] != null)
        {
            return _areaThresholds[areaIndex];
        }

        return _defaultThreshold;
    }

    // =========================================================
    // Inspector値の補正
    // =========================================================

    private void OnValidate()
    {
        _defaultThreshold?.Sanitize();

        for (int i = 0; i < _areaThresholds.Count; i++)
        {
            _areaThresholds[i]?.Sanitize();
        }
    }
}
