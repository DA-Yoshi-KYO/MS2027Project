using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewFbx
 Summary: Binary FBX 7.4 writer for the preview export (works in the built player, no FBX SDK).
          Writes the hierarchy (transforms), meshes (normals, 2 UV sets, vertex colours,
          material slots) and one material per slot name.
          - Same convention as Maya's FBX export: right-handed, Y up, centimetres.
            Unity (left-handed, metres) -> FBX: X mirrored, polygons flipped, x100.
            Unity's FBX importer converts it back (Convert Units), so the model comes in at the
            same size and position as in the preview, and Maya opens it at real size.
          - Control points are welded by position (one shell like in Maya); normals, UVs and
            colours are stored per polygon vertex.
 History: 2026/10/11 New
=====================================+
*/

// Mesh data of one node (local space of the node, Unity units).
public class CS_MayaPreviewFbxMesh
{
    public string name;
    public Vector3[] vertices;
    public Vector3[] normals;        // null = none
    public Vector2[] uv0, uv1;       // null = none
    public Color[] colors;           // null = none
    public readonly List<int[]> submeshes = new List<int[]>();   // polygon corners per submesh
    public readonly List<int> sizes = new List<int>();           // corners per polygon (3 or 4)
    public int[] materials;          // per submesh: index into the material list (-1 = none)

    public static CS_MayaPreviewFbxMesh From(Mesh mesh, Matrix4x4 transform, string name)
    {
        var data = new CS_MayaPreviewFbxMesh { name = name };
        Vector3[] vertices = mesh.vertices;
        int count = vertices.Length;
        Vector3[] normals = mesh.normals;
        if (transform != Matrix4x4.identity)
        {
            Matrix4x4 normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < count; i++) vertices[i] = transform.MultiplyPoint3x4(vertices[i]);
            if (normals.Length == count)
                for (int i = 0; i < count; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
        }
        data.vertices = vertices;
        data.normals = normals.Length == count ? normals : null;
        Vector2[] uv0 = mesh.uv, uv1 = mesh.uv2;
        Color[] colors = mesh.colors;
        data.uv0 = uv0.Length == count ? uv0 : null;
        data.uv1 = uv1.Length == count ? uv1 : null;
        data.colors = colors.Length == count ? colors : null;

        bool mirrored = transform.determinant < 0f;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            MeshTopology topology = mesh.GetTopology(s);
            int size = topology == MeshTopology.Triangles ? 3 : topology == MeshTopology.Quads ? 4 : 0;
            int[] corners = size > 0 ? mesh.GetIndices(s) : new int[0];
            if (size > 0 && corners.Length % size != 0) Array.Resize(ref corners, corners.Length - corners.Length % size);
            if (mirrored) for (int p = 0; p + size <= corners.Length; p += size) Array.Reverse(corners, p + 1, size - 1);
            data.submeshes.Add(corners);
            data.sizes.Add(Mathf.Max(3, size));
        }
        return data;
    }
}

public class CS_MayaPreviewFbxNode
{
    public string name;
    public int parent = -1;                       // index of an earlier node, -1 = top level
    public Vector3 position;                      // local, Unity units
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public CS_MayaPreviewFbxMesh mesh;            // null = empty (group)
}

public static class CS_MayaPreviewFbx
{
    private const int Version = 7400;
    private const double UnitScale = 100.0;      // metres -> centimetres

    // Header / footer values as written by the FBX SDK based exporters (Blender, Assimp).
    private static readonly byte[] HeadMagic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\u001a\0");
    private static readonly byte[] FileId = { 0x28, 0xb3, 0x2a, 0xeb, 0xb6, 0x24, 0xcc, 0xc2, 0xbf, 0xc8, 0xb0, 0x2a, 0xa9, 0x2b, 0xfc, 0xf1 };
    private static readonly byte[] FootId = { 0xfa, 0xbc, 0xab, 0x09, 0xd0, 0xc8, 0xd4, 0x66, 0xb1, 0x76, 0xfb, 0x83, 0x1c, 0xf7, 0x26, 0x7e };
    private static readonly byte[] FootMagic = { 0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b };
    private const string CreationTime = "1970-01-01 10:00:00:000";
    private const string Separator = "\u0000\u0001";


    // =========================================================
    // Public
    // =========================================================

    public static void Write(string path, IList<CS_MayaPreviewFbxNode> nodes, IList<string> materialNames, IList<Color> materialColors, string creator)
    {
        string fileName = Path.GetFileName(path);
        long nextId = 1000000;
        long NewId() => ++nextId;

        var top = new List<Node>();
        top.Add(HeaderExtension(creator, fileName));
        top.Add(new Node("FileId").Raw(FileId));
        top.Add(new Node("CreationTime").Str(CreationTime));
        top.Add(new Node("Creator").Str(creator));
        top.Add(GlobalSettings());

        var documents = new Node("Documents");
        documents.Add("Count").Int(1);
        Node document = documents.Add("Document").Long(NewId()).Str("").Str("Scene");
        Node documentProps = document.Add("Properties70");
        P(documentProps, "SourceObject", "object", "", "");
        P(documentProps, "ActiveAnimStackName", "KString", "", "").Str("");
        document.Add("RootNode").Long(0);
        top.Add(documents);
        top.Add(new Node("References"));

        var objects = new Node("Objects");
        var connections = new Node("Connections");

        // Materials
        var materialIds = new long[materialNames.Count];
        int materialCount = 0;
        var usedMaterials = new bool[materialNames.Count];
        foreach (CS_MayaPreviewFbxNode node in nodes)
            if (node?.mesh?.materials != null)
                foreach (int m in node.mesh.materials)
                    if (m >= 0 && m < usedMaterials.Length) usedMaterials[m] = true;
        for (int m = 0; m < materialNames.Count; m++)
        {
            if (!usedMaterials[m]) continue;
            materialIds[m] = NewId();
            materialCount++;
            Color color = materialColors != null && m < materialColors.Count ? materialColors[m] : Color.white;
            Node material = objects.Add("Material").Long(materialIds[m]).Str(materialNames[m] + Separator + "Material").Str("");
            material.Add("Version").Int(102);
            material.Add("ShadingModel").Str("lambert");
            material.Add("MultiLayer").Int(0);
            Node props = material.Add("Properties70");
            P(props, "DiffuseColor", "Color", "", "A").Dbl(color.r).Dbl(color.g).Dbl(color.b);
            P(props, "Diffuse", "Vector3D", "Vector", "").Dbl(color.r).Dbl(color.g).Dbl(color.b);
        }

        // Models + geometry
        var modelIds = new long[nodes.Count];
        int geometryCount = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            CS_MayaPreviewFbxNode node = nodes[i];
            modelIds[i] = NewId();
            bool hasMesh = node.mesh != null && node.mesh.vertices != null && node.mesh.vertices.Length > 0;
            Node model = objects.Add("Model").Long(modelIds[i]).Str(Clean(node.name) + Separator + "Model").Str(hasMesh ? "Mesh" : "Null");
            model.Add("Version").Int(232);
            Node props = model.Add("Properties70");
            Vector3 t = node.position;
            Vector3 r = MirroredEulerXyz(node.rotation);
            Vector3 s = node.scale;
            P(props, "Lcl Translation", "Lcl Translation", "", "A").Dbl(-t.x * UnitScale).Dbl(t.y * UnitScale).Dbl(t.z * UnitScale);
            P(props, "Lcl Rotation", "Lcl Rotation", "", "A").Dbl(r.x).Dbl(r.y).Dbl(r.z);
            P(props, "Lcl Scaling", "Lcl Scaling", "", "A").Dbl(s.x).Dbl(s.y).Dbl(s.z);
            if (hasMesh) P(props, "DefaultAttributeIndex", "int", "Integer", "").Int(0);
            P(props, "InheritType", "enum", "", "").Int(1);
            model.Add("MultiLayer").Int(0);
            model.Add("MultiTake").Int(0);
            model.Add("Culling").Str("CullingOff");

            long parentId = node.parent >= 0 && node.parent < i ? modelIds[node.parent] : 0;
            Connect(connections, modelIds[i], parentId);

            if (!hasMesh) continue;
            long geometryId = NewId();
            geometryCount++;
            List<int> local = Geometry(objects, geometryId, node.mesh, Clean(node.name));
            Connect(connections, geometryId, modelIds[i]);
            foreach (int m in local) Connect(connections, materialIds[m], modelIds[i]);
        }

        var definitions = new Node("Definitions");
        definitions.Add("Version").Int(100);
        definitions.Add("Count").Int(1 + nodes.Count + geometryCount + materialCount);
        definitions.Add("ObjectType").Str("GlobalSettings").Add("Count").Int(1);
        if (nodes.Count > 0) definitions.Add("ObjectType").Str("Model").Add("Count").Int(nodes.Count);
        if (geometryCount > 0) definitions.Add("ObjectType").Str("Geometry").Add("Count").Int(geometryCount);
        if (materialCount > 0) definitions.Add("ObjectType").Str("Material").Add("Count").Int(materialCount);
        top.Add(definitions);
        top.Add(objects);
        top.Add(connections);
        var takes = new Node("Takes");
        takes.Add("Current").Str("");
        top.Add(takes);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".writing";
        using (var writer = new BinaryWriter(File.Create(temporary)))
        {
            writer.Write(HeadMagic);
            writer.Write((uint)Version);
            foreach (Node node in top) node.Write(writer);
            writer.Write(new byte[Node.SentinelLength]);
            writer.Write(FootId);
            writer.Write(new byte[4]);
            long offset = writer.BaseStream.Position;
            int pad = (int)(((offset + 15) & ~15L) - offset);
            if (pad == 0) pad = 16;
            writer.Write(new byte[pad]);
            writer.Write((uint)Version);
            writer.Write(new byte[120]);
            writer.Write(FootMagic);
        }
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
    }


    // =========================================================
    // Sections
    // =========================================================

    private static Node HeaderExtension(string creator, string fileName)
    {
        var header = new Node("FBXHeaderExtension");
        header.Add("FBXHeaderVersion").Int(1003);
        header.Add("FBXVersion").Int(Version);
        header.Add("EncryptionType").Int(0);
        Node stamp = header.Add("CreationTimeStamp");
        stamp.Add("Version").Int(1000);
        stamp.Add("Year").Int(1970);
        stamp.Add("Month").Int(1);
        stamp.Add("Day").Int(1);
        stamp.Add("Hour").Int(10);
        stamp.Add("Minute").Int(0);
        stamp.Add("Second").Int(0);
        stamp.Add("Millisecond").Int(0);
        header.Add("Creator").Str(creator);

        Node info = header.Add("SceneInfo").Str("GlobalInfo" + Separator + "SceneInfo").Str("UserData");
        info.Add("Type").Str("UserData");
        info.Add("Version").Int(100);
        Node meta = info.Add("MetaData");
        meta.Add("Version").Int(100);
        meta.Add("Title").Str("");
        meta.Add("Subject").Str("");
        meta.Add("Author").Str("");
        meta.Add("Keywords").Str("");
        meta.Add("Revision").Str("");
        meta.Add("Comment").Str("");
        Node props = info.Add("Properties70");
        P(props, "DocumentUrl", "KString", "Url", "").Str(fileName);
        P(props, "SrcDocumentUrl", "KString", "Url", "").Str(fileName);
        P(props, "Original", "Compound", "", "");
        P(props, "Original|ApplicationVendor", "KString", "", "").Str("MS2027");
        P(props, "Original|ApplicationName", "KString", "", "").Str("Maya Preview");
        P(props, "Original|ApplicationVersion", "KString", "", "").Str("1");
        P(props, "Original|DateTime_GMT", "DateTime", "", "").Str("01/01/1970 00:00:00.000");
        P(props, "Original|FileName", "KString", "", "").Str(fileName);
        P(props, "LastSaved", "Compound", "", "");
        P(props, "LastSaved|ApplicationVendor", "KString", "", "").Str("MS2027");
        P(props, "LastSaved|ApplicationName", "KString", "", "").Str("Maya Preview");
        P(props, "LastSaved|ApplicationVersion", "KString", "", "").Str("1");
        P(props, "LastSaved|DateTime_GMT", "DateTime", "", "").Str("01/01/1970 00:00:00.000");
        return header;
    }

    // Maya: Y up, right-handed, centimetres.
    private static Node GlobalSettings()
    {
        var settings = new Node("GlobalSettings");
        settings.Add("Version").Int(1000);
        Node props = settings.Add("Properties70");
        P(props, "UpAxis", "int", "Integer", "").Int(1);
        P(props, "UpAxisSign", "int", "Integer", "").Int(1);
        P(props, "FrontAxis", "int", "Integer", "").Int(2);
        P(props, "FrontAxisSign", "int", "Integer", "").Int(1);
        P(props, "CoordAxis", "int", "Integer", "").Int(0);
        P(props, "CoordAxisSign", "int", "Integer", "").Int(1);
        P(props, "OriginalUpAxis", "int", "Integer", "").Int(1);
        P(props, "OriginalUpAxisSign", "int", "Integer", "").Int(1);
        P(props, "UnitScaleFactor", "double", "Number", "").Dbl(1.0);
        P(props, "OriginalUnitScaleFactor", "double", "Number", "").Dbl(1.0);
        P(props, "AmbientColor", "ColorRGB", "Color", "").Dbl(0).Dbl(0).Dbl(0);
        P(props, "DefaultCamera", "KString", "", "").Str("Producer Perspective");
        P(props, "TimeMode", "enum", "", "").Int(11);
        P(props, "TimeProtocol", "enum", "", "").Int(2);
        P(props, "SnapOnFrameMode", "enum", "", "").Int(0);
        P(props, "TimeSpanStart", "KTime", "Time", "").Long(0);
        P(props, "TimeSpanStop", "KTime", "Time", "").Long(46186158000L);
        P(props, "CustomFrameRate", "double", "Number", "").Dbl(-1.0);
        P(props, "TimeMarker", "Compound", "", "");
        P(props, "CurrentTimeMarker", "int", "Integer", "").Int(-1);
        return settings;
    }

    // Returns the materials connected to the model, in slot order.
    private static List<int> Geometry(Node objects, long id, CS_MayaPreviewFbxMesh mesh, string name)
    {
        // Welded control points (mirrored X, centimetres).
        Vector3[] vertices = mesh.vertices;
        var weld = new Dictionary<Vector3, int>(vertices.Length);
        var control = new int[vertices.Length];
        var points = new List<double>(vertices.Length * 3);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            if (!weld.TryGetValue(v, out int index))
            {
                index = points.Count / 3;
                weld[v] = index;
                points.Add(-v.x * UnitScale);
                points.Add(v.y * UnitScale);
                points.Add(v.z * UnitScale);
            }
            control[i] = index;
        }

        // Local material slots.
        var local = new List<int>();
        var slotOfSubmesh = new int[mesh.submeshes.Count];
        int fallback = -1;
        if (mesh.materials != null) foreach (int m in mesh.materials) if (m >= 0) { fallback = m; break; }
        for (int s = 0; s < mesh.submeshes.Count; s++)
        {
            int m = mesh.materials != null && s < mesh.materials.Length && mesh.materials[s] >= 0 ? mesh.materials[s] : fallback;
            if (m >= 0 && !local.Contains(m)) local.Add(m);
            slotOfSubmesh[s] = m >= 0 ? local.IndexOf(m) : 0;
        }

        // Polygons: Unity order (a, b, c) becomes (a, c, b) because X is mirrored.
        var polygonIndex = new List<int>();
        var corners = new List<int>();           // Unity vertex index per polygon vertex
        var polygonMaterial = new List<int>();
        for (int s = 0; s < mesh.submeshes.Count; s++)
        {
            int[] indices = mesh.submeshes[s];
            int size = mesh.sizes[s];
            for (int p = 0; p + size <= indices.Length; p += size)
            {
                for (int k = 0; k < size; k++)
                {
                    int corner = indices[p + (k == 0 ? 0 : size - k)];
                    if (corner < 0 || corner >= vertices.Length) corner = 0;
                    corners.Add(corner);
                    int point = control[corner];
                    polygonIndex.Add(k == size - 1 ? -point - 1 : point);
                }
                polygonMaterial.Add(slotOfSubmesh[s]);
            }
        }

        Node geometry = objects.Add("Geometry").Long(id).Str(name + Separator + "Geometry").Str("Mesh");
        geometry.Add("GeometryVersion").Int(124);
        geometry.Add("Vertices").Doubles(points.ToArray());
        geometry.Add("PolygonVertexIndex").Ints(polygonIndex.ToArray());

        var layer0 = new List<string>();
        var layer1 = new List<string>();
        if (mesh.normals != null)
        {
            var normals = new double[corners.Count * 3];
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 n = mesh.normals[corners[i]];
                normals[i * 3] = -n.x;
                normals[i * 3 + 1] = n.y;
                normals[i * 3 + 2] = n.z;
            }
            Node element = geometry.Add("LayerElementNormal").Int(0);
            element.Add("Version").Int(101);
            element.Add("Name").Str("");
            element.Add("MappingInformationType").Str("ByPolygonVertex");
            element.Add("ReferenceInformationType").Str("Direct");
            element.Add("Normals").Doubles(normals);
            layer0.Add("LayerElementNormal");
        }
        if (mesh.uv0 != null) { UvElement(geometry, 0, "map1", mesh.uv0, corners); layer0.Add("LayerElementUV"); }
        if (mesh.uv1 != null) { UvElement(geometry, 1, "uv2", mesh.uv1, corners); layer1.Add("LayerElementUV"); }
        if (mesh.colors != null)
        {
            var colors = new double[mesh.colors.Length * 4];
            for (int i = 0; i < mesh.colors.Length; i++)
            {
                Color c = mesh.colors[i];
                colors[i * 4] = c.r; colors[i * 4 + 1] = c.g; colors[i * 4 + 2] = c.b; colors[i * 4 + 3] = c.a;
            }
            Node element = geometry.Add("LayerElementColor").Int(0);
            element.Add("Version").Int(101);
            element.Add("Name").Str("colorSet1");
            element.Add("MappingInformationType").Str("ByPolygonVertex");
            element.Add("ReferenceInformationType").Str("IndexToDirect");
            element.Add("Colors").Doubles(colors);
            element.Add("ColorIndex").Ints(corners.ToArray());
            layer0.Add("LayerElementColor");
        }
        if (local.Count > 0)
        {
            Node element = geometry.Add("LayerElementMaterial").Int(0);
            element.Add("Version").Int(101);
            element.Add("Name").Str("");
            element.Add("MappingInformationType").Str(local.Count == 1 ? "AllSame" : "ByPolygon");
            element.Add("ReferenceInformationType").Str("IndexToDirect");
            element.Add("Materials").Ints(local.Count == 1 ? new[] { 0 } : polygonMaterial.ToArray());
            layer0.Add("LayerElementMaterial");
        }

        Layer(geometry, 0, layer0, 0);
        if (layer1.Count > 0) Layer(geometry, 1, layer1, 1);
        return local;
    }

    private static void UvElement(Node geometry, int index, string name, Vector2[] uv, List<int> corners)
    {
        var values = new double[uv.Length * 2];
        for (int i = 0; i < uv.Length; i++) { values[i * 2] = uv[i].x; values[i * 2 + 1] = uv[i].y; }
        Node element = geometry.Add("LayerElementUV").Int(index);
        element.Add("Version").Int(101);
        element.Add("Name").Str(name);
        element.Add("MappingInformationType").Str("ByPolygonVertex");
        element.Add("ReferenceInformationType").Str("IndexToDirect");
        element.Add("UV").Doubles(values);
        element.Add("UVIndex").Ints(corners.ToArray());
    }

    private static void Layer(Node geometry, int index, List<string> types, int typedIndex)
    {
        Node layer = geometry.Add("Layer").Int(index);
        layer.Add("Version").Int(100);
        foreach (string type in types)
        {
            Node element = layer.Add("LayerElement");
            element.Add("Type").Str(type);
            element.Add("TypedIndex").Int(type == "LayerElementUV" ? typedIndex : 0);
        }
    }

    private static void Connect(Node connections, long child, long parent) =>
        connections.Add("C").Str("OO").Long(child).Long(parent);

    private static Node P(Node props, string name, string type, string label, string flags) =>
        props.Add("P").Str(name).Str(type).Str(label).Str(flags);

    private static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Object";
        return name.Replace("\0", "").Replace("\u0001", "");
    }

    // Unity rotation -> FBX "Lcl Rotation" (degrees, XYZ order: R = Rz * Ry * Rx) after mirroring X.
    public static Vector3 MirroredEulerXyz(Quaternion rotation)
    {
        Matrix4x4 m = Matrix4x4.Rotate(rotation);
        // M R M with M = diag(-1, 1, 1): entries with exactly one 0 index change sign.
        double r00 = m.m00, r01 = -m.m01, r02 = -m.m02;
        double r10 = -m.m10, r11 = m.m11, r12 = m.m12;
        double r20 = -m.m20, r21 = m.m21, r22 = m.m22;
        double x, y, z;
        double sinY = -r20;
        if (sinY >= 0.9999995 || sinY <= -0.9999995)
        {
            y = sinY > 0 ? Math.PI * 0.5 : -Math.PI * 0.5;
            z = 0.0;
            x = Math.Atan2(-r12, r11);
        }
        else
        {
            y = Math.Asin(sinY);
            x = Math.Atan2(r21, r22);
            z = Math.Atan2(r10, r00);
        }
        const double toDegrees = 180.0 / Math.PI;
        return new Vector3((float)(x * toDegrees), (float)(y * toDegrees), (float)(z * toDegrees));
    }


    // =========================================================
    // Binary node records (FBX 7.4: 32 bit offsets, 13 byte null record)
    // =========================================================

    private sealed class Node
    {
        public const int SentinelLength = 13;
        private readonly string _name;
        private readonly List<byte> _types = new List<byte>();
        private readonly List<byte[]> _data = new List<byte[]>();
        private readonly List<Node> _children = new List<Node>();
        private long _size = -1;

        public Node(string name) { _name = name; }

        public Node Add(string name)
        {
            var child = new Node(name);
            _children.Add(child);
            return child;
        }

        public Node Int(int value) => Prop('I', BitConverter.GetBytes(value));
        public Node Long(long value) => Prop('L', BitConverter.GetBytes(value));
        public Node Dbl(double value) => Prop('D', BitConverter.GetBytes(value));

        public Node Str(string value)
        {
            byte[] text = Encoding.UTF8.GetBytes(value ?? "");
            return Prop('S', Join(BitConverter.GetBytes((uint)text.Length), text));
        }

        public Node Raw(byte[] value) => Prop('R', Join(BitConverter.GetBytes((uint)value.Length), value));

        public Node Ints(int[] values)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Prop('i', Join(ArrayHeader(values.Length, bytes.Length), bytes));
        }

        public Node Doubles(double[] values)
        {
            var bytes = new byte[values.Length * 8];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Prop('d', Join(ArrayHeader(values.Length, bytes.Length), bytes));
        }

        private Node Prop(char type, byte[] data)
        {
            _types.Add((byte)type);
            _data.Add(data);
            return this;
        }

        // Count, encoding (0 = not compressed), byte length.
        private static byte[] ArrayHeader(int count, int length)
        {
            var header = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)count), 0, header, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)length), 0, header, 8, 4);
            return header;
        }

        private static byte[] Join(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        private long PropertyLength()
        {
            long length = 0;
            foreach (byte[] data in _data) length += 1 + data.Length;
            return length;
        }

        public long Size()
        {
            if (_size >= 0) return _size;
            long size = 13 + _name.Length + PropertyLength();
            if (_children.Count > 0)
            {
                foreach (Node child in _children) size += child.Size();
                size += SentinelLength;
            }
            else if (_data.Count == 0) size += SentinelLength;
            _size = size;
            return size;
        }

        public void Write(BinaryWriter writer)
        {
            long end = writer.BaseStream.Position + Size();
            if (end > uint.MaxValue) throw new IOException("The FBX file would be larger than 4 GB.");
            writer.Write((uint)end);
            writer.Write((uint)_data.Count);
            writer.Write((uint)PropertyLength());
            writer.Write((byte)_name.Length);
            writer.Write(Encoding.ASCII.GetBytes(_name));
            for (int i = 0; i < _data.Count; i++)
            {
                writer.Write(_types[i]);
                writer.Write(_data[i]);
            }
            if (_children.Count > 0)
            {
                foreach (Node child in _children) child.Write(writer);
                writer.Write(new byte[SentinelLength]);
            }
            else if (_data.Count == 0) writer.Write(new byte[SentinelLength]);
        }
    }
}
