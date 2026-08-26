namespace CAO.Shared;

/// <summary>
/// Centralized, structured error codes (FASE 28). Each entry pairs a stable
/// code with a safe, localized-ready message; developer details stay out of
/// user-facing surfaces.
/// </summary>
public static class ErrorCodes
{
    // ---- Security / IPC ----
    public const string SecStandardUserDenied = "CAO-SEC-003";
    public const string SecUnknownCallerDenied = "CAO-SEC-004";
    public const string IpcProtocolVersionMismatch = "CAO-IPC-001";
    public const string IpcMalformedRequest = "CAO-IPC-002";
    public const string IpcRequestExpired = "CAO-IPC-003";
    public const string IpcReplayDetected = "CAO-IPC-004";
    public const string IpcPayloadSchemaInvalid = "CAO-IPC-005";
    public const string IpcRequestTooLarge = "CAO-IPC-006";
    public const string SecReadOnlyMode = "CAO-SEC-020";

    // ---- Transactions ----
    public const string TxnPrecheckFailed = "CAO-TXN-001";
    public const string TxnCompatibilityFailed = "CAO-TXN-002";
    public const string TxnApplyFailed = "CAO-TXN-003";
    public const string TxnRecoveryPending = "CAO-TXN-004";
    public const string TxnUnknownOptimization = "CAO-TXN-005";
    public const string TxnNotElevated = "CAO-TXN-006";

    // ---- Verification / rollback ----
    public const string VerifyFailed = "CAO-VERIFY-001";
    public const string VerifyUnknownState = "CAO-VERIFY-002";
    public const string RollbackFailed = "CAO-ROLLBACK-001";

    // ---- UI / diagnostics (Fase 50) ----
    public const string UiAnalyzeFailed = "CAO-UI-001";
    public const string UiDiagnosticsFailed = "CAO-UI-002";
    public const string UiBenchmarkFailed = "CAO-UI-003";
    public const string UiGamingScanFailed = "CAO-UI-004";
    public const string UiServiceUnavailable = "CAO-UI-005";
}
