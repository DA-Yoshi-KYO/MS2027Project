# Unity .meta 自動生成フロー

デザイナーが Unity を開かずに素材を追加できるよう、GitHub Actions で `.meta` を自動生成する。
デザイナーの操作は **`DesignerDevelop` で「素材配置 → commit → push」だけ**。

## ブランチ

| ブランチ | 用途 |
|---|---|
| `DesignerDevelop` | デザイナーが作業するブランチ |
| `DesignerOutput` | `.meta` 生成済みのデータを持つ倉庫ブランチ(フルプロジェクト) |
| `develop` | 本流。管理者が `DesignerOutput` から PR でマージする |

## 全体フロー

```
[デザイナー] Assets/Designer 以下に出力 → DesignerDevelop へ commit & push
   ↓
[Sync Designer Assets to Output]  .github/workflows/designer-sync-output.yml
   Assets/Designer の実体ファイルだけを DesignerOutput へミラー(.meta は触らない)
   実体が消えたファイルの孤児 .meta を削除 → 変更があれば push
   ↓ (完了を workflow_run で検知)
[Generate Designer Meta Files]   .github/workflows/designer-generate-meta.yml
   .meta が無いファイルがあるか事前チェック → 無ければ Unity を起動せず終了
   あれば LFS を取得し Unity batchmode(game-ci/unity-builder)でインポート
   生成された .meta だけを commit & push
   ↓
[管理者] DesignerOutput → develop へ PR & マージ
```

### ポイント

- `.meta` は `DesignerOutput` 側で一元管理する。既存ファイルの上書きでは `.meta`(GUID)が変わらないので参照が壊れない。
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
git push origin origin/develop:refs/heads/DesignerDevelop
git push origin origin/develop:refs/heads/DesignerOutput
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
`DesignerOutput` にブランチ保護を掛ける場合は GitHub Actions の push を許可すること。

### 4. 動作確認

`DesignerDevelop` にテスト用の素材を push し、Actions タブで2つのワークフローが順に成功し、
`DesignerOutput` に素材と `.meta` が入ることを確認する。

## 注意・制約

- 初回は Library が無いためフルインポートになり時間がかかる(HDRP のため数十分かかる可能性あり)。2回目以降は Library キャッシュが効く。
- `.meta` 生成時は LFS 実体をダウンロードするため、GitHub LFS の帯域を消費する(.meta 不足が無い場合はダウンロードしない)。
- game-ci の Docker イメージは `ProjectSettings/ProjectVersion.txt`(現在 6000.3.23f1)から自動選択される。該当バージョンのイメージが無い場合は `unityVersion` を明示する必要がある。
- デザイナーが Unity で作った `.meta` を `DesignerDevelop` に commit しても同期されない(`DesignerOutput` 側の `.meta` が正)。
- `DesignerDevelop` を develop に追従させる場合、develop の `Assets/Designer` が DesignerDevelop と異なると同期時に上書き・削除されるので注意。
