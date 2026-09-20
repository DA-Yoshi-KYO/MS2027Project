# Script Surveillance

コードエディター内で、問題を赤の波線、コーディング規約を黄色の波線で表示するIDE専用Roslynアナライザーです。
Unityの再生・ビルドには追加診断を渡しません。IDE側からビルドした場合は赤の診断がエラーになります。

## 使い方

1. Unityに戻りスクリプトのインポート完了を待ちます。既存の `.csproj` は自動設定されます。
2. Unityのプロジェクト／ソリューションをコードエディターで開き直します。
3. 設定をやり直す場合は `Tools > Script Surveillance > コードエディターの解析を設定`。
   プロジェクトが未生成の場合は、先にUnityからC#ファイルを開いてください。

Visual Studio、Rider、C#のRoslyn解析が有効なVS Codeなど、プロジェクトのAnalyzer参照を扱える環境が必要です。
単独のC#ファイルを開くだけでは動作しません。波線の配色はエディターのテーマによって変わります。
VS CodeはC#拡張機能、旧OmniSharpはRoslyn解析の有効化が必要です。
未使用フィールドは開いているファイルの解析でも検出します（ソリューション全体の解析設定は不要）。
アナライザーDLL更新後も古い結果が残る場合は、作業を保存してVisual Studioを再起動してください。

## 検査内容

| ID | 色 | 対象 |
| --- | --- | --- |
| MS27001 | 赤 | 未使用using、未使用ローカル変数、未使用privateフィールド（CS8019・CS0168・CS0219・CS0169・CS0414） |
| MS27002 | 赤 | setter内の自分自身への直接代入による無限再帰 |
| MS27101 | 黄 | 変数・引数のlowerCamelCase、private/protectedフィールドの `_` 接頭辞 |
| MS27102 | 黄 | CS_/CSO_/CSV_/CSED_/CSE_ のファイル名接頭辞 |
| MS27103 | 黄 | MonoBehaviourのUpdate/LateUpdate/FixedUpdate内に直接書かれたUnityのFind系・Debug.Log系呼び出し |
| MS27104 | 黄 | 型800行、メソッド・コンストラクター・アクセサー・ローカル関数80行、制御構文3重の目安を超えるコード |
| MS27105 | 黄 | publicフィールド（constを除く）。privateフィールドとプロパティでの公開を促す |

構文エラー・型の不一致などはC#本来の赤い診断が引き続き表示されます。
規約の出典: [プロジェクトWiki](https://github.com/DA-Yoshi-KYO/MS2027Project/wiki/ルール:-コーディング規則)（2026-09-20確認）。
Wiki内のsetter例 `set => playerHP = value` は自己再帰になるため、正しくは `set => _playerHP = value` です。
命名は本文のlowerCamelCaseを採用し、例にある `_HPValue` も `_hpValue` を推奨します。

## 判定範囲・限界

- `Assets` のC#が対象。Packages、Plugins、TutorialInfo、自動生成コードを除外します。
- `[SerializeField]`・`[SerializeReference]` を含む属性付きフィールド、Serializable型のフィールドは未使用エラーから除外します。
- 別partialファイルの参照、using alias・拡張メソッドはC#の意味解析で判断します。
- 公開メンバー・未使用引数・副作用を持つ初期化など、コンパイラーが未使用と断定しない宣言は赤にしません。
- リフレクションによる属性なしフィールドの使用は検出できません。意図的なものは該当箇所で `#pragma warning disable MS27001` / `restore` を使用してください。
- FindやLogの間接呼び出し・コールバック内、プロパティ経由の再帰、全種類の危険コードを網羅するものではありません。
- コメントの分かりやすさ、AIコードの理解、英語の意味・スペル、責任分割などは人によるレビューが必要です。
- プロパティの式形式／バッキングフィールドの完全性、C#以外のアセット名はこの版では検査しません。
- Editor判定はEditorフォルダーまたはEditor/EditorWindow継承。Editor専用asmdefだけで分類する特殊な配置は未対応です。
- ファイル接頭辞は最初の型で判定します。メソッドを経由した検索や条件付きログも含め実行頻度の厳密な解析はしません。

## 保守

配布DLLを同梱しているため、通常の利用に.NET SDKやNuGetは不要です。
`Source~` はUnityがインポートしない開発用フォルダーです。
.NET 9 SDKで `Source~/Build.ps1` を実行すると、復元・ビルド・回帰テスト後に配布DLLを更新します。
初回ビルドにはNuGetへの接続が必要です。Microsoft.CodeAnalysis.CSharp 3.8.0 / netstandard2.0を使用します。
DLLのPluginImporterは全プラットフォーム無効、RoslynAnalyzerラベルも付けません。
`CSED_SurveillanceProject.cs` がIDE用Analyzer参照を設定し、Unityによるプロジェクト再生成後も設定を復元します。
