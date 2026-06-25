using System.Text;

namespace Jukebox.Ota.Agent.Infrastructure.Logging;

/// <summary>
/// Logger em arquivo compartilhado com o kiosk (rotação 5 MB × 5 arquivos).
/// Apenas Linux e Windows; outras plataformas ignoram gravação silenciosamente.
/// </summary>
public static class FileAgentLogger
{
    private const string LogBaseName = "jukebox_ota_agent";
    private const int MaxLogSizeBytes = 5 * 1024 * 1024;
    private const int MaxRotatedFiles = 5;
    private static readonly object WriteLock = new();

    /// <summary>Substitui o diretório de logs (apenas testes).</summary>
    internal static string? TestLogsDirectoryOverride { get; set; }

    public static void LogBoot(string command, int pid) =>
        WriteLine($"boot command={command} pid={pid}");

    public static void LogExit(string command, int exitCode, long durationMs) =>
        WriteLine($"exit command={command} code={exitCode} duration_ms={durationMs}");

    public static void LogCheck(string message) =>
        WriteLine($"check: {message}");

    /// <summary>Falha de check com URL e detalhe técnico (espelha journald no arquivo do kiosk).</summary>
    public static void LogCheckUrlFailure(
        string deviceId,
        string? otaBaseUrl,
        string? checkEndpoint,
        Exception ex)
    {
        var baseUrl = string.IsNullOrWhiteSpace(otaBaseUrl) ? "desconhecida" : otaBaseUrl.Trim();
        var endpoint = string.IsNullOrWhiteSpace(checkEndpoint) ? baseUrl : checkEndpoint.Trim();
        LogCheck(
            $"falhou (device_id={deviceId}, url={baseUrl}): {OtaCheckErrorFormatter.Format(ex)}");
        LogCheck($"endpoint={endpoint}");
        LogCheck($"detalhe_tecnico: {ex.Message}");
    }

    public static void LogApply(string message) =>
        WriteLine($"apply: {message}");

    public static void LogVerify(string message) =>
        WriteLine($"verify: {message}");

    public static void LogSignManifest(string message) =>
        WriteLine($"sign-manifest: {message}");

    internal static string? ResolveLogsDirectory()
    {
        if (TestLogsDirectoryOverride is not null)
        {
            return TestLogsDirectoryOverride;
        }

        if (OperatingSystem.IsLinux())
        {
            return "/home/jukebox/.local/share/com.jukeeo.kiosk/logs";
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "com.jukeeo", "kiosk", "logs");
        }

        return null;
    }

    internal static string? ResolveLogFilePath()
    {
        var logsDir = ResolveLogsDirectory();
        return logsDir is null ? null : Path.Combine(logsDir, $"{LogBaseName}.log");
    }

    private static int _writeFailureLogged;

    private static void WriteLine(string content)
    {
        var logsDir = ResolveLogsDirectory();
        if (logsDir is null)
        {
            return;
        }

        lock (WriteLock)
        {
            try
            {
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                var logPath = Path.Combine(logsDir, $"{LogBaseName}.log");
                RotateIfNeeded(logsDir, logPath);

                var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var line = $"[{timestamp}] {content}{Environment.NewLine}";
                File.AppendAllText(logPath, line, Encoding.UTF8);
                TryMakeLogReadableByKiosk(logPath);
            }
            catch (Exception ex)
            {
                // Journald/console permanecem como fallback; avisa uma vez por processo.
                if (Interlocked.CompareExchange(ref _writeFailureLogged, 1, 0) == 0)
                {
                    Console.Error.WriteLine(
                        $"FileAgentLogger: falha ao gravar log OTA ({ex.Message}). Ver permissões em {logsDir}.");
                }
            }
        }
    }

    private static void RotateIfNeeded(string logsDir, string logPath)
    {
        if (!File.Exists(logPath))
        {
            return;
        }

        var size = new FileInfo(logPath).Length;
        if (size < MaxLogSizeBytes)
        {
            return;
        }

        for (var i = MaxRotatedFiles - 1; i >= 1; i--)
        {
            var sourcePath = Path.Combine(logsDir, $"{LogBaseName}.{i}.log");
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            if (i == MaxRotatedFiles - 1)
            {
                File.Delete(sourcePath);
            }
            else
            {
                var destPath = Path.Combine(logsDir, $"{LogBaseName}.{i + 1}.log");
                File.Move(sourcePath, destPath, overwrite: true);
            }
        }

        var rotatedPath = Path.Combine(logsDir, $"{LogBaseName}.1.log");
        File.Move(logPath, rotatedPath, overwrite: true);
    }

    /// <summary>644 para o utilizador kiosk ler o ficheiro (grupo/outros com leitura).</summary>
    private static void TryMakeLogReadableByKiosk(string logPath)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                logPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        catch
        {
            // Falha não bloqueia a gravação.
        }
    }
}
