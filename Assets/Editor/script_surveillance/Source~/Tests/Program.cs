using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MS2027.ScriptSurveillance;

internal static class Program
{
    private const string Stubs = @"
namespace UnityEngine {
 public class Object { }
 public class MonoBehaviour : Object { }
 public class ScriptableObject : Object { }
 public class SerializeField : System.Attribute { }
 public class SerializeReference : System.Attribute { }
 public class GameObject { public static GameObject Find(string name) => null; }
 public static class Debug { public static void Log(object value) { } }
}
namespace UnityEngine.Rendering { public class VolumeComponent : UnityEngine.ScriptableObject { } }
";

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source, string path = "Assets/CS_Test.cs", string extra = "", bool live = false)
    {
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: path),
            CSharpSyntaxTree.ParseText(Stubs, path: "Packages/Unity.cs"),
            CSharpSyntaxTree.ParseText(extra, path: "Assets/CS_Other.cs") };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(System.IO.Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("Tests", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new Exception(string.Join("\n", errors.Select(d => d.ToString())));
        var analysis = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CSED_SurveillanceAnalyzer()));
        if (live) return await analysis.GetAnalyzerSemanticDiagnosticsAsync(compilation.GetSemanticModel(trees[0]),
            null, default);
        return await analysis.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task Check(string label, string source, string id, int count,
        string path = "Assets/CS_Test.cs", string extra = "")
    {
        var diagnostics = await Analyze(source, path, extra);
        if (diagnostics.Any(d => d.Id == "AD0001") || diagnostics.Count(d => d.Id == id) != count)
            throw new Exception(label + ": " + string.Join("\n", diagnostics.Select(d => d.ToString())));
        if (diagnostics.Any(d => d.Id.StartsWith("MS270") && d.Severity != DiagnosticSeverity.Error
            || d.Id.StartsWith("MS271") && d.Severity != DiagnosticSeverity.Warning)) throw new Exception("Wrong severity");
        Console.WriteLine("PASS " + label);
    }

    private static async Task Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--project")
        {
            await ProjectTests.AnalyzeProject(args[1]);
            return;
        }
        ProjectTests.Run();
        var liveDiagnostics = await Analyze("using UnityEngine; public class CS_NewFile : MonoBehaviour { private int a = 0; void Start() {} void Update() {} }", live: true);
        if (!liveDiagnostics.Any(d => d.Id == "MS27001" && d.Severity == DiagnosticSeverity.Error))
            throw new Exception("Live document analysis did not report the screenshot's unused field");
        Console.WriteLine("PASS live document unused field (screenshot reproduction)");
        var liveSerialized = await Analyze("class CS_Test { [UnityEngine.SerializeField] private int _value = 0; }", live: true);
        if (liveSerialized.Any(d => d.Id == "MS27001")) throw new Exception("Live serialized field false positive");
        var livePartial = await Analyze("partial class CS_Test { private int _value = 0; }",
            extra: "partial class CS_Test { public int Get() => _value; }", live: true);
        if (livePartial.Any(d => d.Id == "MS27001")) throw new Exception("Live partial field false positive");
        Console.WriteLine("PASS live document serialized/partial field exclusions");
        await Check("unused using", "using System; class CS_Test {}", "MS27001", 1);
        await Check("used alias", "using Text = System.String; class CS_Test { public Text Get() => null; }", "MS27001", 0);
        await Check("extension using", "using System.Linq; class CS_Test { public int Get() => new int[0].Count(); }", "MS27001", 0);
        await Check("unused locals", "class CS_Test { void Run() { int unused; int assigned = 1; } }", "MS27001", 2);
        await Check("unused fields", "class CS_Test { private int _unused; private int _assigned = 1; }", "MS27001", 2);
        await Check("serialized fields", "class CS_Test { [UnityEngine.SerializeField] private int _unused; [UnityEngine.SerializeReference] private object _reference; }", "MS27001", 0);
        await Check("serializable data", "[System.Serializable] class CS_Test { private int _unused; }", "MS27001", 0);
        await Check("partial reference", "partial class CS_Test { private int _value = 1; }", "MS27001", 0,
            extra: "partial class CS_Test { public int Get() => _value; }");
        await Check("naming", "class CS_Test { private int Bad; protected int wrong; public void Run(int BadParameter) { int BadLocal = BadParameter; System.Console.WriteLine(BadLocal); } }", "MS27101", 4);
        await Check("valid naming", "class CS_Test { private int _playerHP; protected int _value; public void Run(int value) {} }", "MS27101", 0);
        await Check("public field", "class CS_Test { public int value; public const int max = 10; }", "MS27105", 1);
        await Check("file prefix", "class Test {}", "MS27102", 1, "Assets/Test.cs");
        await Check("editor prefix", "class Test {}", "MS27102", 0, "Assets/Editor/CSED_Test.cs");
        await Check("enum prefix", "enum Test { A }", "MS27102", 0, "Assets/CSE_Test.cs");
        await Check("indirect SO", "class Test : Base {} class Base : UnityEngine.ScriptableObject {}", "MS27102", 0, "Assets/CSO_Test.cs");
        await Check("volume prefix", "class Test : UnityEngine.Rendering.VolumeComponent {}", "MS27102", 0, "Assets/CSV_Test.cs");
        await Check("frame calls", "class CS_Test : UnityEngine.MonoBehaviour { void Update() { UnityEngine.GameObject.Find(\"x\"); UnityEngine.Debug.Log(1); } }", "MS27103", 2);
        await Check("startup allowed", "class CS_Test : UnityEngine.MonoBehaviour { void Start() { UnityEngine.GameObject.Find(\"x\"); } }", "MS27103", 0);
        await Check("non Unity Update", "class CS_Test { void Update() { UnityEngine.Debug.Log(1); } }", "MS27103", 0);
        await Check("deferred lambda", "class CS_Test : UnityEngine.MonoBehaviour { void Update() { System.Action log = () => UnityEngine.Debug.Log(1); log(); } }", "MS27103", 0);
        await Check("recursive setter", "class CS_Test { public int value { get => 0; set => this.value = value; } }", "MS27002", 1);
        await Check("backing setter", "class CS_Test { private int _value; public int value { get => _value; set => _value = value; } }", "MS27002", 0);
        await Check("other instance", "class CS_Test { private CS_Test _other; public int value { get => 0; set => _other.value = value; } }", "MS27002", 0);
        await Check("nesting", "class CS_Test { void Run(bool flag) { if(flag) { while(flag) { for(;;) { if(flag) break; } } } } }", "MS27104", 1);
        await Check("else if", "class CS_Test { void Run(bool flag) { if(flag) {} else if(flag) {} else if(flag) {} else if(flag) {} } }", "MS27104", 0);
        await Check("method length", "class CS_Test { void Run() {\n" + new string('\n', 81) + "} }", "MS27104", 1);
        await Check("class length", "class CS_Test {\n" + new string('\n', 801) + "}", "MS27104", 1);
        await Check("packages excluded", "using System; class Bad {}", "MS27001", 0, "Packages/Bad.cs");
        await Check("generated excluded", "// <auto-generated/>\nusing System; class Bad {}", "MS27001", 0);
        await Check("plugins excluded", "using System; class Bad {}", "MS27102", 0, "Assets/Plugins/Bad.cs");
        Console.WriteLine("All analyzer tests passed.");
    }
}
