using UnityEngine;

/*
 * 必殺技を使った後、一定時間続く効果の種類(CSO_PlayableCharacterで選び、CS_PlayerCharacterが効かせる)
 * InspectorNameは、プランナー向けの表示名(調整ウィンドウ・Inspector)
 *
 * 制作者：　秋野翔太
 */

public enum CSE_PlayerSpecialEffect
{
    [InspectorName("なし")] None,                          // 効果なし(攻撃だけ)
    [InspectorName("正面から奇襲")] FrontalAmbush,          // 正面からでも奇襲できる(奇襲の「背後か上」の条件を見ない)。ギロ
    [InspectorName("身体能力の強化")] StatusBoost,          // 身体能力の強化(移動速度・スプリント速度・ジャンプ力・攻撃力・コンボの速さに倍率を掛ける)。アブ
    [InspectorName("全員が見える")] RevealAll,              // 全ての悪人グループ・警察・巡回ドローンが見え続ける(スキャンと同じ表示)。キース
    [InspectorName("警察に見つからない")] HiddenFromPolice, // 警察に見つからない(警察の視界に入っても見つからない)。イシュ
}
