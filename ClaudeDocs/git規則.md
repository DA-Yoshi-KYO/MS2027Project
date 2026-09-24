# 📋 概要
このページはgitの取り扱いやルールを明文化したものです。  

# 💻 想定環境
**[GitHubDesktop](https://desktop.github.com/download/)を使用します。**  
~~SourceTree must die.~~  
リポジトリの招待を受け取ったらローカル上にクローンしましょう。  

# ✅ gitルール一覧
## **ブランチはdevelopから切りましょう**  
ブランチは**必ず**developから切り、developにPR(Pull Request)を送りましょう。  
間違えてmainに送った場合は自動的にRejectされるようになっていますが、その他ブランチへのPRもRejectします。
## **ブランチ名は「名前/実装内容」としましょう**  
進捗確認の際にメンバーの実装内容を分かりやすくする為です。  
このルールに沿わないブランチは作成できません。  
例:吉田京志郎/プレイヤーの移動処理
## **mainへのpush、PRはしないようにしましょう** -  
設定で不可になっています。  
## **Prefixを守りましょう**  
コミットのSummaryを記述する際はPrefix(接頭辞)を付けて、`(Prefix): (実装内容)`としましょう。  
また、:の後に半角空白を空けるようにしてください。  

| Prefix | 意味 | 例 |
|:--|:-- |:--|
| feat | 新機能 | feat: プレイヤーの移動処理を追加 |
| update | 仕様変更 | update: プレイヤーの移動速度を100に変更 |
| fix | バグ修正 | fix: プレイヤーの当たり判定が小さくなるバグを修正 |
| refactor | リファクタリング | refactor: Player.csのコメントアウトを追加 |

もし間違えてしまった時は、該当のCommitで右クリックしてAmmendCommitを選択してコミットメッセージを書き換えてください。  
<img width="800" height="450" alt="image" src="https://github.com/user-attachments/assets/e99f8ed4-943d-4ab9-b8cd-bf357440378b" />  

※gitアプリが入っている場合は、prefixの不一致がある場合コミット時に警告が出されます

## **更新要素が大量にある場合はStackedPRを活用しましょう**
敵Aの実装、  敵Bの更新、敵Cの削除、敵Dの更新...など、それごとにブランチを切ると作業者が面倒だと思います。  
しかし、1つのブランチにまとめてPRを送ると、今度はレビューが面倒になります。  
そこで要素ごとにコミットし、StackedPRを送ることでレビューを容易にしましょう。  
[参考Wiki](https://github.com/DA-Yoshi-KYO/MS2027Project/wiki/%E3%83%84%E3%83%BC%E3%83%AB:-Stack-PR-Builder%E3%81%AE%E4%BD%BF%E3%81%84%E6%96%B9)

## PRを送った後は各種項目が入力されているか確認しましょう
* Assigness - 自分がアサインされていることを確認してください。(自動入力されます)
* Labels - 実装時は「実装依頼」、バグの修正時は「バグ修正」のラベルを付けてください。
* Projects - MS2027が入っており、StartDateがブランチを切った日、EndDateがPRを送った日になっていることを確認してください。(自動入力されます)
* Milestone - 現在のマイルストーンが入力されているか確認してください。(自動入力されます)
<img width="800" height="450" alt="image" src="https://github.com/user-attachments/assets/f1e2aa33-7f4a-4e58-b186-46a09bbe1036" />  

PRに問題が無いかどうかは、GithubActionが確認してくれます。  
全てにチェックマークが付けばそのPRは問題ありません。レビューとマージを待機してください。
<img width="800" height="450" alt="image" src="https://github.com/user-attachments/assets/5aabb747-4511-41d2-ac58-9f499b66f52d" />
