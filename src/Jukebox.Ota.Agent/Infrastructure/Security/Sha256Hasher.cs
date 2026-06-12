using System.Security.Cryptography;

namespace Jukebox.Ota.Agent.Infrastructure.Security;

public static class Sha256Hasher
{
    public static async Task<string> ComputeHexAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
