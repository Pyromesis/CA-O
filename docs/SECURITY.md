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

### Cadena de suministro
- CodeQL + NuGet audit en CI (`.github/workflows/ci.yml`), Dependabot semanal.
- Release firma Authenticode con timestamp cuando existe certificado (`CAO_SIGN_THUMBPRINT`) y genera manifiesto SHA-256 + SBOM.

## Reporte de vulnerabilidades

Reportar de forma privada al mantenedor del repositorio. Por favor incluir repro mínimo y versión afectada.
