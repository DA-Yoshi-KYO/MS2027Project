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
/// </summary>
[Serializable]
public class CS_SeData
{
    [Header("SEのID")]
    [CS_SeIDPopup]
    [SerializeField]
    private CSE_SeId _id;

    [Header("再生するAudioClip")]
    [Tooltip("複数登録すると、再生時にランダムで1つ選ぶ")]
    [SerializeField]
    private AudioClip[] _clips;

    [Header("基本設定")]
    [SerializeField]
    private CS_SeBasicSettings _basicSettings =
        new CS_SeBasicSettings();

    [Header("3D設定")]
    [SerializeField]
    private CS_Se3DSettings _threeDimensionalSettings =
        new CS_Se3DSettings();

    public CSE_SeId id => _id;

    // SoundManager側との互換性を維持
    public float volume =>
        _basicSettings.volume;

    public int maxConcurrent =>
        _basicSettings.maxConcurrent;

    public bool is3D =>
        _threeDimensionalSettings.is3D;

    public float minDistance =>
        _threeDimensionalSettings.minDistance;

    public float maxDistance =>
        _threeDimensionalSettings.maxDistance;

    public AudioRolloffMode rolloff =>
        _threeDimensionalSettings.rolloff;

    public AudioClip GetRandomClip()
    {
        if (_clips == null ||
            _clips.Length == 0)
        {
            return null;
        }

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                _clips.Length
            );

        return _clips[randomIndex];
    }

    public float GetRandomPitch()
    {
        return _basicSettings.GetRandomPitch();
    }
}