using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MS2027.EditorTools
{
    // 旧実装で差し替えたメニューを、この移行時に一度だけUnity標準へ戻す。
    // 通常の作成時・再コンパイル時にはメニューを変更しない。
    [InitializeOnLoad]
    internal static class CSED_DefaultScriptPrefix
    {
        private const string RestoredKey = "MS2027.NativeCreateMenusRestored.v1";

        static CSED_DefaultScriptPrefix()
        {
            SessionState.EraseString("MS2027.DefaultScriptPrefix.Pending");
            if (!SessionState.GetBool(RestoredKey, false))
                EditorApplication.delayCall += RestoreOnce;
        }

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
