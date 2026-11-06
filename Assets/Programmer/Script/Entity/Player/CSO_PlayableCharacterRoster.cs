using System.Collections.Generic;
using UnityEngine;

/*
 * プレイアブルキャラクターの一覧(ScriptableObject)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・CS_PlayerCharacterがこの一覧の番号でキャラクターを決める(番号は全員に同期する)
 * ・キャラクター選択の画面はまだ無いので、今は全員が_testCharacterIndexのキャラクターになる
 *   選択画面を作る時は、スポーン前にCS_PlayerCharacter.SetCharacterIndex(サーバー)で番号を決める
 * ・プランナーは「Tools → プレイヤー調整」のウィンドウから編集する
 */
// ========================================

[CreateAssetMenu(fileName = "DB_PlayableCharacterRoster", menuName = "Player/Playable Character Roster")]
public class CSO_PlayableCharacterRoster : ScriptableObject
{
    [SerializeField] private List<CSO_PlayableCharacter> _characters = new List<CSO_PlayableCharacter>();
    [SerializeField][Min(0)] private int _testCharacterIndex;     // 選択画面が無い間、全員が使うキャラクターの番号

    public IReadOnlyList<CSO_PlayableCharacter> characters => _characters;
    public int count => _characters.Count;
    public int testCharacterIndex => Mathf.Clamp(_testCharacterIndex, 0, Mathf.Max(0, _characters.Count - 1));

    // 番号のキャラクター(範囲外・未設定ならnull)
    public CSO_PlayableCharacter Get(int index)
    {
        return index >= 0 && index < _characters.Count ? _characters[index] : null;
    }
}
