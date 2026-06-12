---
name: dotnet-testing-apps
description: >-
  Testes xUnit no jukebox-ota-agent — unitários de domínio/verificação e integração leve com fixtures file://.
  Usar ao adicionar testes em tests/Jukebox.Ota.Agent.Tests ou validar RSA-PSS/sha256.
---

# Testes .NET — jukebox-ota-agent

## Stack

- **xUnit** (`tests/Jukebox.Ota.Agent.Tests/`)
- Sem mocks pesados na POC — preferir ficheiros temporários e `HttpOtaUpdateClient` com `file://`

## Comandos

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~RsaPss"
```

## O que testar

| Área | Exemplos |
|------|----------|
| Config/Manifest | JSON snake_case, campos obrigatórios |
| Verificação | SHA-256 inválido, assinatura RSA-PSS válida/inválida |
| Check | Fixture `file://`, exit code `2` quando há update |

## Convenções

- Nomes de teste em português ou inglês descritivo (`VerifyAsync_Sha256Invalido_RetornaFalha`)
- Limpar ficheiros temporários em `finally`
- Gerar par RSA em teste para assinatura — não commitar PEM reais

## Antes de PR

`dotnet build` + `dotnet test` sem falhas.
