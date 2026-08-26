# Protocolo IPC v2

## Transporte
Named Pipe `CAO Privileged Service` → `CA-O.Privileged.v1` (constante `IpcConstants.PipeName`), byte-mode, una instancia, ACL: SYSTEM Full / Administrators RW / Interactive RW. **Conectar no es autorizar**: cada request pasa por `IPrivilegedCallerAuthorizer`.

## Envelope
```jsonc
// IpcRequest (Shared/IPC/IpcProtocol.cs) — ProtocolVersion = 2
{
  "protocolVersion": 2,
  "requestId": "guid",          // único; anti-replay
  "nonce": "hex-128",           // único; anti-replay
  "createdAtUtc": "...",        // expira en 30 s (MaxAge)
  "operation": 0..4,            // Apply|Revert|Verify|Detect|CaptureSnapshot
  "payload": { "$payload": "apply", "optimizationId": "disable-vbs" }
}
```
Límites duros: request ≤ 64 KB, response ≤ 256 KB (`IpcProtocol.Max*Bytes`).

## Payloads por operación (P1-12)
Cada operación tiene SU tipo; el validador rechaza cualquier cruce:

| Operation | Payload |
|---|---|
| ApplyOptimization | `ApplyOptimizationPayload` |
| RevertOptimization | `RevertOptimizationPayload` |
| VerifyOptimization | `VerifyOptimizationPayload` |
| DetectOptimization | `DetectOptimizationPayload` |
| CaptureSnapshot | `CaptureSnapshotPayload` |

## Respuesta
```jsonc
{ "accepted": true|false, "errorCode": "CAO-XXX-nnn"|null,
  "safeMessage": "...", "detailJson": "..." }
```

## Cadena de validación (en orden, antes del dispatch)
1. Deserialización JSON (→ CAO-IPC-002)
2. Versión de protocolo exacta (CAO-IPC-001)
3. RequestId ≠ empty · nonce ≤128 sin caracteres de control
4. Freshness: `now - createdAtUtc ≤ 30 s` (CAO-IPC-003)
5. Operación definida + payload del tipo correcto (CAO-IPC-005)
6. OptimizationId `[a-z0-9-]{1,80}` (schema)
7. Autorización por token (CAO-SEC-001..005)
8. Anti-replay: RequestId/nonce de un solo uso (CAO-IPC-004)
9. Allowlist de operaciones en el servicio

Garantías probadas en `tests/CA-O.Security.Tests`.
