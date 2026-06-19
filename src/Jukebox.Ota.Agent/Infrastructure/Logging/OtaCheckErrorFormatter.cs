namespace Jukebox.Ota.Agent.Infrastructure.Logging;

/// <summary>Mensagens de falha do check legíveis no log do kiosk (português).</summary>
public static class OtaCheckErrorFormatter
{
    public static string Format(Exception ex)
    {
        var message = ex.Message;

        if (ex is HttpRequestException httpEx && httpEx.InnerException is not null)
        {
            message = $"{message} ({httpEx.InnerException.Message})";
        }

        if (message.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("configured HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
            || (ex is TaskCanceledException tce && !tce.CancellationToken.IsCancellationRequested))
        {
            return "timeout ao contactar o servidor OTA (limite 100 s) — verifique rede, DNS e ota_base_url";
        }

        if (message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No route to host", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return "servidor OTA inacessível na rede — verifique ota_base_url e conectividade";
        }

        if (message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No such host", StringComparison.OrdinalIgnoreCase))
        {
            return "host do servidor OTA não resolvido (DNS) — verifique ota_base_url";
        }

        if (ex is HttpRequestException && message.Contains("401", StringComparison.Ordinal))
        {
            return "servidor OTA recusou autenticação (HTTP 401) — verifique credenciais/URL";
        }

        if (ex is HttpRequestException && message.Contains("404", StringComparison.Ordinal))
        {
            return "endpoint OTA não encontrado (HTTP 404) — verifique ota_base_url e rota /v1/updates/check";
        }

        if (ex is HttpRequestException && (
            message.Contains("500", StringComparison.Ordinal)
            || message.Contains("502", StringComparison.Ordinal)
            || message.Contains("503", StringComparison.Ordinal)
            || message.Contains("504", StringComparison.Ordinal)))
        {
            return "servidor OTA indisponível (erro HTTP 5xx) — tente novamente mais tarde";
        }

        if (ex is HttpRequestException)
        {
            return $"falha HTTP ao contactar servidor OTA — {message}";
        }

        if (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return $"fixture ou caminho OTA não encontrado: {message}";
        }

        return message;
    }
}
