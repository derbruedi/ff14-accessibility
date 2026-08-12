using System.Text;

namespace cmdump;

/// <summary>
/// [LOCAL 2026-08-09] A hand-written reader for FFXIV model files, version 6.
///
/// WHY IT EXISTS. <c>Lumina.Data.Files.MdlFile</c> throws
/// <c>EndOfStreamException</c> on every character face model in this build (verified
/// again 2026-08-09 against <c>c0101f0001_fac.mdl</c>: it dies in <c>ReadByte</c>
/// AFTER the shape tables, i.e. its cursor has already drifted). The cause is the
/// BONE TABLE: Lumina reads a fixed 132-byte record per table (64 ushorts + count +
/// 3 pad), which is the version-5 layout, and version 6 stores the tables
/// differently. Everything before the bone tables parses correctly with Lumina's own
/// field order, which is why <c>ModelStrings</c> in Program.cs has always worked.
///
/// HOW THE BONE TABLE IS HANDLED, and why this is not a guess. This reader does not
/// claim to know the v6 bone-table layout. It parses deterministically up to the end
/// of <c>BoneNameOffsets</c> and then SEARCHES forward for the <c>Shapes</c> array,
/// accepting only an offset where all of the following hold for every one of the
/// <c>ShapeCount</c> 16-byte records:
///   - <c>StringOffset</c> lands inside the string block and on a name starting "shp_";
///   - every <c>ShapeMeshStartIndex</c>/<c>ShapeMeshCount</c> pair is inside the
///     shape-mesh array;
///   - and the tables that follow (shape meshes, shape values, the submesh bone map
///     and the four bounding boxes plus one per bone) end EXACTLY at
///     <c>68 + StackSize + RuntimeSize</c>.
/// The last condition is the strong one: it consumes the whole runtime section with
/// no slack, so a wrong offset cannot pass. <see cref="ShapeSectionOffset"/> reports
/// where it landed and <see cref="BoneTableBytes"/> what that implies the bone tables
/// occupy, so the assumption is inspectable rather than buried.
///
/// Only what the shape-key measurement needs is exposed: LOD 0 meshes, their vertex
/// positions, the LOD 0 index buffer, the shape tables, the submesh attribute masks
/// and the per-bone bounding boxes.
/// </summary>
public sealed class MdlV6
{
    public uint Version;
    public string[] Strings = Array.Empty<string>();
    private byte[] _strBlock = Array.Empty<byte>();

    public ushort MeshCount, AttributeCount, SubmeshCount, MaterialCount, BoneCount,
                  BoneTableCount, ShapeCount, ShapeMeshCount, ShapeValueCount;

    public sealed class Mesh
    {
        public ushort VertexCount;
        public uint IndexCount;
        public ushort MaterialIndex, SubMeshIndex, SubMeshCount, BoneTableIndex;
        public uint StartIndex;
        public uint[] VertexBufferOffset = new uint[3];
        public byte[] VertexBufferStride = new byte[3];
        public byte VertexStreamCount;
        /// <summary>Decoded positions, one per vertex. Empty when the declaration
        /// carries no usable position element.</summary>
        public float[,] Positions = new float[0, 0];
    }

    public sealed class Submesh
    {
        public uint IndexOffset, IndexCount, AttributeIndexMask;
        public ushort BoneStartIndex, BoneCount;
    }

    public sealed class Shape
    {
        public string Name = string.Empty;
        public ushort[] MeshStart = new ushort[3];
        public ushort[] MeshCount = new ushort[3];
    }

    public sealed class ShapeMesh
    {
        public uint MeshIndexOffset, ValueCount, ValueOffset;
    }

    public Mesh[] Meshes = Array.Empty<Mesh>();
    public Submesh[] Submeshes = Array.Empty<Submesh>();
    public string[] AttributeNames = Array.Empty<string>();
    public string[] MaterialNames = Array.Empty<string>();
    public string[] BoneNames = Array.Empty<string>();
    public Shape[] Shapes = Array.Empty<Shape>();
    public ShapeMesh[] ShapeMeshes = Array.Empty<ShapeMesh>();
    public (ushort Base, ushort Replacing)[] ShapeValues = Array.Empty<(ushort, ushort)>();
    public ushort[] Indices = Array.Empty<ushort>();
    /// <summary>Lod 0's first mesh and how many meshes it owns.</summary>
    public ushort Lod0MeshIndex, Lod0MeshCount;
    /// <summary>Min/Max per bone, parallel to <see cref="BoneNames"/>. Four floats
    /// each in the file; only the first three are spatial.</summary>
    public float[][] BoneBoxMin = Array.Empty<float[]>();
    public float[][] BoneBoxMax = Array.Empty<float[]>();

    public int ShapeSectionOffset { get; private set; }
    public int BoneTableBytes { get; private set; }

    /// <summary>Offsets and counts as each section was parsed. Printed by
    /// <c>cmdump mdl6</c> so a bad parse is diagnosable instead of just throwing.</summary>
    public List<string> Trace { get; } = new();

    // ── vertex element usage/type codes ──────────────────────────────────────
    private const byte UsagePosition = 0;

    private sealed class VElem
    {
        public byte Stream, Offset, Type, Usage, UsageIndex;
    }

    /// <param name="d">The whole .mdl as handed back by <c>GameData.GetFile(path).Data</c>.</param>
    public static MdlV6 Parse(byte[] d)
    {
        var m = new MdlV6();
        m.Version = BitConverter.ToUInt32(d, 0);
        uint stackSize = BitConverter.ToUInt32(d, 4);
        uint runtimeSize = BitConverter.ToUInt32(d, 8);
        int vertDeclCount = BitConverter.ToUInt16(d, 12);
        int fileMaterialCount = BitConverter.ToUInt16(d, 14);
        var lodVertexOffset = new uint[3];
        var lodIndexOffset = new uint[3];
        for (int i = 0; i < 3; i++) lodVertexOffset[i] = BitConverter.ToUInt32(d, 16 + i * 4);
        for (int i = 0; i < 3; i++) lodIndexOffset[i] = BitConverter.ToUInt32(d, 28 + i * 4);

        // ── vertex declarations: 136 bytes each, 8-byte elements, 0xFF stream ends one
        int p = 0x44;
        var decls = new List<VElem[]>();
        for (int i = 0; i < vertDeclCount; i++)
        {
            var list = new List<VElem>();
            int q = p;
            while (true)
            {
                byte stream = d[q];
                if (stream == 0xFF) break;
                list.Add(new VElem { Stream = stream, Offset = d[q + 1], Type = d[q + 2], Usage = d[q + 3], UsageIndex = d[q + 4] });
                q += 8;
                if (q - p >= 136) break;
            }
            decls.Add(list.ToArray());
            m.Trace.Add($"decl {i}: " + string.Join("  ", list.Select(e => $"s{e.Stream}+{e.Offset} type={e.Type} usage={e.Usage}.{e.UsageIndex}")));
            p += 136;
        }

        // ── string block
        int stringCount = BitConverter.ToUInt16(d, p);
        int stringSize = (int)BitConverter.ToUInt32(d, p + 4);
        int strBase = p + 8;
        m._strBlock = new byte[stringSize];
        Array.Copy(d, strBase, m._strBlock, 0, stringSize);
        m.Strings = Encoding.UTF8.GetString(d, strBase, stringSize).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        p = strBase + stringSize;

        // ── ModelHeader, 56 bytes (Lumina field order; every field lands on its
        //    natural alignment so there is no hidden padding)
        int mh = p;
        m.MeshCount = BitConverter.ToUInt16(d, mh + 4);
        m.AttributeCount = BitConverter.ToUInt16(d, mh + 6);
        m.SubmeshCount = BitConverter.ToUInt16(d, mh + 8);
        m.MaterialCount = BitConverter.ToUInt16(d, mh + 10);
        m.BoneCount = BitConverter.ToUInt16(d, mh + 12);
        m.BoneTableCount = BitConverter.ToUInt16(d, mh + 14);
        m.ShapeCount = BitConverter.ToUInt16(d, mh + 16);
        m.ShapeMeshCount = BitConverter.ToUInt16(d, mh + 18);
        m.ShapeValueCount = BitConverter.ToUInt16(d, mh + 20);
        int elementIdCount = BitConverter.ToUInt16(d, mh + 24);
        int terrainShadowMeshCount = d[mh + 26];
        byte flags2 = d[mh + 27];
        int terrainShadowSubmeshCount = BitConverter.ToUInt16(d, mh + 38);
        bool extraLod = (flags2 & 0x10) != 0;
        p = mh + 56;

        p += elementIdCount * 32;

        // ── 3 LodStructs, 60 bytes each. Only LOD 0 is measured.
        m.Lod0MeshIndex = BitConverter.ToUInt16(d, p);
        m.Lod0MeshCount = BitConverter.ToUInt16(d, p + 2);
        uint lod0IndexDataOffset = BitConverter.ToUInt32(d, p + 56);
        uint lod0IndexBufferSize = BitConverter.ToUInt32(d, p + 48);
        for (int l = 0; l < 3; l++)
            m.Trace.Add($"lod {l}: meshIdx={BitConverter.ToUInt16(d, p + l * 60)} meshCount={BitConverter.ToUInt16(d, p + l * 60 + 2)} " +
                        $"edgeSize={BitConverter.ToUInt32(d, p + l * 60 + 28)} poly={BitConverter.ToUInt32(d, p + l * 60 + 36)} " +
                        $"vtxSize={BitConverter.ToUInt32(d, p + l * 60 + 44)} idxSize={BitConverter.ToUInt32(d, p + l * 60 + 48)} " +
                        $"vtxOff={BitConverter.ToUInt32(d, p + l * 60 + 52)} idxOff={BitConverter.ToUInt32(d, p + l * 60 + 56)}");
        p += 3 * 60;
        if (extraLod) p += 3 * 40;   // ExtraLodStruct: 20 ushorts

        // ── meshes, 36 bytes each
        m.Meshes = new Mesh[m.MeshCount];
        for (int i = 0; i < m.MeshCount; i++)
        {
            int o = p + i * 36;
            var me = new Mesh
            {
                VertexCount = BitConverter.ToUInt16(d, o),
                IndexCount = BitConverter.ToUInt32(d, o + 4),
                MaterialIndex = BitConverter.ToUInt16(d, o + 8),
                SubMeshIndex = BitConverter.ToUInt16(d, o + 10),
                SubMeshCount = BitConverter.ToUInt16(d, o + 12),
                BoneTableIndex = BitConverter.ToUInt16(d, o + 14),
                StartIndex = BitConverter.ToUInt32(d, o + 16),
            };
            for (int s = 0; s < 3; s++) me.VertexBufferOffset[s] = BitConverter.ToUInt32(d, o + 20 + s * 4);
            for (int s = 0; s < 3; s++) me.VertexBufferStride[s] = d[o + 32 + s];
            me.VertexStreamCount = d[o + 35];
            m.Meshes[i] = me;
            if (i < 6) m.Trace.Add($"mesh {i}: verts={me.VertexCount} start={me.StartIndex} vbOff=[{string.Join(",", me.VertexBufferOffset)}] stride=[{string.Join(",", me.VertexBufferStride)}] streams={me.VertexStreamCount}");
        }
        p += m.MeshCount * 36;

        // ── name-offset arrays
        m.AttributeNames = m.ReadNames(d, p, m.AttributeCount, strBase); p += m.AttributeCount * 4;
        p += terrainShadowMeshCount * 20;

        m.Submeshes = new Submesh[m.SubmeshCount];
        for (int i = 0; i < m.SubmeshCount; i++)
        {
            int o = p + i * 16;
            m.Submeshes[i] = new Submesh
            {
                IndexOffset = BitConverter.ToUInt32(d, o),
                IndexCount = BitConverter.ToUInt32(d, o + 4),
                AttributeIndexMask = BitConverter.ToUInt32(d, o + 8),
                BoneStartIndex = BitConverter.ToUInt16(d, o + 12),
                BoneCount = BitConverter.ToUInt16(d, o + 14),
            };
        }
        p += m.SubmeshCount * 16;
        p += terrainShadowSubmeshCount * 12;

        m.MaterialNames = m.ReadNames(d, p, m.MaterialCount, strBase); p += m.MaterialCount * 4;
        m.BoneNames = m.ReadNames(d, p, m.BoneCount, strBase); p += m.BoneCount * 4;

        // ── the bone tables, whose v6 layout is what defeats Lumina. Find the shape
        //    array by validation instead of assuming a size; see the class comment.
        int runtimeEnd = 0x44 + (int)stackSize + (int)runtimeSize;
        m.Trace.Add($"stack={stackSize} runtime={runtimeSize} runtimeEnd=0x{runtimeEnd:X} fileLen={d.Length}");
        m.Trace.Add($"strBase=0x{strBase:X} stringSize={stringSize} stringCount={stringCount} modelHeader=0x{mh:X}");
        m.Trace.Add($"elementIds={elementIdCount} tsMesh={terrainShadowMeshCount} tsSub={terrainShadowSubmeshCount} extraLod={extraLod}");
        m.Trace.Add($"lod0 vtxOff={lodVertexOffset[0]} idxOff={lod0IndexDataOffset} idxSize={lod0IndexBufferSize}");
        m.Trace.Add($"after boneNameOffsets p=0x{p:X}; shapeCount={m.ShapeCount} shapeMeshCount={m.ShapeMeshCount} shapeValueCount={m.ShapeValueCount}");
        int shapeAt = -1;
        for (int cand = p; cand <= p + 65536 && cand + m.ShapeCount * 16 <= d.Length; cand += 4)
        {
            if (!m.ShapesLookRight(d, cand, strBase, stringSize)) continue;
            bool fits = m.TailFits(d, cand, runtimeEnd);
            m.Trace.Add($"  candidate shapes at 0x{cand:X} (+{cand - p}) names ok, tailFits={fits} tailEnd=0x{m.TailEnd(d, cand):X}");
            if (fits) { shapeAt = cand; break; }
        }
        if (shapeAt < 0)
        {
            // Fall back to the FIRST offset whose shape names all validate, so the
            // trace shows how far off the tail arithmetic is instead of only failing.
            for (int cand = p; cand <= p + 65536 && cand + m.ShapeCount * 16 <= d.Length; cand += 4)
                if (m.ShapesLookRight(d, cand, strBase, stringSize)) { shapeAt = cand; m.Trace.Add($"  ACCEPTED name-only match at 0x{cand:X}"); break; }
        }
        if (shapeAt < 0) throw new InvalidDataException("could not locate the Shapes array after the bone tables:\n  " + string.Join("\n  ", m.Trace));
        m.ShapeSectionOffset = shapeAt;
        m.BoneTableBytes = shapeAt - p;
        p = shapeAt;

        m.Shapes = new Shape[m.ShapeCount];
        for (int i = 0; i < m.ShapeCount; i++)
        {
            int o = p + i * 16;
            var sh = new Shape { Name = m.StrAt(BitConverter.ToUInt32(d, o)) };
            for (int l = 0; l < 3; l++) sh.MeshStart[l] = BitConverter.ToUInt16(d, o + 4 + l * 2);
            for (int l = 0; l < 3; l++) sh.MeshCount[l] = BitConverter.ToUInt16(d, o + 10 + l * 2);
            m.Shapes[i] = sh;
        }
        p += m.ShapeCount * 16;

        m.ShapeMeshes = new ShapeMesh[m.ShapeMeshCount];
        for (int i = 0; i < m.ShapeMeshCount; i++)
        {
            int o = p + i * 12;
            m.ShapeMeshes[i] = new ShapeMesh
            {
                MeshIndexOffset = BitConverter.ToUInt32(d, o),
                ValueCount = BitConverter.ToUInt32(d, o + 4),
                ValueOffset = BitConverter.ToUInt32(d, o + 8),
            };
        }
        p += m.ShapeMeshCount * 12;

        m.ShapeValues = new (ushort, ushort)[m.ShapeValueCount];
        for (int i = 0; i < m.ShapeValueCount; i++)
            m.ShapeValues[i] = (BitConverter.ToUInt16(d, p + i * 4), BitConverter.ToUInt16(d, p + i * 4 + 2));
        p += m.ShapeValueCount * 4;

        // ── the per-bone bounding boxes, read from the END of the runtime section.
        //
        // Walking forward from the shape values does NOT reach them: something in
        // the v6 runtime section between the submesh bone map and the boxes is
        // ~200 kB larger than the v5 layout accounts for (measured on
        // c0101f0001_fac.mdl: forward walk ends at 0x225F3, the runtime ends at
        // 0x540CC). Rather than invent a section, anchor on the fact the layout DOES
        // fix: the file's four model-wide boxes plus one box per bone are the last
        // thing in the runtime section, 32 bytes each, ending exactly where the LOD 0
        // vertex data begins. Validated in Mdl6Check by requiring every box to be
        // finite, ordered min<=max and inside the model's own extent.
        int boxes = runtimeEnd - (4 + m.BoneCount) * 32;
        m.BoneBoxMin = new float[m.BoneCount][];
        m.BoneBoxMax = new float[m.BoneCount][];
        for (int i = 0; i < m.BoneCount; i++)
        {
            int o = boxes + 4 * 32 + i * 32;
            m.BoneBoxMin[i] = new[] { BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8) };
            m.BoneBoxMax[i] = new[] { BitConverter.ToSingle(d, o + 16), BitConverter.ToSingle(d, o + 20), BitConverter.ToSingle(d, o + 24) };
        }

        // ── LOD 0 index buffer
        int idxCount = (int)(lod0IndexBufferSize / 2);
        m.Indices = new ushort[idxCount];
        for (int i = 0; i < idxCount; i++) m.Indices[i] = BitConverter.ToUInt16(d, (int)lod0IndexDataOffset + i * 2);

        // ── vertex positions for LOD 0's meshes.
        // MeshStruct.VertexBufferOffset is RELATIVE to its LOD's VertexDataOffset,
        // not absolute: measured on c0101f0001_fac.mdl, mesh 0 reads 0 and mesh 1
        // reads 290160 = 5580*(28+24), i.e. exactly the bytes mesh 0 occupies, and
        // LOD 1's first mesh restarts at 0.
        for (int i = m.Lod0MeshIndex; i < m.Lod0MeshIndex + m.Lod0MeshCount && i < m.Meshes.Length; i++)
        {
            var me = m.Meshes[i];
            var decl = i < decls.Count ? decls[i] : null;
            var pos = decl?.FirstOrDefault(e => e.Usage == UsagePosition);
            if (pos == null) continue;
            int stride = me.VertexBufferStride[pos.Stream];
            long baseOff = lodVertexOffset[0] + me.VertexBufferOffset[pos.Stream] + pos.Offset;
            var arr = new float[me.VertexCount, 3];
            for (int v = 0; v < me.VertexCount; v++)
            {
                long o = baseOff + (long)v * stride;
                var xyz = DecodePosition(d, (int)o, pos.Type);
                arr[v, 0] = xyz.X; arr[v, 1] = xyz.Y; arr[v, 2] = xyz.Z;
            }
            me.Positions = arr;
        }

        return m;
    }

    /// <summary>The vertex position formats seen on character models. Anything else
    /// throws rather than silently producing nonsense coordinates.</summary>
    private static (float X, float Y, float Z) DecodePosition(byte[] d, int o, byte type) => type switch
    {
        2 or 3 => (BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8)),
        13 => (Half(d, o), Half(d, o + 2), 0f),
        14 => (Half(d, o), Half(d, o + 2), Half(d, o + 4)),
        _ => throw new InvalidDataException($"unsupported position vertex type {type}"),
    };

    private static float Half(byte[] d, int o) => (float)BitConverter.ToHalf(d, o);

    private string[] ReadNames(byte[] d, int at, int count, int strBase)
    {
        var r = new string[count];
        for (int i = 0; i < count; i++) r[i] = StrAt(BitConverter.ToUInt32(d, at + i * 4));
        return r;
    }

    private string StrAt(uint off)
    {
        if (off >= _strBlock.Length) return string.Empty;
        int end = (int)off;
        while (end < _strBlock.Length && _strBlock[end] != 0) end++;
        return Encoding.UTF8.GetString(_strBlock, (int)off, end - (int)off);
    }

    private bool ShapesLookRight(byte[] d, int at, int strBase, int stringSize)
    {
        if (ShapeCount == 0) return false;
        for (int i = 0; i < ShapeCount; i++)
        {
            int o = at + i * 16;
            if (o + 16 > d.Length) return false;
            uint so = BitConverter.ToUInt32(d, o);
            if (so >= stringSize) return false;
            if (!StrAt(so).StartsWith("shp_", StringComparison.Ordinal)) return false;
            for (int l = 0; l < 3; l++)
            {
                int start = BitConverter.ToUInt16(d, o + 4 + l * 2);
                int cnt = BitConverter.ToUInt16(d, o + 10 + l * 2);
                if (start + cnt > ShapeMeshCount) return false;
            }
        }
        return true;
    }

    /// <summary>The strong half of the search: from a candidate Shapes offset the
    /// remaining runtime tables must consume the runtime section EXACTLY.</summary>
    private long TailEnd(byte[] d, int at)
    {
        long q = at + ShapeCount * 16 + ShapeMeshCount * 12L + ShapeValueCount * 4L;
        if (q + 4 > d.Length) return -1;
        uint boneMapSize = BitConverter.ToUInt32(d, (int)q);
        if (boneMapSize > 0x100000) return -2;
        q += 4 + boneMapSize;
        if (q >= d.Length) return -3;
        q += 1 + d[(int)q];
        return q + 4L * 32 + (long)BoneCount * 32;
    }

    private bool TailFits(byte[] d, int at, int runtimeEnd)
    {
        long q = at + ShapeCount * 16 + ShapeMeshCount * 12L + ShapeValueCount * 4L;
        if (q + 4 > d.Length) return false;
        uint boneMapSize = BitConverter.ToUInt32(d, (int)q);
        if (boneMapSize > 0x100000) return false;
        q += 4 + boneMapSize;
        if (q >= d.Length) return false;
        q += 1 + d[(int)q];
        q += 4L * 32 + (long)BoneCount * 32;
        return q == runtimeEnd;
    }
}
