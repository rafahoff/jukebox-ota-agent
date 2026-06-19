using Jukebox.Ota.Agent.Infrastructure.Logging;

namespace Jukebox.Ota.Agent.Tests;

public class OtaCheckErrorFormatterTests
{
    [Fact]
    public void Format_TimeoutHttpClient_RetornaMensagemEmPortugues()
    {
        var ex = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");

        var result = OtaCheckErrorFormatter.Format(ex);

        Assert.Contains("timeout", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100 s", result);
        Assert.Contains("ota_base_url", result);
    }

    [Fact]
    public void Format_ConnectionRefused_RetornaMensagemDeRede()
    {
        var ex = new HttpRequestException("Connection refused (127.0.0.1:443)");

        var result = OtaCheckErrorFormatter.Format(ex);

        Assert.Contains("inacessível", result);
    }

    [Fact]
    public void Format_Http404_RetornaMensagemDeEndpoint()
    {
        var ex = new HttpRequestException("Response status code does not indicate success: 404 (Not Found).");

        var result = OtaCheckErrorFormatter.Format(ex);

        Assert.Contains("404", result);
        Assert.Contains("/v1/updates/check", result);
    }

    [Fact]
    public void Format_ErroDesconhecido_PreservaMensagemOriginal()
    {
        var ex = new InvalidOperationException("erro customizado");

        Assert.Equal("erro customizado", OtaCheckErrorFormatter.Format(ex));
    }
}
