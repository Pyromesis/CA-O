# Política de seguridad de CA-O

## Modelo de amenazas

CA-O manipula configuración crítica de Windows. Las superficies relevantes son: (1) el canal IPC entre UI sin privilegios y el servicio SYSTEM, (2) la ejecución de herramientas externas, (3) los archivos de estado en `%ProgramData%`.

## Controles implementados

### Canal privilegiado
- Named Pipe con ACL restrictiva creada vía `NamedPipeServerStreamAcl` (Administrators: RW, SYSTEM: Full, Interactive: RW).
- Verificación de identidad del cliente con `GetImpersonationUserName()`; solicitudes sin identidad se rechazan.
- Validación estricta de esquema (`PrivilegedOperationValidator`): versión de protocolo exacta, `RequestId` no vacío, nonce sin caracteres de control y longitud acotada, `OptimizationId` con whitelist regex `[a-z0-9-]{1,80}`, nombres de servicio alfanuméricos, letra de unidad A–Z.
- Protección replay: cada `RequestId`/nonce acepta una sola vez por vida del servicio.
- Timeout por conexión (15 s); fallos aislados por conexión sin tumbar el host.
- **Lista blanca de operaciones**: sólo Apply/Revert/Detect/CaptureSnapshot/Verify. No hay operación "ejecutar comando".
- Auditoría estructurada de cada request (identidad, operación, resultado) en el log del servicio.

### Ejecución externa
- Catálogo estático `ElevatedCommandCatalog`: ejecutables permitidos (powercfg/netsh) con patrones de argumentos cerrados; probado contra inyección, encadenamiento y expansión de variables (`tests\CA-O.Security.Tests\ElevatedCommandCatalogTests`).
- `bcdedit` nunca pasa por el runner genérico; las operaciones boot-level viven como optimizaciones tipadas con confirmación Expert.

### Datos
- `history.jsonl` y snapshots no contienen secretos ni entrada personal del usuario.
- Los snapshots restauran ausencias (DELETE) — jamás valores por defecto inventados.
- Sin telemetría, analytics ni red hacia terceros salvo sondas de latencia explícitas del usuario (ping/DNS/Cloudflare speed endpoints para bufferbloat).

### Autorización por token (FASE 2)

Tras la impersonación del cliente en el pipe, el servicio extrae la identidad real del token (SID, nombre, grupo Administrators, elevación) y aplica `AdministratorsOnlyAuthorizer`:

- Permitido: token elevado con grupo Administrators, o SID en la lista configurada.
- Denegado: usuario estándar (CAO-SEC-005), admin sin elevación / token filtrado (CAO-SEC-003), SID inválido (CAO-SEC-004).
- Auditoría con RequestedBy (SID llamante) vs ExecutedBy=SYSTEM.

### Protocolo v2 tipado (FASE 3)

Envelope versionado (`IpcProtocol.Version = 2`): RequestId GUID, nonce, CreatedAtUtc (expira en 30 s), operación enumerada, payload polimórfico por discriminador. Límites: 64 KB request / 256 KB respuesta. Anti-replay: RequestId+nonce de un solo uso. Errores estructurados CAO-XXX-nnn.

### Gateway de ejecución (FASE 4)

Toda ejecución privilegiada pasa por `IPrivilegedCommandExecutor` → `CommandPolicy.Resolve`: rutas absolutas %SystemRoot% (anti PATH-hijacking), tokens exactos sin metacaracteres (anti chaining/redirección/inyección), sin shell; Optimize-Volume usa powershell.exe con -Command estático. Desviación → CAO-SEC-010.

### Cancelación segura (FASE 6)

La cancelación solo se atiende antes de SNAPSHOT/APPLY; durante APPLY es atómica (token ignorado) y una cancelación en vuelo se marca `CancellationDeferred` tras completar apply→verify→commit.

### Verificación estricta (FASE 10)

`VerificationStatus { Passed, Failed, Unknown, NotApplicable }`. Unknown jamás es éxito: provoca rollback automático (CAO-VERIFY-002). Acciones irreversibles: NotApplicable + auditada como irreversible.

### Cadena de suministro
- CodeQL + NuGet audit en CI (`.github/workflows/ci.yml`), Dependabot semanal.
- Release firma Authenticode con timestamp cuando existe certificado (`CAO_SIGN_THUMBPRINT`) y genera manifiesto SHA-256 + SBOM.

## Reporte de vulnerabilidades

Reportar de forma privada al mantenedor del repositorio. Por favor incluir repro mínimo y versión afectada.
