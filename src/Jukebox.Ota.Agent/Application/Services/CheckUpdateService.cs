using Jukebox.Ota.Agent.Domain.Entities;

using Jukebox.Ota.Agent.Domain.Repositories;

using Jukebox.Ota.Agent.Domain.Services;

using Jukebox.Ota.Agent.Domain.ValueObjects;

using Jukebox.Ota.Agent.Infrastructure.Config;

using Jukebox.Ota.Agent.Infrastructure.ExternalServices;

using Jukebox.Ota.Agent.Infrastructure.Logging;

using Jukebox.Ota.Agent.Infrastructure.Manifest;

using Jukebox.Ota.Agent.Infrastructure.Policy;



namespace Jukebox.Ota.Agent.Application.Services;



public sealed class CheckUpdateService

{

    private readonly JsonConfigLoader _configLoader;

    private readonly OtaConfigVersionSync _versionSync;

    private readonly IOtaUpdateClient _otaClient;

    private readonly ITelemetryReporter _telemetry;

    private readonly IOtaPolicyProvider _policyProvider;

    private readonly IOtaUpdateStatusStore _statusStore;

    private readonly IPackageVerifier _packageVerifier;

    private readonly JsonManifestWriter _manifestWriter;



    public CheckUpdateService(

        JsonConfigLoader configLoader,

        OtaConfigVersionSync versionSync,

        IOtaUpdateClient otaClient,

        ITelemetryReporter telemetry,

        IOtaPolicyProvider policyProvider,

        IOtaUpdateStatusStore statusStore,

        IPackageVerifier packageVerifier,

        JsonManifestWriter manifestWriter)

    {

        _configLoader = configLoader;

        _versionSync = versionSync;

        _otaClient = otaClient;

        _telemetry = telemetry;

        _policyProvider = policyProvider;

        _statusStore = statusStore;

        _packageVerifier = packageVerifier;

        _manifestWriter = manifestWriter;

    }



    public async Task<int> RunAsync(

        string configPath,

        bool force = false,

        CancellationToken cancellationToken = default)

    {

        var outcome = await ExecuteAsync(configPath, force, cancellationToken);

        return outcome.ExitCode;

    }



    public async Task<CheckUpdateOutcome> ExecuteAsync(

        string configPath,

        bool force = false,

        CancellationToken cancellationToken = default)

    {

        OtaAgentConfig? config = null;



        try

        {

            config = _configLoader.Load(configPath);

            config = _versionSync.ResolveAndSync(configPath, config);

            WriteStatus(config, status => status with

            {

                Phase = OtaUpdatePhases.Checking,

                CurrentVersion = config.CurrentVersion,

                ErrorMessage = null,

            });



            if (!force)

            {

                var policy = _policyProvider.GetPolicy(config);



                if (!policy.Enabled)

                {

                    Console.WriteLine("Check ignorado: verificação OTA desabilitada (ota_check_enabled=false).");

                    _telemetry.ReportCheckSkipped(config.DeviceId, "disabled");

                    WriteSkippedStatus(config, "Verificação OTA desabilitada.");

                    return new CheckUpdateOutcome(0, config, null, false, "disabled", null, null, null);

                }



                var nowLocal = TimeOnly.FromDateTime(DateTime.Now);

                if (!OtaCheckSchedule.IsWithinWindow(nowLocal, policy.WindowStart, policy.WindowEnd))

                {

                    Console.WriteLine(

                        $"Check ignorado: fora da janela horária ({policy.WindowStart:HH\\:mm}–{policy.WindowEnd:HH\\:mm}, agora {nowLocal:HH\\:mm}).");

                    _telemetry.ReportCheckSkipped(config.DeviceId, "outside_window");

                    WriteSkippedStatus(config, "Fora da janela horária de verificação OTA.");

                    return new CheckUpdateOutcome(0, config, null, false, "outside_window", null, null, null);

                }



                var lastCheck = _statusStore.GetCheckedAt(config);

                if (lastCheck.HasValue)

                {

                    var elapsed = DateTimeOffset.UtcNow - lastCheck.Value;

                    var interval = TimeSpan.FromMinutes(policy.IntervalMinutes);

                    if (elapsed < interval)

                    {

                        Console.WriteLine(

                            $"Check ignorado: intervalo mínimo não atingido ({policy.IntervalMinutes} min; última verificação há {elapsed.TotalMinutes:F0} min).");

                        _telemetry.ReportCheckSkipped(config.DeviceId, "interval_not_elapsed");

                        WriteSkippedStatus(config, "Intervalo mínimo de verificação OTA não atingido.");

                        return new CheckUpdateOutcome(0, config, null, false, "interval_not_elapsed", null, null, null);

                    }

                }

            }



            FileAgentLogger.LogCheck("Consultando servidor OTA…");

            var manifest = await _otaClient.CheckAsync(config, cancellationToken);

            var checkedAt = DateTimeOffset.UtcNow;



            if (manifest is null)

            {

                Console.WriteLine("Nenhuma atualização disponível.");

                FileAgentLogger.LogCheck("Nenhuma atualização disponível.");

                _telemetry.ReportCheckResult(config.DeviceId, false, null, null);

                WriteStatus(config, status => status with

                {

                    Phase = OtaUpdatePhases.Idle,

                    CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),

                    CurrentVersion = config.CurrentVersion,

                    RemoteVersion = null,

                    UpdateAvailable = false,

                    ErrorMessage = null,

                });

                return new CheckUpdateOutcome(0, config, null, false, null, null, null, null);

            }



            var updateAvailable = !string.Equals(

                manifest.Version,

                config.CurrentVersion,

                StringComparison.Ordinal);



            if (!updateAvailable)

            {

                Console.WriteLine($"Versão remota {manifest.Version} coincide com a atual.");

                FileAgentLogger.LogCheck($"Versão remota {manifest.Version} coincide com a atual.");

                _telemetry.ReportCheckResult(config.DeviceId, false, manifest.Version, null);

                WriteStatus(config, status => status with

                {

                    Phase = OtaUpdatePhases.Idle,

                    CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),

                    CurrentVersion = config.CurrentVersion,

                    RemoteVersion = manifest.Version,

                    UpdateAvailable = false,

                    ErrorMessage = null,

                });

                return new CheckUpdateOutcome(0, config, manifest, false, null, null, null, null);

            }



            Console.WriteLine($"Atualização disponível: {manifest.Version} (atual: {config.CurrentVersion})");

            FileAgentLogger.LogCheck($"Atualização disponível: {manifest.Version} (atual: {config.CurrentVersion})");

            _telemetry.ReportCheckResult(config.DeviceId, true, manifest.Version, null);



            WriteStatus(config, status => status with

            {

                Phase = OtaUpdatePhases.Downloading,

                CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),

                CurrentVersion = config.CurrentVersion,

                RemoteVersion = manifest.Version,

                UpdateAvailable = true,

                ErrorMessage = null,

            });



            var downloadDir = OtaDownloadCache.GetDownloadDirectory(config);

            Directory.CreateDirectory(downloadDir);



            Console.WriteLine($"Baixando pacote {manifest.Version}+{manifest.Arch}...");

            FileAgentLogger.LogCheck($"Baixando pacote {manifest.Version}+{manifest.Arch}...");

            var packagePath = await _otaClient.DownloadPackageAsync(config, manifest, downloadDir, cancellationToken);



            var manifestPath = OtaDownloadCache.GetManifestPath(config, manifest.Version);

            _manifestWriter.Write(manifestPath, manifest);



            var verifyResult = await _packageVerifier.VerifyAsync(

                manifest,

                packagePath,

                config.PublicKeyPath,

                cancellationToken);



            if (!verifyResult.Success)

            {

                Console.Error.WriteLine($"Verificação do pacote falhou: {verifyResult.Message}");

                FileAgentLogger.LogCheck($"Verificação do pacote falhou: {verifyResult.Message}");

                WriteStatus(config, status => status with

                {

                    Phase = OtaUpdatePhases.Error,

                    CurrentVersion = config.CurrentVersion,

                    RemoteVersion = manifest.Version,

                    UpdateAvailable = true,

                    ErrorMessage = verifyResult.Message,

                });

                return new CheckUpdateOutcome(1, config, manifest, true, null, verifyResult.Message, manifestPath, packagePath);

            }



            WriteStatus(config, status => status with

            {

                Phase = OtaUpdatePhases.ReadyToApply,

                CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),

                CurrentVersion = config.CurrentVersion,

                RemoteVersion = manifest.Version,

                UpdateAvailable = true,

                ErrorMessage = null,

            });



            Console.WriteLine($"Pacote {manifest.Version} pronto para aplicar (cache em {downloadDir}).");

            FileAgentLogger.LogCheck($"Pacote {manifest.Version} pronto para aplicar.");



            return new CheckUpdateOutcome(2, config, manifest, true, null, null, manifestPath, packagePath);

        }

        catch (Exception ex)

        {

            var deviceId = config?.DeviceId ?? "desconhecido";

            Console.Error.WriteLine($"check falhou: {ex.Message}");

            var checkEndpoint = config is not null

                ? HttpOtaUpdateClient.DescribeCheckEndpoint(config)

                : null;

            FileAgentLogger.LogCheckUrlFailure(

                deviceId,

                config?.OtaBaseUrl,

                checkEndpoint,

                ex);

            _telemetry.ReportCheckResult(deviceId, false, null, ex.Message);



            if (config is not null)

            {

                WriteStatus(config, status => status with

                {

                    Phase = OtaUpdatePhases.Error,

                    CurrentVersion = config.CurrentVersion,

                    ErrorMessage = ex.Message,

                });

            }



            return new CheckUpdateOutcome(1, config, null, false, null, ex.Message, null, null);

        }

    }



    private void WriteSkippedStatus(OtaAgentConfig config, string _)

    {

        var current = _statusStore.Read(config);

        WriteStatus(config, current with

        {

            Phase = OtaUpdatePhases.Idle,

            CurrentVersion = config.CurrentVersion,

            ErrorMessage = null,

        });

    }



    private void WriteStatus(OtaAgentConfig config, Func<OtaUpdateStatus, OtaUpdateStatus> mutator)

    {

        var current = _statusStore.Read(config);

        _statusStore.Write(config, mutator(current));

    }



    private void WriteStatus(OtaAgentConfig config, OtaUpdateStatus status) =>

        _statusStore.Write(config, status);

}


