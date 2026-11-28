/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-11-28 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// ソロの、エリア1つ分のクリアタイム
/// ・クリアできなかったエリアも、この形で受け取る(isCleared を false にする)
/// ・seconds は、そのエリアにかかった秒数(未クリアのエリアの秒数の決め方は、渡す側が決める)
/// ・CS_SoloAreaResult の areaTimes に、エリアの並び順で入れて渡す
/// </summary>
public readonly struct CS_SoloAreaTime
{
    private readonly bool _isCleared;
    private readonly float _seconds;

    /// <summary>そのエリアをクリアできたか</summary>
    public bool isCleared => _isCleared;

    /// <summary>そのエリアにかかった秒数</summary>
    public float seconds => _seconds;

    public CS_SoloAreaTime(bool isCleared, float seconds)
    {
        _isCleared = isCleared;
        _seconds = Mathf.Max(0f, seconds);
    }
}
