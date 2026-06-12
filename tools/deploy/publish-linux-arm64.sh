#!/usr/bin/env bash
# Publish self-contained do agente OTA para Raspberry Pi 64-bit.
# Pré-requisito: .NET SDK 8+ instalado.
# Saída: artifacts/linux-arm64/

set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT_DIR="$ROOT/artifacts/linux-arm64"
PROJECT="$ROOT/src/Jukebox.Ota.Agent/Jukebox.Ota.Agent.csproj"

echo "Publicando jukebox-ota-agent para linux-arm64 (self-contained)..."
dotnet publish "$PROJECT" -c Release -r linux-arm64 --self-contained true -o "$OUT_DIR"
echo "Artefato em: $OUT_DIR"
