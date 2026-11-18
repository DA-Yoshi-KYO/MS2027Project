using UnityEngine;

/*
 * エリアごとの犯罪完遂ゲージUI(CS_UICrimeProgressModel)へ、ゲーム側の値を流すクラス(ソロ)
 * エリア分け(CS_CrimeAreaLayout)が持ち、毎フレーム Tick を呼ぶ
 *
 * 制作者：　中出峻輔
 */

// ========================================
/*
 * メモ
 * ・使い方はClaudeDocs/CrimeProgressUI_使い方.md(UIはUICanvasのCrimeProgress)
 * ・作った時にModelを作ってBindし、Dispose で破棄する(エリア分けが無効になった時)
 * ・完遂率: 各エリア(CS_CrimeArea)の progress(0〜1) × 100 を、エリアの areaType のゲージへ渡す
 *   (値が変わらなければModelがイベントを出さないので、毎フレーム渡してよい)
 * ・現在地: 自分の画面のプレイヤー(CS_Player.localPlayer)の位置が入る扇形のエリア
 *   プレイヤーがまだいない間は「現在地なし」(全ゲージ小さく、数字なし)
 * ・ソロは通信せずに遊ぶので、ゲージはネットワークで同期しない(エリア分けはField_Solo1にしか無い)
 */
// ========================================

public class CS_CrimeAreaProgressFeed
{
    private readonly CS_CrimeAreaLayout _layout;
    private readonly CS_UICrimeProgressModel _model;

    public CS_CrimeAreaProgressFeed(CS_CrimeAreaLayout layout)
    {
        _layout = layout;
        _model = new CS_UICrimeProgressModel();
        Tick();   // Bindの時点で今の値が全部反映されるよう、先に入れておく
        _model.Bind();
    }

    public void Tick()
    {
        foreach (CS_CrimeAreaLayout.Sector sector in _layout.sectors)
        {
            CS_CrimeArea area = sector.area;
            if (area == null) continue;

            _model.SetProgress(area.areaType, area.progress * 100f);
        }

        CS_Player player = CS_Player.localPlayer;
        if (player == null) return;

        CS_CrimeArea current = _layout.GetArea(player.transform.position);
        if (current != null) _model.SetCurrentArea(current.areaType);
    }

    public void Dispose()
    {
        _model.Dispose();
    }
}
