# Nova HDRP ローカル移植版

CyberAgent NOVA Shader 3.6.0を、このプロジェクトのUnity 6000.3.23f1／HDRP 17.3.0向けに移植したものです。CyberAgent公式のHDRP版ではありません。元のMITライセンスはLICENSE.mdに保持しています。

## 使い方

既存の `Nova/Particles/UberUnlit`、`Nova/Particles/UberLit`、`Nova/Particles/Distortion` を引き続き使用します。シェーダーの名前、既存GUID、マテリアルのプロパティ名を維持しています。UI用の2種類も同じ名前で利用できます。

マテリアルのInspectorでRender TypeとBlend Modeを設定してください。パーティクルのCustom Dataを使用する場合は、従来どおりNovaのInspectorからVertex Streamsを修正します。

DistortionはHDRP標準の `DistortionVectors` に出力します。HDRP AssetのDistortionサポート、および対象カメラのFrame SettingsのDistortionを有効にしてください。URPのScreenSpaceDistortion Renderer Featureは不要です。旧URP用C#ファイルは履歴を残すために保持し、`NOVA_LEGACY_URP` 条件でコンパイル対象から外しています。このHDRP版でこのシンボルを追加しないでください。

## 移植内容

- HDRPのForwardOnly、DepthForwardOnly、ShadowCaster、DistortionVectorsに対応。
- Novaのフリップブック、Custom Coords、Flow Map、ディゾルブ、色調補正、頂点変形などの既存処理を維持。
- LitはHDRPのLit BSDF／LightLoopを使用。ライト、シャドウ、環境反射の評価をHDRP側へ移行。
- ソフトパーティクルはHDRPの深度テクスチャを参照。透視／平行投影の深度に対応。
- ワールド描画の露出と透明物のフォグをHDRPに対応。UI Unlitの色は露出補正から除外。
- 法線バッファをHDRP形式で出力。最適化シェーダーでもOpaque／Cutoutに必要な深度・法線パスを保持。

## 検証

`Tools > Nova > HDRP > Validate Shaders` で6シェーダーの全パスと代表的なキーワードの組み合わせを同期コンパイルします。結果は `Temp/NovaHDRPValidation.txt` に出力します。全キーワードの総当たり検証ではありません。

`Tools > Nova > HDRP > Render Smoke Test` で、一時的なプレビューシーンにUnlit（左）／Lit（右）を描画し、`Temp/NovaHDRPSmokeTest.png` に保存します。編集中のシーンや既存マテリアルは変更しません。

## 対応範囲

この移植はラスタライズのForward描画を対象とします。HDRPのDecal、SSR、レイトレーシング、ベイク用Metaパス、オブジェクトMotion Vectorsは実装していません。XR、各種Canvasモード、実機ビルド、全機能の組み合わせは別途検証が必要です。Litのライト評価は全ライトを走査する方式のため、ライトの多いシーンでは負荷を確認してください。

URPとHDRPではライティングと露出の単位が異なります。特にLitの見た目はURP版と完全一致しません。実際のエフェクトシーンで明るさ・透過・歪みの強さを確認してください。
