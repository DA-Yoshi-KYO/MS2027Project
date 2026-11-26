# OwlAssistAI — Unity 取り込み手順

## 入れるもの
このフォルダ（`OwlAssistAI/`）ごと Unity プロジェクトの `Assets/` にコピーしてください。

```
OwlAssistAI/
  OwlAssistAI.fbx          モデル＋リグ（20ボーン）＋アニメーション Fly_Loop
  textures/                T_Bandana.png / T_EarCup_Logo.png（FBXにも埋め込み済み）
  Editor/OwlAssistAIImport.cs   取り込み設定を自動で行うスクリプト
```

`OwlAssistAIImport.cs` が入っていれば、FBXの取り込み時に次の設定が自動で入ります。

| 項目 | 設定 |
|---|---|
| Model > Bake Axis Conversion | ON（Blender の Z-up を Unity の Y-up に変換） |
| Model > Use File Scale / Scale Factor | ON / 1（高さ約 1.02 m） |
| Rig > Animation Type | Generic |
| Animation > クリップ名 | Fly_Loop |
| Animation > Loop Time | ON |

スクリプトを使わない場合は、上の表と同じ設定を Inspector で手動で行ってください。

## アニメーション
- **Fly_Loop**：30fps・30フレーム（1秒）、羽ばたき2回でループ。最後のフレーム＝最初のフレームなので継ぎ目なし。
- その場で羽ばたく動き（上下 ±2.8cm の浮き沈みを含む）。移動はゲーム側で GameObject を動かしてください。
- 使い方：FBX を Hierarchy に置き、Animator Controller を作って Fly_Loop を既定ステートにする。

## 向き・大きさ
- 正面は Unity の +Z 方向になる想定（Blender の -Y を正面に、Forward -Z / Up Y で書き出し）。
- 高さ約 1.02 m。ゲーム内の大きさは Transform の Scale で調整してください。

## マテリアル
- FBX のマテリアルは Unity の取り込み機能で自動生成されます（白・グレー・紺・水色の発光など 9 種）。
- 発光（瞳・ヘッドホンのリングとMロゴ・翼の先端）は FBX の Emissive として入っていますが、パイプライン（Built-in / URP）によっては Emission を手動で ON にする必要があります。発光色は水色 (0.05, 0.75, 1.0) 付近。
- ロゴ（バンダナの M / MINUTES、ヘッドホンの M）は仮のテクスチャです。正式なロゴ画像に差し替えてください（UVはバンダナ前面とヘッドホン面に合わせてあります）。

## ボーン
```
OwlAssist_Armature
└ Root
   └ Body
      ├ Head ─ Eye_L / Eye_R / Ear_L / Ear_R
      ├ Wing_L_1 ─ Wing_L_2 ─ Wing_L_3   （Wing_R も同様）
      ├ Leg_L ─ Foot_L                   （Leg_R も同様）
      ├ Tail
      ├ Scarf_F
      └ Scarf_B
```
瞳（Eye_L / Eye_R）は独立した板なので、ボーンの移動・拡縮で目線や表情を付けられます。

※ この取り込み手順と C# スクリプトは、Unity 本体での動作確認をしていません（Blender での FBX 再読み込み検証のみ済み）。
