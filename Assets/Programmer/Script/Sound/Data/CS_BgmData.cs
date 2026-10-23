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
/// BGM1曲分の設定データ。
/// </summary>
[Serializable]
public class CS_BgmData
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("BGMのID")]
    [SerializeField] private CSE_BgmId _id;

    [Header("AudioClip")]
    [SerializeField] private AudioClip _clip;

    [Header("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Header("ループ再生")]
    [SerializeField] private bool _loop = true;

    // =========================================================
    // プロパティ公開
    // =========================================================

    public CSE_BgmId id => _id;
    public AudioClip clip => _clip;
    public float volume => _volume;
    public bool loop => _loop;
}