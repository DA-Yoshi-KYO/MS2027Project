using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.IO;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityEditor
{
    internal class AssetPostprocessor { }
    internal class InitializeOnLoadAttribute : Attribute { }
    internal class MenuItem : Attribute { public MenuItem(string path) { } }
    internal static class EditorApplication { public static Action delayCall; }
}

namespace UnityEngine
{
    internal static class Application { public static string dataPath => System.IO.Path.GetFullPath("Assets"); }
    internal static class Debug
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
    }
}

internal static class ProjectTests
{
    internal static async Task AnalyzeProject(string projectPath)
    {
        projectPath = Path.GetFullPath(projectPath);
        string root = Path.GetDirectoryName(projectPath);
        var document = XDocument.Load(projectPath);
        XNamespace ns = document.Root.Name.Namespace;
        string defines = document.Descendants(ns + "DefineConstants").First().Value;
        var options = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defines.Split(';'));
        var files = document.Descendants(ns + "Compile").Select(n => Path.GetFullPath(Path.Combine(root, (string)n.Attribute("Include")))).ToList();
        string hook = Path.Combine(root, "Assets/Editor/script_surveillance/CSED_SurveillanceProject.cs");
        if (projectPath.EndsWith("-Editor.csproj") && !files.Contains(hook)) files.Add(hook);
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file));
        var references = document.Descendants(ns + "HintPath").Select(n => Path.GetFullPath(Path.Combine(root, n.Value)))
            .Where(File.Exists).Distinct().Select(file => MetadataReference.CreateFromFile(file));
        var compilation = CSharpCompilation.Create("Integration", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
            new MS2027.ScriptSurveillance.CSED_SurveillanceAnalyzer())).GetAnalyzerDiagnosticsAsync();
        foreach (var group in diagnostics.GroupBy(d => d.Id).OrderBy(g => g.Key))
            Console.WriteLine(group.Key + ": " + group.Count());
        foreach (var diagnostic in diagnostics.Where(d => d.Id == "AD0001")) Console.WriteLine(diagnostic);
        if (diagnostics.Any(d => d.Id == "AD0001")) throw new Exception("Analyzer crashed");
        var hookErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error
            && d.Location.SourceTree?.FilePath == hook).ToArray();
        if (hookErrors.Length != 0) throw new Exception(string.Join("\n", hookErrors.Select(d => d.ToString())));
        Console.WriteLine("PASS " + Path.GetFileName(projectPath) + " actual sources: " + files.Count);
    }

    internal static void Run()
    {
        MethodInfo hook = typeof(MS2027.EditorTools.CSED_SurveillanceProject).GetMethod("OnGeneratedCSProject",
            BindingFlags.Static | BindingFlags.NonPublic);
        foreach (string ns in new[] { "", " xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"" })
        {
            string source = "<Project" + ns + "><PropertyGroup><NoWarn>0169;USG0001</NoWarn></PropertyGroup><ItemGroup><Compile Include=\"Assets/Test.cs\" /></ItemGroup></Project>";
            string result = (string)hook.Invoke(null, new object[] { "Assembly-CSharp.csproj", source });
            var xml = XDocument.Parse(result);
            if (xml.Descendants().Single(n => n.Name.LocalName == "NoWarn").Value != "USG0001")
                throw new Exception("Unused field suppression not restored");
            if (xml.Descendants().Count(n => n.Name.LocalName == "Analyzer") != 1) throw new Exception("Analyzer missing");
            if ((string)hook.Invoke(null, new object[] { "Assembly-CSharp.csproj", result }) != result)
                throw new Exception("Duplicate configuration");
            source = source.Replace("Assets/Test.cs", "Packages/Test.cs");
            if ((string)hook.Invoke(null, new object[] { "Package.csproj", source }) != source)
                throw new Exception("Package modified");
        }
        Console.WriteLine("PASS IDE project registration, regeneration, package exclusion (legacy/SDK XML)");
    }
}
