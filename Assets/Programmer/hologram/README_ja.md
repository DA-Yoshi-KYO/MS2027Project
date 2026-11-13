# ホログラム シェーダー (HDRP)

Andy Duboc 氏の「Hologram Shader」(https://github.com/andydbc/HologramShader, MIT License) を、
このプロジェクトの HDRP (Unity 6) 用に Shader Graph で作り直したものです。
元のシェーダーは Built-in RP 用なので、計算式だけを `Shader/Hologram_HDRP.hlsl` に移しています。

## フォルダ構成

| パス | 内容 |
|---|---|
| `Shader/SHG_Hologram.shadergraph` | HDRP Unlit / Transparent の Shader Graph（マテリアルではこれを選ぶ。Shader Graphs/SHG_Hologram） |
| `Shader/Hologram_HDRP.hlsl` | Shader Graph の Custom Function から呼ぶ計算本体 |
| `Texture/T_Hologram_FlickerSmooth.png` | ちらつき用テクスチャ（なめらか）。元配布物の `flicker_smooth.png` |
| `Texture/T_Hologram_FlickerHard.png` | ちらつき用テクスチャ（パチパチ切り替わる）。元配布物の `flicker_hard.png` |
| `Material/MT_Hologram_Blue.mat` | 元のデモ用マテリアル `_Hologram_Rim_Flicker_Blue` と同じ値 |
| `Material/MT_Hologram_Red.mat` | リムだけの赤いホログラム（スキャンライン・グロー・グリッチなし） |
| `Original/` | 元配布物の README・LICENSE・元のシェーダー（参考用。`.txt` なので Unity では読み込まれない） |

## マテリアルの値

| 項目 | 変数名 | 何が変わるか |
|---|---|---|
| メインカラー | `_MainColor` | 本体の色。A は不透明度にも掛かる |
| メインテクスチャ | `_MainTex` | 本体に貼る模様（空なら白）。Tiling/Offset 欄は効かない |
| 明るさ | `_Brightness` | 全体の明るさ。1 より大きくすると Bloom で光りやすい |
| 不透明度 | `_Alpha` | 全体の透け具合 |
| エフェクトの向き | `_Direction` | スキャンラインとグローが流れる向き（ワールド座標）。(0,1,0) で上下、(1,0,0) で左右 |
| 露出の影響 | `_ExposureWeight` | 0 = シーンの露出に関係なく同じ明るさ、1 = 普通の発光体のように露出で明るさが変わる |
| リムの色 | `_RimColor` | 輪郭（視線に対して横向きの面）が光る色 |
| リムの鋭さ | `_RimPower` | 大きいほどリムが細くなる。小さいと全体がぼんやり光る |
| スキャンラインの強さ | `_ScanStrength` | 縞の濃さ。0 でスキャンラインなし |
| スキャンラインの細かさ | `_ScanTiling` | 大きいほど縞の間隔が狭い。縞はワールド座標基準なので、物を動かしても縞は動かない |
| スキャンラインの速度 | `_ScanSpeed` | 縞の流れる速さ。マイナスで逆向き |
| グローの強さ | `_GlowStrength` | ゆっくり流れる明るい帯の強さ。0 でなし |
| グローの細かさ | `_GlowTiling` | 帯の幅（小さいほど帯が太い） |
| グローの速度 | `_GlowSpeed` | 帯の流れる速さ。マイナスで逆向き |
| グリッチの幅 | `_GlitchIntensity` | ときどき起きる横ずれの距離（オブジェクトの X 方向）。0 でなし |
| グリッチの頻度 | `_GlitchSpeed` | 大きいほど横ずれが頻繁に起きる |
| ちらつきテクスチャ | `_FlickerTex` | ちらつき方のパターン（R チャンネルを使う）。Smooth / Hard を差し替えて使う |
| ちらつきの強さ | `_FlickerStrength` | 0 でちらつきなし、1 で元のシェーダーと同じ |
| ちらつきの速度 | `_FlickerSpeed` | ちらつきの速さ |

このほか、マテリアルの Surface Options で Blending Mode を Additive にすると加算合成になります（初期値は元と同じアルファブレンド）。

## 元のシェーダーとの違い

- 元はキーワード（Scan / Glow / Glitch の ON/OFF）で切り替えていましたが、「強さ」や「幅」を 0 にすると OFF になる形にしました。
- ちらつきとグローの強さ、露出の影響は新しく足した値です。それ以外の計算式は元のシェーダーと同じです。

## ライセンス

元の配布物は MIT License です（`Original/LICENSE.txt`）。この著作権表示とライセンス文は消さずに残してください。
