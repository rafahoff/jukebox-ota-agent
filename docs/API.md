# API OTA — contrato consumido pelo agente

Referência mínima para a POC. Servidor completo: repo `jukebox-ota-server` (futuro).

## GET `/v1/updates/check`

**Query:** `device_id`, `channel`, `version` (versão actual reportada pelo dispositivo).

**Respostas:**

| Código | Corpo | Significado |
|--------|-------|-------------|
| `204` | vazio | Sem actualização |
| `200` | JSON manifesto | Actualização disponível |

### Manifesto (200)

```json
{
  "app": "jukebox_tv",
  "version": "1.4.2",
  "arch": "aarch64",
  "sha256": "...",
  "signature_b64": "...",
  "signature_algorithm": "rsa-pss-sha256",
  "released_at": "2026-06-12T12:00:00Z"
}
```

## Assinatura

- Algoritmo preferido: **RSA-PSS com SHA-256**
- Payload assinado: JSON canónico do manifesto **sem** o campo `signature_b64`
- Pacote: validado por `sha256` do ficheiro no disco

## Mock local (POC)

Configurar `ota_base_url` como `file:///caminho/absoluto/manifest.json` no `ota-agent.json`.
