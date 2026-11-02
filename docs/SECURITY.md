# Política de seguridad de CA-O

## Modelo de amenazas

CA-O manipula configuración crítica de Windows. Las superficies relevantes son: (1) el canal IPC entre UI elevada (requireAdministrator, UAC siempre) y el servicio SYSTEM — la UI está elevada pero toda mutación sigue auditada via pipe tipado, (2) la ejecución de herramientas externas, (3) los archivos de estado en `%ProgramData%`.

## Controles implementados

### Canal privilegiado (v2 + Ping health)
- Named Pipe `CA-O.Privileged.v1` con ACL restrictiva `NamedPipeServerStreamAcl` (SYSTEM Full, Administrators R/W, Interactive R/W — conectar ≠ autorizar).
- `GetCallerIdentity()` vía `RunAsClient()` + `WindowsCallerInspector` (SID real, nombre, `SessionId` del token, elevación) — `P1-8`.
- Validación `IpcRequestValidator` (§10): `ProtocolVersion==2`, `RequestId!=Empty`, `Nonce` 16..128 sin control chars, `CreatedAtUtc` ±30s/ +1m futuro, `Operation` enum, `Payload` polimórfico exacto, `OptimizationId` regex `[a-z0-9-]{1,80}`.
- **Ping / GetServiceStatus** (§10): operaciones sin `OptimizationId` para health check (`ServiceVersion, ProtocolVersion, ProcessId, IsSystem, Status, Capabilities`) — no usan optimización real como ping.
- Protección replay `ReplayCache` con reloj inyectable + `MaxAge 30s` + tamaño 64KB/256KB; timeout 15s por conexión.
- **Allowlist 17 operaciones**: `Apply/Revert/Detect/Verify/CaptureSnapshot/Ping/GetServiceStatus/SetDns/SetTimerResolution/FixDriver/InstallDriver/RemovePhantomDevices/SearchDriverUpdates/InstallDriverUpdates/ExportDriver/SearchCatalogDrivers/DownloadCatalogDriver` — no hay “ejecutar comando”. `SetTimerResolution` valida rango 5000..156250 y fija el valor con `NtSetTimerResolution` en el proceso del servicio.
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

`app.manifest` + `CA-O.InstallerGui` + `CA-O.Setup` ambos `requireAdministrator` — UAC siempre. `CA-O.Setup.exe` 135 MB self-contained single-file instala en `C:\Program Files\CA-O`, registra `CAO.Privileged` (failure 86400) y crea atajos Escritorio/Inicio. `CA-O-Setup-GUI-x64.zip` 88 MB con el instalador GUI. Log `%TEMP%\CA-O-Setup*.log`.

### Cadena de suministro
- CodeQL + NuGet audit en CI (`.github/workflows/ci.yml`), Dependabot semanal.
- **Actualizaciones con firma obligatoria**: cada ZIP distribuible publica `*.manifest.json` (schema, versión, asset, tamaño y SHA-256) y `*.manifest.sig` (RSA-3072, PKCS#1 v1.5/SHA-256 sobre los bytes exactos del manifiesto). La app y `CA-O.Setup` verifican la firma contra las **dos anclas públicas autofirmadas** compiladas en `CAO.Shared.Updates.ReleaseTrust` (`primary` y `recovery`) y **fallan cerrados** si falta el manifiesto, la firma no valida, el asset/versión no coincide o el SHA-256 difiere. El SHA-256 aislado ya no se acepta: quien puede reemplazar el ZIP puede reemplazar también su sidecar `.sha256`.
- **Dos raíces de confianza**: `Primary` firma el día a día; `Recovery` es una segunda clave RSA-3072 cuya parte privada debe guardarse **offline**, fuera del equipo habitual. Si `Primary` se pierde o se compromete, `Recovery` permite publicar una versión de transición. Limitación explícita: las versiones ya instaladas siguen aceptando `Primary`; la recuperación no revoca una clave comprometida por arte de magia, solo permite publicar la transición antes de que el atacante gane la carrera de versiones.
- **Clave privada protegida**: el PFX nunca se guarda sin contraseña. `generate-release-key.ps1` genera una contraseña robusta si no se define `CAO_RELEASE_PFX_PASSWORD` / `CAO_RELEASE_RECOVERY_PASSWORD` y la guarda cifrada con DPAPI del usuario actual (`*.password.dpapi`); `show-release-key-password.ps1` permite copiarla al gestor de contraseñas. `protect-release-key.ps1` reexporta un PFX existente con contraseña **sin cambiar** thumbprint ni clave pública. La ACL se aplica con `icacls /inheritance:r /grant:r <usuario> /T` (incluye los ficheros ya existentes) y se fija además sobre el PFX. La contraseña y una copia cifrada del PFX van a un medio offline separado.
- `scripts/package.ps1` firma el paquete estable y falla si no encuentra la clave (salvo `-SkipSigning` para builds locales no publicables). `scripts/verify-release.ps1` verifica el manifiesto firmado contra cualquiera de las dos anclas antes de publicar.
- **Firma de release ≠ Authenticode**: la firma RSA del manifiesto prueba procedencia e integridad para CA-O y no depende de una CA comercial. Authenticode es la firma de editor que Windows y SmartScreen evalúan; sin un certificado comercial el instalador puede seguir mostrando avisos de SmartScreen. La firma propia no se presenta como sustituto de Authenticode ni garantiza reputación de editor.
- Release `v2.1.34` firma Authenticode con timestamp cuando existe certificado (`CAO_SIGN_THUMBPRINT`) y genera `SHA256SUMS.txt` + **SBOM CycloneDX 1.7 `bom.json` 67 packages** (`artifacts/sbom/bom.json`). El updater usa `SHA256SUMS.txt`/`.sha256` solo como verificación manual; la puerta de instalación es el manifiesto firmado.

## Reporte de vulnerabilidades

Reportar de forma privada al mantenedor del repositorio. Por favor incluir repro mínimo y versión afectada.
