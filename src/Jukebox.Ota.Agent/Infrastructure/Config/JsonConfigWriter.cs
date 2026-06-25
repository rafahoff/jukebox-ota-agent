using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jukebox.Ota.Agent.Infrastructure.Config;

/// <summary>Grava campos mutáveis de <c>/etc/jukeeo/ota-agent.json</c> após apply/check.</summary>
public sealed class JsonConfigWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public void UpdateCurrentVersion(string path, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("version é obrigatória.", nameof(version));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Arquivo de configuração não encontrado: {path}");
        }

        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("Configuração OTA inválida.");

        node["current_version"] = version;
        var output = node.ToJsonString(Options) + Environment.NewLine;

        // Gravação in-place: grupo jukebox-ota tem escrita no ficheiro (660) mas não no diretório /etc/jukeeo.
        File.WriteAllText(path, output);
    }
}
