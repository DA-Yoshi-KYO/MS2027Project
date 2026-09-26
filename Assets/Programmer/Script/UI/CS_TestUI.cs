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
    [Header("操作するプレイヤーHPバーの番号")]
    [SerializeField] private int _playerNumber = 0;

    [Header("PlayerHP設定")]
    [SerializeField] private int _initPlayerHP = 300;
    [SerializeField] private int _maxPlayerHP = 300;

    // ---------------- 必殺技ゲージ設定 ----------------
    [Header("Special Gauge設定")]
    [SerializeField] private float _initSpecialGauge = 0f;   // 初期値
    [SerializeField] private float _maxSpecialGauge = 300f;  // 最大値

    private CS_UISpecialGaugeModel[] _specialModels = new CS_UISpecialGaugeModel[4];

    // ---------------- 敵HP設定（複数敵対応） ----------------
    [SerializeField] private GameObject _villainHpPrefab;
    private GameObject _villainHpUIInstance;

    [Header("VillainHP設定")]
    [SerializeField] private int _initVillainHP = 300;
    [SerializeField] private int _maxVillainHP = 300;

    // ---------------- タイマー設定 ----------------
    [Header("Timer設定")]
    [SerializeField] private int _initTimer = 300;
    [SerializeField] private int _maxTime = 300;

    private CS_UIScoreModel[] _scoreModels = new CS_UIScoreModel[4];

    // ---------------- Modelの保持 ----------------
    private CS_UIPlayerHpModel[] _hpModels = new CS_UIPlayerHpModel[4];
    private CS_UITimerModel _uiTimerModel;
    private CS_UIVillainHpModel _uiVillainHpModel;

    void Awake()
    {
        // ★ プレイヤーHP Model生成 & Bind（4人分）
        for (int i = 0; i < 2; i++)
        {
            _hpModels[i] = new CS_UIPlayerHpModel(_maxPlayerHP, _initPlayerHP);
            _hpModels[i].Bind(i);
        }

        // ★ 必殺技ゲージ Model生成 & Bind（4人分）
        for (int i = 0; i < 2; i++)
        {
            _specialModels[i] = new CS_UISpecialGaugeModel(_maxSpecialGauge, _initSpecialGauge);
            _specialModels[i].Bind(i);
        }

        // ★ スコア Model生成 & Bind（4人分）
        for (int i = 0; i < 4; i++)
        {
            _scoreModels[i] = new CS_UIScoreModel(0);
            _scoreModels[i].Bind(i);
        }

        // ★ タイマー Model生成（Bind不要）
        _uiTimerModel = new CS_UITimerModel(_maxTime);
        _uiTimerModel.SetTime(_initTimer);
    }

    void Update()
    {
        // ---------------- プレイヤーHPテスト（プレイヤー0のHPを操作） ----------------
        if (Input.GetKeyDown(KeyCode.UpArrow))
            _hpModels[_playerNumber].SetHp(_hpModels[_playerNumber].currentHp.CurrentValue + 10);

        if (Input.GetKeyDown(KeyCode.DownArrow))
            _hpModels[_playerNumber].SetHp(_hpModels[_playerNumber].currentHp.CurrentValue - 10);

        // ---------------- 必殺技ゲージテスト（プレイヤー0のゲージを操作） ----------------
        if (Input.GetKeyDown(KeyCode.K))
            _specialModels[_playerNumber].AddGauge(10f);

        if (Input.GetKeyDown(KeyCode.L))
            _specialModels[_playerNumber].AddGauge(-10f);

        // ---------------- タイマー減少 ----------------
        _uiTimerModel.SetTime(_uiTimerModel.currentTime.CurrentValue - Time.deltaTime);

        // ---------------- 敵HP UI生成 ----------------
        if (Input.GetKeyDown(KeyCode.P))
        {
            _villainHpUIInstance = Instantiate(_villainHpPrefab, transform);

            _uiVillainHpModel = new CS_UIVillainHpModel(_maxVillainHP, _initVillainHP);
            _uiVillainHpModel.Bind(transform);
        }

        // 敵HPテスト（スペースキーでダメージ）
        if (Input.GetKeyDown(KeyCode.Space))
            _uiVillainHpModel?.SetHp(_uiVillainHpModel.currentHp.CurrentValue - 20);

        // HP0で敵UI削除
        if (_uiVillainHpModel != null && _uiVillainHpModel.currentHp.CurrentValue <= 0)
        {
            _uiVillainHpModel.Dispose();
            Destroy(_villainHpUIInstance);
            _uiVillainHpModel = null;
        }

        // スコアテスト
        if (Input.GetKeyDown(KeyCode.S))
            _scoreModels[_playerNumber].AddScore(100);
    }

    void OnDestroy()
    {
        for (int i = 0; i < 4; i++)
        {
            _hpModels[i]?.Dispose();
            _specialModels[i]?.Dispose();
            _scoreModels[i]?.Dispose();
        }

        _uiTimerModel?.Dispose();
        _uiVillainHpModel?.Dispose();
    }
}
