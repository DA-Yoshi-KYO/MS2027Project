/* ================================================
 * 
 * ================================================
 * 制作者：元浪梨緒
 * ------------------------------------------------
 * 2026-10-06 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// プレイヤー1人分のリザルトデータ
/// ・Player が playerNumber と一緒に new して持つ
/// ・Score は「加算済みの点数」と「回数」を両方持つ
///   → 点数はゲーム中に加算するたびに計算済みで入れる
///   → 暗躍ボーナス・悪人の種類による点数差も表現できる
/// ・合計は0で止めない(減点が多ければマイナスになる)
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

        // 追加: 回数(ランク・称号の表示に使う。点数には入らない)
        private int _surpriseAttackCount;  // 奇襲の成功回数
        private int _saUseCount;           // SAの使用回数

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

        // 追加
        public int surpriseAttackCount => _surpriseAttackCount;
        public int saUseCount => _saUseCount;

        // ---- コンストラクタ ----
        // 追加した2つの引数は省略できる(今までの呼び出しは、そのままコンパイルできる)。
        // ただし、Score を作り直す時は、追加の2つも引き継ぐこと(省略すると0に戻る)
        public Score(
            int defeatVillainScore,
            int foundByPoliceScore,
            int crimeCompletedScore,
            int defeatPlayerScore,
            int defeatVillainCount,
            int foundByPoliceCount,
            int crimeCompletedCount,
            int defeatPlayerCount,
            int surpriseAttackCount = 0,
            int saUseCount = 0)
        {
            _defeatVillainScore = defeatVillainScore;
            _foundByPoliceScore = foundByPoliceScore;
            _crimeCompletedScore = crimeCompletedScore;
            _defeatPlayerScore = defeatPlayerScore;
            _defeatVillainCount = defeatVillainCount;
            _foundByPoliceCount = foundByPoliceCount;
            _crimeCompletedCount = crimeCompletedCount;
            _defeatPlayerCount = defeatPlayerCount;
            _surpriseAttackCount = surpriseAttackCount;
            _saUseCount = saUseCount;
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
            serializer.SerializeValue(ref _surpriseAttackCount);
            serializer.SerializeValue(ref _saUseCount);
        }
    }

    // =========================================================
    // データ（バッキングフィールド）
    // =========================================================

    private readonly int _playerNumber;
    private Score _score;
    private string _title = "---";
    private CSE_CharacterType _characterType; // 追加: 使ったキャラ

    // =========================================================
    // プロパティ公開（読み取り専用）
    // =========================================================

    public int playerNumber => _playerNumber;
    public Score score => _score;
    public string title => _title;

    /// <summary>追加: 使ったキャラ(リザルトのアイコンに使う)</summary>
    public CSE_CharacterType characterType => _characterType;

    /// <summary>
    /// 合計点（各項目の加算済みスコアを合計する）
    /// 減点が多ければマイナスにもなる(0で止めない)
    /// </summary>
    public int totalScore =>
          _score.defeatVillainScore
        + _score.foundByPoliceScore
        + _score.crimeCompletedScore
        + _score.defeatPlayerScore;

    // =========================================================
    // コンストラクタ
    // =========================================================

    public CS_ResultData(int playerNumber)
    {
        _playerNumber = playerNumber;
    }

    // =========================================================
    // 称号(追加)
    // =========================================================

    /// <summary>
    /// 称号の一覧。条件を満たした称号を全部返す(複数)。
    /// 1つも満たしていなければ、条件に一番近い称号を1つ返す。必ず1つ以上入っている。
    /// 変更: 条件の回数を Inspector で変えられるように、設定(DB_ResultTitleSettings)を受け取る。
    /// 設定が null の時は、初期値(3回 / 50回 / 50回)で判定する。
    /// 決め方は CS_ResultTitleDecider。
    /// </summary>
    public IReadOnlyList<CSE_ResultTitle> GetResultTitles(CSO_ResultTitleSettings titleSettings)
    {
        return CS_ResultTitleDecider.DecideAll(
            titleSettings,
            _score.foundByPoliceCount,
            _score.surpriseAttackCount,
            _score.saUseCount
        );
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

    /// <summary>追加: 使ったキャラをセットする（結果を集める時に入れる）</summary>
    public void SetCharacterType(CSE_CharacterType characterType) => _characterType = characterType;
}
