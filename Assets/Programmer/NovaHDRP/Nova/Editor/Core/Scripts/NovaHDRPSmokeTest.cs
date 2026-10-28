using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Nova.Editor.Core.Scripts
{
    public static class NovaHDRPSmokeTest
    {
        [MenuItem("Tools/Nova/HDRP/Render Smoke Test")]
        public static void RenderSmokeTest()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var resources = new System.Collections.Generic.List<Object>();
            var previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Nova HDRP validation camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -7);
                camera.fieldOfView = 35;
                var hdCamera = cameraObject.AddComponent<HDAdditionalCameraData>();
                hdCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hdCamera.backgroundColorHDR = new Color(0.025f, 0.025f, 0.025f);
                var lightObject = new GameObject("Validation light");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 10000;
                lightObject.AddComponent<HDAdditionalLightData>();
                lightObject.transform.rotation = Quaternion.Euler(30, -30, 0);
                var volumeObject = new GameObject("Validation exposure");
                SceneManager.MoveGameObjectToScene(volumeObject, scene);
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10000;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                resources.Add(profile);
                var exposure = profile.Add<Exposure>();
                resources.Add(exposure);
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(8);
                volume.sharedProfile = profile;
                var texture = new Texture2D(32, 32);
                resources.Add(texture);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        texture.SetPixel(x, y, ((x / 4 + y / 4) % 2 == 0) ? new Color(0.2f, 0.8f, 1) : new Color(1, 0.3f, 0.1f));
                texture.Apply();
                for (int index = 0; index < 2; index++)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    SceneManager.MoveGameObjectToScene(sphere, scene);
                    sphere.transform.position = new Vector3(index == 0 ? -1.1f : 1.1f, 0, 0);
                    sphere.transform.localScale = Vector3.one * 1.7f;
                    var shader = Shader.Find(index == 0 ? "Nova/Particles/UberUnlit" : "Nova/Particles/UberLit");
                    var material = new Material(shader);
                    resources.Add(material);
                    material.SetTexture("_BaseMap", texture);
                    material.SetFloat("_RenderType", 0);
                    material.SetFloat("_BlendSrc", 1);
                    material.SetFloat("_BlendDst", 0);
                    material.SetFloat("_ZWrite", 1);
                    material.renderQueue = 2000;
                    material.EnableKeyword("_BASE_MAP_MODE_2D");
                    material.EnableKeyword("_SPECULAR_HIGHLIGHTS_ENABLED");
                    material.EnableKeyword("_RECEIVE_SHADOWS_ENABLED");
                    sphere.GetComponent<Renderer>().sharedMaterial = material;
                }
                var target = new RenderTexture(768, 384, 24, RenderTextureFormat.ARGBHalf);
                resources.Add(target);
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                // Let HDRP initialize its camera history before the captured frame.
                for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                var capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                resources.Add(capture);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                Directory.CreateDirectory("Temp");
                File.WriteAllBytes("Temp/NovaHDRPSmokeTest.png", capture.EncodeToPNG());
                Debug.Log("Nova HDRP preview saved to Temp/NovaHDRPSmokeTest.png (Unlit left, Lit right).");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var resource in resources) Object.DestroyImmediate(resource);
            }
        }
    }
}
