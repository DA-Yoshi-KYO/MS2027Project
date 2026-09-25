/* ================================================
 * 実装したUIが動くか確認用のCS
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | Modelをnewして使う形に変更
 * ================================================ */

using UnityEngine;

/// <summary>
///  実装したUIが動くか確認用のCS
///  HPバーを使う側の書き方の例も兼ねる
/// </summary>
public class CS_TestUI : MonoBehaviour
{
    [Header("表示するHPバーの番号")][SerializeField] private int _playerNumber = 0;
    [Header("HP設定")]
    [SerializeField] private int _initHP = 300;
    [SerializeField] private int _maxHP = 300;

    [Header("Timer設定")]
    [SerializeField] private int _initTimer = 300;
    [SerializeField] private int _maxTime = 300;

    //使用者が持つUIの変数はModelだけ
    private CS_UIHpModel _hpModel;
    private CS_UITimerModel _uiTimerModel;

    void Start()
    {
        _hpModel = new CS_UIHpModel(_maxHP, _initHP);
        _hpModel.Bind(_playerNumber);

        _uiTimerModel = new CS_UITimerModel(_maxTime);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) _hpModel.SetHp(_hpModel.currentHp.CurrentValue + 10);
        if (Input.GetKeyDown(KeyCode.DownArrow)) _hpModel.SetHp(_hpModel.currentHp.CurrentValue - 10);

        _uiTimerModel.SetRemaining(_uiTimerModel.currentTime.CurrentValue - Time.deltaTime);
    }

    void OnDestroy()
    {
        //Disposeで公開も自動で外れる
        _hpModel?.Dispose();
        _uiTimerModel.Dispose();
    }
}
