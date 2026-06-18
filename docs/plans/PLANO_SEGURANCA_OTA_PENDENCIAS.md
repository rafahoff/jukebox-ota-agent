# Plano — Pendências de segurança OTA

- **Status:** em aberto
- **Data:** 2026-06-18
- **Escopo:** documentar lacunas de segurança identificadas na POC do `jukebox-ota-agent` e opções de mitigação — com foco em proteção da chave pública e limites reais quando o agente corre com privilégios elevados.

**Origem:** análise do código em `RsaPssPackageVerifier`, `HttpOtaUpdateClient`, mock `ota_mock_server.py` e checklist da skill `insecure-defaults`. Contexto de produto: [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]].

---

## Resumo executivo

O modelo OTA actual separa duas preocupações:

| Preocupação | Responsável | Estado na POC |
|-------------|-------------|---------------|
| **Quem pode baixar** actualizações | `jukebox-ota-server` (futuro) | Não implementado |
| **O que foi baixado é autêntico** | Agente (`verify`) com RSA-PSS + SHA-256 | Parcial — assinatura opcional na POC |

A verificação criptográfica no dispositivo **não depende de esconder a chave pública**. Depende de o agente **confiar na chave certa** e de um atacante **não conseguir substituir o verificador** nem os ficheiros de confiança. Num Pi já comprometido com `root`, qualquer defesa só no cliente é **contornável** — o objectivo é elevar o custo do ataque e combinar camadas (servidor + dispositivo + operação).

---

## 1. Pendências do servidor OTA (`jukebox-ota-server`)

O repositório actual contém apenas o **agente cliente** e um mock HTTP sem autenticação.

### 1.1 O que falta

- [ ] **Autorização de download** — qualquer cliente que conheça a URL pode chamar `GET /v1/updates/check` (mock e API futura sem camada extra).
- [ ] **Validação de `device_id`** — o agente envia `device_id`, `channel` e `version` na query; o mock ignora estes parâmetros para decisão de acesso.
- [ ] **Protecção dos artefatos** — URLs de pacote não devem ser públicas; preferir URLs pré-assinadas de curta duração ou download só após autorização.
- [ ] **Rate limiting e auditoria** — registar tentativas por `device_id`/IP; detectar scraping da fleet.
- [ ] **HTTPS obrigatório** em produção (`ota_base_url` não pode ser `file://` nem HTTP plano).

### 1.2 Opções de desenho (servidor)

| Abordagem | Prós | Contras |
|-----------|------|---------|
| Allowlist de `device_id` por canal | Simples; alinha com rollout gradual | `device_id` pode ser clonado se vazar da config |
| Token Bearer / API key por dispositivo | Revogável; rotação por fleet | Segredo no Pi — mesmo problema de protecção local |
| mTLS (certificado cliente no Pi) | Forte identidade; sem segredo partilhado na query | Provisionamento e revogação mais complexos |
| URLs pré-assinadas (S3, CDN) | Pacote não listável; TTL curto | Servidor ainda precisa autorizar quem recebe o manifesto |
| Rede privada / VPN / Cloudflare Access | Reduz superfície na internet | Não substitui verificação no dispositivo |

**Decisão pendente:** escolher combinação no `jukebox-ota-server` e documentar no plano de produto [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] § proteção de download.

---

## 2. Pendências do agente (integridade e fluxo)

### 2.1 Assinatura opcional na POC

Em `RsaPssPackageVerifier`, manifesto **sem** `signature_b64` é aceite se o SHA-256 do pacote coincidir:

```csharp
// Infrastructure/Security/RsaPssPackageVerifier.cs — comportamento actual da POC
if (string.IsNullOrWhiteSpace(manifest.SignatureB64))
    return new PackageVerificationResult(true, "SHA-256 válido; assinatura ausente no manifesto (aceito na POC).");
```

- [ ] **Produção:** rejeitar manifesto sem assinatura válida.
- [ ] **Produção:** exigir `public_key_path` válido antes de qualquer swap de bundle.

### 2.2 `check` não verifica assinatura

O comando `check` só obtém o manifesto remoto; **não** invoca `RsaPssPackageVerifier`. Quando existir download + instalação:

- [ ] Verificar assinatura **antes** de gravar o pacote em disco permanente ou antes do swap.
- [ ] Não confiar no manifesto recebido em `check` sem validação criptográfica no fluxo completo.

### 2.3 Download e swap ainda fora de escopo

Sem fluxo de download/instalação, a cadeia de confiança está incompleta na operação real. Pendente para fase pós-POC.

### 2.4 TLS

- [ ] Manter validação de certificado TLS padrão do `HttpClient` (não desactivar).
- [ ] Avaliar **certificate pinning** para `ota_base_url` em produção (mitiga MITM com CA comprometida no `check`).

---

## 3. Chave pública no dispositivo — o problema real

### 3.1 Esclarecimento importante

A **chave pública não é segredo**. Em RSA, quem deve permanecer protegido é a **chave privada** (só no pipeline de release / servidor de assinatura).

O risco não é “alguém copiar a chave pública”. O risco é:

1. **Substituir** a chave pública (ficheiro ou referência no binário) pela chave pública do atacante.
2. O atacante assina manifestos e pacotes com a **sua** chave privada.
3. O agente verifica com a chave trocada e **aceita malware**.

Ou, de forma equivalente:

4. **Patch do binário** ou `LD_PRELOAD` para saltar a verificação — especialmente fácil se o processo já corre com `root`.

### 3.2 Por que `public_key_path` em `/etc/jukebox/` não basta hoje

Configuração actual (`OtaAgentConfig`):

- `public_key_path` aponta para PEM no disco.
- Unit systemd (`packaging/systemd/jukebox_ota_agent.service`) não define `User=` — execução típica via `systemctl` corre como **root**.
- Utilizador `jukebox` do kiosk também pode ter sudo ou acesso físico ao SD card.

Com `root` no dispositivo comprometido:

| Acção do atacante | Dificuldade |
|-------------------|-------------|
| `cat /etc/jukebox/ota-public.pem` | Trivial (só leitura — não é o ataque principal) |
| Substituir PEM por chave do atacante | Trivial |
| Alterar `ota-agent.json` → outro `public_key_path` | Trivial |
| Substituir `/opt/jukebox/ota-agent/jukebox-ota-agent` | Trivial |
| Montar SD noutro sistema e editar partições | Trivial com acesso físico |

**Conclusão:** permissões `chmod 444` ou dono `root:root` no PEM **aumentam a barreira para o utilizador kiosk normal**, mas **não resistem** a root ou acesso físico ao cartão.

### 3.3 Por que embutir a chave no binário não resolve

| Argumento a favor | Contra-argumento |
|-------------------|------------------|
| Remove dependência de ficheiro externo trocável | Binário self-contained é extraível (`strings`, descompilação .NET, cópia do ficheiro) |
| Evita path errado na config | Atacante com root substitui o binário inteiro |
| “Obscuridade” adicional | Chave pública em recursos .NET ou constante é **recuperável**; não é segredo |

Embutir pode ser **conveniente** (menos ficheiros soltos, fingerprint fixo no build), mas **não é controlo de segurança** contra root. Tratar como integridade do artefacto, não como cofre.

### 3.4 O que realmente protege (defesa em profundidade)

Num quiosque com root comprometido, **nenhuma verificação só-cliente é absoluta**. O objectivo é combinar camadas:

```text
┌─────────────────────────────────────────────────────────────┐
│  Camada 1 — Servidor: só dispositivos autorizados baixam   │
├─────────────────────────────────────────────────────────────┤
│  Camada 2 — Assinatura: manifesto + sha256 ligados (RSA)   │
├─────────────────────────────────────────────────────────────┤
│  Camada 3 — Agente: verify obrigatório antes do swap       │
├─────────────────────────────────────────────────────────────┤
│  Camada 4 — Privilégio mínimo: agente não corre como root  │
├─────────────────────────────────────────────────────────────┤
│  Camada 5 — Integridade do SO: read-only rootfs, dm-verity │
├─────────────────────────────────────────────────────────────┤
│  Camada 6 — Hardware: TPM / secure boot (futuro, se justif.) │
└─────────────────────────────────────────────────────────────┘
```

#### 3.4.1 Privilégio mínimo (prioridade alta, baixo custo)

- [x] Criar utilizador de sistema dedicado (`jukebox-ota`) sem shell de login — `pi_install_ota.sh`.
- [x] Unit systemd: `User=jukebox-ota`, `Group=jukebox-ota` — `packaging/systemd/jukebox_ota_agent.service`.
- [x] PEM e `ota-agent.json` com dono `root`, grupo `jukebox-ota`, modo `640`; `/etc/jukebox` modo `750`.
- [x] Binário em `/opt/jukebox/ota-agent/` legível/executável pelo grupo; **não** gravável pelo agente.
- [x] Estado futuro em `/var/lib/jukebox-ota` (`StateDirectory` + `750` no install).
- [x] Agente **não** precisa de root para `check`/`verify`; root só no script de **install** (deploy).

**Validação manual no Pi** (utilizador `jukebox` com sudo):

```bash
sudo -u jukebox-ota /opt/jukebox/ota-agent/jukebox-ota-agent version
sudo systemctl start jukebox_ota_agent.service
journalctl -t jukebox-ota -n 20 --no-pager
```

O utilizador `jukebox` **não** consegue executar o binário nem ler a config directamente (`750`/`640`); precisa de `sudo -u jukebox-ota` ou do timer systemd.

Isto não impede root, mas impede o processo **normal** do kiosk e scripts do utilizador `jukebox` de trocar a chave sem escalar.

#### 3.4.2 Duplo ancora de confiança (prioridade média)

- [ ] **Fingerprint embutido no build** (ex.: SHA-256 do SPKI da chave pública de produção).
- [ ] Em runtime: carregar PEM de `public_key_path` **e** comparar fingerprint com o embutido.
- [ ] Se divergir → falhar fechado, telemetria de “trust anchor mismatch”.

Um atacante precisa de patch **do binário e** do ficheiro PEM de forma consistente — custo maior que só trocar um JSON.

#### 3.4.3 Read-only e imutabilidade (prioridade média/alta para fleet)

- [ ] Partição root read-only ou overlay (padrão em appliances).
- [ ] `/opt/jukebox/ota-agent/` e `/etc/jukebox/ota-public.pem` na imagem golden; actualizações do agente via OTA assinado ou reinstall controlado.
- [ ] Desactivar ou restringir sudo do utilizador kiosk em produção.

#### 3.4.4 Rotação de chaves (operacional)

- [ ] Suportar **duas chaves públicas** válidas durante janela de rotação (antiga + nova), documentado no manifesto ou na config.
- [ ] Chave privada só no CI/release; rotação rara e auditada.
- [ ] Comprometimento da chave privada → nova chave + rejeitar releases antigos após janela (política de produto).

#### 3.4.5 mTLS ou token no servidor (complementar)

Mesmo com chave pública trocada localmente, um atacante precisa de um **pacote malicioso distribuído**. Se o servidor só entrega pacotes a dispositivos autenticados, o atacante externo não consegue alimentar o Pi com builds arbitrários — reduz vetor de “substituir só o PEM e servir o meu tarball noutro host”.

#### 3.4.6 O que **não** vale a pena como única defesa

- Ofuscar chave no binário (.NET).
- Criptografar PEM no disco com chave derivada no mesmo binário (root extrai ambos).
- Confiar só em permissões de ficheiro com agente root.

### 3.5 Modelo de ameaça honesto

| Cenário | Verificação RSA no agente ajuda? |
|---------|----------------------------------|
| MITM na internet entrega manifesto falso | Sim, se `verify` com chave correcta |
| Funcionário mal-intencionado com SSH root no Pi | **Não** de forma definitiva — pode patchar o agente |
| Roubo físico do SD card | Limitado — depende de RO rootfs / encryption |
| Scraping de APK/bundle legítimo do servidor | Não — problema de autorização no servidor |
| Utilizador kiosk sem root tenta trocar PEM | Sim, com user dedicado + permissões |

**Mensagem para produto:** OTA seguro no Pi assume **dispositivo não comprometido no momento do update**. A verificação assinada protege contra **rede e mirrors não autorizados**; protecção contra **root local** exige hardening do SO e processo, não só esconder a chave pública.

---

## 4. Checklist consolidado (produção)

### Servidor (`jukebox-ota-server`)

- [ ] HTTPS obrigatório
- [ ] Autorização por dispositivo/canal (allowlist, mTLS ou token)
- [ ] URLs de pacote não públicas / pré-assinadas
- [ ] Rate limit e logs de acesso

### Agente

- [ ] Assinatura RSA-PSS obrigatória (`signature_b64` não vazio)
- [ ] `verify` integrado no fluxo de download antes do swap
- [x] Utilizador de sistema dedicado (não root) — `jukebox-ota` + unit systemd
- [ ] Fingerprint da chave embutido no build + validação em runtime
- [ ] Avaliar certificate pinning em `ota_base_url`
- [ ] Telemetria de falhas de verificação e mismatch de fingerprint

### Operação / imagem Pi

- [ ] PEM e config na golden image; permissões mínimas
- [ ] Rootfs read-only onde viável
- [ ] Chave privada nunca no Pi nem no repositório
- [ ] Procedimento de rotação de chave documentado

---

## 5. Próximos passos sugeridos

1. **Decisão de produto** no `jukebox-ota-server`: modelo de autorização de download (ver §1.2).
2. ~~**Quick win no agente:** utilizador `jukebox-ota` + unit systemd sem root~~ — **feito** (ver §3.4.1).
3. **Código:** flag ou compilação `Release` que rejeita manifesto sem assinatura.
4. **Código:** `PublicKeyTrustAnchor` — fingerprint embutido + validação ao carregar PEM.
5. **ADR** em `docs/adr/` quando a decisão de ancora de confiança estiver fechada.

---

## Referências no repo

| Artefacto | Papel |
|-----------|-------|
| `src/Jukebox.Ota.Agent/Infrastructure/Security/RsaPssPackageVerifier.cs` | SHA-256 + RSA-PSS |
| `src/Jukebox.Ota.Agent/Infrastructure/ExternalServices/HttpOtaUpdateClient.cs` | Cliente HTTP sem auth |
| `tools/mock/ota_mock_server.py` | Mock sem autorização |
| `packaging/systemd/jukebox_ota_agent.service` | Execução oneshot como `jukebox-ota` |
| `tools/deploy/pi_install_ota.sh` | Cria utilizador de sistema e permissões mínimas |
| `.cursor/skills/insecure-defaults/SKILL.md` | Checklist de defaults |
| `docs/API.md` | Contrato `/v1/updates/check` |
