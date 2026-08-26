# Transacciones y Recuperación

## Fases (P0-11 orden obligatorio)

```
PRECHECK → COMPATIBILITY → SNAPSHOT → APPLY → VERIFY → COMMIT → POST-COMMIT BENCHMARK
```

- PRECHECK: gates específicos de la optimización.
- COMPATIBILITY: reglas de contexto (build, térmico, batería, SSD, anti-cheat).
- SNAPSHOT: estado capturado y persistido ANTES de mutar (crash-safe).
- VERIFY: siempre se ejecuta — también en irreversibles (P0-5).
- COMMIT: journal terminal; después llega el benchmark.
- BENCHMARK post-commit: su fallo NO invalida el cambio (eventos separados).

## Estados terminales del journal

Commit · RolledBack · Failed · CancelledBeforeApply · RecoveryCompleted.

Una transacción sin evento terminal es **INCOMPLETA** (spec 122): el proceso murió a mitad de operación.

## Rollback verificado (P0-6)

Tras revertir se recaptura el estado y se compara con el original mediante `SnapshotComparison`:

| Nivel | Significado | RollbackVerified |
|---|---|---|
| ExactMatch | existencia+kind+valor idénticos | ✅ true |
| Equivalent | valores iguales, kind heredado sin info | ⚠️ false |
| Mismatch / Unknown | difiere o indeterminado | ❌ false |

El snapshot persistido solo se elimina cuando la reversión fue ExactMatch.

## Irreversibles (P0-5)

Irreversible ≠ inverificable: se ejecuta VERIFY igualmente. Si falla o es Unknown → transacción Failed con CAO-VERIFY-nnn, sin rollback automático y con el snapshot conservado como evidencia.

## Recuperación (FASE 12)

Fuente de verdad: `ITransactionJournal` (`%ProgramData%\CA-O\transactions\{txid}.jsonl`).

1. Al arrancar, el servicio escanea journals sin evento terminal.
2. Por cada uno resuelve el snapshot por TransactionId y compara el estado vivo.
3. Decide: SafeToIgnore / AlreadyCommitted / RollbackRequired / RecoveryRequired / Corrupted / Unknown.
4. Decisiones serias bloquean nuevas mutaciones (engine consulta `HasPendingRecovery`, CAO-TXN-004).
5. Tras la reversión real, `MarkRecovered` cierra el journal con RecoveryCompleted.

## Auditoría

Cada fase emite a `history.jsonl` (cadena SHA-256, FASE 13) y al journal: TransactionId, caller SID/name (P1-13), resultado, rollbackAvailable y resumen de benchmark. Nunca secrets ni entrada personal.
