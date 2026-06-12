# Publish self-contained do agente OTA para Raspberry Pi 64-bit.
# Pré-requisito: .NET SDK 8+ instalado.
# Saída: artifacts/linux-arm64/

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$OutDir = Join-Path $Root "artifacts\linux-arm64"
$Project = Join-Path $Root "src\Jukebox.Ota.Agent\Jukebox.Ota.Agent.csproj"

Write-Host "Publicando jukebox-ota-agent para linux-arm64 (self-contained)..."
dotnet publish $Project -c Release -r linux-arm64 --self-contained true -o $OutDir
Write-Host "Artefato em: $OutDir"
