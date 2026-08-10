using System.Text;

namespace cmdump;

/// <summary>
/// [LOCAL 2026-08-09] Turns one face model's shape keys into numbers.
///
/// A type-0 appearance menu (Jaw, Eye Shape, Eyebrows, Nose, Mouth, Fang Length,
/// Elezen/Lalafell Ear Shape) is a morph target on the face model - entry 1 is the
/// untouched mesh and entries 2..N are shapes a..N-1 (docs/game-api.md). A shape is
/// stored as a list of (BaseIndicesIndex, ReplacingVertexIndex) pairs: the vertex the
/// index buffer normally points at is swapped for another vertex in the same mesh.
/// The difference between those two positions IS the per-vertex displacement, and
/// that is the only thing measured here. Nothing is inferred about what the shape
/// "looks like"; that is left to the word generator, which sees only these numbers.
///
/// AXES, established from the model itself and not assumed:
///   - the face mesh of c0101f0001 spans x[-0.083, 0.083], y[1.526, 1.749],
///     z[-0.075, 0.127]. The mesh is symmetric about x = 0, so X is lateral and the
///     model origin is the character's centre line;
///   - Y runs 1.53-1.75, i.e. a head-height above the origin, so +Y is UP;
///   - the iris mesh (material iri_a - the eyes) sits at z = +0.082..0.096 while the
///     face mesh reaches z = -0.075, so the eyes are at positive Z: +Z is FORWARD.
/// Units are the game's metres: that head is 22 cm tall and 17 cm wide.
/// </summary>
public static class ShapeMeasure
{
    public sealed class Result
    {
        public string Shape = string.Empty;
        /// <summary>Vertices whose position actually moves.</summary>
        public int Moved;
        /// <summary>Shape values examined, including ones that move nothing.</summary>
        public int Values;
        /// <summary>Shape values whose indices did not resolve - must stay 0.</summary>
        public int Bad;
        public float MeanMag, MaxMag;
        /// <summary>Centroid of the ORIGINAL positions of the moved vertices.</summary>
        public float Cx, Cy, Cz;
        /// <summary>Mean displacement: outward (away from the centre line), up, forward.
        /// Magnitude-weighted so a large local move is not diluted by the fringe.</summary>
        public float DOut, DUp, DFwd;
        /// <summary>Change in the moved set's extent along each axis: width (X, measured
        /// as the full span so a symmetric pair counts once), height (Y), depth (Z).</summary>
        public float DExtX, DExtY, DExtZ;
        /// <summary>Change in the moved set's highest and lowest point.</summary>
        public float DTop, DBottom;
    }

    public static List<Result> Measure(MdlV6 m)
    {
        var results = new List<Result>();
        foreach (var sh in m.Shapes)
        {
            var r = new Result { Shape = sh.Name };
            double sumMag = 0, wOut = 0, wUp = 0, wFwd = 0, wsum = 0;
            double cx = 0, cy = 0, cz = 0;
            float ax0 = float.MaxValue, ax1 = float.MinValue, ay0 = float.MaxValue, ay1 = float.MinValue, az0 = float.MaxValue, az1 = float.MinValue;
            float bx0 = float.MaxValue, bx1 = float.MinValue, by0 = float.MaxValue, by1 = float.MinValue, bz0 = float.MaxValue, bz1 = float.MinValue;

            for (int smi = sh.MeshStart[0]; smi < sh.MeshStart[0] + sh.MeshCount[0] && smi < m.ShapeMeshes.Length; smi++)
            {
                var sm = m.ShapeMeshes[smi];
                // The shape mesh names its mesh by that mesh's StartIndex.
                var mesh = m.Meshes.FirstOrDefault(x => x.StartIndex == sm.MeshIndexOffset && x.Positions.Length > 0);
                if (mesh == null) continue;

                for (uint v = sm.ValueOffset; v < sm.ValueOffset + sm.ValueCount && v < m.ShapeValues.Length; v++)
                {
                    r.Values++;
                    var (bi, rep) = m.ShapeValues[(int)v];
                    // BaseIndicesIndex is relative to the MESH's own slice of the LOD
                    // index buffer, not absolute. Measured on c0101f0001_fac: the shape
                    // meshes of shp_brw_a that belong to mesh 2 (StartIndex 10360,
                    // IndexCount 1182) carry base indices 384..485, i.e. inside [0,1182)
                    // rather than near 10360. Reading them as absolute silently resolves
                    // to another mesh's vertices and produces 12 cm "eyebrow" moves.
                    long abs = mesh.StartIndex + bi;
                    if (bi >= mesh.IndexCount || abs >= m.Indices.Length || rep >= mesh.VertexCount) { r.Bad++; continue; }
                    int orig = m.Indices[(int)abs];
                    if (orig >= mesh.VertexCount) { r.Bad++; continue; }

                    float x0 = mesh.Positions[orig, 0], y0 = mesh.Positions[orig, 1], z0 = mesh.Positions[orig, 2];
                    float x1 = mesh.Positions[rep, 0], y1 = mesh.Positions[rep, 1], z1 = mesh.Positions[rep, 2];
                    float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
                    float mag = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (mag < 1e-5f) continue;

                    r.Moved++;
                    sumMag += mag;
                    if (mag > r.MaxMag) r.MaxMag = mag;
                    cx += x0; cy += y0; cz += z0;

                    // Outward is away from the centre line, so a symmetric pair of
                    // vertices moving apart both count as +.
                    double outward = x0 >= 0 ? dx : -dx;
                    wOut += outward * mag; wUp += dy * mag; wFwd += dz * mag; wsum += mag;

                    bx0 = MathF.Min(bx0, x0); bx1 = MathF.Max(bx1, x0);
                    by0 = MathF.Min(by0, y0); by1 = MathF.Max(by1, y0);
                    bz0 = MathF.Min(bz0, z0); bz1 = MathF.Max(bz1, z0);
                    ax0 = MathF.Min(ax0, x1); ax1 = MathF.Max(ax1, x1);
                    ay0 = MathF.Min(ay0, y1); ay1 = MathF.Max(ay1, y1);
                    az0 = MathF.Min(az0, z1); az1 = MathF.Max(az1, z1);
                }
            }

            if (r.Moved > 0)
            {
                r.MeanMag = (float)(sumMag / r.Moved);
                r.Cx = (float)(cx / r.Moved); r.Cy = (float)(cy / r.Moved); r.Cz = (float)(cz / r.Moved);
                if (wsum > 0) { r.DOut = (float)(wOut / wsum); r.DUp = (float)(wUp / wsum); r.DFwd = (float)(wFwd / wsum); }
                r.DExtX = (ax1 - ax0) - (bx1 - bx0);
                r.DExtY = (ay1 - ay0) - (by1 - by0);
                r.DExtZ = (az1 - az0) - (bz1 - bz0);
                r.DTop = ay1 - by1;
                r.DBottom = ay0 - by0;
            }
            results.Add(r);
        }
        return results;
    }

    public static string Header =>
        "code\tface\tshape\tmoved\tvalues\tbad\tmeanMag\tmaxMag\tcx\tcy\tcz\tdOut\tdUp\tdFwd\tdExtX\tdExtY\tdExtZ\tdTop\tdBottom";

    public static string Row(string code, int face, Result r)
    {
        var sb = new StringBuilder();
        sb.Append(code).Append('\t').Append(face).Append('\t').Append(r.Shape).Append('\t')
          .Append(r.Moved).Append('\t').Append(r.Values).Append('\t').Append(r.Bad).Append('\t');
        foreach (var f in new[] { r.MeanMag, r.MaxMag, r.Cx, r.Cy, r.Cz, r.DOut, r.DUp, r.DFwd, r.DExtX, r.DExtY, r.DExtZ, r.DTop, r.DBottom })
            sb.Append(f.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)).Append('\t');
        sb.Length--;
        return sb.ToString();
    }
}
