using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>Resultado interno do fluxo de verificação OTA (check + download + verificação).</summary>
public sealed record CheckUpdateOutcome(
    int ExitCode,
    OtaAgentConfig? Config,
    UpdateManifest? Manifest,
    bool UpdateAvailable,
    string? SkipReason,
    string? ErrorMessage,
    string? ManifestPath = null,
    string? PackagePath = null);
