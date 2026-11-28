using System;
using System.Collections.Generic;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewSkin
 Summary: Skinned meshes from Maya (joints + skin weights), deformed on the CPU.
          - Maya sends a skinned mesh in its BIND pose (MESH_DATA_V2 with "skin"): the joints
            (full DAG paths, parents), each joint's bindPreMatrix (world -> joint at bind time),
            the joints' current world matrices and up to 4 weights per vertex.
          - JOINT_POSE (sent when the Maya time changes: playback, scrubbing, posing) carries only
            the joints' world matrices, so animation is cheap to send and plays in the preview.
          - The live object keeps its MeshFilter / MeshRenderer (selection, materials, outlines and
            export keep working); this component rewrites the mesh vertices / normals.
          - Export: BindMesh() gives the bind-pose mesh and the joints for the FBX skeleton + skin.
          All matrices are kept in Maya's convention (row vectors, centimetres, right-handed) as
          Unity Matrix4x4 in column form (transposed); the result is converted to Unity (Z flipped).
 History: 2026/11/26 New
=====================================+
*/
public class CS_MayaPreviewSkin : MonoBehaviour
{
    // =========================================================
    // Messages (JsonUtility)
    // =========================================================

    [Serializable]
    public class SkinData
    {
        public string[] joints;      // full DAG paths of the influences
        public int[] parents;        // index of the nearest ancestor joint in joints, -1 = none
        public float[] bindPre;      // 16 per joint, Maya row-major: world -> joint at bind time
        public float[] pose;         // 16 per joint, Maya row-major: joint world matrix now
        public int[] bones;          // 4 per vertex: index into joints
        public float[] weights;      // 4 per vertex
    }

    [Serializable]
    public class PoseData
    {
        public string type;
        public string[] joints;
        public float[] matrices;     // 16 per joint, Maya row-major world matrices
    }

    private static readonly List<CS_MayaPreviewSkin> s_all = new List<CS_MayaPreviewSkin>();

    // =========================================================
    // State
    // =========================================================

    public string[] Joints { get; private set; }
    public int[] Parents { get; private set; }
    public Matrix4x4[] BindPre { get; private set; }    // Maya space, column form
    public Matrix4x4[] Pose { get; private set; }       // Maya space, column form

    private Dictionary<string, int> _jointIndex;
    private Vector3[] _bindPositions;     // Maya space
    private Vector3[] _bindNormals;       // Maya space (null = none)
    private int[] _bones;
    private float[] _weights;
    private Vector3[] _vertices, _normals;
    private Matrix4x4[] _skin;
    private Mesh _mesh;
    private bool _dirty;
    private bool _tangents;

    public int VertexCount => _bindPositions != null ? _bindPositions.Length : 0;

    // =========================================================
    // From the runtime server
    // =========================================================

    // MESH_DATA_V2: positions / normals are the raw Maya values (bind pose when skinned).
    public static void Apply(GameObject target, Mesh mesh, float[] positions, float[] normals, SkinData skin)
    {
        bool skinned = skin != null && skin.joints != null && skin.joints.Length > 0 && positions != null
                       && skin.bones != null && skin.weights != null
                       && skin.bones.Length == positions.Length / 3 * 4 && skin.weights.Length == skin.bones.Length
                       && skin.bindPre != null && skin.bindPre.Length == skin.joints.Length * 16;
        var component = target.GetComponent<CS_MayaPreviewSkin>();
        if (!skinned)
        {
            if (component != null) Destroy(component);
            return;
        }
        if (component == null) component = target.AddComponent<CS_MayaPreviewSkin>();
        component.Set(mesh, positions, normals, skin);
    }

    // JOINT_POSE: every skinned mesh that uses one of these joints follows.
    public static void ApplyPose(PoseData pose)
    {
        if (pose == null || pose.joints == null || pose.matrices == null || pose.matrices.Length < pose.joints.Length * 16) return;
        foreach (CS_MayaPreviewSkin skin in s_all)
        {
            if (skin == null || skin._jointIndex == null) continue;
            bool changed = false;
            for (int i = 0; i < pose.joints.Length; i++)
            {
                if (!skin._jointIndex.TryGetValue(pose.joints[i], out int j)) continue;
                skin.Pose[j] = FromMaya(pose.matrices, i * 16);
                changed = true;
            }
            if (changed) skin._dirty = true;
        }
    }

    private void Set(Mesh mesh, float[] positions, float[] normals, SkinData skin)
    {
        _mesh = mesh;
        int count = positions.Length / 3;
        _bindPositions = new Vector3[count];
        for (int i = 0; i < count; i++) _bindPositions[i] = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
        _bindNormals = null;
        if (normals != null && normals.Length == positions.Length)
        {
            _bindNormals = new Vector3[count];
            for (int i = 0; i < count; i++) _bindNormals[i] = new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]);
        }
        _bones = skin.bones;
        _weights = skin.weights;

        int joints = skin.joints.Length;
        Joints = skin.joints;
        Parents = skin.parents != null && skin.parents.Length == joints ? skin.parents : Fill(joints, -1);
        BindPre = new Matrix4x4[joints];
        Pose = new Matrix4x4[joints];
        _jointIndex = new Dictionary<string, int>(joints);
        bool hasPose = skin.pose != null && skin.pose.Length == joints * 16;
        for (int j = 0; j < joints; j++)
        {
            _jointIndex[skin.joints[j]] = j;
            BindPre[j] = FromMaya(skin.bindPre, j * 16);
            Pose[j] = hasPose ? FromMaya(skin.pose, j * 16) : BindPre[j].inverse;
        }
        _skin = new Matrix4x4[joints];
        _vertices = new Vector3[count];
        _normals = _bindNormals != null ? new Vector3[count] : null;
        _tangents = mesh != null && mesh.uv != null && mesh.uv.Length == count;
        Deform();
    }

    private static int[] Fill(int count, int value)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = value;
        return result;
    }

    // Maya row-major (row vectors) -> Unity Matrix4x4 holding the transpose (column vectors).
    private static Matrix4x4 FromMaya(float[] m, int o)
    {
        var u = new Matrix4x4();
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                u[c, r] = m[o + r * 4 + c];
        return u;
    }

    // =========================================================
    // Deformation
    // =========================================================

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
    private void OnDisable() { s_all.Remove(this); }

    private void LateUpdate()
    {
        if (_dirty) Deform();
    }

    private void Deform()
    {
        _dirty = false;
        if (_mesh == null || _bindPositions == null || _mesh.vertexCount != _bindPositions.Length) return;

        // Maya: p' = p * bindPre * pose  ->  column form: pose * bindPre * p
        for (int j = 0; j < _skin.Length; j++) _skin[j] = Pose[j] * BindPre[j];

        int count = _bindPositions.Length;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = _bindPositions[i];
            Vector3 position = Vector3.zero, normal = Vector3.zero;
            float total = 0f;
            int o = i * 4;
            for (int k = 0; k < 4; k++)
            {
                float w = _weights[o + k];
                if (w <= 0f) continue;
                int b = _bones[o + k];
                if ((uint)b >= (uint)_skin.Length) continue;
                Matrix4x4 m = _skin[b];
                position += m.MultiplyPoint3x4(p) * w;
                if (_bindNormals != null) normal += m.MultiplyVector(_bindNormals[i]) * w;
                total += w;
            }
            if (total <= 0f) { position = p; normal = _bindNormals != null ? _bindNormals[i] : Vector3.zero; }
            else if (Mathf.Abs(total - 1f) > 1e-4f) position /= total;
            // Maya (X Y Z) -> Unity (X Y -Z), same as the mesh sync.
            _vertices[i] = new Vector3(position.x, position.y, -position.z);
            if (_normals != null) { normal.Normalize(); _normals[i] = new Vector3(normal.x, normal.y, -normal.z); }
        }

        _mesh.SetVertices(_vertices);
        if (_normals != null) _mesh.SetNormals(_normals);
        else _mesh.RecalculateNormals();
        if (_tangents) _mesh.RecalculateTangents();
        _mesh.RecalculateBounds();
        CS_MayaPreviewSelection.NotifyMeshChanged(_mesh);
        CS_MayaPreviewOutline.NotifyMeshChanged(_mesh);
        CS_MayaPreviewFrameCache.MarkDirty();
    }

    // =========================================================
    // Export
    // =========================================================

    // The bind-pose mesh (Unity space, same units as the live mesh). Caller destroys it.
    public Mesh BindMesh()
    {
        if (_mesh == null || _bindPositions == null) return null;
        Mesh copy = Instantiate(_mesh);
        copy.name = _mesh.name;
        var vertices = new Vector3[_bindPositions.Length];
        for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(_bindPositions[i].x, _bindPositions[i].y, -_bindPositions[i].z);
        copy.SetVertices(vertices);
        if (_bindNormals != null)
        {
            var normals = new Vector3[_bindNormals.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(_bindNormals[i].x, _bindNormals[i].y, -_bindNormals[i].z);
            copy.SetNormals(normals);
        }
        copy.RecalculateBounds();
        return copy;
    }

    // Up to 4 influences per vertex (index into Joints) and their weights.
    public void Influences(out int[] bones, out float[] weights)
    {
        bones = _bones;
        weights = _weights;
    }

    // Joint world matrix at bind time, in Unity space (same units as the live mesh).
    public Matrix4x4 BindWorldUnity(int joint) => MayaToUnity(BindPre[joint].inverse);

    // Joint world matrix now, in Unity space.
    public Matrix4x4 PoseWorldUnity(int joint) => MayaToUnity(Pose[joint]);

    // Maya (right-handed) -> Unity (left-handed): Z mirrored on both sides.
    private static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
    private static Matrix4x4 MayaToUnity(Matrix4x4 m) => FlipZ * m * FlipZ;
}
