using System.Net;
using System.Text.Json;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Manifest;

namespace Jukebox.Ota.Agent.Infrastructure.ExternalServices;

/// <summary>
/// Cliente OTA via HTTP(S) ou fixture local (<c>file://</c>).
/// </summary>
public sealed class HttpOtaUpdateClient : IOtaUpdateClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public HttpOtaUpdateClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<UpdateManifest?> CheckAsync(OtaAgentConfig config, CancellationToken cancellationToken = default)
    {
        if (config.OtaBaseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadFromFileAsync(config.OtaBaseUrl, cancellationToken);
        }

        var uri = BuildCheckUri(config);
        using var response = await _httpClient.GetAsync(uri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonManifestLoader.FromJson(json);
    }

    public async Task<string> DownloadPackageAsync(
        OtaAgentConfig config,
        UpdateManifest manifest,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);

        var fileName = $"jukeeo-{manifest.Version}+{manifest.Arch}.tar.zst";
        var destinationPath = Path.Combine(destinationDirectory, fileName);

        var sourceUrl = ResolveDownloadUrl(config, manifest);
        if (sourceUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var sourcePath = new Uri(sourceUrl).LocalPath;
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Pacote OTA não encontrado: {sourcePath}");
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            return destinationPath;
        }

        using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        return destinationPath;
    }

    public static string ResolveDownloadUrl(OtaAgentConfig config, UpdateManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            return manifest.DownloadUrl;
        }

        if (config.OtaBaseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var manifestPath = new Uri(config.OtaBaseUrl).LocalPath;
            var manifestDir = Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidOperationException($"Diretório do manifesto inválido: {manifestPath}");
            var packageName = $"jukeeo-{manifest.Version}+{manifest.Arch}.tar.zst";
            var packagePath = Path.Combine(manifestDir, packageName);
            return new Uri(packagePath).AbsoluteUri;
        }

        var baseUri = config.OtaBaseUrl.TrimEnd('/');
        return $"{baseUri}/ota/{manifest.App}/{manifest.Version}/{manifest.Arch}/jukeeo-{manifest.Version}+{manifest.Arch}.tar.zst";
    }

    private static Uri BuildCheckUri(OtaAgentConfig config)
    {
        var baseUri = config.OtaBaseUrl.TrimEnd('/');
        var path = $"{baseUri}/v1/updates/check" +
                   $"?device_id={Uri.EscapeDataString(config.DeviceId)}" +
                   $"&channel={Uri.EscapeDataString(config.Channel)}" +
                   $"&version={Uri.EscapeDataString(config.CurrentVersion)}";
        return new Uri(path);
    }

    /// <summary>Endpoint usado no check (para logs do kiosk).</summary>
    public static string DescribeCheckEndpoint(OtaAgentConfig config)
    {
        if (config.OtaBaseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return config.OtaBaseUrl;
        }

        return BuildCheckUri(config).ToString();
    }

    private static async Task<UpdateManifest?> LoadFromFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        var path = new Uri(fileUrl).LocalPath;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture OTA não encontrada: {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonManifestLoader.FromJson(json);
    }

    public void Dispose() => _httpClient.Dispose();
}
