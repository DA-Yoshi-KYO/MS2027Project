/* ================================================
 *
 * ================================================
 * 制作者：吉田京志郎
 * ------------------------------------------------
 * 2026-10-29 | 初回作成(Issue #342)
 * ================================================ */

using System.Collections.Generic;

// ========================================
/*
 * メモ
 * ・エリア選択シーン(AreaSelect)で決まった開始エリアを、ゲームシーンまで持ち越すためのクラス
 *   CS_GameModeStoreと同じく、シーンをまたいで残るようにstaticで持つ
 * ・書き込むのはCS_AreaSelectController、読むのはCS_PlayerSpawner(どちらもサーバーだけ)
 *   キーはクライアントID(オフラインの時はNetworkManager.ServerClientIdと同じ0を使う)
 * ・エリア選択を通らずにゲームシーンを始めた時(デバッグ用の起動シーンから始めた時など)は何も入っていないので、
 *   CS_PlayerSpawnerは全員をランダムなエリアから生成する
 *   ゲームシーンを直接再生した時はソロになり、ソロの開始地点(Field_Solo1のPlayerSpawner)から生成する
 */
// ========================================
public static class CS_SpawnAreaStore
{
    private static readonly Dictionary<ulong, int> _areaByClient = new Dictionary<ulong, int>();

    public static void SetArea(ulong clientId, int area)
    {
        _areaByClient[clientId] = area;
    }

    /// <summary>
    /// 選んだエリアを取り出す。選んでいなければfalse
    /// </summary>
    public static bool TryGetArea(ulong clientId, out int area)
    {
        return _areaByClient.TryGetValue(clientId, out area);
    }

    public static void Clear()
    {
        _areaByClient.Clear();
    }
}
