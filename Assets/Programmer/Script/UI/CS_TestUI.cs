/* ================================================
 * 実装したUIが動くか確認用のCS(メインでは使用しません)
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-09-24 | 初回作成
 * 2026-09-25 | Modelをnewして使う形に変更
 * ================================================ */

using UnityEngine;

/// <summary>
/// UIテスト用の簡易スクリプト
/// プレイヤーHP・敵HP・タイマーのModelを生成し、BindしてUIと紐付ける
/// PresenterやViewは自動でModelを拾って表示する
/// </summary>
public class CS_TestUI : MonoBehaviour
{
    // ---------------- プレイヤーHP設定 ----------------
    [Header("表示するプレイヤーHPバーの番号")]
    [SerializeField] private int _playerNumber = 0;

    [Header("PlayerHP設定")]
    [SerializeField] private int _initPlayerHP = 300;
    [SerializeField] private int _maxPlayerHP = 300;

    // ---------------- 敵HP設定（複数敵対応） ----------------
    [SerializeField] private GameObject _enemyHpPrefab;

    private GameObject _enemyHpUIInstance; // 生成したUIの実体を保持

    [Header("表示する敵HPバーの番号")]
    [SerializeField] private int _enemyNumber = 0;

    [Header("EnemyHP設定")]
    [SerializeField] private int _initEnemyHP = 300;
    [SerializeField] private int _maxEnemyHP = 300;

    // ---------------- タイマー設定 ----------------
    [Header("Timer設定")]
    [SerializeField] private int _initTimer = 300;
    [SerializeField] private int _maxTime = 300;

    private CS_UIScoreModel[] _scoreModels = new CS_UIScoreModel[4];

    // ---------------- Modelの保持（UI使用者が持つのはModelだけ） ----------------
    private CS_UIPlayerHpModel _hpModel;
    private CS_UITimerModel _uiTimerModel;
    private CS_UIEnemyHpModel _uiEnemyHpModel;

    void Start()
    {
        // ★ プレイヤーHP Model生成 & Bind
        _hpModel = new CS_UIPlayerHpModel(_maxPlayerHP, _initPlayerHP);
        _hpModel.Bind(_playerNumber);  // Presenterが自動で拾う

        // ★ タイマー Model生成（Bind不要：Instance方式）
        _uiTimerModel = new CS_UITimerModel(_maxTime);
        _uiTimerModel.SetTime(_initTimer);

        for (int i = 0; i < 4; i++)
        {
            _scoreModels[i] = new CS_UIScoreModel(0); // 初期スコア0
            _scoreModels[i].Bind(i);                 // プレイヤー番号で公開
        }
    }

    void Update()
    {
        // プレイヤーHPのテスト（↑↓キーで増減）
        if (Input.GetKeyDown(KeyCode.UpArrow))
            _hpModel.SetHp(_hpModel.currentHp.CurrentValue + 10);

        if (Input.GetKeyDown(KeyCode.DownArrow))
            _hpModel.SetHp(_hpModel.currentHp.CurrentValue - 10);

        // タイマーの減少（毎フレーム）
        _uiTimerModel.SetTime(_uiTimerModel.currentTime.CurrentValue - Time.deltaTime);

        // ★ Pキーで敵HP UIを生成
        if (Input.GetKeyDown(KeyCode.P))
        {
            // UI を生成（WorldSpace Canvas の子にする）
            _enemyHpUIInstance = Instantiate(_enemyHpPrefab);

            // Model生成 & Bind
            _uiEnemyHpModel = new CS_UIEnemyHpModel(_maxEnemyHP, _initEnemyHP);
            _uiEnemyHpModel.Bind(_enemyNumber);
        }

        // 敵HPのテスト（スペースキーでダメージ）
        if (Input.GetKeyDown(KeyCode.Space))
            _uiEnemyHpModel.SetHp(_uiEnemyHpModel.currentHp.CurrentValue - 20);

        // ★ HPが0以下になったらUIを消す
        if (_uiEnemyHpModel != null && _uiEnemyHpModel.currentHp.CurrentValue <= 0)
        {
            _uiEnemyHpModel.Dispose(); // Bind解除
            Destroy(_enemyHpUIInstance); // ← 生成したUIを破壊する
            _uiEnemyHpModel = null; // 二重処理防止
        }

        if(Input.GetKeyDown(KeyCode.O))
            _scoreModels[1].AddScore(100);
    }

    void OnDestroy()
    {
        // Disposeで公開も自動で外れる
        _hpModel?.Dispose();
        _uiTimerModel?.Dispose();
        _uiEnemyHpModel?.Dispose();
    }
}

