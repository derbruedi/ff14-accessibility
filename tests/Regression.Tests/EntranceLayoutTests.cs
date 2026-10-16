using System.Buffers.Binary;
using System.Numerics;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class EntranceLayoutTests
{
    [Theory]
    [InlineData(8, 1000102)]
    [InlineData(45, 2000087)]
    public void MissingLevelObjectUsesItsExactSourceLayout(byte type, uint baseId)
    {
        var bytes = Layout(type, baseId);
        var item = Assert.Single(EntranceLayoutReader.Read(bytes));
        Assert.Equal(2210466u, item.InstanceId);
        Assert.Equal(type, item.Type);
        Assert.Equal(baseId, item.BaseId);
        Assert.Equal(new Vector3(1, 2, 3), item.Position);
    }

    [Fact]
    public void EnemyAndSceneryNeverBecomeInteractionEntrances()
        => Assert.Empty(EntranceLayoutReader.Read(Layout(9, 1000102)));

    [Fact]
    public void NonFiniteSourcePositionIsRejected()
    {
        var bytes = Layout(45, 2000087);
        Write(bytes, 72, BitConverter.SingleToInt32Bits(float.NaN));
        Assert.Empty(EntranceLayoutReader.Read(bytes));
    }

    [Theory]
    [InlineData(28, int.MaxValue)]
    [InlineData(32, int.MaxValue)]
    [InlineData(32, -1)]
    [InlineData(36, -100)]
    [InlineData(48, int.MaxValue)]
    [InlineData(52, int.MaxValue)]
    [InlineData(56, int.MaxValue)]
    public void InvalidOffsetOrCountIsBoundedAndRejected(int offset, int value)
    {
        var bytes = Layout(45, 2000087);
        Write(bytes, offset, value);
        Assert.Empty(EntranceLayoutReader.Read(bytes));
    }

    [Fact]
    public void TruncatedPayloadAndWrongFormatAreRejected()
    {
        var bytes = Layout(45, 2000087);
        Assert.Empty(EntranceLayoutReader.Read(bytes[..110]));
        bytes[0] = 0;
        Assert.Empty(EntranceLayoutReader.Read(bytes));
    }

    [Fact]
    public void DamagedLaterObjectDoesNotReturnAnEarlierPartialEntrance()
    {
        var bytes = Layout(45, 2000087);
        Write(bytes, 52, 2);
        Write(bytes, 60, int.MaxValue);
        Write(bytes, 56, 8);
        Write(bytes, 64, 45);
        Assert.Empty(EntranceLayoutReader.Read(bytes));
    }

    private static byte[] Layout(byte type, uint baseId)
    {
        var bytes = new byte[128];
        "LGB1"u8.CopyTo(bytes);
        "LGP1"u8.CopyTo(bytes.AsSpan(12));
        Write(bytes, 28, 16); // layer offset table at 20+16
        Write(bytes, 32, 1);
        Write(bytes, 36, 4); // layer at 40
        Write(bytes, 48, 16); // object offset table at 56
        Write(bytes, 52, 1);
        Write(bytes, 56, 4); // object at 60
        Write(bytes, 60, type);
        Write(bytes, 64, 2210466);
        Write(bytes, 72, BitConverter.SingleToInt32Bits(1));
        Write(bytes, 76, BitConverter.SingleToInt32Bits(2));
        Write(bytes, 80, BitConverter.SingleToInt32Bits(3));
        Write(bytes, 108, (int)baseId);
        return bytes;
    }

    private static void Write(byte[] bytes, int offset, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
}
