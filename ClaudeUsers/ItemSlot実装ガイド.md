# アイテムスロット実装ガイド（プレイヤー担当向け）

このドキュメントは、プレイヤーのアイテムスロットを実装する担当者（とそのClaude）に向けた引き継ぎ資料です。
アイテム側（`Assets/Programmer/Script/Entity/Item/`）はすでに実装済みで、プレイヤー側が1つのインターフェースを実装するだけでアイテムスロットに組み込めるようになっています。

仕様の出典: [Confluence「アイテム」ページ](https://summervacationgamejam.atlassian.net/wiki/spaces/20272/pages/21332019)
- シンプルなゲームにするため、アイテムスロットは**1つ**を想定。
- スポーン後、誰かがアイテムを拾う、または1分経過で消滅し、新たなアイテムになる（スポーン側は実装済み、消滅ロジックは未実装）。

## 前提として知っておくべきアイテム側の設計

```
CSO_ItemData (abstract ScriptableObject)
 ├─ CSO_ItemDataInstant   … 拾った瞬間に効果を即時発動して消費する（実装済み・例:救急箱、現状の爆弾）
 └─ CSO_ItemDataCarriable … 拾った瞬間はスロットに格納するだけ。効果の発動は任意のタイミング（今回プレイヤー側が実装する部分）
```

フィールド上の実体は `CS_ItemBase`（`NetworkBehaviour`）です。プレイヤーのコライダーが触れると `OnTriggerEnter` から
`_itemData.OnPickup(other.gameObject)` が呼ばれ、`true` が返るとアイテムはフィールドから消費（Despawn/Destroy）されます。
**この判定はオンライン時サーバーのみが行います**（`IsSpawned && !IsServer` は無視）。

## プレイヤー側が実装するもの: `ICarriableItemHolder`

ファイル: [`CS_ICarriableItemHolder.cs`](CS_ICarriableItemHolder.cs)

```csharp
public interface ICarriableItemHolder
{
    // 携帯型アイテムをスロットに格納する。格納できたらtrueを返す
    bool TryStoreItem(CSO_ItemDataCarriable item);
}
```

`CS_Player`（またはプレイヤーの子オブジェクトのコンポーネント）にこのインターフェースを実装してください。

```csharp
public class CS_Player : NetworkBehaviour, ICarriableItemHolder
{
    private CSO_ItemDataCarriable _heldItem; // スロットは1つの想定

    public bool TryStoreItem(CSO_ItemDataCarriable item)
    {
        if (_heldItem != null) return false; // スロットが埋まっていれば拾えない(フィールドに残る)

        _heldItem = item;
        return true;
    }
}
```

- `CSO_ItemDataCarriable.OnPickup(picker)` の中で `picker.GetComponentInParent<ICarriableItemHolder>()` を探して
  `TryStoreItem(this)` を呼んでいます。**`OnPickup`自体はアイテム側がトリガーで自動的に呼ぶものなので、プレイヤー側から呼ぶ必要はありません。**
- `ICarriableItemHolder`が未実装の場合、または`TryStoreItem`が`false`を返した場合は警告ログが出るだけでアイテムはフィールドに残ります。

## アイテムを使う（発動する）とき

格納したアイテムの効果を発動するのはプレイヤー側の責務です。任意のタイミング（使用ボタン押下など）で以下を呼んでください。

```csharp
_heldItem.Activate(gameObject); // 効果(CSO_ItemEffect[])を全て対象に適用する
_heldItem = null;               // スロットを空にする
```

`CSO_ItemDataCarriable.Activate(GameObject user)` は [`CSO_ItemDataCarriable.cs`](CSO_ItemDataCarriable.cs) にあります。

## 表示に使えるプロパティ

`CSO_ItemData`（基底クラス）が持つUI表示用のプロパティです。スロットの中身を表示する際に使えます。

```csharp
_heldItem.itemName    // string
_heldItem.icon        // Sprite
_heldItem.description // string
```

## 現状未実装で、プレイヤー側の検討が必要な点

- **入力**: 現在 `CustomInputAction`（`Assets/Programmer/Script/Input/`）の`Player`アクションマップには
  `Move` / `Look` / `LookStick` / `Attack` しかありません。アイテム使用ボタン（例: `UseItem`）を追加する必要があります。
- **ネットワーク同期**: `CSO_ItemDataCarriable`はScriptableObject（アセット）なので、`NetworkVariable`にそのまま入れることはできません。
  他クライアントにも「誰が何を持っているか」を見せたい場合は、アイテムをIDやインデックスで持つ（`NetworkVariable<int>`など）→
  IDから対応する`CSO_ItemDataCarriable`アセットを引き直す、というレジストリ的な仕組みが別途必要になります。
  （アイテム側にはまだこの仕組みはありません。設計が必要ならアイテム担当と相談してください）
- **拾得の確定はサーバーのみ**（`TryStoreItem`もサーバー上でしか呼ばれない想定）なので、スロットの中身をサーバー権威で管理する設計にしてください。

## 参考: 既存アイテムの実装例

- `Assets/Programmer/Database/Item/DB_ItemDataInstant_Heal.asset` … 即時発動型（`CSO_ItemDataInstant`）
- `Assets/Programmer/Database/Item/DB_ItemDataInstant_Bomb.asset` … 現状は`CSO_ItemDataInstant`で代用（本来は`CSO_ItemDataCarriable`にすべきだが、スロット未実装のため暫定対応。スロット実装後にアイテム側で差し替え予定）

まだ`CSO_ItemDataCarriable`を使ったアイテムのデータアセットは1つも作られていません。動作確認用に、`CSO_ItemDataCarriable`の
`DB_ItemDataCarriable`アセットを1つ作成し（`_effects`は空でもOK）、`CS_ItemBase`を持つプレハブに設定すればテストできます。
