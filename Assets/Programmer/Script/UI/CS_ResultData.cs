/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-06 | 初回作成
 * ================================================ */

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤー1人分のリザルトデータ
/// ・Player が playerNumber と一緒に new して持つ
/// ・Score は「加算済みの点数」と「回数」を両方持つ
///   → 点数はゲーム中に加算するたびに計算済みで入れる
///   → 暗躍ボーナス・悪人の種類による点数差も表現できる
/// ・合計を0未満にしない処理は加算するたびに行う
/// ・称号もここが持つ
/// </summary>
public class CS_ResultData
{
    // =========================================================
    // 同期する点数と回数（NetworkVariable で同期する）
    // =========================================================

    public struct Score : INetworkSerializable
    {
        // ---- バッキングフィールド ----

        // 加算済みの点数（暗躍ボーナス・悪人の種類による点数差を含む）
        private int _defeatVillainScore;   // 悪人撃破スコア（＋）
        private int _foundByPoliceScore;   // 警察発見スコア（－）
        private int _crimeCompletedScore;  // 犯罪完遂スコア（－）
        private int _defeatPlayerScore;    // 他P撃破スコア（＋？仮）

        // 回数（リザルトの内訳表示に使う）
        private int _defeatVillainCount;   // 倒した悪人の数
        private int _foundByPoliceCount;   // 警察に見つかった数
        private int _crimeCompletedCount;  // 犯罪完遂された数
        private int _defeatPlayerCount;    // 他プレイヤーを倒した数

        // ---- プロパティ公開（読み取り専用）----

        // 点数
        public int defeatVillainScore => _defeatVillainScore;
        public int foundByPoliceScore => _foundByPoliceScore;
        public int crimeCompletedScore => _crimeCompletedScore;
        public int defeatPlayerScore => _defeatPlayerScore;

        // 回数
        public int defeatVillainCount => _defeatVillainCount;
        public int foundByPoliceCount => _foundByPoliceCount;
        public int crimeCompletedCount => _crimeCompletedCount;
        public int defeatPlayerCount => _defeatPlayerCount;

        // ---- コンストラクタ ----
        public Score(
            int defeatVillainScore,
            int foundByPoliceScore,
            int crimeCompletedScore,
            int defeatPlayerScore,
            int defeatVillainCount,
            int foundByPoliceCount,
            int crimeCompletedCount,
            int defeatPlayerCount)
        {
            _defeatVillainScore = defeatVillainScore;
            _foundByPoliceScore = foundByPoliceScore;
            _crimeCompletedScore = crimeCompletedScore;
            _defeatPlayerScore = defeatPlayerScore;
            _defeatVillainCount = defeatVillainCount;
            _foundByPoliceCount = foundByPoliceCount;
            _crimeCompletedCount = crimeCompletedCount;
            _defeatPlayerCount = defeatPlayerCount;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _defeatVillainScore);
            serializer.SerializeValue(ref _foundByPoliceScore);
            serializer.SerializeValue(ref _crimeCompletedScore);
            serializer.SerializeValue(ref _defeatPlayerScore);
            serializer.SerializeValue(ref _defeatVillainCount);
            serializer.SerializeValue(ref _foundByPoliceCount);
            serializer.SerializeValue(ref _crimeCompletedCount);
            serializer.SerializeValue(ref _defeatPlayerCount);
        }
    }

    // =========================================================
    // データ（バッキングフィールド）
    // =========================================================

    private readonly int _playerNumber;
    private Score _score;
    private string _title = "---";

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public int playerNumber => _playerNumber;
    public Score score => _score;
    public string title => _title;

    /// <summary>
    /// 合計点（各項目の加算済みスコアを合計する）
    /// 0未満にはならない
    /// </summary>
    public int totalScore => Mathf.Max(0,
          _score.defeatVillainScore
        + _score.foundByPoliceScore
        + _score.crimeCompletedScore
        + _score.defeatPlayerScore);

    // =========================================================
    // コンストラクタ
    // =========================================================

    public CS_ResultData(int playerNumber)
    {
        _playerNumber = playerNumber;
    }

    // =========================================================
    // 値の変更
    // =========================================================

    /// <summary>
    /// NetworkVariable の OnValueChanged で呼ぶ
    /// 「取り出す → 変える → Value に入れ直す」で同期する
    /// </summary>
    public void SetScore(Score score) => _score = score;

    /// <summary>称号をセットする（リザルト画面で決める）</summary>
    public void SetTitle(string title) => _title = title;
}
