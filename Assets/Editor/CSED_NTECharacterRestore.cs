#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Restores the shared chat's BaseMap * BaseColor stage using the installed Shader Graph API.
[InitializeOnLoad]
internal static class CSED_NTECharacterRestore
{
    const string Folder = "Assets/Programmer/Shader/Character";
    const string Body = Folder + "/SH_NTECharacter_Body.shadergraph";
    const string Transparent = Folder + "/SH_NTECharacter_Body_Transparent.shadergraph";
    const string Teto = "Assets/Teto_GameReady_UnityHDRP_v5/Teto_GameReady_UnityHDRP";
    const string Request = "Library/NTECharacterRestore.request";
    const string Report = "Library/NTECharacterRestore.result.txt";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string SG = "UnityEditor.ShaderGraph.";
    const string HD = "UnityEditor.Rendering.HighDefinition.ShaderGraph.";

    static CSED_NTECharacterRestore()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
    }

    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequested;
            return;
        }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Restore();
    }

    [MenuItem("Tools/NTE Character/Restore BaseMap and Teto Materials")]
    static void Restore()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            CreateGraph(Body, false);
            CreateGraph(Transparent, true);
            AssetDatabase.ImportAsset(Body, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Transparent, ImportAssetOptions.ForceSynchronousImport);
            var body = AssetDatabase.LoadAssetAtPath<Shader>(Body);
            var transparent = AssetDatabase.LoadAssetAtPath<Shader>(Transparent);
            if (body == null || transparent == null) throw new Exception("Shader Graph import failed.");
            if (ShaderUtil.ShaderHasError(body) || ShaderUtil.ShaderHasError(transparent))
                throw new Exception("Shader compilation error; materials were not changed.");

            var specs = (IEnumerable)TypeOf("TetoGameReadyImporter").GetField("Specs", Flags).GetValue(null);
            var entries = new List<Tuple<object, Material, Texture2D, string>>();
            foreach (object spec in specs)
            {
                string name = (string)Get(spec, "sourceName");
                string path = Teto + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Teto + "/Textures/" + Get(spec, "baseTex"));
                if (mat == null || tex == null) throw new Exception("Missing original material or texture: " + path);
                entries.Add(Tuple.Create(spec, mat, tex, path));
            }
            string backup = "Backups/NTECharacterRestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            foreach (var e in entries)
            {
                File.Copy(e.Item4, backup + "/" + Path.GetFileName(e.Item4));
                File.Copy(e.Item4 + ".meta", backup + "/" + Path.GetFileName(e.Item4) + ".meta");
            }
            var lines = new List<string> { "BaseMap * BaseColor restoration", "Backup: " + backup };
            foreach (var e in entries)
            {
                object spec = e.Item1;
                Material mat = e.Item2;
                bool blend = (string)Get(spec, "alphaMode") == "BLEND";
                Undo.RecordObject(mat, "Restore Teto NTE material");
                mat.shader = blend ? transparent : body;
                mat.shaderKeywords = Array.Empty<string>();
                mat.SetTexture("_BaseMap", e.Item3);
                mat.SetTextureScale("_BaseMap", Vector2.one);
                mat.SetTextureOffset("_BaseMap", Vector2.zero);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_SurfaceType", blend ? 1 : 0);
                mat.SetFloat("_AlphaCutoffEnable", blend ? 0 : 1);
                mat.SetFloat("_AlphaCutoff", (float)Get(spec, "alphaCutoff"));
                mat.SetFloat("_DoubleSidedEnable", (bool)Get(spec, "doubleSided") ? 1 : 0);
                mat.SetFloat("_DoubleSidedNormalMode", 1);
                mat.SetFloat("_BlendMode", 0);
                mat.SetFloat("_ZWrite", blend ? 0 : 1);
                mat.renderQueue = blend ? 3000 : 2450;
                mat.SetOverrideTag("RenderType", blend ? "Transparent" : "TransparentCutout");
                // Remove stale shader-specific disabled passes from previous HoyoToon assignments.
                var serialized = new SerializedObject(mat);
                var passes = serialized.FindProperty("disabledShaderPasses");
                if (passes != null) { passes.ClearArray(); serialized.ApplyModifiedPropertiesWithoutUndo(); }
                UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(mat);
                EditorUtility.SetDirty(mat);
                lines.Add(mat.name + " -> " + e.Item3.name + (blend ? " (transparent)" : " (alpha clip)"));
            }
            AssetDatabase.SaveAssets();
            int checkedCount = 0;
            foreach (var e in entries)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(e.Item4);
                if (mat.GetTexture("_BaseMap") != e.Item3 || mat.GetColor("_BaseColor") != Color.white)
                    throw new Exception("Material verification failed: " + e.Item4);
                checkedCount++;
            }
            lines.Add("PASS: " + checkedCount + " materials; both shaders imported without shader errors.");
            File.WriteAllLines(Report, lines);
            Debug.Log("NTE restore complete: " + checkedCount + " materials. Backup: " + backup);
            Selection.activeObject = body;
            EditorGUIUtility.PingObject(body);
            SceneView.RepaintAll();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Report, "FAILED\n" + ex);
            Debug.LogException(ex);
        }
    }

    static void CreateGraph(string path, bool transparent)
    {
        // Never overwrite graph work from a later lesson.
        if (File.Exists(path)) return;
        object graph = New(SG + "GraphData");
        Call(graph, "AddContexts");
        Set(graph, "path", "Shader Graphs");
        object target = New(HD + "HDTarget");
        Call(target, "TrySetActiveSubTarget", TypeOf(HD + "HDLitSubTarget"));
        object system = Get(Get(target, "activeSubTarget"), "systemData");
        SetEnum(system, "surfaceType", transparent ? "Transparent" : "Opaque");
        SetEnum(system, "renderQueueType", transparent ? "Transparent" : "Opaque");
        SetEnum(system, "doubleSidedMode", "MirroredNormals");
        Set(system, "alphaTest", !transparent);
        var descriptors = new List<object>();
        foreach (string name in new[] { "Position", "Normal", "Tangent" }) descriptors.Add(Descriptor("BlockFields", "VertexDescription", name));
        foreach (string name in new[] { "BaseColor", "NormalTS", "Metallic", "Emission", "Smoothness", "Occlusion", "Alpha" }) descriptors.Add(Descriptor("BlockFields", "SurfaceDescription", name));
        descriptors.Add(Descriptor("HDBlockFields", "SurfaceDescription", "BentNormal"));
        if (!transparent) descriptors.Add(Descriptor("BlockFields", "SurfaceDescription", "AlphaClipThreshold"));
        Call(graph, "InitializeOutputs", ArrayOf(TypeOf(SG + "Target"), new[] { target }), ArrayOf(descriptors[0].GetType(), descriptors));
        object category = New(SG + "CategoryData");
        Set(category, "name", "");
        Call(graph, "AddCategory", category);
        object baseMap = New(SG + "Internal.Texture2DShaderProperty");
        object baseColor = New(SG + "Internal.ColorShaderProperty");
        Set(baseColor, "value", Color.white);
        foreach (var item in new[] { Tuple.Create(baseMap, "BaseMap", "_BaseMap"), Tuple.Create(baseColor, "BaseColor", "_BaseColor") })
        {
            Set(item.Item1, "displayName", item.Item2);
            Set(item.Item1, "overrideReferenceName", item.Item3);
            Set(item.Item1, "generatePropertyBlock", true);
            Call(graph, "AddGraphInput", item.Item1, -1);
            Call(graph, "InsertItemIntoCategory", Get(category, "categoryGuid"), item.Item1, -1);
        }
        object mapNode = Node(graph, "PropertyNode", -850, 0);
        Set(mapNode, "property", baseMap);
        object colorNode = Node(graph, "PropertyNode", -500, 350);
        Set(colorNode, "property", baseColor);
        object sample = Node(graph, "SampleTexture2DNode", -600, 0);
        object multiply = Node(graph, "MultiplyNode", -250, 0);
        Connect(graph, mapNode, 0, sample, 1);
        Connect(graph, sample, 0, multiply, 0);
        Connect(graph, colorNode, 0, multiply, 1);
        var blocks = (IEnumerable)graph.GetType().GetMethod("GetNodes", Flags).MakeGenericMethod(TypeOf(SG + "BlockNode")).Invoke(graph, null);
        foreach (object block in blocks)
        {
            string name = (string)Get(Get(block, "descriptor"), "name");
            if (name == "BaseColor") Connect(graph, multiply, 2, block, 0);
            if (name == "Alpha") Connect(graph, sample, 7, block, 0);
        }
        Call(graph, "ValidateGraph");
        if (Call(TypeOf(SG + "FileUtilities"), "WriteShaderGraphToDisk", path, graph) == null)
            throw new IOException("Could not save " + path);
    }

    static object Node(object graph, string type, float x, float y)
    {
        object node = New(SG + type);
        object state = Get(node, "drawState");
        Set(state, "position", new Rect(x, y, 200, 150));
        Set(node, "drawState", state);
        Call(graph, "AddNode", node, true);
        return node;
    }
    static void Connect(object graph, object from, int fromSlot, object to, int toSlot)
    {
        if (Call(graph, "Connect", Call(from, "GetSlotReference", fromSlot), Call(to, "GetSlotReference", toSlot)) == null)
            throw new Exception("Could not connect shader nodes.");
    }
    static object Descriptor(string type, string group, string name) => TypeOf((type == "HDBlockFields" ? HD : SG) + type).GetNestedType(group, Flags).GetField(name, Flags).GetValue(null);
    static Array ArrayOf(Type type, IEnumerable<object> values)
    {
        var list = values.ToArray(); var array = Array.CreateInstance(type, list.Length);
        for (int i = 0; i < list.Length; i++) array.SetValue(list[i], i);
        return array;
    }
    static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null) ?? throw new TypeLoadException(name);
    static object New(string name) => Activator.CreateInstance(TypeOf(name), true);
    static object Get(object obj, string name)
    {
        var property = obj.GetType().GetProperty(name, Flags);
        return property != null ? property.GetValue(obj) : obj.GetType().GetField(name, Flags).GetValue(obj);
    }
    static void Set(object obj, string name, object value)
    {
        var property = obj.GetType().GetProperty(name, Flags);
        if (property != null) property.SetValue(obj, value); else obj.GetType().GetField(name, Flags).SetValue(obj, value);
    }
    static void SetEnum(object obj, string name, string value) => Set(obj, name, Enum.Parse(obj.GetType().GetProperty(name, Flags).PropertyType, value));
    static object Call(object obj, string name, params object[] args)
    {
        Type type = obj as Type ?? obj.GetType();
        var method = type.GetMethods(Flags).First(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(b => b));
        return method.Invoke(obj is Type ? null : obj, args);
    }
}
#endif
