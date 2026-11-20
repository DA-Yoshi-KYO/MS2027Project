# Unity .meta 自動生成フロー

デザイナーが Unity を開かずに素材を追加できるよう、GitHub Actions で `.meta` を自動生成する。
デザイナーの操作は **`designer_develop` で「素材配置 → commit → push」だけ**。

## ブランチ

| ブランチ | 用途 |
|---|---|
| `designer_develop` | デザイナーが作業するブランチ |
| `designer_output` | `.meta` 生成済みのデータを持つ倉庫ブランチ(フルプロジェクト) |
| `develop` | 本流。管理者が `designer_output` から PR でマージする |

## 全体フロー

```
[デザイナー] Assets/Designer 以下に出力 → designer_develop へ commit & push
   ↓
[Sync Designer Assets to Output]  .github/workflows/designer-sync-output.yml
   push で追加・変更・削除された Assets/Designer の実体ファイルだけを designer_output へ反映(.meta は触らない)
   (develop 側にだけあるファイルは消さない)
   実体が消えたファイルの孤児 .meta を削除 → 変更があれば push
   ↓ (完了を workflow_run で検知)
[Generate Designer Meta Files]   .github/workflows/designer-generate-meta.yml
   先に develop を designer_output へ取り込む(競合したらここで失敗して止まる)
   .meta が無いファイルがあるか事前チェック → 無ければ Unity を起動せず終了
   あれば LFS を取得し Unity batchmode(game-ci/unity-builder)でインポート
   生成された .meta だけを commit & push
   ↓
[管理者] designer_output → develop へ PR & マージ
```

### ポイント

- `.meta` を生成する前に `develop` を `designer_output` へ取り込む。develop 側に既にある `.meta`(GUID)を先に取り込むことで、同じファイルに別の GUID の `.meta` が作られて参照が切れるのを防ぐ。
- develop の取り込みで競合したら、ワークフローは失敗して止まる(自動では解決しない)。管理者が `designer_output` に `develop` を手動でマージして競合を解決し、`designer_output` へ push する。手動マージのコミット(`Merge ...`)の push では「作業完了」通知は飛ばない。
- `.meta` は `designer_output` 側で一元管理する。既存ファイルの上書きでは `.meta`(GUID)が変わらないので参照が壊れない。
- `GITHUB_TOKEN` による push は他のワークフローを起動しない仕様のため、生成ワークフローは同期ワークフローの**完了(workflow_run)**を契機にしている。同じ理由で無限ループも起きないので `[skip ci]` は付けていない(付けると develop への PR で CI がスキップされるため)。
- 2つのワークフローは `concurrency: designer-output` で直列化している。
- bot のコミットメッセージは `chore:` prefix(develop の commit-lint を通すため)。
- Unity が再シリアライズした `.meta` 以外のファイルはコミットしない。
- Unity 起動用のメソッドは `Assets/Editor/CI/DesignerMetaGenerator.cs`。

## 初期設定(管理者が1回だけ)

### 1. ブランチ作成

このワークフローが入った `develop` から作成する(ワークフローファイルは各ブランチ上に存在している必要がある)。

```bash
git fetch origin
git push origin origin/develop:refs/heads/designer_develop
git push origin origin/develop:refs/heads/designer_output
```

### 2. Unity ライセンスを Secrets に登録

Unity Personal は `.alf` を使った手動アクティベーション(license.unity3d.com/manual)が廃止されているため、
**ローカルで Unity Hub にログイン済みの PC の `.ulf` を使う**(game-ci の現行手順)。

1. Unity Hub で Personal ライセンスを有効化しておく
2. ライセンスファイルを開く
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
3. GitHub の **Settings → Secrets and variables → Actions** に登録

| Secret | 値 |
|---|---|
| `UNITY_LICENSE` | `Unity_lic.ulf` の中身全体 |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

### 3. Actions の書き込み権限

**Settings → Actions → General → Workflow permissions** が read-only の場合でも、ワークフロー側で `contents: write` を指定しているので通常は不要。
`designer_output` にブランチ保護を掛ける場合は GitHub Actions の push を許可すること。

### 4. 動作確認

`designer_develop` にテスト用の素材を push し、Actions タブで2つのワークフローが順に成功し、
`designer_output` に素材と `.meta` が入ることを確認する。

## 注意・制約

- 初回は Library が無いためフルインポートになり時間がかかる(HDRP のため数十分かかる可能性あり)。2回目以降は Library キャッシュが効く。
- `.meta` 生成時は LFS 実体をダウンロードするため、GitHub LFS の帯域を消費する(.meta 不足が無い場合はダウンロードしない)。
- game-ci の Docker イメージは `ProjectSettings/ProjectVersion.txt`(現在 6000.3.23f1)から自動選択される。該当バージョンのイメージが無い場合は `unityVersion` を明示する必要がある。
- デザイナーが Unity で作った `.meta` を `designer_develop` に commit しても同期されない(`designer_output` 側の `.meta` が正)。
- 同期は push の差分(追加・変更・削除)だけを反映するので、develop 側にだけあるファイルは消えない。`designer_develop` で消したファイルだけが `designer_output` からも消える。
- 手動実行(workflow_dispatch)の同期は追加・更新だけを反映し、削除はしない。

## Discord 通知

| タイミング | 送信元 | 通知 |
|---|---|---|
| `designer_develop` への `Artist/`(作業データ)の push | `discord-notify-push.yml` | デザイナー進捗チャンネルに「📝 作業進捗」+ コミット一覧 |
| `.meta` 生成まで完了した時 | `designer-generate-meta.yml` | デザイナー進捗チャンネルに「✅ 作業完了」+ 元の `designer_develop` のコミット一覧 + 担当者へのメンション |

- 「作業完了」のコミット一覧は、同期ワークフローが artifact として渡した `designer_develop` の push 内容を使う(bot の `chore:` コミットは出さない)。
- 同期で差分が無かった場合(同じ内容の上書き等)は「作業完了」は送らない。
- メンション先はワークフロー内の `MENTION_USER_ID` で変更できる。
