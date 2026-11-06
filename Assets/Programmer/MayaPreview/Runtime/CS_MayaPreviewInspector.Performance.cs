using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Performance
 Summary: Performance panel (toolbar [Performance], between Hierarchy and View).
          Shows only THIS process (the preview exe), not Maya or other apps:
            - CPU    : this process's CPU time / wall time (100% = all cores busy), main thread
            - GPU    : GPU usage / video memory of this process (Windows counters, like Task Manager)
            - Disk   : read / write per second of this process
            - Frame  : drawn / s, updates / s, frame time, current frame cap (IdleThrottle)
            - Memory : working set, private bytes, Unity allocated / reserved, GC heap
            - Render : batches / SetPass / triangles (when the player exposes them)
          GPU / disk / process memory: CS_MayaPreviewProcessMonitor (background thread).
          The panel scrolls (mouse wheel) when it is taller than its space.
          Sampled every 0.5 s and only while the panel is open, so it costs nothing when closed.
          Layout: below the Hierarchy in the left column; at the top of the column when the
          Hierarchy is closed.
 History: 2026/11/05 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private const float PerformanceInterval = 0.5f;
    private const float PerfLabelWidth = 96f;

    private bool _performanceOpen;
    private Rect _performanceRect;
    private Vector2 _performanceScroll;
    private GUIStyle _perfLabel, _perfValue;

    // Samples
    private CS_MayaPreviewProcessMonitor _monitor;
    private CS_MayaPreviewProcessMonitor.Snapshot _proc;
    private Process _process;
    private TimeSpan _lastCpuTime;
    private float _lastSampleTime = -1f;
    private int _framesSinceSample, _renderedSinceSample;
    private float _renderFps, _sceneFps;
    private int _lastSceneRenders;
    private float _cpuPercent, _cpuCorePercent, _fps, _frameMs, _worstFrameMs, _frameAccumulator;
    private double _mainThreadMs, _gpuMs;
    private long _unityAllocated, _unityReserved, _gcHeap, _gfxMemory;
    private long _batches = -1, _setPass = -1, _triangles = -1;
    private int _lastGcCount, _gcPerSecond;
    private readonly FrameTiming[] _timings = new FrameTiming[1];

    private ProfilerRecorder _mainThreadRecorder, _batchesRecorder, _setPassRecorder, _trianglesRecorder;

    private void StartPerformance()
    {
        try { _process = Process.GetCurrentProcess(); _lastCpuTime = _process.TotalProcessorTime; }
        catch { _process = null; }
        _monitor = new CS_MayaPreviewProcessMonitor();
        _monitor.Start();
        _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _lastSampleTime = Time.unscaledTime;
        _framesSinceSample = 0;
        _renderedSinceSample = 0;
        _frameAccumulator = 0f;
        _worstFrameMs = 0f;
        _lastGcCount = GC.CollectionCount(0);
        _lastSceneRenders = CS_MayaPreviewFrameCache.SceneRenderCount;
    }

    private void StopPerformance()
    {
        _mainThreadRecorder.Dispose();
        _batchesRecorder.Dispose();
        _setPassRecorder.Dispose();
        _trianglesRecorder.Dispose();
        _monitor?.Dispose();
        _monitor = null;
        _process?.Dispose();
        _process = null;
        _lastSampleTime = -1f;
    }

    private void SetPerformanceOpen(bool open)
    {
        if (open == _performanceOpen) return;
        _performanceOpen = open;
        if (open) StartPerformance(); else StopPerformance();
    }

    // Called from Update.
    private void TickPerformance()
    {
        if (!_performanceOpen || _lastSampleTime < 0f) return;

        float dt = Time.unscaledDeltaTime;
        _framesSinceSample++;
        _frameAccumulator += dt;
        _worstFrameMs = Mathf.Max(_worstFrameMs, dt * 1000f);
        if (UnityEngine.Rendering.OnDemandRendering.willCurrentFrameRender) _renderedSinceSample++;
        FrameTimingManager.CaptureFrameTimings();

        float now = Time.unscaledTime;
        float elapsed = now - _lastSampleTime;
        if (elapsed < PerformanceInterval) return;

        // CPU of this process only.
        if (_process != null)
        {
            try
            {
                _process.Refresh();
                TimeSpan cpu = _process.TotalProcessorTime;
                double used = (cpu - _lastCpuTime).TotalSeconds;
                _lastCpuTime = cpu;
                _cpuCorePercent = (float)(used / elapsed * 100.0);
                _cpuPercent = _cpuCorePercent / Mathf.Max(1, Environment.ProcessorCount);
            }
            catch { _process = null; }
        }

        if (_monitor != null) _proc = _monitor.Latest;

        _fps = _framesSinceSample / Mathf.Max(0.0001f, _frameAccumulator);
        _frameMs = _frameAccumulator / Mathf.Max(1, _framesSinceSample) * 1000f;

        _mainThreadMs = _mainThreadRecorder.Valid ? Average(_mainThreadRecorder) * 1e-6 : -1;
        _batches = _batchesRecorder.Valid ? _batchesRecorder.LastValue : -1;
        _setPass = _setPassRecorder.Valid ? _setPassRecorder.LastValue : -1;
        _triangles = _trianglesRecorder.Valid ? _trianglesRecorder.LastValue : -1;

        _gpuMs = FrameTimingManager.GetLatestTimings(1, _timings) > 0 ? _timings[0].gpuFrameTime : 0;

        _unityAllocated = Profiler.GetTotalAllocatedMemoryLong();
        _unityReserved = Profiler.GetTotalReservedMemoryLong();
        _gcHeap = Profiler.GetMonoHeapSizeLong();
        _gfxMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();

        int gc = GC.CollectionCount(0);
        _gcPerSecond = Mathf.RoundToInt((gc - _lastGcCount) / elapsed);
        _lastGcCount = gc;

        _lastSampleTime = now;
        _renderFps = _renderedSinceSample / elapsed;
        int sceneRenders = CS_MayaPreviewFrameCache.SceneRenderCount;
        _sceneFps = (sceneRenders - _lastSceneRenders) / elapsed;
        _lastSceneRenders = sceneRenders;
        _renderedSinceSample = 0;
        _framesSinceSample = 0;
        _frameAccumulator = 0f;
        _worstFrameMs = Mathf.Max(0f, _worstFrameMs * 0.5f);   // decays so a single spike fades out
    }

    private static double Average(ProfilerRecorder recorder)
    {
        int count = recorder.Count;
        if (count == 0) return recorder.LastValue;
        double sum = 0;
        for (int i = 0; i < count; i++) sum += recorder.GetSample(i).Value;
        return sum / count;
    }


    // =========================================================
    // Drawing
    // =========================================================

    // Section titles and value rows (height of the scrolled content).
    private const int PerfSections = 6;
    private const int PerfRows = 23;

    private void DrawPerformance()
    {
        if (_perfLabel == null)
        {
            _perfLabel = new GUIStyle(_dim) { clipping = TextClipping.Clip, fontSize = 11 };
            _perfValue = new GUIStyle(_row) { clipping = TextClipping.Clip, fontSize = 11 };
        }

        Rect content = DrawWindow(_performanceRect, "Performance", "this exe only");
        float height = PerfSections * (RowHeight + 6) + PerfRows * RowHeight + 8;
        bool scroll = height > content.height;
        Rect view = new Rect(0, 0, content.width - (scroll ? 12 : 0), Mathf.Max(height, content.height));
        _performanceScroll = GUI.BeginScrollView(content, _performanceScroll, view, false, false, GUIStyle.none, _skin.verticalScrollbar);

        float x = 6, y = 4, w = view.width - 12;

        y = PerfSection(x, y, w, "CPU");
        y = PerfBar(x, y, w, "Usage", _cpuPercent, _cpuPercent.ToString("0.0") + " %");
        y = PerfLine(x, y, w, "Cores busy", (_cpuCorePercent / 100f).ToString("0.00") + " / " + Environment.ProcessorCount);
        y = PerfLine(x, y, w, "Main thread", _mainThreadMs >= 0 ? _mainThreadMs.ToString("0.0") + " ms" : "n/a");

        y = PerfSection(x, y, w, "GPU");
        string waiting = _monitor != null ? "measuring..." : "n/a";
        if (_proc.gpuAvailable)
        {
            string engine = string.IsNullOrEmpty(_proc.gpuEngine) || _proc.gpuEngine == "3D" ? "" : "  " + _proc.gpuEngine;
            y = PerfBar(x, y, w, "Usage", _proc.gpuPercent, Mathf.Min(100f, _proc.gpuPercent).ToString("0.0") + " %" + engine);
            y = PerfLine(x, y, w, "3D", Mathf.Min(100f, _proc.gpu3DPercent).ToString("0.0") + " %");
            y = PerfLine(x, y, w, "VRAM", PerfMb(_proc.gpuDedicated));
            y = PerfLine(x, y, w, "Shared", PerfMb(_proc.gpuShared));
        }
        else
        {
            y = PerfLine(x, y, w, "Usage", waiting);
            y = PerfLine(x, y, w, "3D", "-");
            y = PerfLine(x, y, w, "VRAM", "-");
            y = PerfLine(x, y, w, "Shared", "-");
        }
        y = PerfLine(x, y, w, "Frame time", _gpuMs > 0 ? _gpuMs.ToString("0.0") + " ms" : "n/a");

        y = PerfSection(x, y, w, "Disk");
        y = PerfLine(x, y, w, "Read", _proc.diskAvailable ? PerfRate(_proc.diskReadPerSec) : waiting);
        y = PerfLine(x, y, w, "Write", _proc.diskAvailable ? PerfRate(_proc.diskWritePerSec) : waiting);

        y = PerfSection(x, y, w, "Frame");
        y = PerfLine(x, y, w, "Scene / s", _sceneFps.ToString("0.0") + "  (3D redraws)");
        y = PerfLine(x, y, w, "Drawn / s", _renderFps.ToString("0.0"));
        y = PerfLine(x, y, w, "Updates / s", _fps.ToString("0.0"));
        y = PerfLine(x, y, w, "Frame", _frameMs.ToString("0.0") + " ms  (worst " + _worstFrameMs.ToString("0") + ")");
        y = PerfLine(x, y, w, "Cap", CS_MayaPreviewIdleThrottle.CurrentModeLabel);

        y = PerfSection(x, y, w, "Memory");
        y = PerfLine(x, y, w, "Working set", _proc.memoryAvailable ? PerfMb(_proc.workingSet) + "  (peak " + PerfMb(_proc.peakWorkingSet) + ")" : waiting);
        y = PerfLine(x, y, w, "Private", _proc.memoryAvailable ? PerfMb(_proc.privateBytes) : waiting);
        y = PerfLine(x, y, w, "Unity alloc", PerfMb(_unityAllocated) + " / " + PerfMb(_unityReserved));
        y = PerfLine(x, y, w, "GC heap", PerfMb(_gcHeap) + "  (" + _gcPerSecond + " GC/s)");

        y = PerfSection(x, y, w, "Render");
        y = PerfLine(x, y, w, "Batches", PerfCount(_batches));
        y = PerfLine(x, y, w, "SetPass", PerfCount(_setPass));
        PerfLine(x, y, w, "Triangles", PerfCount(_triangles));

        GUI.EndScrollView();
    }

    private float PerfSection(float x, float y, float w, string text)
    {
        y += 4;
        GUI.Label(new Rect(x, y, w, RowHeight), text, _bold);
        Fill(new Rect(x, y + RowHeight, w, 1), BorderColor);
        return y + RowHeight + 2;
    }

    private float PerfLine(float x, float y, float w, string label, string value)
    {
        GUI.Label(new Rect(x + 4, y, PerfLabelWidth - 6, RowHeight), label, _perfLabel);
        GUI.Label(new Rect(x + PerfLabelWidth, y, w - PerfLabelWidth, RowHeight), value, _perfValue);
        return y + RowHeight;
    }

    private float PerfBar(float x, float y, float w, string label, float percent, string text)
    {
        GUI.Label(new Rect(x + 4, y, PerfLabelWidth - 6, RowHeight), label, _perfLabel);
        Rect bar = new Rect(x + PerfLabelWidth, y + 2, w - PerfLabelWidth, RowHeight - 4);
        Fill(bar, FieldColor);
        float t = Mathf.Clamp01(percent / 100f);
        Color color = t < 0.25f ? new Color(0.30f, 0.60f, 0.35f) : t < 0.5f ? new Color(0.80f, 0.65f, 0.20f) : new Color(0.80f, 0.30f, 0.25f);
        Fill(new Rect(bar.x, bar.y, bar.width * t, bar.height), color);
        GUI.Label(new Rect(bar.x + 4, y, bar.width - 4, RowHeight), text, _perfValue);
        return y + RowHeight;
    }

    private static string PerfMb(long bytes) => bytes > 0 ? (bytes / (1024.0 * 1024.0)).ToString("0") + " MB" : "n/a";

    private static string PerfRate(double bytesPerSecond) =>
        bytesPerSecond >= 1024 * 1024
            ? (bytesPerSecond / (1024.0 * 1024.0)).ToString("0.0") + " MB/s"
            : (bytesPerSecond / 1024.0).ToString("0") + " KB/s";

    private static string PerfCount(long value) => value >= 0 ? value.ToString("N0") : "n/a";
}
