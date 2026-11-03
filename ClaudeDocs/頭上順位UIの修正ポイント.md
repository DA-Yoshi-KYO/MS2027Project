# PR #213 レビュー：できれば直したい点（4〜6）

対象PR: #213「他プレイヤー順位のUIの処理を作成」
対象ファイル: `Assets/Programmer/Prefab/UI/InGame/Player/PlayerRank.prefab`、`Assets/Programmer/Script/UI/UI_PlayerRankOverhead/CS_UIPlayerRankOverhead.cs`

動作を止めるほどではないものの、見た目や挙動に出るので直しておきたい点です。

## 4. World Space の Canvas が巨大

**現状**
- ルートの Canvas は `Render Mode = World Space`。
- RectTransform が `Width 1920 / Height 1080`、`Scale 1`。
- World Space では 1 = 1m なので、ワールド上で約 1920m × 1080m の板になります。子の `RankRoot`（200×200）や `Rank`（100×100）も 200m、100m 相当です。

**直し方（どちらか）**
- ルートの Scale を `0.01` 程度にする（100px ≒ 1m）。
- または Canvas のサイズを順位画像に合わせて小さくし（例: 200×200）、Scale を `0.005`〜`0.01` で調整する。

あわせて位置も、プレイヤーの頭上（例: ローカル座標 Y = 2.2 前後）に置いてください。

## 5. 同点のとき順位がランダムに入れ替わる

**現状**
- `CS_UIScoreModel.GetRanking()` は Dictionary の中身を `List.Sort` でスコア降順に並べているだけです。
- `List.Sort` は同じ値の順序を保証しないため、同点のプレイヤーに付く順位は不定です。
- ゲーム開始時は全員 0 点なので、1〜4位が適当に割り振られて表示され、スコア更新のたびに入れ替わることがあります。

**直し方（どれか）**
- 同点は同じ順位にする（「自分より点が高い人の数 + 1」を順位にする）。
  ```csharp
  private int FindRank(List<(int playerNumber, int score)> ranking, int playerNumber)
  {
      int myScore = int.MinValue;
      bool found = false;
      foreach (var r in ranking)
      {
          if (r.playerNumber == playerNumber) { myScore = r.score; found = true; break; }
      }
      if (!found) return -1;

      int higher = 0;
      foreach (var r in ranking)
          if (r.score > myScore) higher++;
      return higher + 1;
  }
  ```
- 全員 0 点（まだ誰も得点していない）の間は順位を非表示にする。
- 並び替えにプレイヤー番号を第2キーとして入れて、順序だけは安定させる（`CS_UIScoreModel` 側の変更になるので、スコアUIの順位表示にも影響します）。

おすすめは1つ目（同点は同順位）です。View 側だけで完結し、ほかの画面に影響しません。

## 6. 仮素材・不要コンポーネントの整理

- `_rankSprites` の4番目（4位）がUnity組み込みの仮スプライト（`fileID: 10907`）のままです。4位用の画像を設定するか、4位は非表示にするなら `_defaultRankSprite` と合わせて方針を決めてください。
- `CanvasScaler`：World Space では画面解像度に合わせた拡縮をしないので不要です。
- `GraphicRaycaster`：頭上表示はクリックしないので不要です。あわせて `Rank` の Image の `Raycast Target` もオフで構いません。

---

# 付録：2・3（NetworkObject と IsOwner）の直し方

2（プレハブに独自の NetworkObject が付いている）を直してプレイヤーの子に入れれば、`IsOwner` がプレイヤーの所有者を指すようになり、自分のプレイヤーだけ隠れる正しい挙動になります。3（`ShouldShow()` の `IsOwner`）はコードを変えなくても、2と一緒に解決します。Unityエディタでの手順は以下です。

## 1. PlayerRank.prefab から NetworkObject を外す

1. Projectウィンドウで `Assets/Programmer/Prefab/UI/InGame/Player/PlayerRank.prefab` をダブルクリックしてPrefabモードで開く。
2. ルートの `PlayerRank` を選び、Inspectorの `NetworkObject` コンポーネントの「⋮」から「Remove Component」を選ぶ。
3. 保存する。単体で開いている間は `CS_UIPlayerRankOverhead` に「NetworkObjectがない」という警告が出ることがあるが、プレイヤーの子に入れれば消えるので無視してよい。もし勝手に付け直される場合は、Project Settings > Multiplayer にある NetworkObject の自動追加の設定をオフにする。

## 2. プレイヤープレハブの子に置く

1. プレイヤープレハブ（`CS_Player` が付いているもの）をPrefabモードで開く。
2. Projectウィンドウから `PlayerRank.prefab` をHierarchyのプレイヤーのルートの上にドラッグし、子にする。
3. `PlayerRank` の Position を頭上（例: Y = 2.2）、Scale を 0.01 にする（4の対応も兼ねる）。
4. 保存する。最終的にプレイヤープレハブの中で NetworkObject がルートの1つだけになっていればOK。

## 3. 動作確認

ホストとクライアントの2つで起動し、どちらの画面でも「他人の頭上には順位が出て、自分の頭上には出ない」ことを確認する。表示には秋野さんの Bind が必要なので、確認は Bind が入ってから。

## なぜこれで直るか

`NetworkBehaviour` は自分の親をたどって一番近い `NetworkObject` を使う。UIの NetworkObject を外すとプレイヤーの NetworkObject が使われるので、`IsOwner` が「このプレイヤーは自分が操作しているか」になる。`_showLocalPlayer` はオフのままにしておく。

## 注意

実行時に `Instantiate` してプレイヤーの子にする方法は使えない。NGO はスポーン時の NetworkBehaviour の並びで同期するので、必ずプレハブ上で事前に置くこと。
