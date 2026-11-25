using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// 一時スクリプト(カクつきの原因をプロファイラの記録から調べるため)。使用後に削除する
public static class TempProf
{
    private const string _outPath = "Temp/TempProfReport.txt";

    [MenuItem("Tools/TempProfStart")]
    private static void StartRecording()
    {
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        Debug.Log("TempProf: recording");
    }

    [MenuItem("Tools/TempProfReport")]
    private static void Report()
    {
        ProfilerDriver.enabled = false;
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        List<(int frame, float ms)> frames = new List<(int, float)>();
        List<float> all = new List<float>();
        for (int i = first; i <= last; i++)
        {
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(i, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid) continue;
                frames.Add((i, view.frameTimeMs));
                all.Add(view.frameTimeMs);
            }
        }
        all.Sort();
        frames.Sort((a, b) => b.ms.CompareTo(a.ms));

        StringBuilder sb = new StringBuilder();
        if (all.Count > 0) sb.AppendLine($"frames={all.Count} median={all[all.Count / 2]:0.0}ms p95={all[(int)(all.Count * 0.95f)]:0.0}ms max={all[all.Count - 1]:0.0}ms");

        // 一番重いフレームの、自分の時間(Self)が長い処理と、GCのメモリ確保
        for (int n = 0; n < Mathf.Min(8, frames.Count); n++)
        {
            int frame = frames[n].frame;
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
            {
                sb.AppendLine($"--- frame {frame} {frames[n].ms:0.0}ms");
                List<(string path, float self, float gc)> items = new List<(string, float, float)>();
                Collect(view, view.GetRootItemID(), "", items);
                items.Sort((a, b) => b.self.CompareTo(a.self));
                for (int k = 0; k < Mathf.Min(12, items.Count); k++) sb.AppendLine($"  {items[k].self:0.00}ms gc={items[k].gc:0}B {items[k].path}");
                items.Sort((a, b) => b.gc.CompareTo(a.gc));
                for (int k = 0; k < Mathf.Min(4, items.Count); k++) if (items[k].gc > 0) sb.AppendLine($"  [GC] {items[k].gc:0}B {items[k].path}");
            }
        }

        // 全フレームを通した、GCの合計が多い処理
        Dictionary<string, float> gcTotal = new Dictionary<string, float>();
        Dictionary<string, float> selfTotal = new Dictionary<string, float>();
        foreach ((int frame, float ms) in frames)
        {
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
            {
                List<(string path, float self, float gc)> items = new List<(string, float, float)>();
                Collect(view, view.GetRootItemID(), "", items);
                foreach ((string path, float self, float gc) in items)
                {
                    string key = path.Substring(path.LastIndexOf('/') + 1);
                    gcTotal[key] = (gcTotal.TryGetValue(key, out float g) ? g : 0f) + gc;
                    selfTotal[key] = (selfTotal.TryGetValue(key, out float s) ? s : 0f) + self;
                }
            }
        }
        List<KeyValuePair<string, float>> gcList = new List<KeyValuePair<string, float>>(gcTotal);
        gcList.Sort((a, b) => b.Value.CompareTo(a.Value));
        sb.AppendLine("=== GC total");
        for (int k = 0; k < Mathf.Min(20, gcList.Count); k++) sb.AppendLine($"  {gcList[k].Value / 1024f:0.0}KB {gcList[k].Key}");
        List<KeyValuePair<string, float>> selfList = new List<KeyValuePair<string, float>>(selfTotal);
        selfList.Sort((a, b) => b.Value.CompareTo(a.Value));
        sb.AppendLine("=== Self total");
        for (int k = 0; k < Mathf.Min(25, selfList.Count); k++) sb.AppendLine($"  {selfList[k].Value:0}ms {selfList[k].Key}");

        File.WriteAllText(_outPath, sb.ToString());
        Debug.Log("TempProf: report written");
    }

    private static void Collect(HierarchyFrameDataView view, int id, string path, List<(string, float, float)> items)
    {
        List<int> children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int child in children)
        {
            string name = view.GetItemName(child);
            string childPath = path + "/" + name;
            items.Add((childPath, view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime), view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory)));
            Collect(view, child, childPath, items);
        }
    }
}
