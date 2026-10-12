# 頭上順位UI（CS_UIPlayerRankOverhead）の Bind 対応のお願い

対象PR: #213「他プレイヤー順位のUIの処理を作成」（作成：元浪）

## 担当

- **Bind の呼び出し（プレイヤーへの組み込み）は秋野さんにお願いします。**
- UI本体（`CS_UIPlayerRankOverhead.cs` と `PlayerRank.prefab`）は元浪さんの担当です。

## 現状

`CS_UIPlayerRankOverhead` は `Bind(playerNumber)` が呼ばれるまで何も表示しません。
今は Bind を呼んでいる場所がないため、順位は常に非表示です。

## 使い方

### API

| メソッド | 説明 |
| --- | --- |
| `Bind(int playerNumber)` | このUIが表示するプレイヤー番号を設定する。以後 `CS_UIScoreModel.OnRankingUpdated` を購読し、順位が変わるたびに画像を切り替える。2回呼んでも内部で一度 Unbind するので安全。 |
| `Unbind()` | 購読をやめて非表示にする。`OnNetworkDespawn` / `OnDestroy` で自動的に呼ばれるので、通常は呼ばなくてよい。 |

### 呼ぶタイミング

- **全クライアントで**呼ぶ（他プレイヤーの頭上に出すUIなので、Owner だけでは足りない）。
- `CS_Player.playerNumber` が確定してから呼ぶ。`CS_PlayerSpawner` がスポーン前に割り当て、NetworkVariable でスポーン時に同期されるので、`OnNetworkSpawn` の中なら確定済み。
- `CS_PlayerResultDataHolder.Initialize()` と同じタイミング・同じ考え方でOK。スコアModelの Bind と順番が前後しても、Model 側の Bind で `OnRankingUpdated` が飛ぶので表示は追従します。

### 組み込み例

`PlayerRank.prefab` をプレイヤープレハブの子に置いた前提です（下の「前提」参照）。

```csharp
// 例：CS_PlayerResultDataHolder.Initialize() の末尾、または専用の小さなコンポーネントで
private CS_UIPlayerRankOverhead _rankOverhead;

private void Awake()
{
    _player = GetComponent<CS_Player>();
    _rankOverhead = GetComponentInChildren<CS_UIPlayerRankOverhead>(true);
}

public override void OnNetworkSpawn()
{
    // playerNumber はここで確定済み
    _rankOverhead?.Bind(_player.playerNumber);
}
```

オフラインのテストシーン（NetworkManager なし）では `playerNumber` が全員 0 になるため、複数人置くと順位が正しく出ません。1人だけ置くテストなら問題ありません。

## 前提（元浪さん側の修正）

Bind を組み込む前に、PR #213 で次の2点を直してもらう必要があります。

1. `PlayerRank.prefab` のルートにある `NetworkObject` を削除し、プレハブをプレイヤープレハブの**子として事前に配置**する（実行時に Instantiate して子にするのは不可。NetworkBehaviour の対応がずれるため）。
2. 自分のプレイヤーの頭上を隠す判定を、プレイヤーの Owner 基準にする。1 を直せば `IsOwner` が自動でプレイヤーの所有者を指すようになるので、実質 1 と同時に解決します。

`Bind` の処理自体（購読・解除・順位計算）はPRの実装のままで問題ありません。
