using UnityEngine;

/*
 * NPCと人のプレイヤーのスコア差を見て、NPCの強さを調整するクラス
 * CS_NpcBrainが持ち、判断の速さ・攻撃の間隔などを強さに応じて変える
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・adjustInterval秒ごとに、自分と人のプレイヤー(NPCを除く)のスコアを比べて強さの段階(level)を上げ下げする
 *   ライバル(Rival): 人の中で一番高いスコアと比べ、scoreThreshold以上負けていたら強く、勝っていたら弱くする
 *   雑魚(Weak)     : 人の中で一番低いスコア - weakScoreMargin を上回りそうなら、スコアを狙うのを控える(suppressScoring)
 *                    控えている間は一番弱い段階にする
 *   調整なし(None) : 何もしない(最初の段階のまま)
 * ・人のプレイヤーがいない時(NPCだけのテストなど)は調整しない
 * ・「上手さ」はスコア以外(被弾数・撃破ペースなど)も見る可能性がある(プランナーに確認中)
 *   その場合はEvaluateの比べ方を変える
 */
// ========================================

public class CS_NpcDifficulty
{
    private readonly CSO_NpcPersonality _personality;
    private readonly CS_PlayerResultDataHolder _ownScore;

    private int _level;
    private bool _suppressScoring;
    private float _adjustTimer;

    public int level => _level;
    public bool suppressScoring => _suppressScoring;    // スコアを狙うのを控えているか(雑魚が人を上回らないため)

    // 強さ(0: 一番弱い 〜 1: 一番強い)
    public float strength => _personality.levelCount <= 1 ? 1f : (float)_level / (_personality.levelCount - 1);

    public CS_NpcDifficulty(CSO_NpcPersonality personality, CS_PlayerResultDataHolder ownScore)
    {
        _personality = personality;
        _ownScore = ownScore;
        _level = personality.startLevel;
        _adjustTimer = personality.adjustInterval;
    }

    public void Update(float deltaTime, CS_NpcSensor sensor)
    {
        if (_personality.difficultyMode == CSE_NpcDifficultyMode.None) return;
        if (_ownScore == null) return;

        _adjustTimer -= deltaTime;
        if (_adjustTimer > 0f) return;
        _adjustTimer = _personality.adjustInterval;

        if (!TryGetHumanScores(sensor, out int highest, out int lowest)) return;

        Evaluate(GetTotalScore(_ownScore), highest, lowest);
    }

    private void Evaluate(int ownScore, int highestHuman, int lowestHuman)
    {
        if (_personality.difficultyMode == CSE_NpcDifficultyMode.Rival)
        {
            int difference = ownScore - highestHuman;
            if (difference <= -_personality.scoreThreshold) ChangeLevel(1);
            else if (difference >= _personality.scoreThreshold) ChangeLevel(-1);
            return;
        }

        // 雑魚: 一番低い人より、常にweakScoreMargin以上下にいる
        _suppressScoring = ownScore >= lowestHuman - _personality.weakScoreMargin;
        if (_suppressScoring) _level = 0;
    }

    private void ChangeLevel(int amount)
    {
        _level = Mathf.Clamp(_level + amount, 0, _personality.levelCount - 1);
    }

    // 人のプレイヤー(NPCを除く)の中で一番高い・低いスコア
    private static bool TryGetHumanScores(CS_NpcSensor sensor, out int highest, out int lowest)
    {
        highest = int.MinValue;
        lowest = int.MaxValue;
        bool found = false;

        foreach (CS_Player player in sensor.players)
        {
            if (player == null || player.isNpc) continue;

            CS_PlayerResultDataHolder holder = player.GetComponent<CS_PlayerResultDataHolder>();
            if (holder == null) continue;

            int score = GetTotalScore(holder);
            highest = Mathf.Max(highest, score);
            lowest = Mathf.Min(lowest, score);
            found = true;
        }

        return found;
    }

    // 合計点(CS_ResultData.totalScoreと同じく、各項目の合計を0未満にしない)
    private static int GetTotalScore(CS_PlayerResultDataHolder holder)
    {
        CS_ResultData.Score score = holder.score;
        return Mathf.Max(0, score.defeatVillainScore + score.foundByPoliceScore + score.crimeCompletedScore + score.defeatPlayerScore);
    }
}
