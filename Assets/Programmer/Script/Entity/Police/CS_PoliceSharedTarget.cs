/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// 警察のグループ内で、メンバーが見つけた標的を共有するクラス
/// 報告が途絶えてから一定時間経つと、共有は無効になる
/// </summary>
public class CS_PoliceSharedTarget
{
    // 見えなくなってからも共有し続ける時間(秒)
    private readonly float _shareDuration = 0.0f;

    // 共有中の標的と優先度、最後に報告された時刻
    private Transform _target = null;
    private int _priority = CS_PoliceVision.noTargetPriority;
    private float _reportedTime = float.MinValue;

    /// <summary>
    /// 共有の有効時間を指定して作るコンストラクタ
    /// </summary>
    /// <param name="shareDuration">見えなくなってからも共有し続ける時間(秒)</param>
    public CS_PoliceSharedTarget(float shareDuration)
    {
        _shareDuration = shareDuration;
    }

    /// <summary>
    /// メンバーが見つけた標的を報告するメソッド
    /// 共有中の標的より優先度が低い場合は、共有中の標的が無効になるまで上書きしない
    /// </summary>
    /// <param name="target">見つけた標的</param>
    /// <param name="priority">標的の優先度</param>
    public void Report(Transform target, int priority)
    {
        if (IsValid() && priority < _priority) return;

        _target = target;
        _priority = priority;
        _reportedTime = Time.time;
    }

    /// <summary>
    /// 共有中の標的を取得するメソッド
    /// </summary>
    /// <param name="priority">標的の優先度</param>
    /// <returns>共有中の標的(いなければnull)</returns>
    public Transform Get(out int priority)
    {
        if (!IsValid())
        {
            priority = CS_PoliceVision.noTargetPriority;
            return null;
        }

        priority = _priority;
        return _target;
    }

    /// <summary>
    /// 共有中の標的がまだ有効かを判定するメソッド
    /// </summary>
    /// <returns>有効ならtrue</returns>
    private bool IsValid()
    {
        // 標的が消えた(撃退された悪人など)場合や、報告が途絶えて一定時間経った場合は無効
        return _target != null && Time.time - _reportedTime <= _shareDuration;
    }
}
