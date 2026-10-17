using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class LoadedAssemblyFingerprintTests
{
    [Fact]
    public void StreamLoadingPreservesMetadataAndCodeIdentityWithoutAFileLocation()
    {
        var disk = typeof(LoadedAssemblyFingerprintTests).Assembly;
        var memory = Assembly.Load(File.ReadAllBytes(disk.Location));
        Assert.Empty(memory.Location);
        Assert.Equal(LoadedAssemblyFingerprint.Read(disk), LoadedAssemblyFingerprint.Read(memory));
        Assert.Equal(DungeonMeshCompatibility.Result.Unsupported, DungeonMeshCompatibility.Apply(memory));
    }

    [Fact]
    public void ChangedCodeWithUnchangedVersionMvidAndMetadataIsRejectedByTheFingerprint()
    {
        var disk = typeof(LoadedAssemblyFingerprintTests).Assembly;
        var bytes = File.ReadAllBytes(disk.Location);
        using var pe = new PEReader(new MemoryStream(bytes));
        var definition = pe.GetMetadataReader().GetMethodDefinition(
            (MethodDefinitionHandle)MetadataTokens.Handle(typeof(LoadedAssemblyFingerprintTests)
                .GetMethod(nameof(Probe), BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken));
        var rva = definition.RelativeVirtualAddress;
        var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData);
        var header = section.PointerToRawData + rva - section.VirtualAddress;
        Assert.Equal(2, bytes[header] & 3); // tiny body header
        Assert.Equal(0x17, bytes[header + 1]); // ldc.i4.1
        bytes[header + 1] = 0x18; // ldc.i4.2; valid but different code
        var changed = Assembly.Load(bytes);
        Assert.Equal(disk.GetName().Version, changed.GetName().Version);
        Assert.Equal(disk.ManifestModule.ModuleVersionId, changed.ManifestModule.ModuleVersionId);
        var before = LoadedAssemblyFingerprint.Read(disk);
        var after = LoadedAssemblyFingerprint.Read(changed);
        Assert.Equal(before.Metadata, after.Metadata);
        Assert.NotEqual(before.Code, after.Code);
    }

    private static int Probe() => 1;
}
