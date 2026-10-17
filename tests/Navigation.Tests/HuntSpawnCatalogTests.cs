using System.Numerics;
using FF14Accessibility.Services;

namespace Navigation.Tests;

public class HuntSpawnCatalogTests
{
    private const string Sample = """
        BNpcBaseId,BNpcNameId,TerritoryTypeId,Position,Subtype
        101,317,141,17.5;24.1;132.29753,0
        101,317,141,17.5;24.1;132.29753,0
        999,317,141,17.5;24.1;132,7
        737,250,145,19;27.4;299.66724,0
        737,250,145,21.48;21.48;-1,0
        1711,1453,317,10;10;10,0
        1,6373,700,11.24;11.24;-2,0
        """;

    [Fact]
    public void ParseKeepsKnownCampsAndDropsPlaceholders()
    {
        var catalog = HuntSpawnCatalog.Parse(Sample);

        var spriggan = catalog.Find(317, 141);
        Assert.Single(spriggan);
        Assert.Equal(17.5f, spriggan[0].MapX);
        Assert.Equal(24.1f, spriggan[0].MapY, 3);

        var hunter = catalog.Find(250, 145);
        Assert.Single(hunter);
        Assert.Equal(19f, hunter[0].MapX);
        Assert.Equal(27.4f, hunter[0].MapY, 3);

        Assert.Single(catalog.Find(1453, 317));
        Assert.Empty(catalog.Find(317, 317));
        Assert.Single(catalog.Find(6373, 700));
        Assert.Equal(4, catalog.PointCount);
    }

    [Fact]
    public void CentralThanalanCoordMatchesTheSilDihCamp()
    {
        var world = PlacesService.ConvertMapCoord(17.5f, 24.1f, 100, 0, 0);
        Assert.NotNull(world);
        Assert.Equal(-199f, world!.Value.X, 2);
        Assert.Equal(0f, world.Value.Y);
        Assert.Equal(131f, world.Value.Z, 2);
        Assert.Null(PlacesService.ConvertMapCoord(17.5f, 24.1f, 0, 0, 0));
    }

    [Fact]
    public void SpawnPointsReplaceAreaCentresAndEmptyListKeepsThem()
    {
        var catalog = HuntSpawnCatalog.Parse(Sample);
        var called = false;
        var selected = HuntSpawnCatalog.TrySelectParts(
            catalog.Find(317, 141),
            (x, y) => PlacesService.ConvertMapCoord(x, y, 100, 0, 0),
            Vector3.Zero,
            () =>
            {
                called = true;
                return new List<AreaPart> { new(new Vector3(1, 2, 3), "Kohlenstaub", 0) };
            },
            out var parts,
            out var fromSpawns);

        Assert.True(selected);
        Assert.True(fromSpawns);
        Assert.False(called);
        Assert.Equal(-199f, parts[0].Centre.X, 2);
        Assert.Equal(131f, parts[0].Centre.Z, 2);
        Assert.Equal(string.Empty, parts[0].SpotName);

        called = false;
        selected = HuntSpawnCatalog.TrySelectParts(
            [],
            (_, _) => new Vector3(9, 0, 9),
            Vector3.Zero,
            () =>
            {
                called = true;
                return new List<AreaPart> { new(new Vector3(4, 0, 5), "Sandtor", 0) };
            },
            out parts,
            out fromSpawns);

        Assert.True(selected);
        Assert.False(fromSpawns);
        Assert.True(called);
        Assert.Equal("Sandtor", parts[0].SpotName);
    }

    [Fact]
    public void NearestSpawnIsFirst()
    {
        var points = new List<HuntSpawnPoint>
        {
            new(0f, 0f, 0f),
            new(100f, 0f, 0f),
        };
        var parts = HuntSpawnCatalog.ToAreaParts(
            points, (x, y) => new Vector3(x, 0f, y), new Vector3(90f, 40f, 0f));
        Assert.Equal(100f, parts[0].Centre.X);
        Assert.Equal(0f, parts[1].Centre.X);
    }

    [Fact]
    public void LoadReadsAFileAndRejectsAnEmptyOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ff14-hunt-spawns-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, HuntSpawnCatalog.FileName);
            File.WriteAllText(path, Sample);
            var catalog = new HuntSpawnCatalog();
            Assert.True(catalog.TryLoad(path, out var count, out var error));
            Assert.Null(error);
            Assert.Equal(4, count);
            Assert.Single(catalog.Find(250, 145));

            File.WriteAllText(path, "BNpcBaseId,BNpcNameId,TerritoryTypeId,Position,Subtype\n");
            Assert.False(catalog.TryLoad(path, out _, out error));
            Assert.Equal("keine Standorte", error);
            Assert.Empty(catalog.Find(250, 145));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
