# 頭上順位UI（CS_UIPlayerRankOverhead）の Bind 対応のお願い

対象PR: #213「他プレイヤー順位のUIの処理を作成」（作成：元浪）

## 担当

- **Bind の呼び出し（プレイヤーへの組み込み）は秋野さんにお願いします。**
- UI本体（`CS_UIPlayerRankOverhead.cs` と `PlayerRank.prefab`）は元浪さんの担当です。

## 現状

`CS_UIPlayerRankOverhead` は `Bind(playerNumber, camera)` が呼ばれるまで何も表示しません。
`PlayerRank.prefab` はすでに `Player.prefab` のルート直下に置かれているので、あとは Bind を呼ぶだけです。

**→ 対応済み（秋野）**：`Player.prefab` に付けた `CS_PlayerRankOverheadUI`（`Assets/Programmer/Script/Entity/Player/CS_PlayerRankOverheadUI.cs`）が、下の「組み込み例」と同じ考え方で Bind しています。
ローカルプレイヤーのカメラは `CS_Player.playerCamera` から取っています。詳細は `ClaudeUsers/プレイヤー仕様.md` の「HUD」を参照。

## 使い方

### API

| メソッド | 説明 |
| --- | --- |
| `Bind(int playerNumber, Camera camera)` | このUIが表示するプレイヤー番号と、UIを向けるカメラを設定する。以後 `CS_UIScoreModel.OnRankingUpdated` を購読し、順位が変わるたびに画像を切り替える。2回呼んでも内部で一度 Unbind するので安全。 |
| `Unbind()` | 購読をやめて非表示にする。`OnNetworkDespawn` / `OnDestroy` で自動的に呼ばれるので、通常は呼ばなくてよい。 |

### 引数

| 引数 | 渡すもの |
| --- | --- |
| `playerNumber` | **このUIが乗っているプレイヤー**の番号（`CS_Player.playerNumber`）。 |
| `camera` | **画面を見ている自分（ローカルプレイヤー）のカメラ**。このUIが乗っているプレイヤーのカメラではない。 |

### カメラについての注意（重要）

- 頭上の順位画像は、`camera` の方向を向き続けます（ビルボード）。
- 他人のプレイヤーのカメラは `CS_Player.SetupAsRemotePlayer()` で無効化されます。他人のプレイヤーの UI に**そのプレイヤー自身のカメラ**を渡すと、無効化されたカメラの方を向いてしまい、自分の画面からは正しく見えません。
- なので、**どのプレイヤーの UI にも、ローカルプレイヤーのカメラを渡してください。**
- `camera` に null を渡した場合は、`LateUpdate` で `Camera.main` を取り直します。ただし `Awake` で取得した `Camera.main` は他人のカメラを拾っている可能性があるので、null に頼らず必ずローカルのカメラを渡すこと。

### 呼ぶタイミング

- **全クライアントで、全プレイヤー分**呼ぶ（他プレイヤーの頭上に出すUIなので、Owner だけでは足りない）。
- `CS_Player.playerNumber` が確定してから呼ぶ。`CS_PlayerSpawner` がスポーン前に割り当て、NetworkVariable でスポーン時に同期されるので、`OnNetworkSpawn` の中なら確定済み。
- ローカルプレイヤーのカメラが用意できてから呼ぶ。他人のプレイヤーの方が先にスポーンする（後から参加したクライアントなど）と、その時点ではまだローカルのカメラがありません。ローカルプレイヤーのスポーン後に、全プレイヤー分の Bind をやり直してください。
- スコアModelの Bind と順番が前後しても、Model 側の Bind で `OnRankingUpdated` が飛ぶので表示は追従します。

### 組み込み例

ローカルプレイヤーのカメラを static に持っておき、そろった時点で全員分 Bind する例です。

```csharp
// 例：CS_Player 側（または専用の小さなコンポーネント）
private static readonly List<CS_Player> _players = new List<CS_Player>();
private static Camera _localCamera;   // 自分（ローカルプレイヤー）のカメラ

private CS_UIPlayerRankOverhead _rankOverhead;

private void Awake()
{
    _rankOverhead = GetComponentInChildren<CS_UIPlayerRankOverhead>(true);
}

public override void OnNetworkSpawn()
{
    _players.Add(this);

    if (IsOwner)
    {
        // 自分のプレイヤーのカメラ（_cameraTransform に付いている Camera）
        _localCamera = _cameraTransform.GetComponent<Camera>();

        // 先にスポーンしていた他プレイヤーの分も、ローカルのカメラで Bind し直す
        foreach (CS_Player player in _players)
            player.BindRankOverhead();
        return;
    }

    BindRankOverhead();
}

public override void OnNetworkDespawn()
{
    _players.Remove(this);
}

private void BindRankOverhead()
{
    if (_rankOverhead == null || _localCamera == null) return;

    // playerNumber は「このプレイヤー」、カメラは「ローカルプレイヤー」のもの
    _rankOverhead.Bind(playerNumber, _localCamera);
}
```

static を使う場合は、ほかのクラスと同様に `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` で `_players` と `_localCamera` をリセットしてください（Domain Reload オフ対策）。

オフラインのテストシーン（NetworkManager なし）では `playerNumber` が全員 0 になるため、複数人置くと順位が正しく出ません。1人だけ置くテストなら問題ありません。

## 設定済みの項目（元浪さん側、PR #213 で対応済み）

- `PlayerRank.prefab` の `NetworkObject` は削除済み。`Player.prefab` のルート直下に事前配置済み（実行時に Instantiate して子にするのは不可）。
- 自分のプレイヤーの頭上は自動で隠れる（`IsOwner` がプレイヤーの所有者を指すため）。自分にも出したい場合は `_showLocalPlayer` をオンにする。
- 同点は同順位。`_hideWhenAllScoresAreZero` がオンなので、全員 0 点の間は非表示。
