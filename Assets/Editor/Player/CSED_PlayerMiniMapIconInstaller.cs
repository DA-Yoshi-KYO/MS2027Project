using UnityEditor;
using UnityEngine;

/*
 * Player.prefabにCS_PlayerMiniMapIconを追加するエディタ拡張
 * Tools/Player/ミニマップアイコンを組み込む から実行する(付いていれば何もしない)
 *
 * 制作者：　秋野翔太
 */

public static class CSED_PlayerMiniMapIconInstaller
{
    private const string _prefabPath = "Assets/Programmer/Prefab/Entity/Player/Player.prefab";

    [MenuItem("Tools/Player/ミニマップアイコンを組み込む")]
    public static void Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);

        try
        {
            if (root.GetComponent<CS_PlayerMiniMapIcon>() == null)
            {
                root.AddComponent<CS_PlayerMiniMapIcon>();
                PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
                Debug.Log("CSED_PlayerMiniMapIconInstaller: CS_PlayerMiniMapIconを追加しました");
            }
            else
            {
                Debug.Log("CSED_PlayerMiniMapIconInstaller: すでに追加済みです");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
