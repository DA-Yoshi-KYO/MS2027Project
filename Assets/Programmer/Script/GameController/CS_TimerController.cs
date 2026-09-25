/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-25 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// タイマーだけを管理するコントローラー
/// ・タイマーModelを生成
/// ・毎フレーム時間を減らす
/// ・0になったら SceneTransitioner にシーン移動を依頼する
/// ※ UIはPresenterが自動で拾うので、ここではUIを一切触らない
/// </summary>
public class CS_TimerController : MonoBehaviour
{
    [Header("タイマーの最大時間（秒）")]
    [SerializeField] private float _maxTime = 300f;   // 5分

    [Header("時間が0になったら移動するシーン名")]
    [SerializeField] private string _nextSceneName = "ResultScene";

    // タイマーのModel（UIはPresenterが自動で拾う）
    private CS_UITimerModel _timerModel;

    // シーン遷移担当（同じオブジェクトに付ける前提）
    private CS_SceneTransitioner _sceneTransitioner;

    void Awake()
    {
        //タイマーModel生成
        _timerModel = new CS_UITimerModel(_maxTime);

        //初期残り時間を設定
        _timerModel.SetTime(_maxTime);

        //同じオブジェクトに付いている SceneTransitioner を取得
        _sceneTransitioner = GetComponent<CS_SceneTransitioner>();

        if (_sceneTransitioner == null)
        {
            Debug.LogError("同じオブジェクトに CS_SceneTransitioner が付いていません！");
        }
    }

    void Update()
    {
        //毎フレーム時間を減らす
        float remain = _timerModel.currentTime.CurrentValue - Time.deltaTime;
        _timerModel.SetTime(remain);

        //0以下になったらシーン移動を依頼
        if (_timerModel.currentTime.CurrentValue <= 0.0f)
        {
            _sceneTransitioner.StartTransition(_nextSceneName);
        }
    }

    void OnDestroy()
    {
        //Modelを破棄（Presenterの購読も自動で解除される）
        _timerModel?.Dispose();
    }
}
