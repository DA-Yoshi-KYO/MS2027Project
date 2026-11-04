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
/// ジングル1曲分の設定データ。
/// </summary>
[Serializable]
public class CS_JingleData
{
    [Header("ジングルID")]
    [SerializeField] private CSE_JingleId _id;

    [Header("AudioClip")]
    [SerializeField] private AudioClip _clip;

    [Header("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Header("BGMダッキング")]
    [Tooltip("ジングル再生中のBGM音量倍率。0で消音、1で変化なし")]
    [Range(0f, 1f)]
    [SerializeField] private float _bgmDuckVolume = 0.3f;

    [Tooltip("BGM音量を切り替える時間")]
    [Min(0f)]
    [SerializeField] private float _duckFadeTime = 0.3f;

    public CSE_JingleId id => _id;
    public AudioClip clip => _clip;
    public float volume => _volume;
    public float bgmDuckVolume => _bgmDuckVolume;
    public float duckFadeTime => _duckFadeTime;
}
