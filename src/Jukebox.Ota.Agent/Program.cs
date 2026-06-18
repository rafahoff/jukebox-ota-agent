using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Infrastructure.Backup;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Health;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Release;
using Jukebox.Ota.Agent.Infrastructure.Security;
using Jukebox.Ota.Agent.Infrastructure.System;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;
using Jukebox.Ota.Agent.Interfaces.Cli;

var telemetry = new ConsoleTelemetryReporter();
var configLoader = new JsonConfigLoader();
var manifestLoader = new JsonManifestLoader();
var manifestWriter = new JsonManifestWriter();
using var otaClient = new HttpOtaUpdateClient();
using var ackClient = new HttpOtaAckClient();

var systemService = new SystemctlService();
using var healthChecker = new HttpHealthChecker(systemService);
var releaseManager = new FileSystemReleaseManager();
var backupService = new FileSystemBackupService();
var packageVerifier = new RsaPssPackageVerifier();
var manifestSigner = new RsaPssManifestSigner();

var versionService = new VersionService();
var checkService = new CheckUpdateService(configLoader, otaClient, telemetry);
var verifyService = new VerifyPackageService(manifestLoader, packageVerifier, telemetry);
var signManifestService = new SignManifestService(manifestLoader, manifestWriter, manifestSigner);
var applyService = new ApplyUpdateService(
    configLoader,
    manifestLoader,
    packageVerifier,
    systemService,
    releaseManager,
    backupService,
    healthChecker,
    ackClient);

var cli = new AgentCli(
    versionService,
    checkService,
    verifyService,
    signManifestService,
    applyService);

try
{
    var exitCode = await cli.RunAsync(args);
    Environment.Exit(exitCode);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}
