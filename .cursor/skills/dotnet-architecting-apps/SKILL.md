---
name: dotnet-architecting-apps
description: >-
  Orienta camadas DDD/Clean Architecture no jukebox-ota-agent (.NET 8 console).
  Usar ao criar features em src/Jukebox.Ota.Agent, novos comandos CLI ou refatorar Domain/Application/Infrastructure.
---

# Arquitetura .NET — jukebox-ota-agent

## Camadas (`src/Jukebox.Ota.Agent/`)

| Pasta | Contém | Não contém |
|-------|--------|------------|
| `Domain/` | Entidades, VOs, interfaces (`IOtaUpdateClient`, `IPackageVerifier`) | HTTP, JSON, filesystem |
| `Application/` | `VersionService`, `CheckUpdateService`, `VerifyPackageService` | Detalhe de criptografia |
| `Infrastructure/` | `JsonConfigLoader`, `HttpOtaUpdateClient`, `RsaPssPackageVerifier`, telemetria | Regra de negócio nova |
| `Interfaces/Cli/` | Parsing de argv, uso dos application services | Lógica de verificação |

## Dependências

```
Interfaces → Application → Domain
Infrastructure → Domain (implementa contratos)
Program.cs → compõe dependências (sem DI container na POC)
```

## Princípios

1. **Um caso de uso por service** em `Application/Services/`.
2. **Contratos no Domain**; implementação em `Infrastructure/`.
3. **CLI fina** — sem duplicar validação de manifesto na CLI.
4. **Sem pacotes NuGet** novos sem pedido explícito do usuário.
5. Comentários em **português do Brasil**.

## Novo comando CLI

1. Application service com `RunAsync` retornando exit code.
2. Método em `AgentCli`.
3. Testes xUnit em `tests/Jukebox.Ota.Agent.Tests/`.
4. Actualizar `docs/API.md` ou plano se mudar contrato externo.

## Referência de produto

[[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] · [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]]
