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

    private static Uri BuildCheckUri(OtaAgentConfig config)
    {
        var baseUri = config.OtaBaseUrl.TrimEnd('/');
        var path = $"{baseUri}/v1/updates/check" +
                   $"?device_id={Uri.EscapeDataString(config.DeviceId)}" +
                   $"&channel={Uri.EscapeDataString(config.Channel)}" +
                   $"&version={Uri.EscapeDataString(config.CurrentVersion)}";
        return new Uri(path);
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
