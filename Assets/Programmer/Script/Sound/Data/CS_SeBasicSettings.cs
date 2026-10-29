/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-29 | 初回作成
 * ================================================ */

using System;
using UnityEngine;

/// <summary>
/// SEの基本再生設定。
/// </summary>
[Serializable]
public class CS_SeBasicSettings
{
    [Range(0f, 1f)]
    [SerializeField]
    private float _volume = 1f;

    [Tooltip("0.1の場合、Pitchが0.9～1.1の範囲で変化する")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float _pitchRandom;

    [Tooltip("同じSEを同時に鳴らせる最大数")]
    [Min(1)]
    [SerializeField]
    private int _maxConcurrent = 4;

    public float volume => _volume;
    public float pitchRandom => _pitchRandom;
    public int maxConcurrent => _maxConcurrent;

    public float GetRandomPitch()
    {
        return 1f + UnityEngine.Random.Range(
            -_pitchRandom,
            _pitchRandom
        );
    }
}