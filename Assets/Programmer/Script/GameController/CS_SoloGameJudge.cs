using UnityEngine;

/*
 * ソロの終了条件の判定(全エリアの犯罪を止める)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・置き方: Field_Solo1 の中(CS_SoloAreaProgressionの隣)に1つだけ置く。マルチでは無効なので何もしない
 * ・終了条件
 *   全エリアが決着(CS_SoloAreaProgression。悪人を全員倒す か 制限時間が切れる)したら終了
 *   ゲーム全体の時間切れ(CS_TimerControllerから受ける) → 終了
 * ・終わり方(CSE_SoloEndReason)
 *   全エリアを守れた / 一部だけ守れた → AllCleared(リザルトの見出しは守れた数で変える)
 *   1つも守れなかった → CrimeCompleted(失敗)
 *   ゲーム全体の時間切れ → TimeUp
 * ・終了する時に、守れたエリア数と食い止めた率をCS_SoloResultStoreへ保存する(リザルトが表示する)
 *   時間切れはタイマーが結果の保存とリザルトへの遷移をするので、ここでは保存だけ行う
 *   それ以外はCS_TimerController.FinishNowで、今すぐ結果を保存してリザルトへ遷移する
 * ・食い止めた率 = 倒した悪人の合計 ÷ 出した悪人の合計(CS_SoloAreaProgression.totalDefeatedRate)
 * ・犯罪完遂ゲージはソロでは使わない(エリアごとの制限時間に置き換えた)
 */
// ========================================

public class CS_SoloGameJudge : MonoBehaviour
{
    private CS_SoloAreaProgression _progression;
    private CS_TimerController _timer;
    private bool _isEnded;

    private void Start()
    {
        _progression = CS_SoloAreaProgression.instance;
        if (_progression == null)
        {
            Debug.LogWarning("CS_SoloGameJudge: CS_SoloAreaProgression が見つからないため、判定しません", this);
            return;
        }

        _timer = FindAnyObjectByType<CS_TimerController>();
        _progression.onAllAreasResolved += HandleAllAreasResolved;
        if (_timer != null) _timer.onTimeUp += HandleTimeUp;
    }

    private void OnDestroy()
    {
        if (_progression != null) _progression.onAllAreasResolved -= HandleAllAreasResolved;
        if (_timer != null) _timer.onTimeUp -= HandleTimeUp;
    }

    // 全エリアが決着した時。1つも守れなかったなら失敗、1つでも守れたなら終了
    private void HandleAllAreasResolved()
    {
        End(_progression.clearedCount == 0 ? CSE_SoloEndReason.CrimeCompleted : CSE_SoloEndReason.AllCleared, true);
    }

    private void HandleTimeUp()
    {
        End(CSE_SoloEndReason.TimeUp, false);
    }

    // 結果を保存する。finishNow: 今すぐリザルトへ進めるか(時間切れはタイマーが進めるのでfalse)
    private void End(CSE_SoloEndReason reason, bool finishNow)
    {
        if (_isEnded) return;
        _isEnded = true;

        CS_SoloResultStore.Save(new CS_SoloAreaResult(reason, _progression.clearedCount, _progression.areaCount, _progression.totalDefeatedRate));
        if (finishNow && _timer != null) _timer.FinishNow();
    }
}
