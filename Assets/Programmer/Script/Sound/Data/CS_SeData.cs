/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-22 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// SE1つ分の設定データ。
/// 音量、ピッチ、同時再生数、2D・3D設定を保持する。
/// </summary>
[Serializable]
public class CS_SeData
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("SEのID")]
    [CS_SeIDPopup]
    [SerializeField]
    private CSE_SeId _id;

    [Header("再生するAudioClip")]
    [Tooltip("複数登録すると、再生時にランダムで1つ選びます")]
    [SerializeField] private AudioClip[] _clips;

    [Header("基本設定")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Tooltip("0.1の場合、Pitchが0.9～1.1の範囲で変化します")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pitchRandom;

    [Tooltip("同じSEを同時に鳴らせる最大数")]
    [Min(1)]
    [SerializeField] private int _maxConcurrent = 4;

    [Header("3D設定")]
    [SerializeField] private bool _is3D;

    [Min(0f)]
    [SerializeField] private float _minDistance = 1f;

    [Min(0f)]
    [SerializeField] private float _maxDistance = 30f;

    [SerializeField]
    private AudioRolloffMode _rolloff =
        AudioRolloffMode.Logarithmic;

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public CSE_SeId id => _id;

    public float volume => _volume;

    public int maxConcurrent => _maxConcurrent;

    public bool is3D => _is3D;

    public float minDistance => _minDistance;

    public float maxDistance => _maxDistance;

    public AudioRolloffMode rolloff => _rolloff;

    // =========================================================
    // AudioClip取得
    // =========================================================

    /// <summary>
    /// 登録されているAudioClipからランダムで1つ取得する。
    /// 登録がない場合はnullを返す。
    /// </summary>
    public AudioClip GetRandomClip()
    {
        if (_clips == null || _clips.Length == 0)
            return null;

        int randomIndex = UnityEngine.Random.Range(
            0,
            _clips.Length
        );

        return _clips[randomIndex];
    }

    /// <summary>
    /// 設定された範囲でランダムなPitchを取得する。
    /// </summary>
    public float GetRandomPitch()
    {
        return 1f + UnityEngine.Random.Range(
            -_pitchRandom,
            _pitchRandom
        );
    }
}