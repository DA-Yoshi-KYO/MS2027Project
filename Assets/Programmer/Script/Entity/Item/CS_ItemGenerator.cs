/* ================================================
 * 
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using UnityEngine;

/// <summary>
/// アイテムをフィールド上に生成するクラス
/// </summary>
public class CS_ItemGenerator : MonoBehaviour
{
    // 初期生成する数
    [SerializeField][Tooltip("初期生成するアイテムの数")][Min(0)] private int _initGenerateNum = 0;      // 初期生成するアイテムの数
    [SerializeField][Tooltip("初期生成するアイテムの候補")] private CS_ItemBase[] _initGenerateItemKinds;    // 初期生成するアイテムの候補
    private CS_ItemGeneratePoint[] _itemGeneratePoints; // アイテム生成用のポイント

    void Awake()
    {
        // シーンに存在するアイテム召還用のポイントを取得する
        var generatePointObjects = GameObject.FindGameObjectsWithTag("ItemGeneratePoints");
        if (generatePointObjects.Length <= 0) 
            return;   // シーンにポイントが存在しない場合処理を飛ばす

        _itemGeneratePoints = new CS_ItemGeneratePoint[generatePointObjects.Length];
        for (int i = 0; i < generatePointObjects.Length; ++i)
        {
            _itemGeneratePoints[i] = generatePointObjects[i].GetComponent<CS_ItemGeneratePoint>();
        }

        // アイテムを初期から生成する
        if (_initGenerateItemKinds.Length <= 0) 
            return; // 生成するアイテムの候補がない場合処理を飛ばす

        // 初期生成するアイテムの数がシーンに存在するアイテム召還用ポイントの数を上回っている時、
        // 
        int generateNum = Mathf.Min(_initGenerateNum, _itemGeneratePoints.Length);
        if (_initGenerateNum > _itemGeneratePoints.Length)
            Debug.LogWarning("初期生成する数がシーンに存在するアイテム召還用ポイントの数を上回っています。");
        
        for (int i = 0; i < generateNum; ++i)
        {

        }
    }

    public bool GenerateToRandomPoint(CS_ItemBase item, Transform spawnPoint)
    {
        return true;
    }

    public bool Generate(CS_ItemBase item, Transform spawnPoint)
    {
        return true;
    }
}
