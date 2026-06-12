using Jukebox.Ota.Agent.Application.Services;

namespace Jukebox.Ota.Agent.Interfaces.Cli;

public sealed class AgentCli
{
    private readonly VersionService _versionService;
    private readonly CheckUpdateService _checkService;
    private readonly VerifyPackageService _verifyService;

    public AgentCli(
        VersionService versionService,
        CheckUpdateService checkService,
        VerifyPackageService verifyService)
    {
        _versionService = versionService;
        _checkService = checkService;
        _verifyService = verifyService;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        return args[0] switch
        {
            "version" => RunVersion(),
            "check" => await RunCheckAsync(args, cancellationToken),
            "verify" => await RunVerifyAsync(args, cancellationToken),
            "-h" or "--help" or "help" => RunHelp(),
            _ => UnknownCommand(args[0]),
        };
    }

    private int RunVersion()
    {
        Console.WriteLine(_versionService.GetVersion());
        return 0;
    }

    private async Task<int> RunCheckAsync(string[] args, CancellationToken cancellationToken)
    {
        var configPath = GetOptionValue(args, "--config")
            ?? throw new ArgumentException("check requer --config <caminho>.");

        return await _checkService.RunAsync(configPath, cancellationToken);
    }

    private async Task<int> RunVerifyAsync(string[] args, CancellationToken cancellationToken)
    {
        var manifestPath = GetOptionValue(args, "--manifest")
            ?? throw new ArgumentException("verify requer --manifest <caminho>.");
        var packagePath = GetOptionValue(args, "--package")
            ?? throw new ArgumentException("verify requer --package <caminho>.");
        var publicKeyPath = GetOptionValue(args, "--public-key") ?? string.Empty;

        return await _verifyService.RunAsync(manifestPath, packagePath, publicKeyPath, cancellationToken);
    }

    private static int RunHelp()
    {
        PrintUsage();
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Comando desconhecido: {command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            jukebox-ota-agent — agente OTA Jukebox (POC .NET)

            Uso:
              jukebox-ota-agent version
              jukebox-ota-agent check --config <arquivo.json>
              jukebox-ota-agent verify --manifest <manifest.json> --package <arquivo> [--public-key <chave.pem>]
            """);
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
