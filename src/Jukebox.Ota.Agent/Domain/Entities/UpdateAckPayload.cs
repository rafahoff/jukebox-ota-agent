namespace Jukebox.Ota.Agent.Domain.Entities;

/// <summary>Corpo do POST /v1/updates/ack enviado após tentativa de apply.</summary>
public sealed record UpdateAckPayload(
    string DeviceId,
    string App,
    string Channel,
    string VersionAttempted,
    string VersionPrevious,
    string VersionCurrent,
    string Result,
    string? ErrorCode,
    string? ErrorMessage,
    string PackageType,
    string Arch,
    DateTimeOffset OccurredAt
);
