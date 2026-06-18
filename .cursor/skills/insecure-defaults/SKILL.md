---
name: insecure-defaults
description: >-
  Auditar defaults inseguros no jukebox-ota-agent — chaves, paths, TLS, verificação de assinatura.
  Invocar explicitamente em revisões de segurança OTA ou antes de deploy em fleet.
---

# Defaults inseguros — jukebox-ota-agent

## Checklist

- [ ] Chave privada de assinatura **nunca** no repositório nem no Pi
- [ ] `public_key_path` aponta para PEM confiável em `/etc/jukebox/`
- [ ] Manifesto sem `signature_b64` só aceito na POC — exigir assinatura em produção
- [ ] `ota_base_url` usa HTTPS em produção (não `file://`)
- [ ] Não desactivar verificação TLS (`HttpClient` padrão mantém validação de certificado)
- [ ] Permissões de `/etc/jukebox/ota-agent.json` restritas (`root:jukebox-ota`, `640`)
- [ ] Service systemd com `User=jukebox-ota` (não root)

## Código sensível

- `Infrastructure/Security/RsaPssPackageVerifier.cs`
- `Infrastructure/ExternalServices/HttpOtaUpdateClient.cs`

## Referência

- [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] § proteção de download
- [[PLANO_SEGURANCA_OTA_PENDENCIAS]] — pendências detalhadas (servidor, verify, chave pública / root)
