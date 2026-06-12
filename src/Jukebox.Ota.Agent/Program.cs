using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Security;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;
using Jukebox.Ota.Agent.Interfaces.Cli;

var telemetry = new ConsoleTelemetryReporter();
var configLoader = new JsonConfigLoader();
var manifestLoader = new JsonManifestLoader();
using var otaClient = new HttpOtaUpdateClient();

var versionService = new VersionService();
var checkService = new CheckUpdateService(configLoader, otaClient, telemetry);
var verifyService = new VerifyPackageService(manifestLoader, new RsaPssPackageVerifier(), telemetry);
var cli = new AgentCli(versionService, checkService, verifyService);

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
