# CA-O 2.0

**CA-O** es una plataforma nativa de rendimiento, diagnóstico y optimización para Windows 11, construida en .NET 10 + WinUI 3 con un modelo de seguridad de privilegio mínimo.

## Qué es / qué no es

CA-O **es**: una herramienta evidence-driven que detecta hardware, mide el estado real del sistema, recomienda cambios clasificados por evidencia y riesgo, aplica cada cambio como una transacción con snapshot y verificación, hace benchmark A/B con suelo de ruido explícito y permite revertir todo.

CA-O **no es**: una colección de "registry tweaks" de Internet. No promete FPS sin medición. No desactiva seguridad para inflar números. No ejecuta comandos arbitrarios.

## Modelo de seguridad

- La UI (WinUI 3) corre **sin privilegios de administrador**.
- Las operaciones privilegiadas viajan por **Named Pipes autenticadas** (ACL restrictiva, validación de esquema, nonces anti-replay, timeouts) hacia `CA-O.Privileged`, un servicio Windows aislado.
- El servicio sólo ejecuta operaciones fuertemente tipadas de una lista blanca (`OptimizationId` + operación); no existe ruta para inyectar comandos.
- PowerShell no forma parte de la arquitectura: los comandos externos están en un catálogo estático (`ElevatedCommandCatalog`) probado contra inyección.

## Filosofía

1. **Diagnóstico primero**: nada se recomienda sin medir (hardware, red, térmico, drivers, DPC/ISR, seguridad).
2. **Evidencia clasificada**: cada optimización declara `EvidenceLevel` (Official/Vendor/Benchmark/Empirical/Heuristic/Unknown), riesgo, impacto de seguridad y compatibilidad.
3. **Analizar → buckets**, nunca "optimizar todo": Recommended / Optional / Experimental / Security Sensitive / Not Applicable.
4. **Transacciones**: PRECHECK → SNAPSHOT → APPLY → VERIFY → COMMIT; fallo = rollback automático. El snapshot se persiste *antes* de mutar (crash-safe).
5. **Anti-cheat primero**: Vanguard y otros anti-cheats bloquean por defecto cualquier cambio que reduzca seguridad.
6. **Benchmark honesto**: veredictos con suelo de ruido del 3%; "sin mejora medible" es un resultado válido.
7. **Rollback en tres capas**: punto de restauración de Windows, snapshots propios de CA-O e historial JSONL auditable (`%ProgramData%\CA-O\history.jsonl`).

## Arquitectura

```
CA-O.sln
├── src/CA-O.Shared          DTOs, contratos IPC, constantes, versionado
├── src/CA-O.Core            Dominio: catálogo, motor transaccional, perfiles,
│                            scoring, guardas anti-cheat, reglas de compatibilidad
├── src/CA-O.Infrastructure  Windows: WMI, contadores, DNS/bufferbloat, DPC sampler,
│                            persistencia (snapshots, history.jsonl), benchmark
├── src/CA-O.Privileged      Servicio Windows + servidor Named Pipes
├── src/CA-O.UI              Shell WinUI 3 (NavigationView, Mica) + páginas
└── tests/                   Core (contratos/transacciones/perfiles),
                             Infrastructure (persistencia/DNS), Security (IPC)
```

Detalle completo en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md); catálogo documentado por optimización en [`docs/OPTIMIZATION-CATALOG.md`](docs/OPTIMIZATION-CATALOG.md).

## Requisitos

- Windows 10 1809+ (objetivo: Windows 11), x64
- .NET SDK 10.0 (`global.json` fija la versión)
- Windows App SDK 2.4.x runtime para ejecutar la UI

## Desarrollo

```powershell
powershell -File scripts\build.ps1     # restore + build
powershell -File scripts\test.ps1      # tests completos
dotnet run --project src\CA-O.UI       # lanzar la app
```

## Release

```powershell
powershell -File scripts\verify.ps1        # gates: build+test+contratos
powershell -File scripts\build-release.ps1 # publica UI + servicio firmados con hashes
powershell -File scripts\package.ps1       # zip + SHA-256 + SBOM
```

El signing requiere `CAO_SIGN_THUMBPRINT` (certificado Authenticode en el almacén del usuario). Sin certificado, los artefactos se generan sin firmar y el script lo advierte.

## Instalación del servicio privilegiado

```powershell
# desde una consola de administrador, tras build-release:
powershell -File scripts\install-privileged-service.ps1
```

La UI funciona sin el servicio para todo lo de sólo lectura (diagnósticos, análisis, benchmarks); las operaciones de escritura lo requieren.

## Compatibilidad anti-cheat

CA-O detecta Vanguard, Easy Anti-Cheat, BattlEye, FACEIT y Ricochet mediante lectura de servicios/drivers. Con cualquier anti-cheat presente:

- Los cambios que reducen seguridad quedan **bloqueados por defecto**.
- No se garantiza compatibilidad futura de terceros; la detección es conservadora por diseño.

## Licencia

Proyecto privado. Ver `CONTRIBUTING.md` para el flujo de contribución.
