/* ================================================
 * テトのHDRPトゥーン表現と攻撃モーションを設定する
 * 制作者：吉本竜
 * 2026-09-25 | 初回作成
 * ================================================ */
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MS2027.Teto
{
    /// <summary>
    /// テト専用の素材作成とシーンへの適用を行うエディター処理。
    /// </summary>
    internal static class CSED_TetoEndfieldSetup
    {
        internal const string Root = "Assets/Teto_GameReady_UnityHDRP_v5/Teto_GameReady_UnityHDRP";

        /// <summary>
        /// 現在表示されているテトへ素材とモーションを適用し、専用シーンとして保存する。
        /// </summary>
        [MenuItem("Tools/Teto/Apply Endfield Look and Attack")]
        private static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("停止中に設定してください。");
            var model = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Teto_GameReady");
            if (!model) throw new InvalidOperationException("現在のシーンにTeto_GameReadyがありません。");
            var shader = Shader.Find("MS2027/Teto/Endfield HDRP");
            var outlineShader = Shader.Find("MS2027/Teto/Outline HDRP");
            if (!shader || !outlineShader || ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(outlineShader))
                throw new InvalidOperationException("シェーダーのコンパイルを確認してください。");
            foreach (string dir in new[] { "EndfieldMaterials", "Animations", "Scenes", "Profiles", "Prefabs" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + dir)) AssetDatabase.CreateFolder(Root, dir);
            Undo.RegisterFullObjectHierarchyUndo(model, "Apply Teto Endfield look");
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => !r.name.EndsWith("_Outline")).ToArray();
            foreach (var renderer in renderers)
            {
                var surfaces = renderer.sharedMaterials.Select(source => CreateSurface(source, shader)).ToArray();
                renderer.sharedMaterials = surfaces;
                renderer.updateWhenOffscreen = true;
                string outlineName = renderer.name + "_Outline";
                var existing = renderer.transform.Find(outlineName);
                var outlineObject = existing ? existing.gameObject : new GameObject(outlineName);
                outlineObject.transform.SetParent(renderer.transform, false);
                var outline = GetOrAdd<SkinnedMeshRenderer>(outlineObject);
                outline.sharedMesh = renderer.sharedMesh;
                outline.bones = renderer.bones;
                outline.rootBone = renderer.rootBone;
                outline.localBounds = renderer.localBounds;
                outline.updateWhenOffscreen = true;
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.sharedMaterials = surfaces.Select(m => CreateOutline(m, outlineShader)).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var bones = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, model.transform));
            Transform Find(string name) => bones.Values.First(t => t.name == name);
            var lighting = GetOrAdd<CS_TetoToonLighting>(model);
            lighting.head = Find("J_Bip_C_Head");
            lighting.keyLight = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            lighting.targets = renderers;
            if (lighting.keyLight)
            {
                lighting.keyLight.transform.rotation = Quaternion.Euler(35,-35,0);
                lighting.keyLight.intensity = 2.5f;
            }
            CreateAnimation(model, bones, false);
            CreateAnimation(model, bones, true);
            var animator = model.GetComponent<Animator>();
            if (animator) animator.enabled = false;
            var player = GetOrAdd<Animation>(model);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/Teto_Idle.anim");
            player.AddClip(idle, "Teto_Idle");
            player.AddClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/Teto_Attack.anim"), "Teto_Attack");
            player.clip = idle;
            player.playAutomatically = true;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            if (!model.GetComponent<CS_TetoAttackPreview>()) model.AddComponent<CS_TetoAttackPreview>();
            // 編集中もTポーズではなく構えで見せる。クリップは常にFBXの元姿勢から生成する。
            idle.SampleAnimation(model, 0);
            ConfigurePresentation(model);
            PrefabUtility.SaveAsPrefabAsset(model, Root + "/Prefabs/Teto_Endfield.prefab");
            EditorSceneManager.MarkSceneDirty(model.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(model.scene, Root + "/Scenes/Teto_EndfieldPreview.unity");
        }

        /// <summary>
        /// 元のHDRP素材を残し、部位ごとのトゥーン設定を別素材へ保存する。
        /// </summary>
        private static Material CreateSurface(Material source, Shader shader)
        {
            string name = source.name.Replace("_Endfield", "");
            string path = Root + "/EndfieldMaterials/" + name + "_Endfield.mat";
            var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat") ?? source;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            material.name = name + "_Endfield";
            bool hair = name.Contains("HAIR"), skin = name.Contains("SKIN"), face = name.Contains("Face"), eye = name.Contains("EYE");
            material.SetTexture("_BaseMap", original.GetTexture("_BaseColorMap"));
            material.SetTexture("_NormalMap", original.GetTexture("_NormalMap"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_MaterialKind", hair ? 3 : eye ? 4 : face ? 2 : skin ? 1 : 0);
            material.SetColor("_ShadowColor", skin || face ? new Color(0.87f,0.65f,0.61f) : hair ? new Color(0.62f,0.32f,0.43f) : new Color(0.62f,0.66f,0.76f));
            material.SetColor("_MidColor", skin || face ? new Color(1f,0.86f,0.8f) : new Color(0.84f,0.86f,0.93f));
            material.SetFloat("_Roughness", hair ? 0.35f : skin || face ? 0.7f : name.Contains("Shoes") ? 0.38f : 0.58f);
            material.SetFloat("_Specular", hair ? 0.38f : eye ? 0.75f : 0.28f);
            material.SetFloat("_RimStrength", face ? 0 : 0.14f);
            material.SetFloat("_NormalStrength", face ? 0.05f : hair ? 0.18f : 0.25f);
            material.SetFloat("_Cutoff", eye || face ? 0.2f : 0.4f);
            material.SetFloat("_Softness", skin || face ? 0.2f : 0.1f);
            material.SetFloat("_Brightness", 1.8f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 目やまつ毛には線を足さず、髪と衣装に細い輪郭線を作る。
        /// </summary>
        private static Material CreateOutline(Material surface, Shader shader)
        {
            string path = Root + "/EndfieldMaterials/" + surface.name + "_Outline.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat,path); }
            mat.name = surface.name + "_Outline";
            mat.SetTexture("_BaseMap", surface.GetTexture("_BaseMap"));
            mat.SetFloat("_Width", surface.name.Contains("Face") || surface.name.Contains("EYE") ? 0f : 0.00085f);
            mat.SetColor("_OutlineColor", surface.name.Contains("HAIR") ? new Color(0.12f,0.025f,0.045f) : new Color(0.045f,0.04f,0.065f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// テト固有のボーンへ、構え・溜め・打撃・戻りを60fpsで記録する。
        /// </summary>
        private static void CreateAnimation(GameObject model, System.Collections.Generic.Dictionary<string,Transform> bones, bool attack)
        {
            string name = attack ? "Teto_Attack" : "Teto_Idle";
            string path = Root + "/Animations/" + name + ".anim";
            var clip = new AnimationClip { name = name, legacy = true, frameRate = 60, wrapMode = attack ? WrapMode.Once : WrapMode.Loop };
            float length = attack ? 1.35f : 2.4f;
            foreach (var pair in bones.Where(p => p.Value.name.StartsWith("J_Bip")))
            {
                var curves = Enumerable.Range(0,4).Select(_ => new AnimationCurve()).ToArray();
                for (int frame = 0; frame <= Mathf.RoundToInt(length*60); frame++)
                {
                    float time = frame/60f;
                    Quaternion q = Quaternion.Euler(Pose(pair.Value.name,time,attack));
                    curves[0].AddKey(time,q.x); curves[1].AddKey(time,q.y); curves[2].AddKey(time,q.z); curves[3].AddKey(time,q.w);
                }
                for (int axis=0;axis<4;axis++) clip.SetCurve(pair.Key,typeof(Transform),"localRotation."+"xyzw"[axis],curves[axis]);
            }
            var hips = bones.First(p => p.Value.name == "J_Bip_C_Hips");
            // 元FBXの値を参照し、再適用しても腰位置が積み重ならないようにする。
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Teto_GameReady.fbx");
            var bindHips = modelAsset.GetComponentsInChildren<Transform>(true).First(t => t.name == "J_Bip_C_Hips").localPosition;
            for(int axis=0;axis<3;axis++)
            {
                var curve = new AnimationCurve();
                for(int f=0;f<=Mathf.RoundToInt(length*60);f++)
                {
                    float time=f/60f;
                    float hit=attack ? Pulse(time,0.25f,0.5f,0.7f,1.25f) : 0;
                    float value=bindHips[axis]+(axis==1 ? -0.015f-0.012f*hit+0.003f*Mathf.Sin(time/length*Mathf.PI*2) : axis==2 ? -0.075f*hit : 0);
                    curve.AddKey(time,value);
                }
                clip.SetCurve(hips.Key,typeof(Transform),"localPosition."+"xyz"[axis],curve);
            }
            clip.EnsureQuaternionContinuity();
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved) { EditorUtility.CopySerialized(clip,saved); UnityEngine.Object.DestroyImmediate(clip); }
            else AssetDatabase.CreateAsset(clip,path);
        }

        /// <summary>
        /// 素早い打撃と長めの戻りを、連続した補間値で表す。
        /// </summary>
        private static float Pulse(float time,float start,float peak,float hold,float end)
        {
            if (time < start || time > end) return 0;
            if (time < peak) return Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,peak,time));
            return time < hold ? 1 : Mathf.SmoothStep(1,0,Mathf.InverseLerp(hold,end,time));
        }

        /// <summary>
        /// Tポーズで軸が揃った元FBXに合わせて、各関節の回転を指定する。
        /// </summary>
        private static Vector3 Pose(string bone,float time,bool attack)
        {
            float wind=attack ? Pulse(time,0,0.23f,0.28f,0.48f) : 0;
            float hit=attack ? Pulse(time,0.28f,0.46f,0.53f,1.2f) : 0;
            switch(bone)
            {
                case "J_Bip_C_Spine": return new Vector3(-4-5*hit, -10*wind+14*hit,0);
                case "J_Bip_C_Chest": return new Vector3(0,-12*wind+16*hit,0);
                case "J_Bip_C_Head": return new Vector3(2,8*wind-15*hit,0);
                case "J_Bip_R_UpperArm": return Vector3.Lerp(new Vector3(0,-20+25*wind,65-10*wind),new Vector3(0,-84,8),hit);
                case "J_Bip_R_LowerArm": return Vector3.Lerp(new Vector3(0,-75,0),new Vector3(0,-8,0),hit);
                case "J_Bip_L_UpperArm": return new Vector3(0,25,-64);
                case "J_Bip_L_LowerArm": return new Vector3(0,85,0);
                case "J_Bip_L_UpperLeg": return new Vector3(6+5*hit,0,5);
                case "J_Bip_R_UpperLeg": return new Vector3(-5-4*hit,0,-5);
                case "J_Bip_L_LowerLeg": return new Vector3(-7-4*hit,0,0);
                case "J_Bip_R_LowerLeg": return new Vector3(-7-5*hit,0,0);
                case "J_Bip_L_Foot": return new Vector3(1,0,0);
                case "J_Bip_R_Foot": return new Vector3(12,0,0);
            }
            // 握り拳を作る。左右の指はX方向が逆なので回転符号も反転する。
            if (bone.Contains("Index") || bone.Contains("Middle") || bone.Contains("Ring") || bone.Contains("Little"))
                return new Vector3(0,0,bone.Contains("_L_") ? -65 : 65);
            if (bone.Contains("Thumb")) return new Vector3(0,bone.Contains("_L_") ? -25 : 25,bone.Contains("_L_") ? -25 : 25);
            return Vector3.zero;
        }

        /// <summary>
        /// キャラクターの色が読みやすい露出と背景、全身を収めるカメラを設定する。
        /// </summary>
        private static void ConfigurePresentation(GameObject model)
        {
            var cam = Camera.main;
            if (cam)
            {
                Vector3 focus = model.transform.position + Vector3.up*0.79f;
                cam.transform.position = focus + new Vector3(0.45f,0.15f,-2.8f);
                cam.transform.LookAt(focus);
                cam.fieldOfView=35;
                var hd = GetOrAdd<HDAdditionalCameraData>(cam.gameObject);
                hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hd.backgroundColorHDR = new Color(0.035f,0.047f,0.067f);
                hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            string path = Root + "/Profiles/Teto_Preview.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
            if (!profile.TryGet<Exposure>(out var exposure)) { exposure=profile.Add<Exposure>(true); AssetDatabase.AddObjectToAsset(exposure,profile); }
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(0f);
            if (!profile.TryGet<Tonemapping>(out var tone)) { tone=profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tone,profile); }
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet<Bloom>(out var bloom)) { bloom=profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom,profile); }
            bloom.intensity.Override(0.06f); bloom.threshold.Override(1.2f);
            var go = GameObject.Find("Teto Preview Lighting") ?? new GameObject("Teto Preview Lighting");
            var volume=GetOrAdd<Volume>(go);
            volume.isGlobal=true; volume.priority=50; volume.sharedProfile=profile;
            EditorUtility.SetDirty(profile);
            var stage = GameObject.Find("Teto Preview Stage");
            if (!stage)
            {
                stage=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stage.name="Teto Preview Stage";
                UnityEngine.Object.DestroyImmediate(stage.GetComponent<Collider>());
            }
            stage.transform.position=model.transform.position+Vector3.down*0.026f;
            stage.transform.localScale=new Vector3(1.7f,0.025f,1.7f);
            string stagePath=Root+"/EndfieldMaterials/Teto_Stage.mat";
            var stageMaterial=AssetDatabase.LoadAssetAtPath<Material>(stagePath);
            if(!stageMaterial)
            {
                stageMaterial=new Material(Shader.Find("MS2027/Teto/Endfield HDRP"));
                stageMaterial.name="Teto_Stage";
                AssetDatabase.CreateAsset(stageMaterial,stagePath);
            }
            stageMaterial.shader=Shader.Find("MS2027/Teto/Endfield HDRP");
            stageMaterial.SetColor("_BaseColor",new Color(0.19f,0.23f,0.3f));
            stageMaterial.SetFloat("_NormalStrength",0);
            stageMaterial.SetFloat("_Roughness",0.8f);
            stageMaterial.SetFloat("_Specular",0.05f);
            stageMaterial.SetVector("_TetoLightDirection",new Vector3(-0.5f,0.7f,-0.5f));
            stage.GetComponent<Renderer>().sharedMaterial=stageMaterial;
            EditorUtility.SetDirty(stageMaterial);
        }

        /// <summary>
        /// Unityの疑似null判定を使い、未追加のコンポーネントだけを作成する。
        /// </summary>
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component ? component : go.AddComponent<T>();
        }

    }
}
