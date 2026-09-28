using UnityEditor;
using UnityEngine;

/*
 * Player.prefabにCS_PlayerHpUIを追加するエディタ拡張
 * Tools/Player/HP UIを組み込む から実行する(付いていれば何もしない)
 *
 * 制作者：　秋野翔太
 */

public static class CSED_PlayerHpUIInstaller
{
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";

    [MenuItem("Tools/Player/HP UIを組み込む")]
    public static void Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            if (root.GetComponent<CS_PlayerHpUI>() == null)
            {
                root.AddComponent<CS_PlayerHpUI>();
                PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
                Debug.Log("CSED_PlayerHpUIInstaller: CS_PlayerHpUIを追加しました");
            }
            else
            {
                Debug.Log("CSED_PlayerHpUIInstaller: すでに追加済みです");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
