using System.Collections.Generic;
using UnityEngine;

/*
 * エフェクトの設定をIDごとに管理するデータベース
 * InspectorではList、実行時はDictionaryとして使う(SEのCSO_SeDataBaseと同じ形)
 *
 * 制作者：　吉田京志郎
 */

// ========================================
/*
 * メモ
 * ・アセットは Assets/Programmer/Database/Effect/DB_EffectDataBase.asset
 * ・本番の素材ができたら、ここのPrefabを差し替えるだけで、ゲーム中に出るエフェクトが入れ替わる
 * ・同じIDが2つある時は、先に書いた方を使う(警告を出す)
 */
// ========================================

[CreateAssetMenu(fileName = "DB_EffectDataBase", menuName = "Effect/Effect Database")]
public class CSO_EffectDataBase : ScriptableObject
{
    [Header("エフェクトデータ一覧")]
    [SerializeField] private List<CS_EffectData> _effectList = new();

    private Dictionary<CSE_EffectId, CS_EffectData> _effectTable;

    // 指定したIDのデータを取得する(登録がなければfalse)
    public bool TryGetData(CSE_EffectId id, out CS_EffectData data)
    {
        if (_effectTable == null) BuildTable();

        return _effectTable.TryGetValue(id, out data);
    }

    private void BuildTable()
    {
        _effectTable = new Dictionary<CSE_EffectId, CS_EffectData>();

        foreach (CS_EffectData effectData in _effectList)
        {
            if (effectData == null) continue;

            if (effectData.id == CSE_EffectId.None)
            {
                Debug.LogWarning("[CSO_EffectDataBase] Noneが設定されたエフェクトデータがあります。", this);
                continue;
            }

            if (_effectTable.ContainsKey(effectData.id))
            {
                Debug.LogWarning($"[CSO_EffectDataBase] エフェクトIDが重複しています：{effectData.id}", this);
                continue;
            }

            _effectTable.Add(effectData.id, effectData);
        }
    }

    // Inspectorで編集した時は、次に取得する時にDictionaryを作り直す
    private void OnValidate()
    {
        _effectTable = null;
    }
}
