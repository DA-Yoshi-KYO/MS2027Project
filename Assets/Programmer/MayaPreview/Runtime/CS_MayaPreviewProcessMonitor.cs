using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

/*
+=====================================
 File   : CS_MayaPreviewProcessMonitor
 Summary: GPU / disk usage of THIS process only (Windows), for the Performance panel.
            GPU  : Windows "GPU Engine" counters of this pid (same numbers as Task Manager:
                   per engine type the sum, the busiest type is the GPU usage),
                   "GPU Process Memory" dedicated / shared of this pid.
            Disk : GetProcessIoCounters read / write bytes per second.
            Memory: K32GetProcessMemoryInfo (System.Diagnostics.Process returns 0 under Unity's Mono).
          Runs on its own background thread once a second, only while started
          (the Performance panel is open), so the main thread never waits for it.
 History: 2026/11/05 New
=====================================+
*/
public sealed class CS_MayaPreviewProcessMonitor : IDisposable
{
    public struct Snapshot
    {
        public bool gpuAvailable;
        public float gpuPercent;        // busiest engine type (Task Manager "GPU")
        public float gpu3DPercent;      // 3D engine
        public string gpuEngine;        // name of the busiest engine type
        public long gpuDedicated;       // bytes
        public long gpuShared;          // bytes
        public bool diskAvailable;
        public double diskReadPerSec;   // bytes / s
        public double diskWritePerSec;  // bytes / s
        public ulong diskReadTotal;
        public ulong diskWriteTotal;
        public bool memoryAvailable;
        public long workingSet;         // bytes (Task Manager "Memory" is close to private bytes)
        public long peakWorkingSet;
        public long privateBytes;
    }

    private const int IntervalMs = 1000;

    private readonly object _lock = new object();
    private Snapshot _snapshot;
    private Thread _thread;
    private volatile bool _running;
    private readonly AutoResetEvent _wake = new AutoResetEvent(false);

    public Snapshot Latest { get { lock (_lock) return _snapshot; } }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "MayaPreview ProcessMonitor", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public void Dispose()
    {
        _running = false;
        _wake.Set();
        _thread?.Join(2000);
        _thread = null;
    }

    private void Run()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        int pid;
        using (var process = System.Diagnostics.Process.GetCurrentProcess()) pid = process.Id;
        string prefix = "pid_" + pid + "_";

        IntPtr query = IntPtr.Zero, engine = IntPtr.Zero, dedicated = IntPtr.Zero, shared = IntPtr.Zero;
        bool pdh = false;
        try
        {
            pdh = PdhOpenQueryW(null, IntPtr.Zero, out query) == 0
                && PdhAddEnglishCounterW(query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out engine) == 0;
            if (pdh)
            {
                PdhAddEnglishCounterW(query, @"\GPU Process Memory(*)\Dedicated Usage", IntPtr.Zero, out dedicated);
                PdhAddEnglishCounterW(query, @"\GPU Process Memory(*)\Shared Usage", IntPtr.Zero, out shared);
                PdhCollectQueryData(query);   // rate counters need a first sample
            }
        }
        catch { pdh = false; }   // pdh.dll missing / blocked

        IntPtr self = GetCurrentProcess();
        bool io = GetProcessIoCounters(self, out IO_COUNTERS lastIo);
        long lastTicks = DateTime.UtcNow.Ticks;
        var byType = new Dictionary<string, double>();

        while (_running)
        {
            _wake.WaitOne(IntervalMs);
            if (!_running) break;

            var s = new Snapshot();
            long ticks = DateTime.UtcNow.Ticks;
            double seconds = Math.Max(0.001, (ticks - lastTicks) / (double)TimeSpan.TicksPerSecond);
            lastTicks = ticks;

            if (pdh && PdhCollectQueryData(query) == 0)
            {
                byType.Clear();
                foreach (var item in ReadArray(engine, prefix))
                {
                    // instance: pid_1234_luid_0x..._phys_0_eng_0_engtype_3D
                    int at = item.Key.LastIndexOf("engtype_", StringComparison.Ordinal);
                    string type = at >= 0 ? item.Key.Substring(at + 8) : "?";
                    byType.TryGetValue(type, out double sum);
                    byType[type] = sum + item.Value;
                }
                s.gpuAvailable = true;
                foreach (var pair in byType)
                {
                    if (pair.Value > s.gpuPercent) { s.gpuPercent = (float)pair.Value; s.gpuEngine = pair.Key; }
                    if (pair.Key == "3D") s.gpu3DPercent = (float)pair.Value;
                }
                if (dedicated != IntPtr.Zero) foreach (var item in ReadArray(dedicated, prefix)) s.gpuDedicated += (long)item.Value;
                if (shared != IntPtr.Zero) foreach (var item in ReadArray(shared, prefix)) s.gpuShared += (long)item.Value;
            }

            if (io && GetProcessIoCounters(self, out IO_COUNTERS now))
            {
                s.diskAvailable = true;
                s.diskReadPerSec = (now.ReadTransferCount - lastIo.ReadTransferCount) / seconds;
                s.diskWritePerSec = (now.WriteTransferCount - lastIo.WriteTransferCount) / seconds;
                s.diskReadTotal = now.ReadTransferCount;
                s.diskWriteTotal = now.WriteTransferCount;
                lastIo = now;
            }

            var memory = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX)) };
            if (K32GetProcessMemoryInfo(self, ref memory, memory.cb))
            {
                s.memoryAvailable = true;
                s.workingSet = (long)memory.WorkingSetSize.ToUInt64();
                s.peakWorkingSet = (long)memory.PeakWorkingSetSize.ToUInt64();
                s.privateBytes = (long)memory.PrivateUsage.ToUInt64();
            }

            lock (_lock) _snapshot = s;
        }

        if (query != IntPtr.Zero) PdhCloseQuery(query);
#endif
    }


    // =========================================================
    // Windows API
    // =========================================================

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_FMT_NOCAP100 = 0x00008000;
    private const uint PDH_MORE_DATA = 0x800007D2;

    // PDH_FMT_COUNTERVALUE_ITEM_W: name pointer, then { CStatus, (pad), double } aligned to 8
    private const int ItemSize = 24;
    private const int ValueOffset = 16;
    private const int StatusOffset = 8;

    private static List<KeyValuePair<string, double>> ReadArray(IntPtr counter, string prefix)
    {
        var result = new List<KeyValuePair<string, double>>();
        uint size = 0;
        uint status = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out uint count, IntPtr.Zero);
        if (status != PDH_MORE_DATA || size == 0) return result;
        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out count, buffer) != 0) return result;
            for (int i = 0; i < count; i++)
            {
                IntPtr item = IntPtr.Add(buffer, i * ItemSize);
                string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item));
                if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (Marshal.ReadInt32(IntPtr.Add(item, StatusOffset)) != 0) continue;   // PDH_CSTATUS_VALID_DATA = 0
                double value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(IntPtr.Add(item, ValueOffset)));
                result.Add(new KeyValuePair<string, double>(name, value));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [DllImport("kernel32.dll")]
    private static extern bool K32GetProcessMemoryInfo(IntPtr process, ref PROCESS_MEMORY_COUNTERS_EX counters, uint size);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool GetProcessIoCounters(IntPtr process, out IO_COUNTERS counters);
#endif
}
