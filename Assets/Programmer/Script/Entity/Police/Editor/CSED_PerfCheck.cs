/* ================================================
 *
 * ================================================
 * 制作者：宇留野陸斗
 * ------------------------------------------------
 * 2026-11-15 | 初回作成
 * ================================================ */

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 再生した時の重さの原因を調べる、計測ツール(配置などは変えない)
/// ・「再生中の負荷を計測する」: 再生して _warmupSeconds 秒たった後の _sampleSeconds 秒間の、1フレームの長さ・CPU/GPUの時間・処理ごとの時間・描画の数を記録する
/// ・「警察の巡回ルートの経路計算の時間を調べる」: 警察のグループの巡回ポイント同士の、経路計算にかかる時間を測る(再生しなくてよい)
/// 結果は Temp/PlayPerfReport.txt / Temp/PoliceRoutePerfReport.txt
/// 使い方: メニュー Tools/Police/再生中の負荷を計測する(次の再生から) でオン・オフを切り替える(チェックが付いている間だけ、再生のたびに計測する)
/// 初期値はオフ(設定はこのPCのエディタだけに保存され、他の人には影響しない)。オフの間は、何も計測しない
/// </summary>
[InitializeOnLoad]
public static class CSED_PerfCheck
{
    private const string _enabledKey = "CSED_PerfCheck_Enabled";
    private const string _menuSampling = "Tools/Police/再生中の負荷を計測する(次の再生から)";
    private const float _warmupSeconds = 15.0f;       // 再生を始めてから、計測を始めるまでの時間(秒)
    private const float _sampleSeconds = 20.0f;       // 計測する時間(秒)
    private const int _topMarkerCount = 70;          // 報告に書く、処理ごとの時間の数

    private enum State
    {
        Idle,
        Warmup,
        Sampling,
    }

    private class MarkerRecorder
    {
        public string name;
        public string category;
        public ProfilerRecorder recorder;
        public double sum;
        public double max;
    }

    private static State _state = State.Idle;
    private static float _stateStart;
    private static int _lastFrame;
    private static readonly List<float> _frameTimes = new List<float>();
    private static readonly List<MarkerRecorder> _markers = new List<MarkerRecorder>();
    private static readonly FrameTiming[] _timings = new FrameTiming[1];
    private static double _cpuFrame;
    private static double _cpuMain;
    private static double _cpuRender;
    private static double _gpuFrame;
    private static int _timingCount;
    private static double _batches;
    private static double _drawCalls;
    private static double _setPass;
    private static double _triangles;
    private static double _shadowCasters;
    private static int _statsCount;
    private static string _countsText = "";
    private static ProfilerRecorder _gcBytes;
    private static ProfilerRecorder _gcCount;
    private static double _gcBytesSum;
    private static double _gcCountSum;

    static CSED_PerfCheck()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem(_menuSampling)]
    private static void ToggleSampling()
    {
        EditorPrefs.SetBool(_enabledKey, !EditorPrefs.GetBool(_enabledKey, false));
    }

    [MenuItem(_menuSampling, true)]
    private static bool ToggleSamplingValidate()
    {
        Menu.SetChecked(_menuSampling, EditorPrefs.GetBool(_enabledKey, false));
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(_enabledKey, false))
        {
            _state = State.Warmup;
            _stateStart = Time.realtimeSinceStartup;
            UnityEngine.Debug.Log($"CSED_PerfCheck: {_warmupSeconds:F0}秒後から {_sampleSeconds:F0}秒間、負荷を計測します(その間、ふだん通り遊んでください)");
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Cleanup();
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;

        if (_state == State.Warmup && Time.realtimeSinceStartup - _stateStart >= _warmupSeconds) StartSampling();
        else if (_state == State.Sampling) Sample();
    }

    /// <summary>
    /// 計測を始めるメソッド(時間を計る処理ごとの記録を作り、その時の物の数を数える)
    /// </summary>
    private static void StartSampling()
    {
        _state = State.Sampling;
        _stateStart = Time.realtimeSinceStartup;
        _lastFrame = Time.frameCount;
        _frameTimes.Clear();
        _cpuFrame = _cpuMain = _cpuRender = _gpuFrame = 0.0;
        _timingCount = 0;
        _batches = _drawCalls = _setPass = _triangles = _shadowCasters = 0.0;
        _statsCount = 0;

        List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (ProfilerRecorderHandle handle in handles)
        {
            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;

            // すべての種類の処理を記録する(メインスレッドの分だけ。別スレッドの待ち時間が混ざらないように)
            string category = description.Category.ToString();
            ProfilerRecorder recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            recorder.Start();
            _markers.Add(new MarkerRecorder { name = description.Name, category = category, recorder = recorder });
        }

        // メインスレッドの、スクリプトごとの時間・メモリ確保を、あとで読むために、プロファイラーの記録も始める
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.enabled = true;

        _gcBytes = new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
        _gcCount = new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        _gcBytes.Start();
        _gcCount.Start();
        _gcBytesSum = _gcCountSum = 0.0;
        _countsText = CountObjects();
        UnityEngine.Debug.Log($"CSED_PerfCheck: 計測を始めました({_markers.Count}個の処理を記録)");
    }

    /// <summary>
    /// 新しいフレームごとに、1フレームの長さ・CPU/GPUの時間・処理ごとの時間・描画の数を足し込むメソッド
    /// </summary>
    private static void Sample()
    {
        if (Time.frameCount != _lastFrame)
        {
            _lastFrame = Time.frameCount;
            _frameTimes.Add(Time.unscaledDeltaTime * 1000.0f);

            foreach (MarkerRecorder marker in _markers)
            {
                if (!marker.recorder.Valid) continue;

                double milliseconds = marker.recorder.LastValue / 1000000.0;
                marker.sum += milliseconds;
                if (milliseconds > marker.max) marker.max = milliseconds;
            }

            if (_gcBytes.Valid) _gcBytesSum += _gcBytes.LastValue;
            if (_gcCount.Valid) _gcCountSum += _gcCount.LastValue;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                _cpuFrame += _timings[0].cpuFrameTime;
                _cpuMain += _timings[0].cpuMainThreadFrameTime;
                _cpuRender += _timings[0].cpuRenderThreadFrameTime;
                _gpuFrame += _timings[0].gpuFrameTime;
                _timingCount++;
            }

            _batches += UnityStats.batches;
            _drawCalls += UnityStats.drawCalls;
            _setPass += UnityStats.setPassCalls;
            _triangles += UnityStats.triangles;
            _shadowCasters += UnityStats.shadowCasters;
            _statsCount++;
        }

        if (Time.realtimeSinceStartup - _stateStart >= _sampleSeconds) FinishSampling();
    }

    private static void FinishSampling()
    {
        _state = State.Idle;
        StringBuilder report = new StringBuilder();
        int frames = _frameTimes.Count;
        if (frames == 0)
        {
            report.AppendLine("計測中に、フレームがありませんでした");
        }
        else
        {
            List<float> sorted = new List<float>(_frameTimes);
            sorted.Sort();
            float average = 0.0f;
            foreach (float time in _frameTimes) average += time;
            average /= frames;
            report.AppendLine($"【フレーム】{frames}フレーム / {_sampleSeconds:F0}秒 / 平均 {average:F1}ms ({1000.0f / average:F0}fps) / 中央値 {sorted[frames / 2]:F1}ms / 95%点 {sorted[Mathf.Min(frames - 1, (int)(frames * 0.95f))]:F1}ms / 最悪 {sorted[frames - 1]:F1}ms");
            if (_timingCount > 0)
            {
                report.AppendLine($"【CPU / GPU】CPU全体 平均 {_cpuFrame / _timingCount:F1}ms / メインスレッド {_cpuMain / _timingCount:F1}ms / レンダースレッド {_cpuRender / _timingCount:F1}ms / GPU {_gpuFrame / _timingCount:F1}ms (サンプル {_timingCount}個)");
            }
            else
            {
                report.AppendLine("【CPU / GPU】FrameTimingManager から値を取れませんでした");
            }
            if (_statsCount > 0)
            {
                report.AppendLine($"【描画(Game画面)】バッチ {_batches / _statsCount:F0} / ドローコール {_drawCalls / _statsCount:F0} / SetPass {_setPass / _statsCount:F0} / 三角形 {_triangles / _statsCount / 1000.0:F0}k / 影を落とす物 {_shadowCasters / _statsCount:F0}");
            }
            report.AppendLine($"【GC】1フレームあたりのメモリ確保 {_gcBytesSum / frames / 1024.0:F1}KB / {_gcCountSum / frames:F0}回");
            report.AppendLine();
            report.AppendLine(_countsText);
            report.AppendLine();
            report.AppendLine($"【メインスレッドの処理ごとの1フレームあたりの時間(平均の大きい順。入れ子の物は、親にも含まれる)】");
            List<MarkerRecorder> ranked = new List<MarkerRecorder>(_markers);
            ranked.Sort((a, b) => b.sum.CompareTo(a.sum));
            for (int i = 0; i < ranked.Count && i < _topMarkerCount; i++)
            {
                if (ranked[i].sum <= 0.0) break;

                report.AppendLine($"  {ranked[i].sum / frames,7:F2}ms (最大 {ranked[i].max,6:F1}ms) [{ranked[i].category}] {ranked[i].name}");
            }
        }

        ProfilerDriver.enabled = false;
        AnalyzeProfilerFrames(report);

        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "PlayPerfReport.txt");
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        UnityEngine.Debug.Log("CSED_PerfCheck: 計測が終わりました。Temp/PlayPerfReport.txt を見てください(再生は止めて構いません)");
        Cleanup();
    }

    /// <summary>
    /// プロファイラーが記録したフレーム(メインスレッド)を読んで、処理の名前ごとの、時間(自分の分だけ)・呼ばれた数・メモリ確保の数を集計して、報告に書くメソッド
    /// メモリ確保(GC.Alloc)は、それを起こした処理(1つ上の処理)の分として数える
    /// </summary>
    private static void AnalyzeProfilerFrames(StringBuilder report)
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first)
        {
            report.AppendLine("【プロファイラーの記録】フレームがありませんでした");
            return;
        }

        Dictionary<string, double[]> totals = new Dictionary<string, double[]>();   // 名前 → { 自分の時間(ms), 呼ばれた数, GC.Allocの数, GC.Allocのバイト数 }
        int frames = 0;
        for (int frame = Mathf.Max(first, last - 250); frame <= last; frame++)
        {
            using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frame, 0))
            {
                if (view == null || !view.valid) continue;

                int count = view.sampleCount;
                float[] time = new float[count];
                float[] childSum = new float[count];
                int[] end = new int[count];     // その処理の、一番最後の子孫の番号(サンプルは、深さ優先の順に並んでいる)
                string[] name = new string[count];
                Stack<int> stack = new Stack<int>();
                for (int i = 0; i < count; i++)
                {
                    time[i] = view.GetSampleTimeMs(i);
                    end[i] = i + (int)view.GetSampleChildrenCount(i);
                    name[i] = view.GetSampleName(i);
                    while (stack.Count > 0 && end[stack.Peek()] < i) stack.Pop();

                    if (stack.Count > 0)
                    {
                        int parent = stack.Peek();
                        childSum[parent] += time[i];
                        if (name[i] == "GC.Alloc")
                        {
                            double[] gcEntry = GetEntry(totals, name[parent]);
                            gcEntry[2] += 1.0;
                            if (view.GetSampleMetadataCount(i) > 0) gcEntry[3] += view.GetSampleMetadataAsLong(i, 0);
                        }
                    }
                    stack.Push(i);
                }

                for (int i = 1; i < count; i++)
                {
                    double[] entry = GetEntry(totals, name[i]);
                    entry[0] += time[i] - childSum[i];
                    entry[1] += 1.0;
                }
                frames++;
            }
        }

        report.AppendLine();
        report.AppendLine($"【プロファイラーの記録(メインスレッド・{frames}フレーム。プロファイラーをオンにしているので、時間は少し大きくなる)】");
        List<KeyValuePair<string, double[]>> byTime = new List<KeyValuePair<string, double[]>>(totals);
        byTime.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
        report.AppendLine("自分の時間(子の処理を除く)が大きい順:");
        for (int i = 0; i < byTime.Count && i < 40; i++) report.AppendLine($"  {byTime[i].Value[0] / frames,7:F2}ms/フレーム 呼ばれた数 {byTime[i].Value[1] / frames,7:F0}/フレーム GC.Alloc {byTime[i].Value[2] / frames,8:F0}回 {byTime[i].Value[3] / frames / 1024.0,8:F1}KB  {byTime[i].Key}");

        List<KeyValuePair<string, double[]>> byGc = new List<KeyValuePair<string, double[]>>(totals);
        byGc.Sort((a, b) => b.Value[2].CompareTo(a.Value[2]));
        report.AppendLine("メモリ確保(GC.Alloc)の数が多い順:");
        for (int i = 0; i < byGc.Count && i < 25; i++) report.AppendLine($"  GC.Alloc {byGc[i].Value[2] / frames,8:F0}回/フレーム {byGc[i].Value[3] / frames / 1024.0,8:F1}KB/フレーム  自分の時間 {byGc[i].Value[0] / frames:F2}ms  {byGc[i].Key}");
    }

    private static double[] GetEntry(Dictionary<string, double[]> totals, string key)
    {
        if (!totals.TryGetValue(key, out double[] entry)) totals[key] = entry = new double[4];
        return entry;
    }

    private static void Cleanup()
    {
        foreach (MarkerRecorder marker in _markers) marker.recorder.Dispose();
        _markers.Clear();
        if (_gcBytes.Valid) _gcBytes.Dispose();
        if (_gcCount.Valid) _gcCount.Dispose();
        if (_state == State.Sampling) _state = State.Idle;
    }

    /// <summary>
    /// 今、動いている物の数(警察・悪人・ドローン・ライト・NavMeshAgent・Canvasなど)を数えるメソッド
    /// </summary>
    private static string CountObjects()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("【動いている物の数】");
        text.AppendLine($"  NavMeshAgent {CountActive<NavMeshAgent>()} / Animator {CountActive<Animator>()} / Rigidbody {CountActive<Rigidbody>()}");
        text.AppendLine($"  警察 {CountActive<CS_PoliceBrain>()} / 悪人(HP) {CountActive<CS_VillainHealth>()} / ドローン {CountActive<CS_SecurityDrone>()} / レーザー {CountActive<CS_SecurityLaser>()} / ライト {CountActive<CS_SecurityLight>()} / 悪人の出現点 {CountActive<CS_VillainSpawnPoint>()}");
        text.AppendLine($"  SAポイント {CS_SAPoint.activePoints.Count}");

        int pointLights = 0;
        int spotLights = 0;
        int shadowLights = 0;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!light.enabled) continue;

            if (light.type == LightType.Point) pointLights++;
            else if (light.type == LightType.Spot) spotLights++;
            if (light.shadows != LightShadows.None) shadowLights++;
        }
        text.AppendLine($"  Light: ポイント {pointLights} / スポット {spotLights} / 影あり {shadowLights}");

        int worldCanvas = 0;
        int overlayCanvas = 0;
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!canvas.enabled) continue;

            if (canvas.renderMode == RenderMode.WorldSpace) worldCanvas++;
            else overlayCanvas++;
        }
        text.AppendLine($"  Canvas: ワールド空間 {worldCanvas} / 画面 {overlayCanvas} / パーティクル {CountActive<ParticleSystem>()} / VFX {CountByTypeName("UnityEngine.VFX.VisualEffect, Unity.VisualEffectGraph.Runtime")} / カメラ {CountActive<Camera>()}");

        int renderers = 0;
        int skinned = 0;
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!renderer.enabled) continue;

            renderers++;
            if (renderer is SkinnedMeshRenderer) skinned++;
        }
        text.AppendLine($"  Renderer {renderers}(うちスキンメッシュ {skinned}) / Collider {CountActive<Collider>()}");
        return text.ToString();
    }

    private static int CountActive<T>() where T : Component
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
    }

    private static int CountByTypeName(string typeName)
    {
        System.Type type = System.Type.GetType(typeName);
        return type == null ? -1 : Object.FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
    }

    // =====================================================================
    // 警察の巡回ルートの、経路計算の時間
    // =====================================================================

    /// <summary>
    /// シーンにある警察のグループの、巡回ポイント同士(隣り合う点)の経路計算にかかる時間を測るメソッド(再生は要らない)
    /// 経路計算が重いと、警察が巡回ポイントへ向かい直すたびに、フレームが止まる
    /// </summary>
    [MenuItem("Tools/Police/警察の巡回ルートの経路計算の時間を調べる")]
    private static void CheckPoliceRoutePerf()
    {
        StringBuilder report = new StringBuilder();
        List<(string name, int index, double milliseconds, float walk, bool complete)> results = new List<(string, int, double, float, bool)>();
        NavMeshPath path = new NavMeshPath();
        Stopwatch watch = new Stopwatch();
        int squadCount = 0;
        foreach (CS_PoliceSquad squad in Object.FindObjectsByType<CS_PoliceSquad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            List<Transform> points = squad.GetRouteDrawPoints();
            if (points.Count < 2) continue;

            squadCount++;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 from = points[i].position;
                Vector3 to = points[(i + 1) % points.Count].position;
                if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, 3.0f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out NavMeshHit toHit, 3.0f, NavMesh.AllAreas)) continue;

                watch.Restart();
                bool found = NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path);
                watch.Stop();
                float walk = 0.0f;
                for (int c = 1; c < path.corners.Length; c++) walk += Vector3.Distance(path.corners[c - 1], path.corners[c]);
                results.Add((squad.name, i, watch.Elapsed.TotalMilliseconds, walk, found && path.status == NavMeshPathStatus.PathComplete));
            }
        }

        double total = 0.0;
        int incomplete = 0;
        foreach (var result in results)
        {
            total += result.milliseconds;
            if (!result.complete) incomplete++;
        }
        results.Sort((a, b) => b.milliseconds.CompareTo(a.milliseconds));
        report.AppendLine($"警察のグループ {squadCount}個 / 区間 {results.Count}個 / 合計 {total:F1}ms / 1区間の平均 {(results.Count > 0 ? total / results.Count : 0.0):F3}ms / 最大 {(results.Count > 0 ? results[0].milliseconds : 0.0):F2}ms / 途中までしか行けない区間 {incomplete}個");
        report.AppendLine("重い区間(上位20):");
        for (int i = 0; i < results.Count && i < 20; i++) report.AppendLine($"  {results[i].milliseconds:F2}ms {results[i].name} 点{results[i].index}→次 道のり {results[i].walk:F0}m {(results[i].complete ? "" : "(途中まで)")}");

        string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "PoliceRoutePerfReport.txt");
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        UnityEngine.Debug.Log("CSED_PerfCheck: 警察の巡回ルートの経路計算の時間を調べました(Temp/PoliceRoutePerfReport.txt)");
    }
}
