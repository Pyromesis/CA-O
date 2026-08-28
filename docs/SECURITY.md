# Política de seguridad de CA-O

## Modelo de amenazas

CA-O manipula configuración crítica de Windows. Las superficies relevantes son: (1) el canal IPC entre UI sin privilegios y el servicio SYSTEM, (2) la ejecución de herramientas externas, (3) los archivos de estado en `%ProgramData%`.

## Controles implementados

### Canal privilegiado (v2 + Ping health)
- Named Pipe `CA-O.Privileged.v1` con ACL restrictiva `NamedPipeServerStreamAcl` (SYSTEM Full, Administrators R/W, Interactive R/W — conectar ≠ autorizar).
- `GetCallerIdentity()` vía `RunAsClient()` + `WindowsCallerInspector` (SID real, nombre, `SessionId` del token, elevación) — `P1-8`.
- Validación `IpcRequestValidator` (§10): `ProtocolVersion==2`, `RequestId!=Empty`, `Nonce` 1..128 sin control chars, `CreatedAtUtc` ±30s/ +1m futuro, `Operation` enum, `Payload` polimórfico exacto, `OptimizationId` regex `[a-z0-9-]{1,80}`.
- **Ping / GetServiceStatus** (§10): operaciones sin `OptimizationId` para health check (`ServiceVersion, ProtocolVersion, ProcessId, IsSystem, Status, Capabilities`) — no usan optimización real como ping.
- Protección replay `ReplayCache` con reloj inyectable + `MaxAge 30s` + tamaño 64KB/256KB; timeout 15s por conexión.
- **Allowlist 7 operaciones**: `Apply/Revert/Detect/Verify/CaptureSnapshot/Ping/GetServiceStatus` — no hay “ejecutar comando”.
- Auditoría: `requestedBy SID/Name → executedBy SYSTEM, op, accepted, code`.

### Ejecución externa
- Catálogo estático `ElevatedCommandCatalog`: ejecutables permitidos (powercfg/netsh) con patrones de argumentos cerrados; probado contra inyección, encadenamiento y expansión de variables (`tests\CA-O.Security.Tests\ElevatedCommandCatalogTests`).
- `bcdedit` nunca pasa por el runner genérico; las operaciones boot-level viven como optimizaciones tipadas con confirmación Expert.

### Datos
- `history.jsonl` y snapshots no contienen secretos ni entrada personal del usuario.
- Los snapshots restauran ausencias (DELETE) — jamás valores por defecto inventados.
- Sin telemetría, analytics ni red hacia terceros salvo sondas de latencia explícitas del usuario (ping/DNS/Cloudflare speed endpoints para bufferbloat).

### Autorización por token (FASE 2 + SessionId)

Tras `RunAsClient()` + `WindowsIdentity.GetCurrent()` el servicio inspecciona SID, `IsInRole(Administrators)`, `IsElevated` y `SessionId` del token y aplica `AdministratorsOnlyAuthorizer`:

- Permitido: token elevado con grupo Administrators, o SID en la lista configurada.
- Denegado: usuario estándar (CAO-SEC-005), admin sin elevación / token filtrado (CAO-SEC-003), SID inválido (CAO-SEC-004).
- Auditoría con RequestedBy (SID llamante) vs ExecutedBy=SYSTEM.

### Protocolo v2 tipado (FASE 3)

Envelope versionado (`IpcProtocol.Version = 2`): RequestId GUID, nonce, CreatedAtUtc (expira en 30 s), operación enumerada, payload polimórfico por discriminador. Límites: 64 KB request / 256 KB respuesta. Anti-replay: RequestId+nonce de un solo uso. Errores estructurados CAO-XXX-nnn.

### Gateway de ejecución (FASE 4)

Toda ejecución privilegiada pasa por `IPrivilegedCommandExecutor` → `CommandPolicy.Resolve`: rutas absolutas %SystemRoot% (anti PATH-hijacking), tokens exactos sin metacaracteres (anti chaining/redirección/inyección), sin shell; OptimizeSystemDrive usa defrag.exe con argumentos fijos (`C: /O`). Desviación → CAO-SEC-010.

### Cancelación segura (FASE 6)

La cancelación solo se atiende antes de SNAPSHOT/APPLY; durante APPLY es atómica (token ignorado) y una cancelación en vuelo se marca `CancellationDeferred` tras completar apply→verify→commit.

### Verificación estricta (FASE 10)

`VerificationStatus { Passed, Failed, Unknown, NotApplicable }`. Unknown jamás es éxito: provoca rollback automático (CAO-VERIFY-002). Acciones irreversibles: NotApplicable + auditada como irreversible.

### Gaming — bloqueo real (§24-26)

`GameCompatibilityPolicy` matriz `SAFE/CAUTION/BLOCKED` — `disable-vbs/hvci/hypervisor-launchtype-off` → **BLOCKED `CAO-GAME-001`** si Vanguard/EAC/BattlEye detectado. `OptimizationEngine.ApplyAsync` valida antes de transacción en **Core y Privileged** (no solo UI). `Expert` no bypassa bloqueos críticos.

### Instalador

`app.manifest` + `CA-O.InstallerGui` + `CA-O.Setup` ambos `requireAdministrator` — UAC siempre. `CA-O-Setup-GUI-x64.exe` 135 MB self-contained single-file instala en `C:\Program Files\CA-O`, registra `CAO.Privileged` (failure 86400) y crea atajos Escritorio/Inicio. Log `%TEMP%\CA-O-Setup*.log`.

### Cadena de suministro
- CodeQL + NuGet audit en CI (`.github/workflows/ci.yml`), Dependabot semanal.
- Release `v2.0.1` firma Authenticode con timestamp cuando existe certificado (`CAO_SIGN_THUMBPRINT`) y genera `SHA256SUMS.txt` + **SBOM CycloneDX 1.7 `bom.json` 71 packages** (`artifacts/sbom/bom.json`).

## Reporte de vulnerabilidades

Reportar de forma privada al mantenedor del repositorio. Por favor incluir repro mínimo y versión afectada.
