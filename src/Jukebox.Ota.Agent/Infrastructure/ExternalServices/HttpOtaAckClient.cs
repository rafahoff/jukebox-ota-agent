using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Infrastructure.ExternalServices;

public sealed class HttpOtaAckClient : IOtaAckClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;

    public HttpOtaAckClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task SendAckAsync(OtaAgentConfig config, UpdateAckPayload payload, CancellationToken cancellationToken = default)
    {
        if (config.OtaBaseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(ToDto(payload), JsonOptions);
            Console.WriteLine($"[ack] fixture local — {json}");
            return;
        }

        var url = $"{config.OtaBaseUrl.TrimEnd('/')}/v1/updates/ack";
        using var response = await _httpClient.PostAsJsonAsync(url, ToDto(payload), JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        Console.WriteLine($"[ack] enviado result={payload.Result} version_attempted={payload.VersionAttempted}");
    }

    public void Dispose() => _httpClient.Dispose();

    private static AckDto ToDto(UpdateAckPayload payload) => new()
    {
        DeviceId = payload.DeviceId,
        App = payload.App,
        Channel = payload.Channel,
        VersionAttempted = payload.VersionAttempted,
        VersionPrevious = payload.VersionPrevious,
        VersionCurrent = payload.VersionCurrent,
        Result = payload.Result,
        ErrorCode = payload.ErrorCode,
        ErrorMessage = payload.ErrorMessage,
        PackageType = payload.PackageType,
        Arch = payload.Arch,
        OccurredAt = payload.OccurredAt.ToUniversalTime(),
    };

    private sealed class AckDto
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("app")]
        public string App { get; set; } = string.Empty;

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("version_attempted")]
        public string VersionAttempted { get; set; } = string.Empty;

        [JsonPropertyName("version_previous")]
        public string VersionPrevious { get; set; } = string.Empty;

        [JsonPropertyName("version_current")]
        public string VersionCurrent { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("package_type")]
        public string PackageType { get; set; } = "full";

        [JsonPropertyName("arch")]
        public string Arch { get; set; } = string.Empty;

        [JsonPropertyName("occurred_at")]
        public DateTimeOffset OccurredAt { get; set; }
    }
}
