using System.Numerics;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace Regression.Tests;

public class AoeNativeGroundTests
{
    private static RaycastHit Triangle(Vector3 point, bool reversed = false) => new()
    {
        Point = point,
        V1 = point + new Vector3(-1, 0, -1),
        V2 = point + new Vector3(reversed ? 1 : -1, 0, reversed ? -1 : 1),
        V3 = point + new Vector3(reversed ? -1 : 1, 0, reversed ? 1 : -1),
    };

    [Fact]
    public void MeshFloorWithUnfilledNormalStillSupportsTheCompleteEscape()
    {
        var circle = new DangerZone(DangerShape.Circle, Vector3.Zero, 3, 0, 0, 0);
        Assert.Equal(new Vector3(0, 0, 6), AoeEscapePath.Check(Vector3.Zero, new(0, 0, 6), [circle],
            p => AoeEscapePath.GroundPoint(Triangle(p)), (_, _) => true));
    }

    [Fact]
    public void DownwardTriangleIsRejectedAsAnUnderside()
        => Assert.Null(AoeEscapePath.GroundPoint(Triangle(Vector3.Zero, true)));

    [Fact]
    public void PopulatedNormalIsNormalizedBeforeTheSlopeCheck()
    {
        Assert.NotNull(AoeEscapePath.GroundPoint(new() { Normal = new(0, 0.2f, 0) }));
        Assert.Null(AoeEscapePath.GroundPoint(new() { Normal = new(10, 1, 0) }));
    }

    [Fact]
    public void MissingNormalWithoutANondegenerateTriangleIsRejected()
    {
        Assert.Null(AoeEscapePath.GroundPoint(default));
        var hit = Triangle(Vector3.Zero);
        hit.V3 = hit.V2;
        Assert.Null(AoeEscapePath.GroundPoint(hit));
    }

    [Fact]
    public void VerticalTriangleCannotPretendToBeGround()
    {
        var hit = Triangle(Vector3.Zero);
        hit.V1 = Vector3.Zero; hit.V2 = Vector3.UnitY; hit.V3 = Vector3.UnitZ;
        Assert.Null(AoeEscapePath.GroundPoint(hit));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void CorruptHitDataIsRejected(float bad)
    {
        var hit = Triangle(Vector3.Zero);
        hit.Normal = new(bad, 1, 0);
        Assert.Null(AoeEscapePath.GroundPoint(hit));
        hit.Normal = Vector3.Zero; hit.V2 = new(bad, 0, 0);
        Assert.Null(AoeEscapePath.GroundPoint(hit));
        hit = Triangle(new(bad, 0, 0));
        Assert.Null(AoeEscapePath.GroundPoint(hit));
    }
}
