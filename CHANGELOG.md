# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [2.1.20] - 2026-09-10

### Añadido
- Instalador de un solo `.exe` (`CA-O-Instalador.exe`, Inno Setup): asistente con licencia, destino e iconos; deriva al setup gráfico con ventana de progreso (sin consola) y firma de editor CA; si no hay payload local, descarga el paquete con streaming desde `latest`.
- Setup con ventana de progreso WinForms y reintentos al copiar.

### Corregido
- Instalación/actualización con la app o el servicio en marcha: se cierra la UI y se detiene/elimina el servicio antes de copiar (con remate forzoso de remanentes) — fin del fallo por DLL bloqueada; limpieza de la clave ARP pre-Inno duplicada.
- Apertura de la app tras instalar (error 740): lanzamiento con ShellExecute para que el UAC aparezca como debe.

## [2.1.19] - 2026-09-09

### Corregido
- Update que "descargaba pero cerraba la app sin aplicar": el handler tocaba UI tras `ConfigureAwait(false)` (hilo fondo) y el `catch` volvía a tocar UI → excepción no manejada → cierre. Ahora las continuaciones del flujo vuelven al hilo UI.
- Mark-of-the-Web: se desbloquea el ZIP y el árbol extraído (Zone.Identifier) para que SmartScreen no frene el instalador en silencio; el handoff exige ventana real del instalador (hasta 25 s) antes de ofrecer cerrar la app.

## [2.1.18] - 2026-09-09

### Añadido
- Nueva pestaña **Drivers**: inventario WMI (nombre, clase, fabricante, versión, fecha, firma), contadores (detectados/con problema/sin firmar/sin controlador), conflictos con significado en español y filtro por texto. Solo lectura.
- **Corrección por dispositivo**: Habilitar (código 22), Re-detectar (código 28) y Reinstalar (`pnputil`, con confirmación), siempre verificado contra la lista de problemas.
- **Originales del fabricante**: detección del equipo (fabricante/modelo/serie enmascarada), enlace oficial copiable (Dell por serie, Lenovo, HP, ASUS, Acer, MSI, Gigabyte, Samsung, Surface, Huawei, Xiaomi; catálogo Microsoft de fallback) e instalación de INF descargados con verificación.
- Nueva operación IPC `FixDriver` + `InstallDriver` (validadas) y comandos `pnputil` en allowlist con Instance ID/Ruta INF estrictos (695 tests).

## [2.1.17] - 2026-09-09

### Corregido
- Auto-update que "descargaba pero no aplicaba": la app verifica que el instalador siga vivo tras lanzarlo (si muere al abrir, informa con código de salida + ruta del log en vez de cerrar la app); el instalador arranca aunque falle el evento `Activated` (fallback a los 8 s, una sola vez) y su timeout sube de 10 a 30 min para discos lentos con antivirus.

## [2.1.16] - 2026-09-09

### Corregido
- `restart/recover-windows-explorer`: el Apply ya no lanza excepciones — cada paso (detener, esperar salida, relanzar) reporta su causa real (`kill-failed`, `exit-timeout`, `relaunch-failed`) en vez del genérico "Error inesperado"; si el shell no termina de cerrarse se falla honesto antes de relanzar.
- Menú colapsado: el pie de estado (sistema/servicio/build) se oculta a 52 px — antes se envolvía en una columna ilegible. El estado sigue en la barra superior.

## [2.1.15] - 2026-09-09

### Corregido
- Auto-update: la extracción muestra progreso real (archivo x de y, %) en vez de espera indeterminada de minutos; si el instalador no abre, la app ya no se cierra sola — pide confirmación y muestra la ruta manual del paquete.
- Extracción endurecida contra Zip-Slip (las entradas fuera del destino se rechazan).

## [2.1.14] - 2026-09-09

### Añadido
- Nuevo `recover-windows-explorer`: trae de vuelta barra y escritorio sin matar nada (idempotente, solo actúa si falta el shell). Botón "Recuperar Explorador" en Solucionar.

### Corregido
- Reinicio del Explorador: si la vía servicio falla o está desactualizada, la UI aplica fallback local en tu sesión (relanza `explorer.exe`); nunca más silencio + sin escritorio. Lógica del shell centralizada en `ExplorerShell` (espera de salida/arranque hasta 30 s).
- Ajustes muestra "Servicio desactualizado (x.y.z)" cuando el servicio instalado no coincide con la app, con botón para reinstalarlo: los fixes viven en el servicio y antes se ejecutaba el binario viejo sin avisar.

## [2.1.13] - 2026-09-09

### Corregido
- `disable-hibernate` ya no falla la verificación con CAO-TXN-003: Detect/Verify leen el estado vivo (`HKLM\...\Power\HibernateEnabled`) en vez del valor inyectado `powercfg /a`, que en el servicio siempre era `true` y provocaba rollback (la hibernación se reactivaba sola).
- `restart-windows-explorer` ya no deja sin escritorio: tras `taskkill` relanza `explorer.exe` en la sesión interactiva (token WTS + `CreateProcessAsUser` en `winsta0\default`) y falla con mensaje honesto + instrucciones (Ctrl+Mayús+Esc → explorer.exe) si no vuelve. Verify exige shell en sesión > 0.
- Servicio `CAO.Privileged` multi-instancia concurrente (4 despachos): antes una sola conexión secuencial hacía que cualquier operación lenta (restore point, reinicio explorer) dejara al resto sin poder ni conectar → `flush-dns-cache` y demás fallaban con CAO-IPC-007/004.

## [2.1.12] - 2026-09-08

### Corregido
- Actualización en la app ya no congela la ventana ("sin responder"): la descarga del paquete (~400 MB) corre fuera del hilo UI (`ConfigureAwait(false)` + `Task.Run`), el progreso se limita (≥0,5 % o ≥500 ms) para no inundar el dispatcher, y la extracción ZIP evita `Task.Run` anidado. `CheckAsync` tampoco continúa en el hilo UI.

## [2.1.11] - 2026-09-08

### Corregido
- DNS: el benchmark ya no mezcla proveedores entre primario y secundario (p. ej. nunca ISP `2000.21.200.10` + Cloudflare `1.1.1.1`). El secundario es siempre del mismo proveedor: `1.1.1.1→1.0.0.1`, `8.8.8.8→8.8.4.4`, `9.9.9.9→149.112.112.112`; ISP/red local → hermano mismo `/24` o un solo DNS si no hay hermano.
- `OptimizationEngine.SetDnsAsync` normaliza pares mezclados heredados (snapshots antiguos) antes de aplicar, con rollback exacto si la verificación falla.
- `IpcRequestValidator` valida 1–2 IPs en el payload `SetDns`.

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
