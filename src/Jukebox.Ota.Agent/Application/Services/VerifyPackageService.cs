using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Manifest;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class VerifyPackageService
{
    private readonly JsonManifestLoader _manifestLoader;
    private readonly IPackageVerifier _verifier;
    private readonly ITelemetryReporter _telemetry;

    public VerifyPackageService(
        JsonManifestLoader manifestLoader,
        IPackageVerifier verifier,
        ITelemetryReporter telemetry)
    {
        _manifestLoader = manifestLoader;
        _verifier = verifier;
        _telemetry = telemetry;
    }

    public async Task<int> RunAsync(
        string manifestPath,
        string packagePath,
        string publicKeyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = _manifestLoader.Load(manifestPath);
            var result = await _verifier.VerifyAsync(manifest, packagePath, publicKeyPath, cancellationToken);

            if (result.Success)
            {
                Console.WriteLine(result.Message);
                FileAgentLogger.LogVerify(result.Message);
            }
            else
            {
                Console.Error.WriteLine(result.Message);
                FileAgentLogger.LogVerify(result.Message);
            }

            _telemetry.ReportVerifyResult(result.Success, result.Message);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            var message = $"verify falhou: {ex.Message}";
            Console.Error.WriteLine(message);
            FileAgentLogger.LogVerify($"falhou: {ex.Message}");
            _telemetry.ReportVerifyResult(false, message);
            return 1;
        }
    }
}
