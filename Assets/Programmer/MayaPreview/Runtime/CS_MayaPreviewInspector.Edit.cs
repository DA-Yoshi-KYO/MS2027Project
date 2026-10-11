using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
+=====================================
 File   : CS_MayaPreviewInspector.Edit
 Summary: Scene edits from the keyboard.
          - Ctrl+D : duplicate the selection next to the original ("Name (1)", "Name (2)" ...).
                     Copies of Maya meshes get their own mesh, so later Maya updates only
                     change the original. A selected Maya group is copied as a new group.
          - Delete : delete the selection. Objects are only hidden, so Ctrl+Z brings them back.
          The preview camera and the preview system objects are never duplicated or deleted.
 History: 2026/10/11 New
=====================================+
*/
public partial class CS_MayaPreviewInspector
{
    private static int s_presenceKey;

    // =========================================================
    // Ctrl+D
    // =========================================================

    private void DuplicateSelection()
    {
        if (_selection == null || _selection.Count == 0) return;
        if (_tool != null && _tool.IsDragging) return;
        var originals = new List<GameObject>(_selection.Selected);
        var copies = new List<GameObject>();

        // Copies are made under an inactive holder: nothing in them runs before they are placed.
        var holder = new GameObject("Maya Preview Duplicate");
        holder.SetActive(false);
        holder.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            if (_selection.GroupPath != null)
            {
                GameObject group = DuplicateMayaGroup(_selection.GroupPath);
                if (group != null) copies.Add(group);
            }
            else
            {
                foreach (Transform t in TransformTargets())
                {
                    if (!CanEdit(t.gameObject)) continue;
                    GameObject copy = DuplicateObject(t.gameObject, holder.transform);
                    if (copy != null) copies.Add(copy);
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            DestroyImmediate(holder);
        }
        copies.RemoveAll(c => c == null);
        if (copies.Count == 0) { Flash("Nothing to duplicate"); return; }

        RecordPresence(copies, "Duplicate", originals);
        foreach (GameObject copy in copies)
        {
            CS_MayaPreviewSelection.SetDeleted(copy, false);
            copy.SetActive(true);
        }
        CS_MayaPreviewUndo.Close();
        _selection.Select(copies);
        _nextScan = 0f;
        Flash(copies.Count == 1 ? "Duplicated " + DisplayName(copies[0]) : "Duplicated " + copies.Count + " objects");
    }

    private GameObject DuplicateObject(GameObject original, Transform holder)
    {
        Transform source = original.transform;
        Transform parent = source.parent;
        string name = UniqueName(parent, DisplayName(original), original.scene);

        // Local values are copied as they are, then the copy goes under the same parent.
        GameObject copy = Instantiate(original, holder, false);
        copy.SetActive(false);
        PrepareCopy(copy);
        copy.name = name;
        copy.transform.SetParent(parent, false);
        if (parent == null && copy.scene != original.scene) SceneManager.MoveGameObjectToScene(copy, original.scene);
        copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
        return copy;
    }

    // A Maya group becomes a new group object under Maya_Model_Root with the Maya hierarchy
    // as empty objects (the same layout as the export).
    private GameObject DuplicateMayaGroup(string groupPath)
    {
        Transform root = CS_MayaPreviewExport.ModelRoot();
        List<GameObject> members = CS_MayaPreviewExport.LiveObjectsIn(groupPath);
        members.RemoveAll(o => o == null || !o.activeSelf);
        if (root == null || members.Count == 0) return null;

        var group = new GameObject(UniqueName(root, CS_MayaPreviewExport.LastSegment(groupPath), root.gameObject.scene));
        group.SetActive(false);
        group.layer = members[0].layer;
        group.transform.SetParent(root, false);

        var nodes = new Dictionary<string, Transform> { [groupPath] = group.transform };
        Transform GroupNode(string path)
        {
            if (nodes.TryGetValue(path, out Transform node)) return node;
            int bar = path.LastIndexOf('|');
            Transform parent = bar > 0 && path.Length > groupPath.Length ? GroupNode(path.Substring(0, bar)) : group.transform;
            var empty = new GameObject(CS_MayaPreviewExport.LastSegment(path));
            empty.layer = group.layer;
            empty.transform.SetParent(parent, false);
            nodes[path] = empty.transform;
            return empty.transform;
        }

        // Parents first, so a mesh that is also a Maya parent receives its children.
        members.Sort((a, b) =>
        {
            string pa = CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(a));
            string pb = CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(b));
            int depth = Depth(pa).CompareTo(Depth(pb));
            return depth != 0 ? depth : string.CompareOrdinal(pa, pb);
        });
        foreach (GameObject member in members)
        {
            string path = CS_MayaPreviewExport.TransformPath(CS_MayaPreviewExport.ShapePath(member));
            int bar = path.LastIndexOf('|');
            Transform parent = path == groupPath || bar <= 0 ? group.transform : GroupNode(path.Substring(0, bar));
            GameObject copy = Instantiate(member, parent, false);   // the group is inactive: nothing runs yet
            PrepareCopy(copy);
            copy.name = CS_MayaPreviewExport.LastSegment(path);
            if (!nodes.ContainsKey(path)) nodes[path] = copy.transform;
        }
        return group;
    }

    private static int Depth(string path)
    {
        int count = 0;
        foreach (char c in path) if (c == '|') count++;
        return count;
    }

    // Removes the selection outlines that came with the copy and detaches Maya meshes
    // from the live link (own name, own mesh).
    private void PrepareCopy(GameObject copy)
    {
        foreach (CS_MayaPreviewInternal helper in copy.GetComponentsInChildren<CS_MayaPreviewInternal>(true))
            if (helper != null && helper.gameObject != copy) DestroyImmediate(helper.gameObject);

        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(LivePrefix)) continue;
            var filter = t.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name;
                _owned.Add(mesh);
                filter.sharedMesh = mesh;
            }
            if (t != copy.transform) t.name = DisplayName(t.gameObject).Replace("(Clone)", "").Trim();
        }
    }

    private const string LivePrefix = "MayaLive_";

    // Unity style: "Cube" -> "Cube (1)", "Cube (1)" -> "Cube (2)", unique among the siblings.
    private static readonly Regex s_numbered = new Regex(@"^(.*) \((\d+)\)$");
    private static string UniqueName(Transform parent, string name, Scene scene)
    {
        name = name.Replace("(Clone)", "").Trim();
        Match match = s_numbered.Match(name);
        string stem = match.Success ? match.Groups[1].Value : name;
        int start = match.Success ? int.Parse(match.Groups[2].Value) + 1 : 1;
        var used = new HashSet<string>();
        if (parent != null) foreach (Transform child in parent) used.Add(child.name);
        else if (scene.IsValid()) foreach (GameObject rootObject in scene.GetRootGameObjects()) used.Add(rootObject.name);
        for (int n = start; ; n++)
        {
            string candidate = stem + " (" + n + ")";
            if (!used.Contains(candidate)) return candidate;
        }
    }


    // =========================================================
    // Delete
    // =========================================================

    private void DeleteSelection()
    {
        if (_selection == null || _selection.Count == 0) return;
        if (_tool != null && _tool.IsDragging) return;
        var targets = new List<GameObject>();
        foreach (Transform t in TransformTargets())
            if (CanEdit(t.gameObject) && t.gameObject.activeSelf) targets.Add(t.gameObject);
        if (targets.Count == 0) { Flash("Nothing to delete"); return; }

        string label = targets.Count == 1 ? DisplayName(targets[0]) : targets.Count + " objects";
        RecordPresence(targets, "Delete", null);
        foreach (GameObject o in targets)
        {
            CS_MayaPreviewSelection.SetDeleted(o, true);
            o.SetActive(false);
        }
        CS_MayaPreviewUndo.Close();
        _selection.Select(null);
        _nextScan = 0f;
        Flash("Deleted " + label + "   (Ctrl+Z: restore)");
    }


    // =========================================================
    // Shared
    // =========================================================

    // Not the preview camera, the preview system or the preview's own helper objects.
    private bool CanEdit(GameObject o)
    {
        if (o == null || CS_MayaPreviewInternal.Is(o.transform)) return false;
        if (o == gameObject || transform.IsChildOf(o.transform)) return false;
        if (o.GetComponentInChildren<CS_MayaPreviewRuntimeServer>(true) != null) return false;
        return true;
    }

    // One undo step for showing / hiding several objects. Undo of a duplicate selects the
    // originals again; undo of a delete selects the restored objects.
    private void RecordPresence(List<GameObject> targets, string label, List<GameObject> selectWhenHidden)
    {
        var list = new List<GameObject>(targets);
        var fallback = selectWhenHidden != null ? new List<GameObject>(selectWhenHidden) : null;
        CS_MayaPreviewUndo.Record("presence:" + (++s_presenceKey), label,
            () =>
            {
                var text = new StringBuilder(list.Count);
                foreach (GameObject o in list) text.Append(o != null && o.activeSelf ? '1' : '0');
                return text.ToString();
            },
            state =>
            {
                var shown = new List<GameObject>();
                for (int i = 0; i < list.Count && i < state.Length; i++)
                {
                    GameObject o = list[i];
                    if (o == null) continue;
                    bool on = state[i] == '1';
                    CS_MayaPreviewSelection.SetDeleted(o, !on);
                    o.SetActive(on);
                    if (on) shown.Add(o);
                }
                _nextScan = 0f;
                if (_selection == null) return;
                if (shown.Count > 0) { _selection.Select(shown); return; }
                var keep = new List<GameObject>();
                if (fallback != null) foreach (GameObject o in fallback) if (o != null && o.activeInHierarchy) keep.Add(o);
                if (keep.Count == 0) foreach (GameObject o in _selection.Selected) if (o != null && o.activeInHierarchy) keep.Add(o);
                _selection.Select(keep);
            }, false);
    }
}
