/* ================================================
 * 
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-09-22 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アイテムをフィールド上に生成するクラス
/// </summary>
public class CS_ItemGenerator : MonoBehaviour
{
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
        // シーンに存在する分だけ生成する。
        int generateNum = Mathf.Min(_initGenerateNum, _itemGeneratePoints.Length);
        if (_initGenerateNum > _itemGeneratePoints.Length)
            Debug.LogWarning("初期生成する数がシーンに存在するアイテム召還用ポイントの数を上回っています。\n シーンに存在する分だけ生成します。");

        for (int i = 0; i < generateNum; ++i)
        {
            GenerateToRandomPoint(_initGenerateItemKinds[Random.Range(0, _initGenerateItemKinds.Length)]);
        }
    }

    /// <summary>
    /// シーンに存在する生成位置候補からランダムに選ばれた位置にアイテムを生成する
    /// </summary>
    /// <param name="item">生成するアイテム</param>
    /// <returns>生成に成功したかどうか</returns>
    public bool GenerateToRandomPoint(CS_ItemBase item)
    {
        if (_itemGeneratePoints.Length <= 0)
        {
            Debug.LogError("ポイントが存在しません");
            return false;
        }

        // 既に生成されているアイテム生成位置をリストから除外する
        List<CS_ItemGeneratePoint> pointList = new List<CS_ItemGeneratePoint>(_itemGeneratePoints);
        pointList.RemoveAll(point => point.item != null);
        if (pointList.Count <= 0)
        {
            Debug.LogError("ポイントが全て生成済みです");
            return false;
        }

        // 除外した後の生成位置候補からランダムに決定し、アイテムを生成する
        var point = pointList[Random.Range(0, pointList.Count)];
        Instantiate(item.gameObject, point.transform);
        point.item = item;

        return true;    //　生成に成功
    }

    /// <summary>
    /// 指定の位置にアイテムを生成する
    /// </summary>
    /// <param name="item">生成するアイテム</param>
    /// <param name="spawnPoint">生成する位置</param>
    /// <returns>生成に成功したかどうか</returns>
    public bool Generate(CS_ItemBase item, Transform spawnPoint)
    {
        Instantiate(item.gameObject, spawnPoint);

        return true;
    }
}
