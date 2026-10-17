using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

namespace FF14Accessibility.Services;

// Dalamud loads plugins with LoadFromStream: Assembly.Location is empty.
// Hash the loaded metadata AND every managed method body, including exception
// regions and local-variable settings. A matching version/MVID alone is not proof.
internal readonly record struct LoadedAssemblyFingerprint(string Metadata, string Code)
{
    internal static unsafe LoadedAssemblyFingerprint Read(Assembly assembly)
    {
        if (!assembly.TryGetRawMetadata(out var metadata, out var length))
            throw new NotSupportedException("Loaded assembly metadata is unavailable.");
        var metadataHash = Convert.ToHexString(SHA256.HashData(new ReadOnlySpan<byte>(metadata, length)));
        var reader = new MetadataReader(metadata, length);
        using var code = new MemoryStream();
        using var writer = new BinaryWriter(code);
        foreach (var handle in reader.MethodDefinitions)
        {
            var token = MetadataTokens.GetToken(handle);
            writer.Write(token);
            var body = assembly.ManifestModule.ResolveMethod(token)?.GetMethodBody();
            writer.Write(body != null);
            if (body == null) continue;
            writer.Write(body.MaxStackSize);
            writer.Write(body.InitLocals);
            writer.Write(body.LocalSignatureMetadataToken);
            var il = body.GetILAsByteArray() ?? [];
            writer.Write(il.Length);
            writer.Write(il);
            writer.Write(body.ExceptionHandlingClauses.Count);
            foreach (var clause in body.ExceptionHandlingClauses)
            {
                writer.Write((int)clause.Flags);
                writer.Write(clause.TryOffset);
                writer.Write(clause.TryLength);
                writer.Write(clause.HandlerOffset);
                writer.Write(clause.HandlerLength);
                writer.Write(clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : 0);
                writer.Write(clause.Flags == ExceptionHandlingClauseOptions.Clause ? clause.CatchType?.FullName ?? "" : "");
            }
        }
        writer.Flush();
        return new(metadataHash, Convert.ToHexString(SHA256.HashData(code.GetBuffer().AsSpan(0, (int)code.Length))));
    }
}
