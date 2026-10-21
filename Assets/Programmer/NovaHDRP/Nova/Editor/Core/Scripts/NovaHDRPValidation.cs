using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.Core.Scripts
{
    /// <summary>Compiles representative Nova variants without changing scene or material assets.</summary>
    public static class NovaHDRPValidation
    {
        [MenuItem("Tools/Nova/HDRP/Validate Shaders")]
        public static void ValidateShaders()
        {
            const string folder = "Assets/Programmer/NovaHDRP/Nova/Runtime/Core/Shaders";
            var report = new StringBuilder();
            report.AppendLine("Nova HDRP shader validation - " + DateTime.Now.ToString("s"));
            report.AppendLine("Unity " + Application.unityVersion);
            string[][] variants =
            {
                new[] { "_BASE_MAP_MODE_2D" },
                new[] { "_BASE_MAP_MODE_2D", "INSTANCING_ON" },
                new[] { "_BASE_MAP_MODE_2D", "PROCEDURAL_INSTANCING_ON" },
                new[] { "_BASE_MAP_MODE_2D", "_SOFT_PARTICLES_ENABLED", "_DEPTH_FADE_ENABLED", "_ALPHATEST_ENABLED" },
                new[] { "_BASE_MAP_MODE_2D_ARRAY", "_BASE_MAP_RANDOM_ROW_SELECTION_ENABLED", "_TINT_AREA_RIM", "_TINT_COLOR_ENABLED", "_VERTEX_DEFORMATION_ENABLED" },
                new[] { "_BASE_MAP_MODE_3D", "_NORMAL_MAP_ENABLED", "_METALLIC_MAP_ENABLED", "_SMOOTHNESS_MAP_ENABLED", "_RECEIVE_SHADOWS_ENABLED", "_SPECULAR_HIGHLIGHTS_ENABLED", "_ENVIRONMENT_REFLECTIONS_ENABLED" },
                new[] { "_BASE_MAP_MODE_2D", "_DISSOLVE_TRANSITION_ENABLED", "_ALPHA_TRANSITION_MAP_MODE_2D", "_FLOW_MAP_TARGET_BASE", "_FLOW_MAP_TARGET_ALPHA_TRANSITION", "_EMISSION_AREA_ALL", "_EMISSION_COLOR_COLOR" },
                new[] { "_BASE_MAP_MODE_2D", "_PARALLAX_MAP_TARGET_BASE", "_PARALLAX_MAP_MODE_2D", "_SPECULAR_SETUP", "_SPECULAR_MAP_ENABLED", "_NORMAL_MAP_ENABLED", "_SHADOW_CASTER_ALPHA_TEST_ENABLED" }
            };
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                foreach (string path in Directory.GetFiles(folder, "*.shader"))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader == null) { report.AppendLine("MISSING " + path); continue; }
                    ShaderUtil.ClearShaderMessages(shader);
                    var material = new Material(shader);
                    try
                    {
                        foreach (var keywords in variants)
                        {
                            material.enableInstancing = true;
                            material.shaderKeywords = keywords;
                            for (int pass = 0; pass < material.passCount; pass++)
                                ShaderUtil.CompilePass(material, pass, true);
                        }
                        report.AppendLine(shader.name + " | supported=" + shader.isSupported + " | passes=" + material.passCount);
                        foreach (var message in ShaderUtil.GetShaderMessages(shader))
                            report.AppendLine(message.severity + " " + message.file + ":" + message.line + " " + message.message);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/NovaHDRPValidation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
