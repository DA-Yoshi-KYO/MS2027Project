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
    CS_ItemRegister[] itemSpawnPoints;

    public bool GenerateRandomPoint(CS_ItemBase item, Transform spawnPoint)
    {
        return true;
    }

    public bool Generate(CS_ItemBase item, Transform spawnPoint)
    {
        return true;
    }
}
