using System;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using McpUnity.Unity;
using UnityEditor;
using UnityEngine;
using WebSocketSharp.Server;

/*
 * MCP Unity(com.gamelovers.mcp-unity)のポートが、再生モードに入った後などに解放されず
 * 「Port 8090 is already in use」になり、Unityを再起動するまで直らない問題の回避
 * (上流のIssue #141。パッケージ側は直っていないため、プロジェクト側で回避する)
 *
 * 制作者：　秋野翔太
 */

// ========================================
/*
 * メモ
 * ・原因(Windowsのみ)
 *   UnityのMonoで作ったソケットは、子プロセスに引き継がれる(継承可能な)ハンドルになっている
 *   MCP Unityのサーバーが動いている間にUnityが子プロセス(AI Assistantのrelay_win.exe、AssetImportWorkerなど)を起動すると、
 *   待ち受けソケットのハンドルが子プロセスにも渡る
 *   その後、再生モードに入る時などにMCP Unityがサーバーを止めても、子プロセスがハンドルを持ったままなので
 *   ポートが解放されず、ドメインリロード後の再起動に失敗する(接続は受け付けるが誰も応答しない状態になる)
 *   子プロセスはUnityを終了するまで残るため、Unityを再起動するまで直らない
 *   (relay_win.exeを終了したらポートが解放されることを確認済み)
 * ・回避方法
 *   MCP Unityのサーバーが待ち受けを始めたら、待ち受けソケットのハンドルを継承しない設定に変える
 *   (SetHandleInformationでHANDLE_FLAG_INHERITを外す)
 *   サーバーは再起動のたびに作り直されるので、毎フレーム見て新しいソケットが出来たら設定する
 * ・MCP Unityの中のフィールド(McpUnityServer._webSocketServer、WebSocketServer._listener)をリフレクションで読んでいる
 *   パッケージを更新して名前が変わった場合は、警告を1回だけ出して何もしない
 */
// ========================================

[InitializeOnLoad]
public static class CSED_McpUnityPortReleaseGuard
{
    private const uint HandleFlagInherit = 0x00000001;

    private static readonly FieldInfo s_serverField =
        typeof(McpUnityServer).GetField("_webSocketServer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo s_listenerField =
        typeof(WebSocketServer).GetField("_listener", BindingFlags.Instance | BindingFlags.NonPublic);

    private static IntPtr s_lastHandle = IntPtr.Zero;   // 継承しない設定にした最後のソケット
    private static bool s_warned;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

    static CSED_McpUnityPortReleaseGuard()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor) return;
        if (Application.isBatchMode) return;

        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    private static void HandleUpdate()
    {
        Socket socket = GetListenerSocket();
        if (socket == null) return;

        IntPtr handle = socket.Handle;
        if (handle == s_lastHandle) return;

        s_lastHandle = handle;
        if (!SetHandleInformation(handle, HandleFlagInherit, 0))
        {
            WarnOnce($"ソケットを継承しない設定にできませんでした(エラー {Marshal.GetLastWin32Error()})");
        }
    }

    // MCP Unityの待ち受けソケット(待ち受けていない時はnull)
    private static Socket GetListenerSocket()
    {
        if (s_serverField == null || s_listenerField == null)
        {
            WarnOnce("MCP Unityの中身が変わったため、ポートの回避処理を行いません");
            return null;
        }

        try
        {
            McpUnityServer server = McpUnityServer.Instance;
            if (server == null || !server.IsListening) return null;

            WebSocketServer webSocketServer = s_serverField.GetValue(server) as WebSocketServer;
            TcpListener listener = webSocketServer != null ? s_listenerField.GetValue(webSocketServer) as TcpListener : null;
            return listener?.Server;
        }
        catch (Exception ex)
        {
            WarnOnce(ex.Message);
            return null;
        }
    }

    private static void WarnOnce(string message)
    {
        if (s_warned) return;

        s_warned = true;
        Debug.LogWarning($"[CSED_McpUnityPortReleaseGuard] {message}");
    }
}
