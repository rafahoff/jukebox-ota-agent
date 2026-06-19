using System.Text;
using Jukebox.Ota.Agent.Infrastructure.Logging;

namespace Jukebox.Ota.Agent.Tests;

[CollectionDefinition("FileAgentLogger", DisableParallelization = true)]
public sealed class FileAgentLoggerCollection;

[Collection("FileAgentLogger")]
public class FileAgentLoggerTests : IDisposable
{
    private readonly string _tempDir;

    public FileAgentLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ota-logger-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        FileAgentLogger.TestLogsDirectoryOverride = _tempDir;
    }

    public void Dispose()
    {
        FileAgentLogger.TestLogsDirectoryOverride = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveLogsDirectory_ComOverride_RetornaDiretorioDeTeste()
    {
        Assert.Equal(_tempDir, FileAgentLogger.ResolveLogsDirectory());
    }

    [Fact]
    public void ResolveLogFilePath_ComOverride_RetornaCaminhoDoArquivoPrincipal()
    {
        var expected = Path.Combine(_tempDir, "jukebox_ota_agent.log");
        Assert.Equal(expected, FileAgentLogger.ResolveLogFilePath());
    }

    [Fact]
    public void LogCheck_AppendaLinhaComTimestampEFormato()
    {
        FileAgentLogger.LogCheck("mensagem de teste");

        var logPath = FileAgentLogger.ResolveLogFilePath()!;
        Assert.True(File.Exists(logPath));

        var content = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\] check: mensagem de teste\r?\n$", content);
    }

    [Fact]
    public void LogCheckUrlFailure_GravaUrlEndpointEDetalheTecnico()
    {
        var ex = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");

        FileAgentLogger.LogCheckUrlFailure(
            "machine-pi-100",
            "http://192.168.1.50:8080",
            "http://192.168.1.50:8080/v1/updates/check?device_id=machine-pi-100",
            ex);

        var content = File.ReadAllText(FileAgentLogger.ResolveLogFilePath()!, Encoding.UTF8);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Contains("falhou (device_id=machine-pi-100, url=http://192.168.1.50:8080)", lines[0]);
        Assert.Contains("timeout", lines[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("endpoint=http://192.168.1.50:8080/v1/updates/check", lines[1]);
        Assert.Contains("detalhe_tecnico: The request was canceled", lines[2]);
    }

    [Fact]
    public void LogBootELogExit_AppendamMultiplasLinhas()
    {
        FileAgentLogger.LogBoot("check", 4242);
        FileAgentLogger.LogExit("check", 0, 1500);

        var content = File.ReadAllText(FileAgentLogger.ResolveLogFilePath()!, Encoding.UTF8);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("boot command=check pid=4242", lines[0]);
        Assert.Contains("exit command=check code=0 duration_ms=1500", lines[1]);
    }

    [Fact]
    public void Write_AoAtingirLimite_RotacionaArquivos()
    {
        var logsDir = FileAgentLogger.ResolveLogsDirectory()!;
        var logPath = Path.Combine(logsDir, "jukebox_ota_agent.log");

        // Acima do limite de 5 MB para garantir rotação.
        var payload = new string('x', (5 * 1024 * 1024) + 512);
        File.WriteAllText(logPath, payload, Encoding.UTF8);

        FileAgentLogger.LogCheck("dispara rotação");

        var rotatedPath = Path.Combine(logsDir, "jukebox_ota_agent.1.log");
        Assert.True(File.Exists(rotatedPath));
        Assert.Equal(payload, File.ReadAllText(rotatedPath, Encoding.UTF8));

        Assert.True(File.Exists(logPath));
        var current = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.Contains("check: dispara rotação", current);
        Assert.True(current.Length < 200);
    }

    [Fact]
    public void Write_ComRotacoesExistentes_PreservaCadeiaAteCincoArquivos()
    {
        var logsDir = FileAgentLogger.ResolveLogsDirectory()!;
        var logPath = Path.Combine(logsDir, "jukebox_ota_agent.log");

        File.WriteAllText(Path.Combine(logsDir, "jukebox_ota_agent.1.log"), "rot-1", Encoding.UTF8);
        File.WriteAllText(Path.Combine(logsDir, "jukebox_ota_agent.2.log"), "rot-2", Encoding.UTF8);
        File.WriteAllText(logPath, new string('y', 5 * 1024 * 1024), Encoding.UTF8);

        FileAgentLogger.LogCheck("nova rotação");

        Assert.True(File.Exists(Path.Combine(logsDir, "jukebox_ota_agent.1.log")));
        Assert.True(File.Exists(Path.Combine(logsDir, "jukebox_ota_agent.2.log")));
        Assert.Equal("rot-1", File.ReadAllText(Path.Combine(logsDir, "jukebox_ota_agent.2.log"), Encoding.UTF8));
        Assert.Contains("check: nova rotação", File.ReadAllText(logPath, Encoding.UTF8));
    }
}
