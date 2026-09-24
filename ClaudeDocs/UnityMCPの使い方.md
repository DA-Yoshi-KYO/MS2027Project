# 📋 概要

Claude Code(や Cursor、Codex CLI などのAIエージェント)から **Unity Editorを直接操作**できるようにする、サードパーティ製MCP(Model Context Protocol)サーバーです。

- 使用ライブラリ: [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity)(Unity公式のMCPではありません)
- できること: シーン内GameObjectの作成・移動・削除・コンポーネント編集、マテリアル作成、シーンの作成/読み込み/保存、メニュー項目の実行、Test Runnerの実行、コンソールログの取得、スクリプトの再コンパイルなど

「Unityの単純作業をAIに指示してやらせたい」「AIにシーンの状態を見ながらデバッグしてほしい」といった場面で使います。

# ✍️ 事前準備 (最初の1回だけ)

## 1. Node.js をインストール

- バージョン18以上が必要です。[nodejs.org](https://nodejs.org/) からLTS版をインストールしてください
- 確認:
  ```bash
  node --version
  ```

## 2. Unityパッケージを追加する

`Packages/manifest.json` に以下の1行が入っていればOKです(develop/該当ブランチに既にマージされていればこの手順は不要です)。

```json
"com.gamelovers.mcp-unity": "https://github.com/CoderGamester/mcp-unity.git",
```

まだ入っていない場合は手動で追加します。

1. Unity Editorで **Window > Package Manager** を開く
2. 左上の「+」→「Add package from git URL...」
3. `https://github.com/CoderGamester/mcp-unity.git` を入力して「Add」

Unity Editorがフォーカスされると自動でパッケージが解決されます(GitHubからの取得のため、初回はUnity側でインターネット接続とgitが使える必要があります)。

> 対応Unityバージョン: Unity 6以降

## 3. UnityのMCPサーバーを起動する

1. **Tools > MCP Unity > Server Window** を開く
2. 「Start Server」をクリック(デフォルトポート `8090` でWebSocketサーバーが起動します)

`ProjectSettings/McpUnitySettings.json` の `AutoStartServer` が `true` になっていれば、Unity起動時に自動でサーバーも立ち上がります。

## 4. Claude Code側の設定

同じウィンドウ内の「Configure Claude Code (Project)」ボタンを押すと、プロジェクト直下に `.mcp.json` が自動生成されます。

```json
{
   "mcpServers": {
       "mcp-unity": {
          "command": "node",
          "args": [
             "Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"
          ],
          "env": {
             "MCP_UNITY_SETTINGS_PATH": "<絶対パス>/ProjectSettings/McpUnitySettings.json",
             "MCP_UNITY_AUTH_TOKEN_PATH": "<絶対パス>/Library/McpUnity/bridge-token"
          }
       }
   }
}
```

`<hash>` はパッケージ解決時に決まる値なので、Package Managerの解決状況によって変わります。**この `.mcp.json` は個人環境ごとに絶対パスが変わるため、リポジトリにコミットする場合は各自の環境で上書き生成し直してください。**

設定が終わったら **Claude Codeを再起動(新規セッションを開始)**してください。`.mcp.json` はセッション開始時に読み込まれるため、既に開いているセッションには反映されません。

# ▶️ 使い方

セットアップ後、新しく開いたClaude Codeのセッションで `mcp__mcp-unity__*` という名前のツールが使えるようになります。

例:
- 「シーン内のGameObjectを一覧にして」→ `get_scenes_hierarchy` / `get_gameobject`
- 「〇〇にCubeを追加して」→ `execute_menu_item` / `add_asset_to_scene`
- 「EditModeテストを実行して結果を見せて」→ `run_tests`
- 「コンソールのエラーログを見せて」→ `get_console_logs`
- 「マテリアルの色を変えて」→ `modify_material`

普段は日本語の指示だけで問題ありません。裏側でどのツールが呼ばれるかはClaude側が判断します。

## Unity Editorを開いている必要があります

このMCPはUnity Editorの実行中プロセスとWebSocketで通信します。**Unity Editorが起動していて、対象プロジェクトが開かれている状態でないと動作しません**(ビルド後の実行ファイルには接続できません)。

# ⚠️ セキュリティ上の注意

`ProjectSettings/McpUnitySettings.json` に以下の設定項目があります。**信頼できる環境以外ではデフォルト(false)のままにしてください。**

| 項目 | 内容 | デフォルト |
|---|---|---|
| `AllowPackageInstallation` | AIがPackage Manager経由で任意のパッケージ(git URL含む)をインストールできるようにする | `false` |
| `AllowRemoteConnections` | WebSocketサーバーを `localhost` 以外(`0.0.0.0`)にバインドし、他のPCからも接続できるようにする | `false` |

また `Library/McpUnity/bridge-token` にプロジェクトごとの認証トークンが生成されます。`Library/` はgitignore対象なのでコミットされませんが、他人と共有しないでください。

# 🆘 困ったときは (トラブルシューティング)

| 症状 | 原因 / 対処 |
|---|---|
| Package Managerでパッケージが解決されない | Unity Editorをフォーカスし直す、またはPackage Managerウィンドウを開き直して再解決を促す。社内ネットワークでGitHubへのアクセスが必要 |
| `get_scene_info` などツール呼び出し直後に `Not started - call start() first` と返る | 接続確立直後の一時的な状態です。数秒待ってからもう一度呼び出すと成功します |
| ツールが1件も出てこない/`mcp__mcp-unity__*` が使えない | `.mcp.json` 作成後にClaude Codeのセッションを再起動していない可能性が高いです。新しいセッションを開始してください |
| Unity側のサーバーが繋がらない | **Tools > MCP Unity > Server Window** でサーバーが起動中(緑色表示)か確認してください。ポート `8090` が他のプロセスに使われていないかも確認 |
| プロジェクトパスにスペースが含まれる環境で接続不安定 | パスにスペースのない場所へプロジェクトを移動することが推奨されています(Windows特有の既知の注意点) |

# 🛠️ 参考

- 本体リポジトリ: https://github.com/CoderGamester/mcp-unity
