#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Servidor HTTP mock para a API OTA do jukebox-ota-agent (POC Fase 4).

Uso:
  python tools/mock/ota_mock_server.py
  python tools/mock/ota_mock_server.py --mode has-update
  python tools/mock/ota_mock_server.py --mode auto --manifest tools/mock/manifest.example.json
  python tools/mock/ota_mock_server.py --host 0.0.0.0 --port 8080

Modos (--mode):
  no-update  → HTTP 204 sem corpo (sem actualização)
  has-update → HTTP 200 + JSON do manifesto
  auto       → 204 se query version == versão no manifesto; senão 200 + manifesto

Requisitos: Python 3 (stdlib apenas — sem pip/flask).

Teste rápido (outro terminal):
  curl -i "http://127.0.0.1:8080/v1/updates/check?device_id=pi-001&channel=beta&version=1.4.1"
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse


def _default_manifest_path() -> Path:
    here = Path(__file__).resolve().parent
    manifest = here / "manifest.json"
    if manifest.is_file():
        return manifest
    return here / "manifest.example.json"


def _load_manifest(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


class OtaMockHandler(BaseHTTPRequestHandler):
    mode: str = "no-update"
    manifest_path: Path = _default_manifest_path()
    manifest_data: dict | None = None

    def log_message(self, format: str, *args) -> None:
        # Silencia o log padrão; usamos log_request customizado.
        pass

    def _log_response(self, status: int) -> None:
        query = urlparse(self.path).query
        print(
            f"{self.command} {self.path} query={query!r} -> {status}",
            flush=True,
        )

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path != "/v1/updates/ack":
            self.send_error(404, "Rota não encontrada")
            self._log_response(404)
            return

        length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(length) if length > 0 else b""
        try:
            payload = json.loads(body.decode("utf-8")) if body else {}
        except json.JSONDecodeError:
            self.send_error(400, "JSON inválido")
            self._log_response(400)
            return

        print(f"ACK recebido: {json.dumps(payload, ensure_ascii=False)}", flush=True)
        self.send_response(204)
        self.end_headers()
        self._log_response(204)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path != "/v1/updates/check":
            self.send_error(404, "Rota não encontrada")
            self._log_response(404)
            return

        params = parse_qs(parsed.query)
        version = (params.get("version") or [""])[0]

        status, body = self._resolve_response(version)
        self.send_response(status)
        if body is not None:
            payload = json.dumps(body, ensure_ascii=False).encode("utf-8")
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)
        else:
            self.end_headers()

        self._log_response(status)

    def _resolve_response(self, version: str) -> tuple[int, dict | None]:
        mode = self.server.mode  # type: ignore[attr-defined]

        if mode == "no-update":
            return 204, None

        manifest = self.server.manifest_data  # type: ignore[attr-defined]
        if manifest is None:
            return 500, None

        if mode == "has-update":
            return 200, manifest

        if mode == "auto":
            manifest_version = str(manifest.get("version", ""))
            if version == manifest_version:
                return 204, None
            return 200, manifest

        return 500, None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Servidor mock OTA (stdlib) para desenvolvimento LAN."
    )
    parser.add_argument(
        "--host",
        default="0.0.0.0",
        help="Endereço de bind (padrão: 0.0.0.0)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8080,
        help="Porta TCP (padrão: 8080)",
    )
    parser.add_argument(
        "--mode",
        choices=("no-update", "has-update", "auto"),
        default="no-update",
        help="Comportamento da rota /v1/updates/check",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=None,
        help="Caminho do manifesto JSON (padrão: manifest.json ou manifest.example.json)",
    )
    args = parser.parse_args()

    manifest_path = args.manifest or _default_manifest_path()
    if not manifest_path.is_file():
        print(f"ERRO: manifesto não encontrado: {manifest_path}", file=sys.stderr)
        return 1

    manifest_data = _load_manifest(manifest_path)

    server = ThreadingHTTPServer((args.host, args.port), OtaMockHandler)
    server.mode = args.mode  # type: ignore[attr-defined]
    server.manifest_path = manifest_path  # type: ignore[attr-defined]
    server.manifest_data = manifest_data  # type: ignore[attr-defined]

    print(
        f"Mock OTA a escutar em http://{args.host}:{args.port} "
        f"(modo={args.mode}, manifesto={manifest_path})",
        flush=True,
    )
    print(
        'Exemplo: curl -i '
        f'"http://127.0.0.1:{args.port}/v1/updates/check'
        '?device_id=pi-001&channel=beta&version=1.4.1"',
        flush=True,
    )

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nEncerrado.", flush=True)
    finally:
        server.server_close()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
