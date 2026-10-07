using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class SkinShadowValidation
{
    const string Graph = "Assets/Programmer/Scene/Test/Toon/Shader/Graph/SHG_NTE_Skin.shadergraph";
    // プロジェクト直下 Logs/SkinValidation に出力（Logs は通常 Git 管理外）。
    static readonly string Output = Path.GetFullPath("Logs/SkinValidation");

    [MenuItem("Tools/Skin Work/Validate Cast Shadows")]
    static void Run()
    {
        Directory.CreateDirectory(Output);
        var report = new StringBuilder();
        try
        {
            AssetDatabase.ImportAsset(Graph, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Graph);
            if (shader == null) throw new Exception("Skin shader failed to import.");
            report.AppendLine("Shader: " + shader.name + "; supported=" + shader.isSupported);
            foreach (string part in new[] { "Face", "Body" })
            {
                var path = "Assets/Teto_GameReady_UnityHDRP_v5/Teto_GameReady_UnityHDRP/Materials/N00_000_00_" + part + "_00_SKIN (Instance).mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader != shader) throw new Exception(part + " shader assignment incorrect");
                HDMaterial.ValidateMaterial(mat);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
                report.AppendLine("Shadow filter=" + mat.GetFloat("_ShadowMatteFilter") + "; strength=" + mat.GetFloat("_CelCastShadowStrength"));
                for (int pass = 0; pass < mat.passCount; pass++) ShaderUtil.CompilePass(mat, pass, true);
                report.AppendLine(part + ": brightness=" + mat.GetFloat("_CelBrightness") + ", shadow=" + mat.GetFloat("_CelShadowStrength") + ", texture=" + mat.GetTexture("_BaseMap"));
            }
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.sharedMaterials.Any(m => m != null && m.shader == shader)).ToArray();
            foreach (var r in renderers) report.AppendLine("Renderer: " + r.name + " bounds=" + r.bounds);
            var camera = Camera.main;
            if (camera == null) throw new Exception("No main camera for visual validation");
            Capture(camera, null, "skin-shadow-scene.png");
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                Capture(camera, bounds, "skin-shadow-closeup.png");
                var face = renderers.FirstOrDefault(r => r.name == "Face") ?? renderers[0];
                var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional && l.enabled);
                if (sun == null) throw new Exception("No enabled directional light in scene");
                report.AppendLine("Sun=" + sun.name + "; shadows=" + sun.shadows);
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    blocker.name = "Temporary skin shadow test";
                    blocker.transform.position = face.bounds.center - sun.transform.forward * 0.3f + Vector3.right * 0.04f;
                    blocker.transform.localScale = new Vector3(0.12f, 0.13f, 0.12f);
                    blocker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    blocker.SetActive(false);
                    Capture(camera, bounds, "skin-shadow-blocker-off.png");
                    blocker.SetActive(true);
                    Capture(camera, bounds, "skin-shadow-blocker-on.png");
                }
                finally { UnityEngine.Object.DestroyImmediate(blocker); }

            }
            var messages = ShaderUtil.GetShaderMessages(shader);
            foreach (var m in messages) report.AppendLine(m.severity + ": " + m.message + " " + m.file + ":" + m.line);
            report.AppendLine("Shader message count: " + messages.Length);
        }
        catch (Exception e) { report.AppendLine(e.ToString()); }
        File.WriteAllText(Output + "/skin-shadow-validation.txt", report.ToString());
        Debug.Log("Skin validation report written.");
    }

    static void Capture(Camera source, Bounds? bounds, string name)
    {
        var go = new GameObject("SkinValidationCamera") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(960, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            var data = go.AddComponent<HDAdditionalCameraData>();
            var sourceData = source.GetComponent<HDAdditionalCameraData>();
            if (sourceData != null) EditorUtility.CopySerialized(sourceData, data);
            data.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            camera.enabled = false;
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            if (bounds.HasValue)
            {
                var b = bounds.Value;
                // Frame upper body from the same direction as the active game camera.
                var target = b.center + Vector3.up * b.size.y * 0.35f;
                float distance = Mathf.Max(b.size.y * 0.25f, 0.5f) / Mathf.Tan(25 * Mathf.Deg2Rad);
                camera.fieldOfView = 50;
                go.transform.position = target - source.transform.forward * distance;
                go.transform.LookAt(target);
            }
            camera.aspect = 1;
            rt.Create();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
            RenderTexture.active = rt;
            texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
