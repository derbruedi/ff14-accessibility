using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FF14Accessibility.Services;

/// <summary>
/// vnavmesh 1.2.3.14 predates its author's addition of territory 1330 to the
/// existing Dzemael customization (commit 6fc80725). Reuse that same instance
/// for the reworked territory. No movement, coordinates or collider rules are
/// supplied here, and no dependency files are changed.
/// </summary>
internal static class DungeonMeshCompatibility
{
    internal const string VerifiedSha256 = "889EBFF40D92DB2DF4B4C190F1D81B3971E538908E459F6297F53FEA017E3723";
    // Derived from that exact DLL, including all method bodies. Works both for
    // file loading and Dalamud's LoadFromStream, without guessing a disk path.
    internal static readonly LoadedAssemblyFingerprint VerifiedLoadedFingerprint = new(
        "9FCCEFD516044AC2ABD32ED6006598B2528619E0B5B27B9FC2F7836640E1BD04",
        "B5742B711242160827012187F0716F406012CACD5B7DA89AF34C19EA7ACDBE3D");
    private sealed record Verification(bool Matches);
    private static readonly ConditionalWeakTable<Assembly, Verification> VerifiedAssemblies = new();
    internal enum Result { NotLoaded, AlreadySupported, Applied, Unsupported }

    internal static Result Apply(Assembly assembly)
    {
        var customization = assembly.GetType("Navmesh.Customizations.Z0171DzemaelDarkhold");
        if (customization?.GetCustomAttributesData().Any(a =>
                a.AttributeType.FullName == "Navmesh.CustomizationTerritoryAttribute"
                && a.ConstructorArguments.Count == 1 && Equals(a.ConstructorArguments[0].Value, 1330u)) == true)
            return Result.AlreadySupported;
        if (assembly.GetName().Name != "vnavmesh" || assembly.GetName().Version != new Version(1, 2, 3, 14)
            || !VerifiedAssemblies.GetValue(assembly,
                a => new(LoadedAssemblyFingerprint.Read(a) == VerifiedLoadedFingerprint)).Matches)
            return Result.Unsupported;
        var registryType = assembly.GetType("Navmesh.NavmeshCustomizationRegistry");
        var field = registryType?.GetField("PerTerritory", BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is not IDictionary registry) return Result.Unsupported;
        if (registry.Contains(1330u)) return Result.AlreadySupported;
        // A changed binary must supply its own support or be inspected again.
        var original = registry[171u];
        if (original?.GetType().FullName != "Navmesh.Customizations.Z0171DzemaelDarkhold")
            return Result.Unsupported;
        return Register(registry, original);
    }

    internal static Result Register(IDictionary registry, object original)
    {
        if (registry.Contains(1330u)) return Result.AlreadySupported;
        if (!registry.Contains(171u) || !ReferenceEquals(registry[171u], original)) return Result.Unsupported;
        registry.Add(1330u, original);
        return Result.Applied;
    }
}
