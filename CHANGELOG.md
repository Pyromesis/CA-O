# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [2.0.0] - 2026-08-25

Reconstrucción total como plataforma nativa Windows (WinUI 3 + .NET 10).

### Añadido
- Solución .NET 10 con cinco proyectos: Shared, Core, Infrastructure, Privileged (servicio Windows) y UI (WinUI 3 / Windows App SDK 2.4.x, Mica + NavigationView).
- Servicio privilegiado con IPC Named Pipes autenticado: ACL restrictiva, validación de esquema, nonces anti-replay, timeouts, lista blanca de operaciones y auditoría completa.
- Contrato transaccional completo por optimización: PRECHECK → SNAPSHOT → APPLY → VERIFY → COMMIT con rollback automático; snapshots persistidos antes de mutar (crash recovery).
- Motor de recomendaciones analyze-first con buckets Recommended/Optional/Experimental/SecuritySensitive/NotApplicable y puntuación compuesta 0–100 (sin claims numéricos sin medición).
- Perfiles Safe/Balanced/Gaming/Competitive/Privacy/Security/Maintenance/Expert/Custom consultando hardware, build, térmicas, batería y anti-cheats.
- Guarda anti-cheat: detección de Vanguard/EAC/BattlEye/FACEIT/Ricochet y bloqueo por defecto de cambios que reducen seguridad.
- Diagnósticos reales: red (latencia/jitter/pérdida), benchmark DNS multi-resolver, bufferbloat idle vs loaded, muestreo DPC/ISR por contadores, drivers con problema/firma, almacenamiento, térmicas ACPI, entrada (sin capturar contenido).
- Benchmark in-process reproducible CPU/memoria/disco con línea base persistida, comparación A/B y suelo de ruido del 3 %.
- Historial JSONL auditable en `%ProgramData%\CA-O\history.jsonl` (schema spec 74) tolerante a líneas corruptas.
- UI: Panel (salud/hallazgos/buckets), Analizar, Optimizar (tarjetas completas spec 79), Gaming (anti-cheat + Reflex/Anti-Lag guidance), Diagnóstico, Benchmark, Restaurar (snapshots), Historial, Ajustes (Expert mode con advertencia, tema claro/oscuro/sistema, es-ES/en-US).
- Suites de prueba: 144 tests (contratos de catálogo spec 92, transacciones, perfiles/bloqueos, seguridad IPC adversarial, persistencia, DNS).
- Scripts build/test/verify/sign/package/build-release + CI GitHub Actions con CodeQL y Dependabot.

### Eliminado
- Stack anterior Next.js/Electron/Node por completo (spec 2: cero dependencias web).

### Seguridad
- Ningún comando arbitrario puede llegar al servicio; catálogo estático probado contra inyección.
- Clasificación honesta de toda la evidencia: ninguna optimización queda con Evidence/Risk/Compatibility sin clasificar (contrato automatizado).
