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
/// SEの3D再生設定。
/// </summary>
[Serializable]
public class CS_Se3DSettings
{
    [SerializeField]
    private bool _is3D;

    [Min(0f)]
    [SerializeField]
    private float _minDistance = 1f;

    [Min(0f)]
    [SerializeField]
    private float _maxDistance = 30f;

    [SerializeField]
    private AudioRolloffMode _rolloff =
        AudioRolloffMode.Logarithmic;

    public bool is3D => _is3D;
    public float minDistance => _minDistance;
    public float maxDistance => _maxDistance;
    public AudioRolloffMode rolloff => _rolloff;
}