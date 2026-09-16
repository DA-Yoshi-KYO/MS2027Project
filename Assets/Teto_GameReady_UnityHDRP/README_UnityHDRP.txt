Teto GameReady Unity HDRP Bundle
================================

Source: 重音テトSV　麻十a十式v1.5.vrm
FBX: Models/Teto_GameReady.fbx (FBX 7.4 ASCII)
Triangles: 42,965 (no polygon reduction)
Skinned meshes: 3
Bones/Joints: 233
Materials: 21
Extracted PNG textures: 35 (original resolution; no resizing)
Face BlendShapes: 57

Unity 6.x / HDRP import
-----------------------
1. Copy the entire Teto_GameReady_UnityHDRP folder under your Unity project's Assets/.
2. Let Unity compile Editor/TetoGameReadyImporter.cs.
3. The script detects Models/Teto_GameReady.fbx, creates Materials/*.mat using HDRP/Lit, assigns BaseColor/Normal/Emission textures, configures alpha/double-sided settings, remaps the FBX material slots, and reimports the model.
4. It also tries to create a Humanoid Avatar using the original VRM humanoid mapping. If Humanoid setup fails, it falls back to Generic.
5. If assets were imported before the Editor script compiled, run Tools > Teto GameReady > Rebuild HDRP Materials.

Notes
-----
- No polygon decimation.
- No texture resizing/compression was performed in the files here. Unity may apply its own platform texture compression at import/build time.
- VRM/MToon-specific toon shading is intentionally replaced by Unity HDRP/Lit as requested. Therefore the exact VRM toon look is not guaranteed to match 1:1; texture/material slot correspondence is preserved.
- BlendShape/morph target data is included in the FBX for the face mesh.
- MaterialMap.json records the original VRM material-to-texture mapping.

License
-------
The source VRM metadata indicates non-commercial/CC BY-NC style restrictions. Confirm the model author's terms before commercial distribution.

[2026-09-15 修正]
- 自動ImporterがHumanoid Avatarを強制生成していたため、Unity環境によって "Required human bone Hips not found" が出る問題を修正。
- マテリアル/テクスチャ自動設定とRig設定を分離し、Rigは安全なGenericでImportするよう変更。
- FBX内のスキン、ボーン、BlendShapeは保持。Humanoidリターゲットが必要な場合はRigタブから別途設定する。
