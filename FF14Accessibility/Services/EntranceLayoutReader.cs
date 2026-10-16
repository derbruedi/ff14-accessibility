using System.Buffers.Binary;
using System.Numerics;

namespace FF14Accessibility.Services;

internal readonly record struct EntranceLayoutObject(uint InstanceId, byte Type, uint BaseId, Vector3 Position);

/// <summary>
/// Reads only interaction source objects, with bounded offsets/counts.
/// Format: Lumina LayerCommon.Layer/InstanceObject/GameInstanceObject.
/// Skips unrelated payloads: parsing their arrays can fail on empty ARR layers.
/// </summary>
internal static class EntranceLayoutReader
{
    internal static List<EntranceLayoutObject> Read(byte[] bytes)
    {
        var result = new List<EntranceLayoutObject>();
        try
        {
            if (bytes.Length < 36 || !bytes.AsSpan(0, 4).SequenceEqual("LGB1"u8)
                || !bytes.AsSpan(12, 4).SequenceEqual("LGP1"u8)) return result;
            var table = checked(20 + I(28));
            var layers = I(32);
            Bounds(table, layers, 4);
            for (var n = 0; n < layers; n++)
            {
                var layer = checked(table + I(table + n * 4));
                Bounds(layer, 1, 16);
                var objects = checked(layer + I(layer + 8));
                var count = I(layer + 12);
                Bounds(objects, count, 4);
                for (var k = 0; k < count; k++)
                {
                    var obj = checked(objects + I(objects + k * 4));
                    Bounds(obj, 1, 48);
                    var type = I(obj);
                    if (type is not (8 or 45)) continue;
                    Bounds(obj, 1, 52);
                    var pos = new Vector3(F(obj + 12), F(obj + 16), F(obj + 20));
                    if (!float.IsFinite(pos.X) || !float.IsFinite(pos.Y) || !float.IsFinite(pos.Z)) continue;
                    result.Add(new(U(obj + 4), (byte)type, U(obj + 48), pos));
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException or InvalidDataException)
        {
            // Reject the entire damaged file, including any earlier partial rows.
            result.Clear();
        }
        return result;

        int I(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        float F(int offset) => BitConverter.Int32BitsToSingle(I(offset));
        void Bounds(int offset, int count, int size)
        {
            if (offset < 0 || count < 0 || count > 100_000 || (long)offset + (long)count * size > bytes.Length)
                throw new InvalidDataException("Invalid entrance layout offset/count.");
        }
    }
}
