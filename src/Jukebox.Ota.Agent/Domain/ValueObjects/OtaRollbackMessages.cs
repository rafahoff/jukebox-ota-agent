namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Mensagens de erro partilhadas com o kiosk após rollback automático.</summary>
public static class OtaRollbackMessages
{
    public const string Prefix = "ROLLBACK: ";

    public static string Format(string targetVersion, string restoredVersion, string? detail)
    {
        var baseMessage = $"A atualização para {targetVersion} falhou; versão {restoredVersion} restaurada.";
        if (string.IsNullOrWhiteSpace(detail))
        {
            return Prefix + baseMessage;
        }

        return Prefix + $"{baseMessage} {detail.Trim()}";
    }
}
