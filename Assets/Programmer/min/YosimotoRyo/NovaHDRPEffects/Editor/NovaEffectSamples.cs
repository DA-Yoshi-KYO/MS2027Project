using System;
using System.IO;
using System.Collections.Generic;
using Nova.Editor.Core.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class NovaEffectSamples
{
    const string Root = "Assets/Programmer/min/YosimotoRyo/NovaHDRPEffects";
    static readonly List<GameObject> samples = new List<GameObject>();

    [MenuItem("Tools/Nova/HDRP/Create Effect Samples")]
    public static void Create()
    {
        if (GameObject.Find("Nova HDRP - Effect Samples") != null)
        {
            Selection.activeGameObject = GameObject.Find("Nova HDRP - Effect Samples");
            return;
        }
        foreach (var folder in new[] { "Textures", "Materials", "Prefabs" }) Directory.CreateDirectory(Root + "/" + folder);
        AssetDatabase.Refresh();
        samples.Clear();
        var soft = Texture("SoftGlow", (u,v) => new Color(1,1,1, Mathf.Pow(Mathf.Clamp01(1 - new Vector2(u,v).magnitude), 2)));
        var flame = Texture("FlameWisp", (u,v) => {
            float n = Mathf.PerlinNoise(u * 5 + 17, v * 4 + 9);
            float a = Mathf.Clamp01(1 - Mathf.Abs(u) / Mathf.Max(0.08f, (1-v)*0.5f));
            a *= Mathf.Clamp01(1-v*v) * Mathf.Clamp01(n*1.7f-0.2f);
            return new Color(1,1,1, a*a);
        });
        var rune = Texture("ArcaneRune", (u,v) => {
            float r = new Vector2(u,v).magnitude;
            float angle = Mathf.Atan2(v,u);
            float rings = Mathf.Exp(-Mathf.Pow((r-.8f)*100,2)) + Mathf.Exp(-Mathf.Pow((r-.65f)*110,2)) + Mathf.Exp(-Mathf.Pow((r-.38f)*90,2));
            float glyph = r > .43f && r < .59f ? Mathf.Pow(Mathf.Max(0,Mathf.Cos(angle*12 + r*12)),20) : 0;
            float spokes = r > .67f && r < .79f ? Mathf.Pow(Mathf.Abs(Mathf.Cos(angle*8)),40) : 0;
            return new Color(1,1,1,Mathf.Clamp01(rings+glyph+spokes));
        });
        var ring = Texture("ShockRing", (u,v) => new Color(1,1,1, Mathf.Exp(-Mathf.Pow((new Vector2(u,v).magnitude-.7f)*23,2))));
        var streak = Texture("SparkStreak", (u,v) => new Color(1,1,1,Mathf.Exp(-u*u*60-v*v*3) * Mathf.Clamp01(1-v*v)));
        var distortion = Texture("HeatVector", (u,v) => {
            float fade = Mathf.Pow(Mathf.Clamp01(1-new Vector2(u,v).magnitude),2);
            return new Color(.5f + (Mathf.PerlinNoise(u*4+12,v*4+5)-.5f)*fade,
                .5f+(Mathf.PerlinNoise(u*4+44,v*4+7)-.5f)*fade,0,1);
        }, true);
        var fireMat = Material("Flame_Additive", flame, true);
        var glowMat = Material("Glow_Additive", soft, true);
        var runeMat = Material("Arcane_Additive", rune, true);
        var ringMat = Material("Shockwave_Additive", ring, true);
        var sparkMat = Material("Spark_Additive", streak, true);
        var smokeMat = Material("Smoke_Lit_Alpha", soft, false, true);
        smokeMat.SetFloat("_Smoothness", 0);
        var heatMat = new Material(Shader.Find("Nova/Particles/Distortion"));
        heatMat.name = "Heat_Distortion";
        heatMat.SetTexture("_BaseMap", distortion);
        heatMat.SetFloat("_BaseMapChannelsX", 0);
        heatMat.SetFloat("_BaseMapChannelsY", 1);
        heatMat.SetFloat("_DistortionIntensity", .5f);
        heatMat.SetFloat("_Cull", 0);
        heatMat.renderQueue = 3000;
        ParticlesDistortionMaterialPostProcessor.SetupMaterialKeywords(heatMat);
        heatMat = SaveMaterial(heatMat);

        var fire = Sample("01_Flame", -6);
        var p = Particles(fire, "Flame wisps", fireMat, 30, .9f, 1.1f, .8f, new Color(5,1.2f,.12f), .28f);
        var velocity = p.velocityOverLifetime; velocity.enabled=true; velocity.z=1.2f;
        var shape = p.shape; shape.angle=8;
        Size(p, .3f, 1, 0);
        p = Particles(fire, "Embers", glowMat, 16, 1.5f, 1.5f, .07f, new Color(5,2,.3f), .25f);
        var noise = p.noise; noise.enabled=true; noise.strength=.35f; noise.frequency=.8f;
        p = Particles(fire, "Lit smoke", smokeMat, 8, 2.5f, .6f, .65f, new Color(.25f,.28f,.33f,.3f), .2f);
        p.transform.localPosition = new Vector3(0,1,0); Size(p,.4f,.8f,1.6f);

        var magic = Sample("02_ArcaneCircle", -3);
        p = Particles(magic, "Rotating rune", runeMat, 0, 2.9f, 0, 2.2f, new Color(.15f,2.5f,5), 0);
        Burst(p,1,3); Horizontal(p); p.transform.localPosition=Vector3.up*.08f;
        var rotation = p.rotationOverLifetime; rotation.enabled=true; rotation.z=.35f;
        p = Particles(magic,"Rising motes",glowMat,20,1.9f,.7f,.06f,new Color(.2f,3,5),.75f);
        shape=p.shape; shape.angle=0;

        var shock = Sample("03_Shockwave", 0);
        p=Particles(shock,"Expanding ring",ringMat,0,1.05f,0,1,new Color(.5f,2.4f,5),0);
        Burst(p,1,2.2f); Horizontal(p); Size(p,.15f,3.2f,5); p.transform.localPosition=Vector3.up*.08f;
        p=Particles(shock,"Impact flash",glowMat,0,.22f,0,1.6f,new Color(3,4,6),0); Burst(p,1,2.2f);
        p=Particles(shock,"Radial sparks",sparkMat,0,.7f,4,.12f,new Color(.4f,2,5),.05f); Burst(p,28,2.2f);
        shape=p.shape; shape.shapeType=ParticleSystemShapeType.Sphere;
        p.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.Stretch;

        var sparks=Sample("04_Sparks",3);
        p=Particles(sparks,"Golden sparks",sparkMat,35,1.1f,3,.09f,new Color(7,2.4f,.3f),.12f);
        var main=p.main; main.gravityModifier=1;
        shape=p.shape; shape.angle=28;
        var renderer=p.GetComponent<ParticleSystemRenderer>(); renderer.renderMode=ParticleSystemRenderMode.Stretch; renderer.lengthScale=2; renderer.velocityScale=.12f;
        p=Particles(sparks,"Source glow",glowMat,5,.25f,0,.3f,new Color(4,1,.15f),0);

        var heat=Sample("05_HeatDistortion",6);
        p=Particles(heat,"Heat shimmer",heatMat,6,1.8f,.55f,1.8f,Color.white,.35f);
        Size(p,.5f,1,1.2f);
        p=Particles(heat,"Heat source",glowMat,6,.45f,0,.6f,new Color(2,.35f,.04f),.15f);

        var gallery=new GameObject("Nova HDRP - Effect Samples");
        Undo.RegisterCreatedObjectUndo(gallery,"Create Nova HDRP samples");
        foreach(var sample in samples)
        {
            Vector3 position=sample.transform.position;
            sample.transform.position=Vector3.zero;
            var prefab=PrefabUtility.SaveAsPrefabAsset(sample,Root+"/Prefabs/"+sample.name+".prefab");
            UnityEngine.Object.DestroyImmediate(sample);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,gallery.transform);
            instance.transform.localPosition=position;
        }
        BuildStage(gallery);
        foreach(var ps in gallery.GetComponentsInChildren<ParticleSystem>()) { ps.Simulate(1.2f,false,true); ps.Play(); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(gallery.scene);
        Selection.activeGameObject=gallery;
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0,1,0),Quaternion.Euler(24,0,0),12);
        File.WriteAllText("Temp/NovaHDRPEffectsCreated.txt", "Created 5 prefabs and placed samples in " + gallery.scene.path);
        EditorApplication.delayCall += Capture;
    }

    [MenuItem("Tools/Nova/HDRP/Capture Effect Samples")]
    public static void Capture()
    {
        var gallery=GameObject.Find("Nova HDRP - Effect Samples");
        if(gallery==null || Camera.main==null) return;
        foreach(var label in gallery.GetComponentsInChildren<TextMesh>()) label.characterSize=.055f;
        var backdrop=gallery.transform.Find("Heat distortion backdrop");
        if(backdrop!=null)
        {
            // Nova consumes vertex colors; Unity's primitive quad has no color stream.
            string meshPath=Root+"/Materials/BackdropMesh.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                mesh=UnityEngine.Object.Instantiate(backdrop.GetComponent<MeshFilter>().sharedMesh);
                mesh.name="Nova backdrop with white vertex colors";
                var colors=new Color[mesh.vertexCount]; for(int i=0;i<colors.Length;i++) colors[i]=Color.white;
                mesh.colors=colors; AssetDatabase.CreateAsset(mesh,meshPath);
            }
            backdrop.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        var sampleCamera=gallery.transform.Find("Sample Camera");
        if(sampleCamera!=null) sampleCamera.GetComponent<Camera>().fieldOfView=38;
        foreach(var ps in gallery.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(.65f,false,true);
        var target=new RenderTexture(1440,720,24,RenderTextureFormat.ARGBHalf);
        var previous=RenderTexture.active;
        bool asyncCompilation=ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation=false;
        var capture=new Texture2D(1440,720,TextureFormat.RGBA32,false);
        try
        {
            target.Create();
            var request=new RenderPipeline.StandardRequest { destination=target };
            for(int i=0;i<3;i++) RenderPipeline.SubmitRenderRequest(Camera.main,request);
            RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,1440,720),0,0); capture.Apply();
            // HDR target readback is linear; PNG viewers expect sRGB.
            var pixels=capture.GetPixels(); for(int i=0;i<pixels.Length;i++) pixels[i]=pixels[i].gamma;
            capture.SetPixels(pixels); capture.Apply();
            File.WriteAllBytes(Root+"/Preview.png",capture.EncodeToPNG());
            AssetDatabase.ImportAsset(Root+"/Preview.png");
        }
        finally { ShaderUtil.allowAsyncCompilation=asyncCompilation; RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target); }
    }

    static GameObject Sample(string name,float x)
    {
        var go=new GameObject(name); go.transform.position=new Vector3(x,0,0); samples.Add(go); return go;
    }
    static ParticleSystem Particles(GameObject parent,string name,Material material,float rate,float life,float speed,float size,Color color,float radius)
    {
        var go=new GameObject(name); go.transform.SetParent(parent.transform,false);
        go.transform.localRotation=Quaternion.Euler(-90,0,0);
        var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.duration=3; main.loop=true; main.startLifetime=life; main.startSpeed=speed; main.startSize=size; main.startColor=color; main.maxParticles=256; main.simulationSpace=ParticleSystemSimulationSpace.Local; main.playOnAwake=true;
        ps.useAutoRandomSeed=false; ps.randomSeed=(uint)(samples.Count*100+parent.transform.childCount);
        var emission=ps.emission; emission.rateOverTime=rate;
        var shape=ps.shape; shape.enabled=radius>0; shape.shapeType=ParticleSystemShapeType.Cone; shape.radius=radius; shape.angle=12;
        var col=ps.colorOverLifetime; col.enabled=true;
        var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0,1)}); col.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
        return ps;
    }
    static void Burst(ParticleSystem p,short count,float duration) { var main=p.main; main.duration=duration; var emission=p.emission; emission.SetBursts(new[]{new ParticleSystem.Burst(0,count)}); }
    static void Horizontal(ParticleSystem p) { p.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard; }
    static void Size(ParticleSystem p,float start,float middle,float end) { var size=p.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,start),new Keyframe(.4f,middle),new Keyframe(1,end))); }
    static Texture2D Texture(string name,Func<float,float,Color> pixel,bool linear=false)
    {
        string path=Root+"/Textures/"+name+".png";
        if(!File.Exists(path))
        {
            var tex=new Texture2D(256,256,TextureFormat.RGBA32,false,linear);
            for(int y=0;y<256;y++) for(int x=0;x<256;x++) tex.SetPixel(x,y,pixel((x+.5f)/128-1,(y+.5f)/128-1));
            tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.sRGBTexture=!linear; importer.alphaIsTransparency=!linear; importer.wrapMode=TextureWrapMode.Clamp; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Material(string name,Texture texture,bool additive,bool lit=false)
    {
        var m=new Material(Shader.Find(lit?"Nova/Particles/UberLit":"Nova/Particles/UberUnlit")); m.name=name;
        m.SetTexture("_BaseMap",texture); m.SetFloat("_RenderType",2); m.SetFloat("_TransparentBlendMode",additive?1:0); m.SetFloat("_BlendSrc",5); m.SetFloat("_BlendDst",additive?1:10); m.SetFloat("_ZWrite",0); m.SetFloat("_Cull",0); m.renderQueue=3000;
        ParticlesUberUnlitMaterialPostProcessor.SetupMaterialKeywords(m);
        if(lit) ParticlesUberLitMaterialPostProcessor.SetupMaterialKeywords(m);
        return SaveMaterial(m);
    }
    static Material SaveMaterial(Material material)
    {
        string path=Root+"/Materials/"+material.name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing!=null) { UnityEngine.Object.DestroyImmediate(material); return existing; }
        AssetDatabase.CreateAsset(material,path); return material;
    }
    static void BuildStage(GameObject gallery)
    {
        // A rig belongs to the sample root; existing scene cameras and volumes remain intact.
        if(Camera.main==null)
        {
            var go=new GameObject("Sample Camera"); go.transform.SetParent(gallery.transform,false); go.tag="MainCamera";
            go.transform.position=new Vector3(0,6,-15); go.transform.LookAt(new Vector3(0,.7f,0));
            var camera=go.AddComponent<Camera>(); camera.fieldOfView=48;
            var hd=go.AddComponent<HDAdditionalCameraData>(); hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color; hd.backgroundColorHDR=new Color(.012f,.018f,.03f);
            hd.customRenderingSettings=true; hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Distortion,true);
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Distortion]=true;
            var volumeGo=new GameObject("Sample Exposure and Bloom"); volumeGo.transform.SetParent(gallery.transform,false);
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(0);
            var bloom=profile.Add<Bloom>(); bloom.intensity.Override(.25f); bloom.threshold.Override(1);
            string path=Root+"/Materials/SampleVolume.asset";
            var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(existing==null) { AssetDatabase.CreateAsset(profile,path); AssetDatabase.AddObjectToAsset(exposure,profile); AssetDatabase.AddObjectToAsset(bloom,profile); }
            else { UnityEngine.Object.DestroyImmediate(exposure); UnityEngine.Object.DestroyImmediate(bloom); UnityEngine.Object.DestroyImmediate(profile); profile=existing; }
            var volume=volumeGo.AddComponent<Volume>(); volume.isGlobal=true; volume.sharedProfile=profile;
            var lightGo=new GameObject("Sample Light"); lightGo.transform.SetParent(gallery.transform,false); lightGo.transform.rotation=Quaternion.Euler(40,-30,0);
            var light=lightGo.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=2; lightGo.AddComponent<HDAdditionalLightData>();
        }
        var grid=Texture("HeatBackdrop",(u,v)=> ((Mathf.FloorToInt((u+1)*12)+Mathf.FloorToInt((v+1)*12))%2==0)?new Color(.1f,.25f,.4f):new Color(.6f,.75f,.9f));
        var mat=Material("Backdrop",grid,false); mat.SetFloat("_RenderType",0); mat.SetFloat("_BlendSrc",1); mat.SetFloat("_BlendDst",0); mat.SetFloat("_ZWrite",1); mat.renderQueue=2000;
        var plane=GameObject.CreatePrimitive(PrimitiveType.Quad); plane.name="Heat distortion backdrop"; plane.transform.SetParent(gallery.transform,false); plane.transform.localPosition=new Vector3(6,1.1f,1); plane.transform.localScale=new Vector3(2.5f,2.5f,1); plane.GetComponent<Renderer>().sharedMaterial=mat;
        for(int i=0;i<5;i++)
        {
            var label=new GameObject("Label "+(i+1)); label.transform.SetParent(gallery.transform,false); label.transform.localPosition=new Vector3(-6+3*i,-.25f,-1);
            var text=label.AddComponent<TextMesh>(); text.text=new[]{"01  FLAME","02  ARCANE","03  SHOCKWAVE","04  SPARKS","05  HEAT"}[i]; text.anchor=TextAnchor.MiddleCenter; text.characterSize=.055f; text.fontSize=48; text.color=new Color(.5f,.7f,.9f);
        }
    }
}
