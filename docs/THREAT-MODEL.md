# Threat Model — CA-O 2.0

## Scope
CA-O manipula Registry, Services, Power, Network y Boot (vía optimizaciones tipadas) desde un servicio SYSTEM. El modelo cubre canal IPC, ejecución y persistencia en `%ProgramData%\CA-O`.

## Trust Boundaries
1. **UI (Interactive, Medium IL, sin admin) → Service (SYSTEM, High IL)** vía Named Pipe `CA-O.Privileged.v1`.
2. **Service → Windows** (Registry API, SCM, powercfg, netsh, bcdedit tipado).
3. **Service → Filesystem** (`%ProgramData%\CA-O\{history.jsonl,snapshots/*,benchmarks}`).

## Actors
| Actor | Capacidad |
|---|---|
| Standard User | Lanza UI, conecta pipe, no puede elevar |
| Administrator (filtrado sin elevación) | Conecta pero token sin elevación → denegado CAO-SEC-003 |
| Administrator elevado | Único autorizado CAO-SEC-001 |
| Malicious Local User / Compromised Process en misma sesión | Intenta replay, payload malformado, flooding, command injection |
| Tampered Client / Tampered Data | Binario modificado, snapshots/history corruptos |
| Broken Service / Corrupted Snapshot | Crash mid-transaction, disk full, ACL dañada |

## Assets
- Privilegios SYSTEM, Registry `HKLM\*`, Services, Boot BCD, `history.jsonl` (auditoría), `snapshots/{txid}/` (rollback), `benchmarks/`, datos de máquina (fingerprint sin PII).

## Threats & Mitigations (STRIDE)
| Threat | Surface | Mitigación | Test |
|---|---|---|---|
| **Privilege escalation** vía pipe | Named Pipe ACL + impersonation | ACL: SYSTEM Full, Administrators RW, Interactive RW (connect≠authorize); `GetCallerIdentity` + `AdministratorsOnlyAuthorizer` verifica SID+grupo+ elevación; `IpcRequestValidator` versión exacta, `RequestId` GUID, `Nonce` ≤128 sin control chars, `CreatedAtUtc` ≤30s, `OptimizationId` regex `^[a-z0-9-]{1,80}$`; errores `CAO-IPC-001..005`, `CAO-SEC-003..005`. | `PrivilegedIpcSecurityTests`, `WindowsCallerInspectorTests`, `ReplayCacheTests` |
| **Replay** | Nonce/RequestId reuse | `ReplayCache` single-use por vida de servicio, `MaxAge 30s` (`IpcProtocol:17`). | `ReplayCacheTests` |
| **Spoofing / Token confusion** | Session/IL | `WindowsCallerInspector` extrae SID real, nombre, `IsAdminGroup`, `IsElevated`, `SessionId` del token impersonado (P1-8). | `WindowsCallerInspectorTests` |
| **Tampering** snapshots/history | Filesystem | Snapshots: `snapshot.json` SHA-256 en `integrity.json`, directorios inmutables `{txid}/`, validación post-escritura `SnapshotStateEquals`; History: hash chain `prevHash→hash` por línea JSONL, `GenesisHash`, detección corrupción/truncamiento. Rutas `CaOPaths:5` bajo `%ProgramData%\CA-O` endurecidas vía `harden-data-acls.ps1:14` (`icacls /inheritance:r`, SYSTEM F, Administrators M, Users RX). | `HistoryAndSnapshotPersistenceTests`, `HistoryHashChainTests`, `TransactionJournalRecoveryTests` |
| **Command injection / PATH hijacking** | `IPrivilegedCommandExecutor` | `ElevatedCommandCatalog` allowlist (powercfg/netsh exact tokens), `CommandPolicy.Resolve` rutas absolutas `%SystemRoot%\System32\*`, sin shell, sin `PATH` lookup; `bcdedit` no pasa por runner genérico; `SystemCommandGateway` valida timeout/auditoría. | `ElevatedCommandCatalogTests` |
| **DoS: oversized/malformed/flood** | Pipe | Límites `MaxRequestBytes 64KB` / `MaxResponseBytes 256KB` (`IpcProtocol:11`), timeout por conexión 15s (`PrivilegedPipeService:27`), `JsonSerializer` con `PropertyNamingPolicy camelCase` y `CAO-IPC-002` malformed; `Task` aislado por conexión. | `PrivilegedIpcSecurityTests` (oversized, malformed, flood) |
| **Rollback manipulation** | Snapshot colisión | Identidad primaria `TransactionId` GUID, nunca `OptimizationId`; `TX-A` y `TX-B` para misma optimization generan dirs distintos; `RollbackVerified` solo si `SnapshotComparison.ExactMatch`. | `RegistryExactRoundTripTests`, `OptimizationTransactionTests` |
| **Downgrade / Protocol confusion** | Version | `ProtocolVersion` 2 exacto, `AppVersion.Semantic 2.0.0` en manifest, UI y Service rechazan mismatch `CAO-IPC-001`; `ApplicationVersion`/`SchemaVersion`/`CatalogVersion` futuros a extender. | `IpcRequestValidator` + `CAO.Integration.Tests/DocumentationCodeConsistencyTests` |
| **Audit tampering** | `history.jsonl` | Hash chain + `RequestId`/`CallerSid`/`TimestampUtc` por entrada; corrupción → warning explícito, no `PASS`. | `HistoryHashChainTests` |
| **Cancellation leaving half-state** | Apply atomicity | `CancellationToken.None` durante `APPLY`/`VERIFY`, checkpoints solo antes de snapshot; `CancellationDeferred` reportado. | `CancellationSafetyTests` |

## Residual Risks & Acceptance
- **ETW attribution por driver**: muestreo %DPC/`%Interrupt` por contadores, no por traza kernel — documentado como limitación, no se presenta como `Confirmed root cause` sino `Observed contributor` (Fase 40).
- **WPR ETW collector**: requiere trazas kernel futuras; no se finge precisión.
- **Instalador sin certificado**: sin `CAO_SIGN_THUMBPRINT` artefactos quedan sin Authenticode (warning explícito en `sign.ps1`).
- **DLL search order / Binary replacement**: mitigado por ruta absoluta y servicio ` Demand start`, pero sin firma obligatoria en dev; aceptado hasta firma en release.

## Verification
- `scripts/verify.ps1` gates build+test+contracts; `dotnet audit` 0 vulnerables; `CodeQL` en CI; `Dependabot` semanal (revisión disciplinada, no auto-merge ciego per Fase 64).
- Instalador verifica `sc qc`/`qfailure` recovery `restart/5000/restart/15000`.

## Response
Reporte privado al mantenedor con repro + versión; no incluir `history.jsonl` con PII (no hay). Ver `SECURITY.md:55`.
