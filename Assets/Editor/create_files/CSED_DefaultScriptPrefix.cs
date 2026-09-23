/* ================================================
 * 旧実装で変更した作成メニューを、Unity起動中に一度だけ標準へ戻す。
 * ================================================
 * 制作者：吉本竜
 * ------------------------------------------------
 * 2026-09-23 | 処理説明と日本語コメントを追加
 * ================================================ */
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MS2027.EditorTools
{
    // 旧実装で差し替えたメニューを、この移行時に一度だけUnity標準へ戻す。
    // 通常の作成時・再コンパイル時にはメニューを変更しない。
    /// <summary>
    /// 旧実装で変更した作成メニューを、Unity起動中に一度だけ標準へ戻す。
    /// </summary>
    [InitializeOnLoad]
    internal static class CSED_DefaultScriptPrefix
    {
        private const string RestoredKey = "MS2027.NativeCreateMenusRestored.v1";

        /// <summary>
        /// 旧実装の保留状態を消去し、今回のUnity起動中にまだ復元していなければ処理を予約する。
        /// </summary>
        static CSED_DefaultScriptPrefix()
        {
            SessionState.EraseString("MS2027.DefaultScriptPrefix.Pending");
            if (!SessionState.GetBool(RestoredKey, false))
                EditorApplication.delayCall += RestoreOnce;
        }

        /// <summary>
        /// Unity内部のメニュー再構築を呼び出し、復元済み状態を記録する。APIがない場合は再起動を案内する。
        /// </summary>
        private static void RestoreOnce()
        {
            var restore = typeof(Menu).GetMethod("RebuildAllMenus", BindingFlags.Static | BindingFlags.NonPublic);
            if (restore == null)
            {
                Debug.LogWarning("標準メニューの復元にはUnityを一度再起動してください。");
                return;
            }
            restore.Invoke(null, null);
            SessionState.SetBool(RestoredKey, true);
        }
    }
}
