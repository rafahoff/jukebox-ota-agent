using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.Health;

public sealed class HttpHealthChecker : IHealthChecker, IDisposable
{
    private readonly ISystemService _systemService;
    private readonly HttpClient _httpClient;

    public HttpHealthChecker(ISystemService systemService, HttpClient? httpClient = null)
    {
        _systemService = systemService;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<HealthCheckResult> WaitForHealthyAsync(
        string serviceName,
        string healthUrl,
        string expectedAppVersion,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        var sawWrongVersion = false;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await _systemService.IsServiceActiveAsync(serviceName, cancellationToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

            try
            {
                using var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<HealthResponseDto>(cancellationToken: cancellationToken);
                if (body?.AppVersion is not null &&
                    string.Equals(body.AppVersion, expectedAppVersion, StringComparison.Ordinal))
                {
                    return new HealthCheckResult(true, null, null);
                }

                if (body?.AppVersion is not null)
                {
                    sawWrongVersion = true;
                }
            }
            catch (HttpRequestException)
            {
                // Kiosk ainda a subir — continuar polling
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout HTTP individual — continuar polling
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        var errorCode = sawWrongVersion ? "health_version_mismatch" : "health_check_timeout";
        return new HealthCheckResult(false, errorCode, $"Health não confirmou app_version={expectedAppVersion} em {timeout.TotalSeconds}s.");
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class HealthResponseDto
    {
        [JsonPropertyName("app_version")]
        public string? AppVersion { get; set; }
    }
}
