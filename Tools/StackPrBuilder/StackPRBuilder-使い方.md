# 概要

1つの大きなブランチ(PR)のコミットを、GUI操作だけで [GitHub Stacked PR](https://github.blog/changelog/2026-07-30-stacked-pull-requests-are-now-in-public-preview/) に分割するための社内ツールです。ターミナル操作はほぼ不要で、ボタンを順に押していくだけで使えます。

- 実行ファイル: `Tools/StackPrBuilder/exe/StackPrBuilder.exe`
- ソースコード: `Tools/StackPrBuilder/develop/`

1つのPRに複数の機能のコミットが混ざっていて大きくなってしまった時、レビューしやすいように develop ← PR1 ← PR2 ← PR3 … という**連鎖したPR(Stacked PR)**に自動で分割します。手動でやると「ブランチを切る→push→PRのbaseを1つずつ設定」という作業が必要ですが、このツールはそれを自動化します。

## 事前準備 (最初の1回だけ)

1. **.NET 8 ランタイム(x64)** をインストール
   - https://dotnet.microsoft.com/download/dotnet/8.0 → 「.NET Desktop Runtime 8.0.x (x64)」
   - Visual StudioやUnityでC#開発をしているPCなら既に入っていることが多いです
2. **GitHub CLI (`gh`)** をインストール: https://cli.github.com/
3. ログイン
   ```bash
   gh auth login
   ```
   対話形式で質問されるので、以下の通り選択してください(矢印キー+Enterで選択):

   | 質問 | 選ぶ内容 |
   |---|---|
   | Where do you use GitHub? | `GitHub.com` |
   | What is your preferred protocol for Git operations on this host? | `HTTPS` |
   | Authenticate Git with your GitHub credentials? | `Yes` |
   | How would you like to authenticate GitHub CLI? | `Login with a web browser` |

   最後にワンタイムコードが表示されます:
   ```
   ! First copy your one-time code: XXXX-XXXX
   Press Enter to open https://github.com/login/device in your browser...
   ```
   1. このコードを**コピー**しておく(そのまま次の操作でブラウザが開くと消えてしまうため、忘れずに)
   2. `Enter` キーを押すとブラウザで https://github.com/login/device が開く
   3. 開いたページの「Authorize your device」画面で、先ほどコピーしたコードを入力
   4. 「Continue」→「Authorize <組織/ユーザー名>」(緑のボタン)をクリック
   5. ターミナルに戻り、ログイン成功のメッセージが出ていればOK

4. **`gh-stack` 拡張**をインストール
   ```bash
   gh extension install github/gh-stack
   ```
   確認:
   ```bash
   gh extension list
   ```
   `gh stack   github/gh-stack   vX.X.X` と表示されればOK

> これらが揃っていない場合、アプリ起動時に自動でチェックされ、何が足りないかログに日本語で表示されます。

## 使い方

`Tools/StackPrBuilder/exe/StackPrBuilder.exe` をダブルクリックして起動します。

### 1. リポジトリ・ブランチを指定

| 項目 | 内容 |
|---|---|
| リポジトリパス | 起動時に自動検出されます(見つからなければ手入力) |
| Base | 分岐元のブランチ。通常 `develop` |
| 分割対象ブランチ | 分割したい元の大きなPRのブランチ名 |

「現在のPR一覧を表示」ボタンでオープン中のPRを確認することで、分割対象ブランチを絞ることをお勧めします。

### 2. コミット読み込み

**「① コミット読み込み」**を押すと、自動で `git fetch` してからコミット一覧を古い→新しい順に表示します。

### 3. (任意) コミットの並び替え

順番を変えたい場合、行を選択して **「↑ / ↓」**で並び替えたあと、**「並び替えを確定」**を押します。

- 表示中の順序で1つずつ cherry-pick して**コミットを実際に作り直します**(SHAが変わります)
- コンフリクトが起きた場合は自動解決せず、その場で中断してどのコミットで失敗したか表示します
- **作業ツリーを一時的にbaseへ切り替えて処理する**ので、未コミットの変更が無い状態で実行してください
- Unity Editorを開いたままだと再インポートが走る可能性があるため、閉じてからの実行を推奨します

不要ならこの手順はスキップしてOKです。

### 4. レイヤー(=PRの区切り)を決める

コミット一覧の「レイヤー番号」列に、そのコミットがどのPRに属するかを数字で入力、編集します。

例: `PR1(コミットA) / PR2(コミットB) / PR3(コミットC,D,E)` に分けたい場合

| コミット | レイヤー番号 |
|---|---|
| A | 1 |
| B | 2 |
| C | 3 |
| D | 3 |
| E | 3 |

**番号は表示順で増加方向のみ**(1→2→3…)。並び替えではなく区切りを入れるだけなので、`3→2` のように戻すことはできません(必要ならステップ3で先に並び替えてください)。

入力したら**「② レイヤー計算」**を押すと、下にレイヤーごとのブランチ名候補が表示されます。

ブランチ名は `stacked/大要素/要素` の形式です。

- `stacked/` は固定・編集不可（後述のRulesetにより `stacked/**` 以外のブランチ作成が禁止されているため）
- **大要素**: このスタック全体の分類名(例: `PlayerStatus`)。「② レイヤー計算」の横の入力欄に入力し「全レイヤーへ一括反映」を押すと、全レイヤーへまとめて設定できる
- **要素**: 各レイヤー固有の内容(例: `BaseCreate`)。デフォルトは `layer-N` だが自由に編集可

> **注意1**: このリポジトリではブランチ作成が `stacked/**` という名前だけ許可されています(Repository Ruleset)。それ以外の名前で作成しようとすると push 時に `Cannot create ref due to creations being restricted` というエラーになります。
>
> **注意2**: 大要素・要素の入力欄は**半角英数字・ハイフン・アンダースコアのみ**入力できます(IME自体が無効化されており、日本語入力はできません)。git のブランチ名として安全な文字に強制するためです。

### 5. Stack作成

**「③ Stack作成」**を押すと、以下が自動で実行されます。

1. 各レイヤーの境目にローカルブランチを作成
2. 各ブランチを `git push`
3. `gh stack init` でスタックとして一括登録
4. `gh stack submit` でPRを作成し、スタックとして連結

チェックボックス「Stack作成成功時に分割元PRをクローズする」がONの場合、成功後に**分割元の大きなPRへ自動でコメントを付けてクローズ**します。

### 6. 下位レイヤーが修正されたとき

**「Sync (自動リベース連鎖)」**を押すと、下位レイヤーの変更が上位レイヤーへ自動でリベースされ、GitHub上のPRも更新されます。

### 7. レビューが揃ったら

**「Merge All (一括マージ)」**を押すと、スタック内の全PRが1回の操作でまとめてマージされます。

> **このボタンはリポジトリ管理者(Admin権限)のみ実行できます。** 押すとまず実行者のGitHub権限を自動確認し、Adminでない場合は「一括マージはリポジトリ管理者のみ実行できます」というダイアログが出てキャンセルされます。管理者以外がまとめてマージしたい場合は、リポジトリのAdmin権限を持つ人に依頼してください。

## 困ったときは (トラブルシューティング)

| 症状 | 原因 / 対処 |
|---|---|
| `対象branchが見つかりません` | ローカルのリモート追跡ブランチが古いだけです。「Fetch」ボタンを押してから再度読み込んでください(「①コミット読み込み」は自動fetch付きなので通常は発生しません) |
| push時に `Cannot create ref due to creations being restricted` | ブランチ名がRuleset違反です。`stacked/` から始まる名前になっているか確認してください |
| `gh stack is available as an official extension. To install it, run: gh extension install github/gh-stack` | そのPCに `gh-stack` 拡張が入っていません。「事前準備」の手順4を実行してください |
| `ブランチが既に存在します` | 前回の実行が途中で失敗した状態です。既に同じコミットを指しているブランチであれば、そのまま**もう一度「③ Stack作成」を押せば自動でスキップして再開**されます(別のコミットを指している場合はエラーメッセージに従って手動確認してください) |
| 起動直後に「前提条件が揃っていません」と出る | ログに表示された対応手順(gh未インストール/未ログイン/拡張未インストール等)にひとつずつ対応してください |
| 「一括マージはリポジトリ管理者のみ実行できます」 | あなたのGitHub権限がAdminではありません。リポジトリ管理者に依頼するか、個別のPRを1つずつ手動でマージしてください |

## 開発者向け情報

ソース改造・再ビルド方法は [`Tools/StackPrBuilder/develop/README.md`](../Tools/StackPrBuilder/develop/README.md) を参照してください。
