# Hypertune — Investigación verificada y mapa integral de CA-O

**Documento de referencia** · Fecha: 2026-10-10 · **Revisión 3 — verificada y con las 9 decisiones cerradas** · Versión de CA-O documentada: **2.2.3** (commit `eb6c561`)
**Propósito:** consolidar (a) la investigación sobre [hypertune.gg](https://hypertune.gg/), (b) el estado real del repositorio `CA-O` verificado fichero a fichero, y (c) el estado del arte técnico de qué es *realmente* posible hacer en modo usuario en Windows 11 en 2026 — como base para diseñar e implementar la sección **Overclock**.

> **Documento vivo, con el diseño cerrado.** La Revisión 2 corrigió los errores del borrador; la Revisión 3 cierra las decisiones (§0.5) y reduce el alcance a lo que el hardware disponible permite validar de verdad. Todo dato del repositorio de la Parte 2 lleva el comando que lo reproduce. Toda afirmación externa lleva fuente con fecha.

---

# PARTE 0 — VEREDICTO DE LA VERIFICACIÓN

## 0.1 Método

| Qué | Cómo |
|---|---|
| Hechos del repositorio | Lectura directa + conteos reproducibles (`find`/`grep`/`wc -l`) sobre el árbol de trabajo, excluyendo `bin/` y `obj/` |
| Compilación declarada | `dotnet build CA-O.sln -c Release` ejecutado: **succeeded, 0 warnings, 0 errors, 1m40s** |
| Suite declarada | `dotnet test CA-O.sln -c Release` ejecutado: **0 fallos en las 6 suites, 1314 tests** (la baseline documentada de 1278 es la del commit 2.2.3 — ver §2.14) |
| Hechos de Hypertune | Búsqueda web + lectura de fuentes primarias (sitio, prensa, Trustpilot, YouTube, X) con fecha y URL |
| Viabilidad técnica | Documentación oficial de NVIDIA/AMD/Intel/Microsoft, avisos de seguridad, y fuentes de ingeniería de anti-cheat |

## 0.2 Errores materiales del borrador (Revisión 1)

### Bloqueantes — invalidan el diseño tal como está escrito

| # | Afirmación del borrador | Realidad verificada | Impacto |
|---|---|---|---|
| **B1** | §6.16: `BenchmarkPolicy.cs` y `BenchmarkAnalyzer.cs` son *"el score heurístico, exactamente lo que se rechaza"* y `BenchmarkModels.cs` son *"DTOs del score"* → **borrar** | **Falso.** `BenchmarkPolicy` (18 líneas) define el **suelo de ruido del 3 %**, las 3 trials medidas y el warmup: es la fuente única de verdad metodológica. `BenchmarkAnalyzer` (78 líneas) calcula percentiles de frame-time (avg FPS, 1 % low, 0.1 % low, P99) y el veredicto A/B. **En CA-O no existe ningún score 0–1000.** El `CpuScore` es una métrica de throughput (ops/s) reportada como delta %. El anillo 0–100 de salud pertenece a Dashboard/Analyze, no a Benchmark | El borrador propone **amputar el motor de medición honesto que su propio §6.8 necesita**. Es una autocontradicción: §6.8 mide el delta con `SystemBenchmarkRunner`, y §6.16 borra la política y el analizador que lo hacen defendible |
| **B2** | §3.3: *"El propio Hypertune usa el XTU SDK precisamente para evitar [el driver kernel]"* | **Falso.** Hypertune **sí carga drivers kernel-mode de fabricante** (`/3p/`, movidos a `SYSTEM32`), con `AUTOSTART`, **y no se desinstalan al desinstalar Hypertune**. Además modifica `HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU` para **desactivar Windows Update** | La premisa competitiva del documento entero está invertida. La diferenciación de CA-O («anti-cheat-safe por construcción, sin driver») es **más fuerte** de lo que el borrador creía, pero por el motivo contrario del que afirma. Y §1.4/§1.8 pierden su base |
| **B3** | §3.2 y §6.4: GPU AMD vía `amdocl64.dll` (ADL2) | **Falso.** `amdocl64.dll` es el **ICD de OpenCL**, no ADL. ADL es `atiadlxx.dll` (x64) / `atiadlxy.dll` (x86), y su superficie pública es de display, sin entrada de overdrive publicada. La vía pública *soportada* de tuning de AMD es **ADLX** (`adlx.dll`, v2.0 sep-2026), con `IADLXManualPowerTuning::SetPowerLimit` e `IADLXManualGraphicsTuning2::SetGPUMaxFrequency` documentadas | El backend AMD estaba diseñado sobre una DLL equivocada. ADLX además es **mejor** que ADL2: está documentado y versionado |
| **B4** | §2.11 + §6.5 + §6.12: telemetría térmica 100 % WMI; el motor no aplica OC «sin sensor térmico» | **No es alcanzable para CPU.** No hay API de Windows en modo usuario para temperatura de CPU: `MSAcpi_ThermalZoneTemperature` devuelve «not supported» en la mayoría de equipos o la zona equivocada (ambiente de placa, no núcleo). LibreHardwareMonitor / OpenHardwareMonitor / HWiNFO **usan un driver kernel firmado** (WinRing0 → PawnIO desde 2025-09-16). Intel Power Gadget está **descatalogado** (INTEL-SA-01037). Sí es alcanzable temperatura de **GPU** vía NVAPI/ADLX | El guardarraíl térmico del §6.5 (decisión 2, margen de 15 °C) **no es implementable para CPU**. La regla «sin sensor térmico no hay OC» es honesta pero implica que **el OC de CPU queda desactivado en la práctica en la mayoría de equipos** — algo que el documento debe decir en voz alta |
| **B5** | §6.6: `TuningPhase { Preparing \| Tuning \| Verifying \| Committed \| Reverted }` y fase terminal `OverclockCommitted` | **Dos violaciones de fuente única de verdad.** Ya existen `TransactionPhase` (`Shared/Contracts/Lifecycle.cs:75`, 18 miembros) y `TransactionEvent.IsTerminal` (`Shared/DTO/TransactionEvent.cs:26`), cuyo conjunto terminal es exactamente `{Commit, RolledBack, Failed, CancelledBeforeApply, RecoveryCompleted}`. `Incomplete()` (`Core/Rollback/ITransactionJournal.cs:31`) marca incompleta toda transacción cuyo **último evento apilado no sea terminal** | Con la propuesta del borrador, **cada run de OC se reportaría como incompleto para siempre** y bloquearía nuevas mutaciones (`HasPendingRecovery` → `CAO-TXN-004`). Es exactamente el bug CAO-BUG-2026-10-06 que 2.2.3 acaba de cerrar |
| **B6** | §6.3: `enum EvidenceTier { Official, Vendor, Benchmark, Empirical, Heuristic, Unknown }` nuevo en `Core/Overclock/` | **Duplicado.** `EvidenceLevel` ya existe con esos seis valores exactos en `Shared/Enums/Enums.cs:33`, con **123 consumidores** en `src/`. Igual pasa con `KnobRisk { Safe, Moderate, High }` frente a `RiskLevel { Safe, Low, Moderate, High, Critical }` | Es literalmente la regla dura que el propio documento cita (`TimeoutProfile`, «bug real ocurrido dos veces»): duplicar una tabla de constantes en otro sitio |
| **B7** | §6.6: tras un cuelgue, al arrancar **revertir automáticamente** al último bueno | **Contradice una decisión deliberada de la release anterior.** 2.2.3 (CHANGELOG, `[2.2.3] - 2026-10-07`, tres días antes de este documento): *«El servicio revisa al arrancar si hay recuperaciones pendientes y lo deja en su log, **sin revertir: revertir es mutar el sistema y debe ser decisión del usuario, no un efecto secundario de un reinicio**»*. La recuperación existe como operación IPC `RecoverTransaction` con botones **Revertir** / **Descartar** en Restaurar | El invariante central del §6.6 revierte una decisión de producto cerrada. Puede defenderse un **caso excepcional** (un OC inestable puede impedir alcanzar la UI), pero debe argumentarse explícitamente, no asumirse |
| **B8** | §6.11: el flujo va de `OverclockViewModel` → `OptimizationEngine.ApplyAsync` | **Falta el pipe.** Toda mutación en CA-O cruza `UI → Named Pipe (ACL+nonce) → servicio SYSTEM → Core`. Añadir operaciones de OC exige nuevos miembros de `PrivilegedOperationKind` (hoy 18, `Shared/IPC/IpcProtocol.cs:21`), tipos de payload nuevos, actualización del validador y de los tests de seguridad, y respetar los límites duros: **request ≤ 64 KB, response ≤ 256 KB**, versión de protocolo con coincidencia exacta (`CAO-IPC-001`) | El flujo de extremo a extremo del borrador es inimplementable tal como se dibuja. Además el borrador no dice **en qué proceso se escribe el reloj de la GPU** — ver B9 |
| **B9** | §6.2: `CAO.Privileged` es la frontera SYSTEM y `TuningEngine` escribe vía backends | **Ambigüedad crítica no resuelta.** El servicio corre en **sesión 0**; el tuning de GPU por NVAPI/ADLX depende del estado de display de la **sesión interactiva** del usuario. Y `ArchitectureDependencyTests.UiNeverSpawnsProcessesOrHostsThePipe` prohíbe `Process.Start` y `using System.Management` en `CA-O.UI` (con 3 excepciones nombradas y justificadas), por lo que «lanzar un helper» tampoco es gratis | Sin decidir esto, el diseño no se puede implementar. Es la decisión arquitectónica número uno que el borrador omite |
| **B10** | §6.6: checkpoint en `C:\ProgramData\CA-O\overclock\runs\<runId>.json` con `History` completo | Ruta literal, en un repo cuyo contrato es `CaOPaths` (`Shared/CaOPaths.cs`) y cuyas escrituras son atómicas (`tmp → flush → Move`) con mutex de un solo escritor. El CHANGELOG de 2.2.3 documenta precisamente un bug de mutex de snapshots que perdía la exclusión en silencio. Y `History` sin cota crece sin límite | Ruta literal prohibida por convención; falta política de escritura atómica, exclusión mutua y cota de tamaño. El journal ya creció sin límite una vez (88 de 98 entradas) |

### Errores de hecho — no bloquean, pero envenenan la base

| # | Afirmación del borrador | Realidad verificada |
|---|---|---|
| **F1** | §2.2: «12 proyectos» con tabla que incluye 8 de `src`, y nota: *«`CA-O.Uninstaller`, `CA-O.InstallerGui` y `CA-O.Setup` no están en `CA-O.sln`»* | `CA-O.sln` tiene **12 proyectos**: 6 de `src` (**`CA-O.Uninstaller` SÍ está en la solución**) + 6 de test. Fuera de la solución solo están **`CA-O.InstallerGui`** y **`CA-O.Setup`** |
| **F2** | §2.2: conteos de ficheros y líneas por proyecto (UI 181 cs / 72 xaml / 54.362 líneas; Core 173 / 14.469; …) | **No reproducibles.** Los conteos de `.cs` corresponden a un recuento que **incluye `obj/`** (UI: 181 con obj, 49 sin obj; Core: 173 con obj, 161 sin obj), pero las líneas no cuadran con ninguno de los dos métodos (UI con obj = 40.690, no 54.362; Core con obj = 16.221, no 14.469). Cifras medidas abajo, con comando |
| **F3** | §2.2: «~86.000 líneas de producción · ~10.700 líneas de test» | **~42.100** de producción y **~11.600** de test (medido, sin `bin`/`obj`). El 86.000 está inflado ~2× |
| **F4** | §2.5: `OPTIMIZATION-CATALOG.md` 320 líneas, `ARCHITECTURE.md` 82, `IPC_PROTOCOL.md` 41, `SECURITY.md` 41, `THREAT-MODEL.md` 41, `TRANSACTIONS.md` 33; README «20 secciones» | Reales: OPTIMIZATION-CATALOG **431**, ARCHITECTURE **109**, IPC_PROTOCOL **48**, SECURITY **65**, THREAT-MODEL **49**, TRANSACTIONS **50**. README tiene **867 líneas y 23 cabeceras `##`** |
| **F5** | §2.8: `Benchmark.Tests` = «17 tests (`BenchmarkStoreTests`, `BenchmarkStatisticsTests`, `BenchmarkTrialsTests`, `BenchmarkWorkloadTests`)» | **5 ficheros**, no 4: falta **`FrameCaptureTests.cs`** (2 tests). El total de 17 sí es correcto (5+3+3+4+2) |
| **F6** | §2.11: «Únicos paquetes NuGet de toda la solución» con 8 paquetes | Faltan los 3 de test: `Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2. Son **11** |
| **F7** | §2.12: «existe `OptimizationEngine.BenchmarkAsync`» | Es **`IOptimization.BenchmarkAsync`** (`Core/Interfaces/IOptimization.cs:88`), invocado desde `OptimizationTransaction.cs:327`. `OptimizationEngine` no declara ese método |
| **F8** | §6.2 y Parte 7: *«`ArchitectureDependencyTests.cs:40` prohíbe `using CAO.Privileged` en proyectos de test»* | La línea 40 es correcta **en número** pero el alcance es otro: afirma que **ficheros de `CA-O.UI`** no contienen `using CAO.Privileged` (dentro de `UiNeverSpawnsProcessesOrHostsThePipe`). La regla que §6.2 realmente necesita —Core no referencia Infrastructure— es **`CoreNeverReferencesUiOrInfrastructure`, línea 52** |
| **F9** | §2.15: actualizar `CHANGELOG.md` L100, L301, L303 | Esos son **notas de release históricas** (`[2.1.33]`, `[2.0.0]`) que describen lo que se entregó entonces. Reescribirlas falsifica el historial. Se **añade una entrada nueva**; no se toca el pasado |
| **F10** | §7/§2.15: la lista de superficies a actualizar | Incompleta. Falta lo más importante: **`README.md:64` y `docs/ARCHITECTURE.md:7` declaran los cuatro pilares `DIAGNOSE → OPTIMIZE → BENCHMARK → RECOVER`**. `BENCHMARK` es un **pilar fundacional con nombre propio**, no una página. También falta `README.md:48`/`:690` (modo solo lectura = «diagnóstico + benchmark»), `:138` (orden de fases), `:572`, `:610`, `:624-625`, `:634-635` |
| **F11** | §1.3/§1.5/§1.6: cifras y citas de Hypertune | Ver Parte 1: precios de dos eras mezclados, `$119.88` inexistente, las ganancias son **de Hypertune**, no medidas por Tom's Hardware, la cita del fundador está reescrita, el rango 0–1000 no tiene fuente, el preset «Off» no está atestiguado, el equipo del review es un 14700KF + RTX 5070 Ti (no un 3080 prohibitivo), y falta la fuente independiente más importante (el desmontaje de mayo 2026) |
| **F12** | §3.1: «GPU NVIDIA — viable HOY», power limit incluido | **Matizable y frágil.** Los setters de OC de NVAPI son **API privada no documentada**; NVIDIA afirma oficialmente que *«NVAPI SDK currently does not provide full GPU overclocking and undervolting support»* (foro NVIDIA, 2025-08-19). Y el **driver 615.56 (2026-08-30) bloqueó el control de power limit en RTX 50**, provocando pantallas negras |
| **F13** | §6.5: sonda WHEA vía `Win32_WHEAHardwareError` (WMI) | **Esa clase no existe.** WHEA se reporta por eventos: `Microsoft-Windows-WHEA-Logger` (System, IDs 17/18/19/47) y `Microsoft-Windows-Kernel-WHEA/Errors` |
| **F14** | §6.5: bugcheck = evento 1001 de origen `BugCheck` | El ID es correcto; el proveedor moderno es **`WER-SystemErrorReporting`** / `Microsoft-Windows-WER-SystemErrorReporting`. `BugCheck` es el nombre legado de Windows 7. Complementario: `Kernel-Power` 41 para apagado no limpio |
| **F15** | §6.5: métrica `ClockDrift` para detectar inestabilidad de OC | **No sirve como está descrito.** El QPC/`Stopwatch` **no se ve afectado** por cambios de frecuencia si el TSC es invariante (documentado por Microsoft). Además `\Processor Information\Processor Frequency` es poco fiable. La medición correcta es `% Processor Performance` × `MaxClockSpeed` |
| **F16** | §6.7: «Detectar perfiles XMP/EXPO presentes — lectura SMBIOS vía WMI (`Win32_PhysicalMemory`)» | **SMBIOS Type 17 no tiene campo XMP/EXPO.** Expone velocidad nominal vs configurada, voltaje, tipo, fabricante y part number. Los perfiles XMP/EXPO viven en la EEPROM SPD y leerlos exige SMBus (ring 0). La fila «Real, con matiz» sobrevende |
| **F17** | §9.3: «Invocar la skill `writing-plans`» | Esa skill no existe en el catálogo de este entorno. El proceso real del repo es el que dejó `docs/superpowers/specs/`, `docs/superpowers/plans/` y `.superpowers/sdd/<fecha>-<slug>/` (brief/report/review/rereview por tarea). Además **`docs/superpowers/` y `.superpowers/` están en `.gitignore`** |

## 0.3 Qué acertó el borrador

No todo era erróneo; esto se conserva íntegro porque es sólido:

1. **§2.9 `RouteTable.cs`** — 35 líneas, `["benchmark"]` en L17, `Order` en L27-31 con `"benchmark"` en L30, y el contrato «todo tag de navegación resuelve a un tipo de página existente». **Verificado exacto.**
2. **§2.10 `TimeoutProfile.cs`** — 61 líneas, los cinco timeouts exactos, los 15 ids de `HeavyOptimizationIds` exactos, y la regla dura derivada de un bug real. **Verificado exacto.** El detalle del comentario del fichero (bug de espejos manuales que caducaban a 90 s y 60 s frente a los 21 min reales) está confirmado en el CHANGELOG de 2.2.3.
3. **§2.3 configuración de compilación** — `global.json` SDK 10.0.400 `latestFeature` `allowPrerelease:false`; `Version.props` = 2.2.3 en las cinco propiedades; `Directory.Build.props` con LangVersion 13.0, Nullable, ImplicitUsings, analyzers latest-recommended, el `NoWarn` completo y la nota de que nullability/CA2016/xUnit siguen siendo errores. **Verificado exacto.**
4. **§2.4 directorios por proyecto** — las 16 carpetas de Core, las 13 de Infrastructure, las 12 páginas y los 11 ViewModels. **Verificado exacto.**
5. **§2.11 ausencia de LibreHardwareMonitor/OpenHardwareMonitor** — 0 resultados en todo el repo. **Verificado.**
6. **§2.12 los 14 componentes reutilizables** — los 14 existen, con rutas correctas. **Verificado.**
7. **§2.13 cultura de evidencia** — `README.md:452` es exactamente la línea citada. **Verificado.**
8. **§2.14 los 1278 tests** — coincide con `docs/ARCHITECTURE.md:99-109` y `README.md:752-760`, y la suma por suites cuadra (786+248+60+58+17+109 = 1278). **Verificado.**
9. **§2.7 «93 optimizaciones»** — 93 filas en la tabla maestra del README y 65/15/4/9 en las proyecciones (65+15+4+9 = 93). **Verificado.**
10. **§6.15 «todas las pruebas del motor sin hardware»** — el principio es correcto y es la decisión de diseño más valiosa del documento.
11. **§6.8 «el overclock no es aditivo → no se atribuye delta por knob»** — rigurosamente correcto y contracultural frente a Hypertune. Se conserva como regla dura.
12. **La decisión de no duplicar Network / Windows / Cleaning** — correcta: esas superficies ya existen con transacción, journal y revert.

## 0.4 Qué cambia esta revisión (3)

| Parte | Cambio |
|---|---|
| 0 | Veredicto de verificación con método y errores; **nueva §0.5: registro de decisiones cerradas (D1-D9)** |
| 1 | Reescrita con fuentes fechadas; §1.4 sobre los drivers kernel de Hypertune; corregidos precios, citas, cifras y atribuciones |
| 2 | Reescrita con cifras **medidas** y el comando que las reproduce; §2.15 (contratos invariantes); §2.16 reducida a lo mínimo por D5; §2.17 con el estado real del tutorial |
| 3 | Reescrita: NVIDIA matizada, AMD corregida a ADLX, Intel sin SDK redistribuible, memoria limitada a lo real, y la tabla de «qué es posible en modo usuario»; **nueva §3.9: qué NO se puede validar con el hardware disponible** |
| 4 | Decisiones 1 y 2 conservadas y matizadas; **D3-D7 ahora cerradas con la elección y su coste** |
| 5 | Enfoque 2 conservado; contratos corregidos para reutilizar enums existentes; §5.3 (ejecución) y §5.4 (IPC) **decididas** |
| 6 | Corregidos §6.3, §6.5, §6.7, §6.12, §6.16; §6.6 decidida; §6.13 con **prerrequisito P0**; nuevas §6.18 (licencias), §6.19 (riesgos), §6.20 (criterios de muerte) |
| 7-9 | Ampliadas con las reglas y el flujo reales del repo; §9 refleja las decisiones cerradas |

## 0.5 Registro de decisiones — estado final

> Todas las decisiones de diseño están **cerradas**. Cualquier cambio posterior a esta tabla requiere una revisión explícita del documento.

| # | Decisión | Elección | Fecha | Coste asumido |
|---|---|---|---|---|
| **D1** | Cómo manejar el OC de CPU | **Opción D′** (revisada de la Opción D original): detección de XTU/IPF + flujo guiado, **más tuning por política** en modo usuario; arquitectura lista para un driver firmado sin reescribir. **Nunca «+X MHz» de CPU** | 2026-10-10 | El OC real de CPU no existe en CA-O. Se renuncia a la función y se conserva la honestidad |
| **D2** | Alcance de la sección | **Opción B**: CPU + GPU + Memoria + perfiles de juego. Network / Windows Features / Cleaning **no se duplican**. El **motor de medición tampoco** se reimplementa: se consume | 2026-10-10 | Ninguno; evita duplicar cuatro superficies ya superiores |
| **D3** | **En qué proceso se ejecuta la escritura** | **UI elevada para el OC de GPU** (sesión interactiva); **servicio SYSTEM** para política de CPU, journal, checkpoints y puerta anti-cheat. El OC de GPU es de **sesión** por diseño | 2026-10-10 | El OC de GPU no sobrevive al cierre de sesión; persistirlo exigiría un driver |
| **D4** | Recuperación tras cuelgue | **Primer arranque sucio: avisa y ofrece revertir. Segundo consecutivo: revierte al último bueno y marca `Interrupted`.** Respeta la decisión de 2.2.3 y cubre el caso en que el usuario no puede intervenir | 2026-10-10 | Automatismo acotado a un único escenario; requiere contador de arranques sucios en el checkpoint |
| **D5** | Qué pasa con la sección Benchmark | **Añadir `Overclock` como página y tag nuevos; conservar `Benchmark` intacto** (página, tag, motor y pilar del README). Benchmark mide, Overclock ajusta | 2026-10-10 | Dos secciones coexisten. **A cambio: cero migración, cero riesgo documental, cero pérdida de tests** |
| **D6** | Los 17 tests de `Benchmark.Tests` | **No se retiran ni se mueven.** Prueban el motor de medición, que se conserva | 2026-10-10 | Ninguno |
| **D7** | Driver kernel propio | **Fuera del alcance actual**, con las seis puertas de decisión escritas (§6.14). Hoy la respuesta es no: la blocklist por defecto y el bloqueo por firmante de FACEIT lo hacen inseguro incluso firmado | 2026-10-10 | El OC real de CPU queda permanentemente fuera salvo cambio de decisión |
| **D8** | Orden de entrega | **Fase 1 de solo lectura** (no depende de API privada, demostrable en cualquier máquina); **escritura de GPU en la Fase 3** | 2026-10-10 | El valor «espectacular» llega más tarde; a cambio, el primer incremento no puede romperse por un driver de NVIDIA |
| **D9** | Backend AMD sin hardware para validarlo | **Se implementa, pero se declara sin validar en hardware real**, con su `EvidenceLevel` honesto y la UI diciéndolo. No se presenta como soportado | 2026-10-10 | La Fase 6 se entrega sin prueba de humo real. Es coherente con la regla de que un componente no validado no puede presentarse como soportado |

**Prerrequisito P0 (no es una decisión de diseño, es una dependencia):** cerrar el tutorial antes de empezar. Ver §6.21.

---

# PARTE 1 — INVESTIGACIÓN DE HYPERTUNE.GG

> Todas las afirmaciones de esta parte llevan fuente. `CONFIRMADO` / `PARCIAL` / `REFUTADO` / `NO VERIFICABLE` según lo que sostienen las fuentes, no según el marketing.

## 1.1 Identidad, negocio y posicionamiento

| Dato | Valor | Estado |
|---|---|---|
| Producto | **HYPERTUNE** | CONFIRMADO |
| Razón social | **COMPREADY Inc.**; Hypertune es una **filial** de Compready | CONFIRMADO |
| Fundación | 2022 (Copeland y Schappy son *founding partners*; Copeland fue entrenador de Valorant en TSM como «Apex») | CONFIRMADO |
| Copyright | 2026 COMPREADY Inc. | CONFIRMADO |
| Tagline | «Ultimate PC Optimization for Gaming & Esports» — es el `<title>` de la página. El `h1` es distinto: «Boost your gaming performance with one click» | CONFIRMADO |
| Navegación | `Pricing` · `Technology` · `Tutorial` · `Expert Tuning` · `Log In` · CTA **"Boost my performance"** | CONFIRMADO |
| Beta | 8 meses de beta abierta con **60.000+ participantes** | CONFIRMADO (el borrador lo omitía) |

**Claims de marketing, con su estado real:**

| Claim | Dónde vive | Estado |
|---|---|---|
| «Trusted by Riot, EAC, BattlEye, Valve» | Home | **PARCIAL** — la página `/tutorial` dice en su lugar «Trusted by **Vanguard, FACEIT, EAC, and BattlEye**». El texto cambia según la página |
| «Operates at the hardware level, never touching game files, codes or directories» | Home | CONFIRMADO literal |
| «We collaborate closely with chipmakers» | Home | CONFIRMADO; corroborado por el respaldo público de **Dan Ragland** (Intel Senior Principal Engineer) |
| 25.000+ PCs · 80+ pros · 10+ orgs | **`/expert-tuning`**, no la home | CONFIRMADO (el borrador lo situaba en la home) |
| Comunidad 10.000+ en Discord | Home/tutorial/technology | CONFIRMADO como copy del sitio; sin recuento independiente |

**Posicionamiento:** HYPERTUNE es Overclock3D y el tuning manual de COMPREADY reempaquetados como suscripción de marca blanca, con Intel como socio. El hueco de mercado es real: el usuario que quiere más FPS no entiende de OC y las herramientas existentes le intimidan. Lo que el borrador no vio es que **el producto no es «limpio»**: ver §1.4.

## 1.2 Modelo de negocio — dos eras de precios

**Solo suscripción. No hay tier gratuito. Login obligatorio.**

El borrador fusionó dos épocas. Hay que citarlas por separado:

| Época | Mensual | Anual | Precio de lista | Fuente |
|---|---|---|---|---|
| **Lanzamiento (sep-2026)** | 9,99 USD | 59,99 – 69,99 USD (la prensa no coincide) | **119,99 USD** | [VideoCardz 2026-09-10](https://videocardz.com/newz/intel-backed-hypertune-software-automatically-overclocks-cpus-and-gpus), [TechPowerUp 2026-09-09](https://www.techpowerup.com/352525/intel-backed-hypertune-pc-overlocking-tool-reportedly-boosts-fps-by-up-to-60), [Club386 2026-09-10](https://www.club386.com/hypertune-overclocking-tool), [TweakTown](https://www.tweaktown.com/news/113480/intel-backs-hypertune-overclocking-tool-that-reportedly-boosts-fps-by-up-to-60-percent/index.html) |
| **Actual (oct-2026)** | **3,99 USD** | **23,99 USD** el primer año, luego **27,99 USD/año** (lista 47,88) | — | [hypertune.gg/pricing](https://hypertune.gg/pricing) |

> **`$119.88` no existe en ninguna fuente.** El borrador lo inventó o lo maltranscribió (`$119.99`). El «banner de 23,99 → 27,99» que el borrador describe como «gancho de primer registro» **es el precio actual del checkout**, no un descuento de lanzamiento.

| Afirmación del borrador | Estado |
|---|---|
| Trial de 7 días «sin tarjeta» | **REFUTADO.** «Using the software requires providing billing information prior to downloading the trial» ([@vxunderground, 2026-05-20](https://x.com/vxunderground/status/2056899139099619490)). Hay casos reportados de cobro inmediato al registrarse ([XenoSpectrum](https://xenospectrum.com/en/intel-backed-hypertune-pc-overclocking)) |
| Money-back de 7 días | CONFIRMADO como copy del sitio |

**Trustpilot: 4,7/5 con 32 reseñas** — CONFIRMADO a fecha 2026-10-10 ([trustpilot.com/review/hypertune.gg](https://www.trustpilot.com/review/hypertune.gg)). Matices que el borrador no recogió y que importan: perfil **reclamado en sep-2026**, solo **12 reseñas en 12 meses**, **perfil fusionado** (buena parte de las reseñas son del servicio manual previo de COMPREADY, con nombres de técnicos), y Trustpilot advierte de que las reseñas «may not be representative». El conjunto visible es todo de 5 estrellas.

## 1.3 Funciones anunciadas vs. realidad verificada

| Función anunciada | Realidad comprobada |
|---|---|
| «Powered by official Intel and AMD toolkits» | CONFIRMADO: construido sobre el **Intel XTU SDK**, en colaboración con Intel ([Tom's Hardware, 2026-09-09](https://www.tomshardware.com/pc-components/cpus/intel-backed-auto-overclocking-tool-hypertune-optimizes-individual-systems-not-test-profiles-tool-claims-fps-improvement-of-up-to-60-percent-on-intel-based-systems)). El fundador: *«**not trying to build a tool for overclockers**»* — esta es la cita literal; el borrador la paráfrasearía como «we're not trying to make…». **La firma del artículo no es verificable**: la página queda tras un muro de registro/hCaptcha en toda lectura |
| «Smart overclocking profile» (GPU) | **PARCIAL.** Los presets **Smart** y **High** están atestiguados por el tutorial oficial (*«turn this one on to either smart or high»*) y por el teardown independiente (*«You got smart and high»*). **Un estado «Off» distinto no está atestiguado por ninguna fuente** que haya encontrado; el borrador lo afirmaba |
| «Faster lower-latency RAM settings» | CONFIRMADO. Cita casi literal del teardown: *«if you click on memory, I thought there was going to be memory overclocking. **This is just normal XMP**, right? 6400 CL 32»* |
| «System Health Dashboard» / score | **PARCIAL.** Los valores **373** (antes) y **649** (después) están confirmados en el vídeo, con el comentario del revisor: *«Don't know what that means»*, y califica los puntos de **«cosmetic»**. **El rango 0–1000 no lo afirma ninguna fuente: NO VERIFICABLE.** El borrador lo daba por hecho |
| «Benchmark Leaderboards» | Solo dentro del Game Hub, sin valor de referencia externo |
| «Stability» | Stress test manual, sin recuperación automática |
| «Restore» | Puntos de restauración de Windows periódicos; el usuario restaura el último. No es un sistema de transacciones |
| «Deep-Level PC Cleaning» | Desbloqueo de operaciones de limpieza estándar |
| «Game Hub» | Cards por juego con portada, botón `Open` y toggle por título. Es **la fuente de la mayor parte de la ganancia** en el caso del 3080 (§1.6) |

## 1.4 La corrección más importante: Hypertune SÍ usa drivers kernel

> El borrador afirmaba (§3.3) que *«el propio Hypertune usa el XTU SDK precisamente para evitar [el bloqueo por anti-cheat de un driver kernel]»*. **Es falso, y cambia el argumento competitivo entero.**

Desmontaje independiente de mayo de 2026, anterior al lanzamiento comercial ([@vxunderground, 2026-05-20](https://x.com/vxunderground/status/2056899139099619490)):

- **Carga drivers kernel-mode de fabricante** (AMD e Intel) según el perfil de hardware.
- Esos drivers quedan configurados en **`AUTOSTART`**: arrancan **aunque Hypertune no esté corriendo**.
- **Desinstalar Hypertune no desinstala esos componentes kernel.** Vienen por defecto en el instalador en `/3p/` y se mueven a `SYSTEM32`.
- Además, Hypertune **modifica `HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU` para desactivar Windows Update**. Copeland lo confirmó en respuesta pública: *«Adding back windows updates was on the team whiteboard this week»*.

El mismo análisis concluye, con ecuanimidad: *«Is this malware? No. Is this slop? Probably not, no. Does this actually improve FPS? Yes, unironically… They're achieving this in a legitimate way by working with AMD and Intel with actual SDK documentation»*. De modo que **no hay fraude técnico**; lo que hay es un producto que instala drivers persistentes que sobreviven a la desinstalación y desactiva las actualizaciones del sistema.

**Consecuencias para CA-O:**

1. La ventaja «anti-cheat-safe por construcción, **sin ningún componente kernel**» es **real y más fuerte** de lo que el borrador creía — pero debe formularse como «somos los únicos sin driver», no como «usamos los mismos SDK y por eso estamos limpios».
2. La ventaja «gratis, sin suscripción, sin login, sin telemetría» sigue en pie.
3. **No se puede argumentar superioridad de ingeniería** sobre quién usa el XTU SDK: Intel está detrás de Hypertune y el producto funciona. La superioridad de CA-O es de **modelo** (transparencia, reversibilidad, honestidad de evidencia, cero componentes persistentes), no de acceso privilegiado al silicio.
4. La postura de CA-O debe documentar explícitamente que **nunca instala un driver kernel** hasta que exista uno firmado y auditado (§6.14), y que el estado actual es cero componentes residentes.

## 1.5 El fallo crítico: el tune test de CPU

Esta sigue siendo la pieza que define el estándar al que hay que superar, y está **doblemente confirmada**:

- El OC de CPU requiere un «tune test» previo que encuentra los límites de reloj estable superior e inferior.
- **Cita literal del tutorial oficial de Hypertune** (`@HYPERTUNEGG`, 2026-09-15): *«Also, if your PC crashes or anything during the test, **it's totally normal. Just restart and let it continue cooking** until it's finished finding the right clock speeds for you.»*
- **No existe detección ni reanudación automática tras cuelgue.** El usuario reinicia a mano y vigila el proceso.
- El review independiente concluye: *«Uh **CPU overclock I could not get working**, but I'll explain that in detail now»*. La causa concreta es reveladora: el tune test abortó por un **«overclocking lock» / PL4 (power limit 4) ausente en BIOS modernas**; el revisor lo forzó con ThrottleStop a PL4=1024 y Hypertune siguió fallando.
- Equipo del review: **i7-14700KF + RTX 5070 Ti** (el borrador atribuía un 3080 a este vídeo; el 3080 pertenece a las cifras de §1.6).
- Su TL;DR en la descripción: **«Not worth it»**. Y la aclaración fijada: *«it didnt do much on this system besides the performance gains from the GPU overclock»*.

## 1.6 Ganancias declaradas vs. gains reales

**Corrección de atribución.** Las cuatro cifras son **benchmarks internos de Hypertune**, difundidas por la prensa. Tom's Hardware **nunca midió el producto**: *«we haven't had the chance to test Hypertune ourselves yet»*. Ningún medio ha replicado las cifras de forma independiente (igor'sLAB: *«independent measurements of these values are not yet available»*).

| Hardware | Juego | Ganancia declarada (por Hypertune) |
|---|---|---|
| Core Ultra 9 285K + RTX 5090 | Homeworld 3 | +18,9 % |
| Core Ultra 9 285K + RTX 5090 | Tomb Raider | +28,2 % |
| i7-14700K + RTX 3080 | Rainbow Six Siege | +9,8 % |
| i7-14700K + RTX 3080 | Marvel Rivals | **+4,3 %** |

Tres matices que el borrador no recogía y que son **materiales para la estrategia de CA-O**:

1. **El titular es «up to 60 %»**; la mejor cifra propia de Hypertune con los ajustes del juego sin tocar es **28,2 %** y el suelo es **4,3 %**. igor'sLAB documenta la degradación 60 % → 41 % → 28,2 %.
2. **Las cuatro cifras se tomaron con el Game Hub DESACTIVADO.** Es decir, el titular y las cifras no son comparables entre sí.
3. **En el caso del 3080, la mayor parte de la ganancia del +9,8 % viene del Game Hub**, que cambia los ajustes gráficos del juego a un preset competitivo — no del overclock. Tom's Hardware examinó la contribución por componente y encontró que solo una parte pequeña del 9,8 % vino del control de hardware ([XenoSpectrum, citando a Tom's Hardware](https://xenospectrum.com/en/intel-backed-hypertune-pc-overclocking)). Eso es gratis de hacer a mano.

**Veredicto editorial de Tom's Hardware, cita literal CONFIRMADA:** *«**Automated tuning programs usually don't work as well as advertised**, and we haven't had the chance to test Hypertune ourselves yet»*, y además: *«these tools don't do anything you can't accomplish yourself»*.

## 1.7 Veredicto de las reviews

> **«Save your money.»**

**Corrección de atribución:** esa frase **no es de ningún editorial escrito**, es del vídeo independiente: *«Now I'm just going to straight up tell you **save your money**. Like d- don't don't spend this money on the software»* ([Mostly Positive Reviews, 2026-09-11](https://www.youtube.com/watch?v=4UF46SbwOls)). Los veredictos escritos más cercanos son: igor'sLAB «wait for independent benchmarks», Tom's Hardware «don't do anything you can't accomplish yourself», HotHardware «doesn't quite pass the sniff test», Club386 «treat these claims with due scepticism», VideoCardz «at the current price, I would not expect Hypertune to be very popular».

**Cobertura independiente (para citar):**

| Medio | Fecha | Postura | URL |
|---|---|---|---|
| GamesBeat | 2026-09-08 | Neutro-positivo (entrevista exclusiva de lanzamiento) | [enlace](https://gamesbeat.com/hypertune-brings-automated-overclocking-to-pc-gamers-with-intels-support-exclusive) |
| Tom's Hardware | 2026-09-09 | «no hacen nada que no puedas hacer tú» | [enlace](https://www.tomshardware.com/pc-components/cpus/intel-backed-auto-overclocking-tool-hypertune-optimizes-individual-systems-not-test-profiles-tool-claims-fps-improvement-of-up-to-60-percent-on-intel-based-systems) |
| TechPowerUp | 2026-09-09 | Escéptico; comentarios hostiles | [enlace](https://www.techpowerup.com/352525/intel-backed-hypertune-pc-overlocking-tool-reportedly-boosts-fps-by-up-to-60) |
| VideoCardz | 2026-09-10 | «no esperaría que fuera muy popular» | [enlace](https://videocardz.com/newz/intel-backed-hypertune-software-automatically-overclocks-cpus-and-gpus) |
| Club386 | 2026-09-10 | «due scepticism» | [enlace](https://www.club386.com/hypertune-overclocking-tool) |
| igor'sLAB | sep-2026 | El mejor análisis escrito; precios divergentes | [enlace](https://www.igorslab.de/en/hypertune-automatic-pc-tuning-subscription-60-percent-fps-promise-28-2-percent-own-benchmark/) |
| HotHardware | 2026-09-11 | «doesn't quite pass the sniff test» | [enlace](https://hothardware.com/news/hypertune-auto-overclocking-tool) |
| TweakTown | sep-2026 | El más favorable | [enlace](https://www.tweaktown.com/news/113480/intel-backs-hypertune-overclocking-tool-that-reportedly-boosts-fps-by-up-to-60-percent/index.html) |
| XenoSpectrum | 2026-09-10 | El más incisivo (Game Hub hace el trabajo) | [enlace](https://xenospectrum.com/en/intel-backed-hypertune-pc-overclocking) |
| vx-underground (X) | 2026-05-20 | **El desmontaje técnico de referencia** | [enlace](https://x.com/vxunderground/status/2056899139099619490) |
| Mostly Positive Reviews (YT) | 2026-09-11 | Único benchmark independiente real; 10.237 vistas | [enlace](https://www.youtube.com/watch?v=4UF46SbwOls) |

> **Cuidado con una fuente falsa:** existe un vídeo de comparación «RisxnTweaks vs HyperTune» promocionado por el producto competidor (con cupón del 15 %). **No es independiente.** Del mismo modo, el canal `@HYPERTUNEGG` publica tutoriales de marketing («Boost your FPS by up to 40 %»), no cobertura. Reddit es escaso y no se pudieron extraer cuerpos de comentarios (JS + 403), así que **no se caracteriza el consenso de Reddit**.

**Puntos críticos que se repiten, corregidos:**
1. Suscripción por un trabajo fundamentalmente de una sola vez (el punto vivo; nadie acusa de fraude).
2. El score no significa nada **y su escala es desconocida**.
3. El OC de CPU no se pudo hacer funcionar (causa: PL4 / overclocking lock).
4. «Memory OC» es un checkbox de XMP/EXPO.
5. **Nuevo y más grave: drivers kernel persistentes que sobreviven a la desinstalación, y Windows Update desactivado.**
6. **Fricción de facturación en el trial** (se pide tarjeta antes de descargar).
7. **Ninguna incidencia de baneo reportada** — pero *ausencia de reportes no es prueba de seguridad*. Hay que declararlo **NO VERIFICABLE**, no «seguro».

## 1.8 Lenguaje visual del producto

| Elemento | Especificación |
|---|---|
| Fondo | Casi negro `#0a0b0b` |
| Texto | Blanco puro |
| Acento | Un único verde/mint `#00E08A`–`#10b981` |
| Etiquetas de sección | Numeradas en micro-caps con índice: `01 PROCESSOR`, `02 GRAPHICS`, `03 SYSTEM`, `04 NETWORK`, `05 GAMES` |
| Tarjetas | Rectángulos oscuros redondeados, borde `1px`, padding generoso |
| Layout de tarjeta | Texto a la izquierda, visual a la derecha |
| Chips de estado | Minúsculos, por todas partes: `0 Intel boost`, `6 Power limits applied`, `4 driver settings applied`, `+25 more` |
| Cards de juego | Portada + título + botón `Open` + toggle por título |
| Tipografía de estado | Monoespaciada/técnica, muy pequeña |
| Tipografía de títulos | Condensada bold, muy grande |
| Gráficos | Contadores animados, render del die de Intel, gauge del ventilador de la GPU |

## 1.9 Conductas de Hypertune que CA-O no debe repetir (corregido)

1. **Suscripción obligatoria** para algo que se hace una vez y se revierte.
2. **Login y cuenta** obligatorios.
3. **Score opaco de escala desconocida**, sin metodología explicable.
4. **Sin recuperación tras cuelgue** — el usuario vigila el proceso a mano.
5. **«Memory OC» es un checkbox** — ni timings, ni voltajes, ni detección de perfil.
6. **GPU OC = 2–3 presets** — cero algoritmo de estabilidad propio.
7. **Sin deltas honestos por cambio** — no sabes qué te dio qué.
8. **Sin guardarraíles térmicos** visibles ni margen anunciado.
9. **(NUEVA) Instala drivers kernel persistentes** con `AUTOSTART` que **sobreviven a la desinstalación**, en un producto cuyo claim central es «no tocamos nada».
10. **(NUEVA) Desactiva Windows Update** vía política de `HKLM`.
11. **(NUEVA) Pide tarjeta antes de descargar** el trial que anuncia sin tarjeta.
12. **(NUEVA) Titular de «hasta 60 %»** que no es representativo ni comparable con sus propias cifras (tomadas con Game Hub desactivado), y cuya ganancia en el caso estrella viene de cambiar ajustes gráficos del juego.

## 1.10 Oportunidades de diferenciación para CA-O (corregidas)

1. **Gratis, sin suscripción, sin login y sin telemetría.**
2. **Cero componentes kernel.** Sin driver, sin `AUTOSTART`, sin residuo tras desinstalar. Esta es ahora la ventaja **número uno**, no una nota al pie.
3. **Metodología transparente** con protocolo de estabilidad basado en evidencia real, no un score.
4. **Detección y reanudación automáticas tras cuelgue**, con puntos de control — no *«just restart and let it continue cooking»*.
5. **Revert automático garantizado** ante inestabilidad detectada (dentro de la política de recuperación del repo, ver §6.6).
6. **Granularidad por componente**, con una capa de memoria **honesta**: lo que Windows expone de verdad, no una promesa que no se puede cumplir (ver §3.5).
7. **Deltas honestos y acotados** — el delta medido del perfil completo, con los techos por eje, sin atribuir a cada knob lo que no es aditivo.
8. **Guardarraíles térmicos explícitos y por componente**, con margen anunciado antes de aplicar y con la limitación real declarada cuando no hay sensor.
9. **Historial de auditoría y revert por cambio** — cada ajuste reversible individualmente.
10. **(NUEVA) Estándar de evidencia declarado por ajuste**, reutilizando el vocabulario que el catálogo de 93 optimizaciones ya usa (`EvidenceLevel`).
11. **(NUEVA) Cero modificaciones a la política del sistema operativo ajeno al OC.** No tocamos Windows Update ni nada que el usuario no haya pedido.

---

# PARTE 2 — ESTADO DEL REPOSITORIO CA-O (verificado)

**Ruta:** `C:\Users\berna\OneDrive\Documentos\CA-O` · **Git:** rama `main`, HEAD `eb6c561` («release: CA-O 2.2.3 - auditoria completa del pipeline, 1278 tests») · **Versión:** 2.2.3

## 2.1 Inventario reproducible

```bash
# Conteo por proyecto, EXCLUYENDO bin/ y obj/ (el modo correcto)
for d in src/* tests/*; do
  cs=$(find "$d" -name '*.cs'   -not -path '*/obj/*' -not -path '*/bin/*' | wc -l)
  xa=$(find "$d" -name '*.xaml' -not -path '*/obj/*' -not -path '*/bin/*' | wc -l)
  csl=$(find "$d" -name '*.cs'  -not -path '*/obj/*' -not -path '*/bin/*' -exec cat {} + | wc -l)
  xal=$(find "$d" -name '*.xaml' -not -path '*/obj/*' -not -path '*/bin/*' -exec cat {} + | wc -l)
  printf '%-28s cs=%-4s xaml=%-3s lineas=%s\n' "$d" "$cs" "$xa" "$((csl+xal))"
done
```

**Raíz del repositorio** (21 entradas + 8 ocultas):

```
CA-O.sln  CHANGELOG.md  CONTRIBUTING.md  Directory.Build.props  Directory.Packages.props
LICENSE  PRIVACY.md  README.md  THIRD-PARTY-NOTICES.md  Version.props
artifacts/  assets/  capturas/  docs/  installer/  "memoria HOLA/"  scripts/  src/  tests/
cao-ui-error.log  install-and-launch.ps1
Ocultas: .git/  .github/  .gitignore  .opencode/  .playwright-mcp/  .superpowers/  .tmp/
```

> El borrador omitía `.git`, `.gitignore` y el fichero de log.

## 2.2 Solución, proyectos y tamaño real

**`CA-O.sln` = 12 proyectos en 2 carpetas de solución.**

| Proyecto | Carpeta | En la `.sln` | `.cs` | `.xaml` | Líneas |
|---|---|---|---|---|---|
| `CA-O.UI` | src | ✅ | 49 | 14 | **15.140** (11.710 + 3.430) |
| `CA-O.Core` | src | ✅ | 161 | 0 | **16.069** |
| `CA-O.Infrastructure` | src | ✅ | 42 | 0 | **5.191** |
| `CA-O.Privileged` | src | ✅ | 5 | 0 | **1.094** |
| `CA-O.Shared` | src | ✅ | 33 | 0 | **2.435** |
| `CA-O.Uninstaller` | src | ✅ **sí** | 3 | 3 | **554** (389 + 165) |
| `CA-O.InstallerGui` | src | ❌ **fuera** | 3 | 4 | **1.004** (807 + 197) |
| `CA-O.Setup` | src | ❌ **fuera** | 1 | 0 | **575** |

| Proyecto de test | `.cs` | Líneas |
|---|---|---|
| `CA-O.Core.Tests` | 54 | 6.054 |
| `CA-O.UI.Tests` | 15 | 1.814 |
| `CA-O.Integration.Tests` | 12 | 1.443 |
| `CA-O.Security.Tests` | 8 | 1.101 |
| `CA-O.Infrastructure.Tests` | 12 | 896 |
| `CA-O.Benchmark.Tests` | **5** | 293 |

> Sobre 2.2.3: `src/CA-O.InstallerGui` y `src/CA-O.Setup` **existen en disco y no están en la solución**; se compilan por separado desde `installer/CA-O.iss` (Inno Setup) más `scripts/build-inno.ps1`. **`CA-O.Uninstaller` sí está en la solución** — corrección de F1.

**Totales medidos:** **~42.100 líneas** de producción (8 proyectos de `src`) · **~11.600 líneas** de test (6 proyectos). *El borrador decía 86.000 / 10.700; el primero estaba inflado ~2×.*

**Por qué el borrador se equivocó (y cómo no repetirlo):** sus conteos de ficheros `.cs` coinciden exactamente con un `find` que **incluye `obj/`** (UI 181, Core 173, Core.Tests 63, Benchmark.Tests 14), porque el compilador de XAML y los generadores emiten `.g.cs`/`AssemblyInfo.cs` ahí. Sus cifras de líneas no cuadran con ninguno de los dos métodos. **Regla: contar siempre con `-not -path '*/obj/*' -not -path '*/bin/*'`.**

## 2.3 Configuración de compilación

| Fichero | Contenido — **verificado literal** |
|---|---|
| `global.json` | SDK **10.0.400**, `rollForward: latestFeature`, `allowPrerelease: false` |
| `Version.props` | Fuente única de verdad = **2.2.3**, en `Version`, `AssemblyVersion`, `FileVersion`, `PackageVersion`, `ProductVersion`, `InformationalVersion` (`2.2.3+$(GHTag)`) |
| `Directory.Build.props` | Importa `Version.props`; `LangVersion` 13.0, `Nullable` enable, `ImplicitUsings` enable, `EnableNETAnalyzers` true, `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild` false |

**Identidad del producto:**

`Authors` CA-O Team · `Product` CA-O Windows Optimizer · `ProductName` CA-O · `ProductTitle` CA-O Windows Optimizer · `ServiceName` `CAO.Privileged` · `ServiceDisplayName` CA-O Privileged Service · `TargetFramework` `net10.0-windows10.0.19041.0` · `RuntimeIdentifier` `win-x64` · `InstallDirectoryName` CA-O · `ProgramDataDirectoryName` CA-O · `UiExecutable` `CA-O.UI.exe` · `ServiceExecutable` `CA-O.Privileged.exe` · `UninstallerExecutable` `CA-O.Uninstaller.exe`

**Tratamiento de warnings — verificado literal.** En Release, `TreatWarningsAsErrors=true` con `NoWarn` para:

```
CA1305 CA1310 CA1707 CA1711 CA1716 CA1720 CA1822 CA1859 CA1861 CA1873 CA1848
xUnit1048 xUnit2029 MVVMTK0045 CA1068 CA1001 CA1826
```

**Permanecen como ERRORES:** nullability (`CS8xxx`), **`CA2016`** y las reglas xUnit.
**Comprobación ejecutada:** `dotnet build CA-O.sln -c Release` → **Build succeeded, 0 Warning(s), 0 Error(s), 1m40s.**

**Paquetes NuGet — 11 en total** (el borrador listaba 8, omitiendo los de test), centralizados en `Directory.Packages.props` con versiones fijas:

| Paquete | Versión | Ámbito |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection` | 10.0.0 | UI |
| `Microsoft.Extensions.Hosting` | 10.0.0 | UI, Privileged, InstallerGui |
| `Microsoft.Extensions.Hosting.WindowsServices` | 10.0.0 | Privileged |
| `Microsoft.WindowsAppSDK` | 2.4.0 | UI, Uninstaller, InstallerGui |
| `CommunityToolkit.Mvvm` | 8.4.0 | UI, Uninstaller, InstallerGui |
| `System.Management` | 10.0.0 | Core, Infrastructure |
| `System.Diagnostics.PerformanceCounter` | 10.0.0 | UI, Infrastructure |
| `System.ServiceProcess.ServiceController` | 8.0.1 | Core |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | tests |
| `xunit` | 2.9.2 | tests |
| `xunit.runner.visualstudio` | 2.8.2 | tests |

## 2.4 Estructura interna por proyecto

**`src/CA-O.Core`** (16.069 líneas) — 16 carpetas: `Abstractions` · `Benchmark` · `Compatibility` · `Diagnostics` · `Engine` · `Gaming` · `Interfaces` · `Interop` · `Optimization` · `Optimizations` · `Profiles` · `Rollback` · `Scoring` · `Security` · `Services`

**`src/CA-O.Infrastructure`** (5.191 líneas) — 13 carpetas: `Benchmarking` · `Gaming` · `Input` · `Logging` · `Networking` · `Persistence` · `PowerShell` · `Security` · `Services` · `Storage` · `SystemInterop` · `Windows`

**`src/CA-O.UI`** (15.140 líneas) — páginas planas en `src\CA-O.UI\`: `AnalyzePage` · `App` · `BenchmarkPage` · `DashboardPage` · `DriversPage` · `HistoryPage` · `LimpiezaPage` · `MainWindow` · `OptimizePage` · `RestorePage` · `SettingsPage` · `SolucionarPage`

**`src/CA-O.UI\ViewModels`** — 11 entradas: `AnalyzeViewModel` · `BenchmarkViewModel` · `DashboardViewModel` · `DiagnosticsViewModel` · `GamingViewModel` · `HistoryViewModel` · `OptimizeViewModel` · `RecoveryCandidateInfo` · `RestoreViewModel` · `SettingsViewModel` · `UiState`

> `src\CA-O.Core\Overclock` y `src\CA-O.Infrastructure\Overclock` **no existen todavía**. Código de OC escrito: **0**.

## 2.5 Documentación del repositorio (líneas reales)

| Fichero | Líneas | Contenido |
|---|---|---|
| `README.md` | **867** (23 cabeceras `##`) | Catálogo completo de **93 optimizaciones**; índice; pilares |
| `CHANGELOG.md` | **312** | **31** cabeceras de release, de `[2.0.0] - 2026-08-25` a `[2.2.3] - 2026-10-07` |
| `docs\OPTIMIZATION-CATALOG.md` | **431** | Modelo de evidencia, riesgo, compatibilidad gaming, reglas de seguridad, buckets, 93 optimizaciones + definiciones |
| `docs\ARCHITECTURE.md` | **109** | Principio rector, procesos y confianza, capas, motores, ciclo transaccional, perfiles, persistencia, **Pruebas (1278 passed, 0 failed — Release)** |
| `docs\IPC_PROTOCOL.md` | **48** | Protocolo IPC v2: transporte, envelope, payloads por operación, cadena de validación |
| `docs\SECURITY.md` | **65** | Controles por fases, instalador, cadena de suministro |
| `docs\THREAT-MODEL.md` | **49** | Scope, trust boundaries, actores, assets, STRIDE, riesgos residuales |
| `docs\TRANSACTIONS.md` | **50** | Fases P0-11, estados terminales, rollback verificado, irreversibles, recuperación |
| `PRIVACY.md` | 49 | Datos locales, únicas conexiones de red, RGPD/LOPDGDD, menores |
| `CONTRIBUTING.md` | 30 | Reglas de oro, flujo, cómo añadir una optimización, estilo |
| `THIRD-PARTY-NOTICES.md` | 62 | Componentes en runtime vs. solo compilación |

**Proceso de desarrollo** (fuera de git, ver §2.17):

```
docs/superpowers/specs/2026-10-08-tutorial-recorrido-guiado-design.md
docs/superpowers/plans/2026-10-08-tutorial-recorrido-guiado.md
.superpowers/sdd/2026-10-08-tutorial-recorrido-guiado/{progress,task-N-brief,task-N-report,task-N-review,task-N-rereview-N}.md
```

## 2.6 Workflow de desarrollo documentado

1. `restore` + build Debug.
2. Run de la UI (pide UAC).
3. **5 gates de verificación** (`scripts/verify.ps1`) + contratos + persistencia + E2E.
4. Release self-contained + SBOM → `artifacts/release/` (`ui/`, `service/`, `gui-installer/`, `uninstall/`, `setup/`) + `SHA256SUMS.txt` — **verificado que existe esa estructura**.
5. Packaging vía `scripts/package.ps1` → `artifacts/CA-O-{version}-win-x64.zip` + `.sha256` — **verificado** (hay 20+ pares versión+sha256 hasta `2.2.3`).
6. Endurecer ACLs de datos (`scripts/harden-data-acls.ps1`).

`scripts/` contiene 20 ficheros, incluyendo `build-release.ps1`, `verify.ps1`, `verify-release.ps1`, `package.ps1`, `sign.ps1`, `e2e-windows.ps1`, `install-privileged-service.ps1`, `uninstall-privileged-service.ps1`.

## 2.7 Catálogo de optimizaciones (93)

**93 confirmado**: 93 filas en la tabla maestra del README y `OptimizationCatalog.All` = 93 en `docs\OPTIMIZATION-CATALOG.md`.

**Proyecciones honestas** (`CatalogProjections`, `README.md:277`): `BatchDefault` **65** · `RepairActions` **15** · `Diagnostics` **4** · `Restores` **9**. Suma exacta 93. Es proyección, no borrado: `All` sigue con 93 y todo ID resuelve. Además hay **12 optimizaciones históricas retiradas** en `AllLegacy` solo para trazabilidad.

> **Corrección de F-extra:** el borrador titula su lista de ids «Catálogo de optimizaciones (93)» pero **enumera 65 ids**. Todos los que enumera **existen** en el catálogo compilado (verificado uno a uno), pero **faltan 28**. Debe rotularse como lista parcial o completarse. La fuente canónica es `docs\OPTIMIZATION-CATALOG.md` (431 líneas), que además está **protegida por un test** — ver §2.15.

## 2.8 La superficie Benchmark actual (lo que se propone sustituir)

### Ficheros de producción

| Capa | Ruta | Líneas |
|---|---|---|
| UI | `src\CA-O.UI\BenchmarkPage.xaml` | 314 |
| UI code-behind | `src\CA-O.UI\BenchmarkPage.xaml.cs` (`CAO.UI.Pages.BenchmarkPage`) | 473 |
| ViewModel | `src\CA-O.UI\ViewModels\BenchmarkViewModel.cs` | 195 |
| Core | `src\CA-O.Core\Benchmark\BenchmarkPolicy.cs` | **18** |
| Core | `src\CA-O.Core\Benchmark\BenchmarkAnalyzer.cs` | **78** |
| Infra | `src\CA-O.Infrastructure\Benchmarking\SystemBenchmarkRunner.cs` | 395 |
| Infra | `src\CA-O.Infrastructure\Benchmarking\BenchmarkStore.cs` | 131 |
| Infra | `src\CA-O.Infrastructure\Benchmarking\BootInfoProvider.cs` | 30 |
| Infra | `src\CA-O.Infrastructure\Benchmarking\IFrameCapture.cs` | 9 |
| Infra | `src\CA-O.Infrastructure\Benchmarking\DxgiFrameCapture.cs` | 45 |
| Infra | `src\CA-O.Infrastructure\Networking\DnsBenchmarkProvider.cs` | — |
| Shared DTO | `src\CA-O.Shared\DTO\BenchmarkModels.cs` | 66 |

### Qué hace realmente este motor — **crítico, corrige B1**

```csharp
// src/CA-O.Core/Benchmark/BenchmarkPolicy.cs — 18 líneas, íntegro
public static class BenchmarkPolicy
{
    public const double MinimumEffectPercent = 3.0;  // suelo de ruido, fuente única
    public const int MeasuredRuns = 3;               // trials; se reporta la mediana
    public const int WarmupRuns = 1;                 // pasada descartada
}
```

- `BenchmarkModels.cs` define `BenchmarkVerdict { InsufficientData, NoMeasurableImprovement, Improvement, Regression }`, `FrameTimeStatistics` (**avg FPS, 1 % low, 0.1 % low, P50/P95/P99 frame-time, varianza**) y `BenchmarkComparison` con `ReasonEs`.
- `BenchmarkPage.xaml:11` declara el contrato del usuario literalmente en pantalla: *«Metodología honesta: cargas fijas y reproducibles (CPU/memoria/disco). Veredicto con suelo de ruido 3 %.»*
- `BenchmarkAnalyzer` calcula percentiles y el veredicto A/B. **No hay ningún score.**
- El único «score» del fichero es `CpuScore`, una métrica de **throughput (operaciones/s)** que se muestra como **delta %**. El anillo `ScoreRing` 0–100 vive en `src/CA-O.UI/Controls/RiskBadge.cs:66` y lo usa `DashboardPage` para la salud del sistema.

> **Conclusión que invalida el §6.16 del borrador:** CA-O **ya tiene** exactamente lo que el borrador dice querer construir («deltas honestos, no un score opaco»). El motor A/B, el suelo de ruido del 3 %, la mediana de 3 trials con warmup y los percentiles de frame-time **son el activo, no el pasivo**. Borrarlos sería destruir la base del §6.8.

### Tests afectados

| Ruta | Tests | Nota |
|---|---|---|
| `tests\CA-O.Benchmark.Tests\BenchmarkStoreTests.cs` | 3 | |
| `tests\CA-O.Benchmark.Tests\BenchmarkStatisticsTests.cs` | 5 | |
| `tests\CA-O.Benchmark.Tests\BenchmarkTrialsTests.cs` | 3 | |
| `tests\CA-O.Benchmark.Tests\BenchmarkWorkloadTests.cs` | 4 | |
| `tests\CA-O.Benchmark.Tests\FrameCaptureTests.cs` | **2** | **omitido por el borrador** |
| **Total** | **17** | ✅ coincide con `docs\ARCHITECTURE.md:107` |
| `tests\CA-O.Infrastructure.Tests\DnsBenchmarkProviderTests.cs` | — | DNS, ortogonal al OC |
| `tests\CA-O.UI.Tests\BenchmarkTask5Tests.cs` | — | CSV de sesión, pendiente en `UiState` |
| `tests\CA-O.Core.Tests\OptimizationRecommendationPolicyTests.cs` | 2 de 3 | `BenchmarkAnalyzerReportsFrameTimePercentiles`, `BenchmarkAnalyzerRejectsInsufficientComparisonData` — **el borrador no los menciona y también dependen de `BenchmarkAnalyzer`** |

### Registros de navegación — verificados línea a línea

| Fichero | Línea | Contenido literal |
|---|---|---|
| `src\CA-O.UI\CA-O.UI.csproj` | **38** | `<Page Include="BenchmarkPage.xaml" />` |
| `src\CA-O.UI\Navigation\RouteTable.cs` | **17** | `["benchmark"] = typeof(Pages.BenchmarkPage),` |
| `src\CA-O.UI\Navigation\RouteTable.cs` | **30** | `"benchmark"` dentro de `Order` |

## 2.9 `RouteTable.cs` — navegación (35 líneas, íntegro)

```csharp
public static class RouteTable
{
    public static readonly IReadOnlyDictionary<string, Type> Routes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["dashboard"] = typeof(Pages.DashboardPage),   // L14
        ["analyze"]   = typeof(Pages.AnalyzePage),
        ["optimize"]  = typeof(Pages.OptimizePage),
        ["benchmark"] = typeof(Pages.BenchmarkPage),   // L17
        ["restore"]   = typeof(Pages.RestorePage),
        ["history"]   = typeof(Pages.HistoryPage),
        ["cleanup"]   = typeof(Pages.LimpiezaPage),
        ["solucionar"]= typeof(Pages.SolucionarPage),
        ["drivers"]   = typeof(Pages.DriversPage),
        ["settings"]  = typeof(Pages.SettingsPage),
    };

    /// <summary>Ordered tags as shown in the NavigationView.</summary>
    public static readonly IReadOnlyList<string> Order = new[]   // L27
    {
        "dashboard", "analyze", "optimize", "cleanup", "solucionar", "drivers",
        "benchmark", "restore", "history", "settings",           // L30
    };

    public static Type? Resolve(string? tag) =>
        tag is not null && Routes.TryGetValue(tag, out var pageType) ? pageType : null;
}
```

**10 tags.** Consumidores: `MainWindow.xaml.cs`, `ShellNavigationService.cs`, `ShellNavigationServiceStub.cs`, y los tests `TourCatalogTests` / `TourTargetRegistryTests` (estos últimos, hoy sin commitear). Cualquier tag nuevo debe resolver a un tipo de página existente.

## 2.10 `TimeoutProfile.cs` — timeouts (61 líneas, íntegro)

`src\CA-O.Shared\Constants\TimeoutProfile.cs`

| Constante | Valor |
|---|---|
| `RequestRead` | 15 s |
| `DispatchDefault` | 60 s |
| **`DispatchHeavy`** | **20 min** |
| `ClientResponseDefault` | 90 s |
| `ClientResponseHeavy` | 21 min (20 + 1 de margen) |

**`HeavyOptimizationIds`** — `HashSet<string>` con `StringComparer.OrdinalIgnoreCase`, **exactamente 15 ids** (verificados uno a uno):

```
windows-component-store-cleanup        windows-component-store-resetbase
optimize-system-drive                  retrim-system-ssd
defragment-hdd-only                    cleanup-windows-update-cache
cleanup-app-caches                     analyze-component-store
reset-network-stack-repair             repair-windows-update
cleanup-windows-temp                   cleanup-crash-dumps-extended
cleanup-browser-code-cache             cleanup-gpu-shader-cache
cleanup-nvidia-downloader-cache
```

Consumidores documentados en el comentario del propio fichero: servicio (`PrivilegedPipeService`), gateway (`SystemCommandGateway`) y cliente UI (`PrivilegedPipeClient`).

> **REGLA DURA — BUG REAL OCURRIDO DOS VECES.** El comentario del fichero lo explica: los valores vivían como literales **triplicados** en `PrivilegedPipeService`, `SystemCommandGateway` y `PrivilegedPipeClient`; al divergir, el gateway podía matar un DISM/defrag a mitad y **corromper el almacén de componentes**. Y el CHANGELOG de 2.2.3 añade: *«Los espejos manuales de `TimeoutProfile` en las páginas caducaban antes que el servicio (90 s y 60 s frente a los 21 min reales)»*.
> **Nunca copiar la lista de ids a mano. Consumir siempre `TimeoutProfile.HeavyOptimizationIds`.**

## 2.11 Capa de hardware: 100 % WMI, cero telemetría

Búsqueda de `LibreHardwareMonitor|OpenHardwareMonitor|WinRing0|nvapi|amdocl|atiadl` en `src/`, `tests/`, `docs/`, `README.md` → **0 resultados**. No hay NI UNA dependencia de telemetría de hardware, ni de OC.

**Providers WMI/Interop existentes** (todos en `src\CA-O.Infrastructure\SystemInterop` salvo donde se indica):

`WmiSystemInfoProvider` · `PerformanceDiagnosticsProvider` · `SystemContextProvider` + `SystemContextCache` · `ObservedStateProvider` · `DriverDiagnosticsProvider` · `ThermalDiagnosticsProvider` · `PendingRebootProvider` · `SetupApiDeviceEnumerator` · `WmiRestorePointService` · `DpcLatencySampler`

Más: `Gaming\GameDetectionProvider` · `Gaming\AntiCheatScanProvider` · `Networking\BufferbloatProbe` · `Windows\Etw\WprDpcCollector`

> **HUECO CENTRAL, ahora con precisión.** No existe telemetría viva de temperatura, relojes, consumo ni VRM. `PerformanceCounter` da utilización, no temperatura. Y según §3.6, **la temperatura de CPU no es obtenible en modo usuario sin driver firmado**: el hueco no se cierra «añadiendo un proveedor WMI», se cierra con NVAPI/ADLX (GPU) y aceptando la ausencia (CPU).

## 2.12 Infraestructura reutilizable — los 14 verificados

| Necesidad | Componente existente (ruta real) |
|---|---|
| Game Hub / detección de juegos | `Infrastructure\Gaming\GameDetectionProvider.cs` |
| Anti-cheat | `Infrastructure\Gaming\AntiCheatScanProvider.cs` |
| Latencia DPC | `Infrastructure\SystemInterop\DpcLatencySampler.cs` |
| Bufferbloat | `Infrastructure\Networking\BufferbloatProbe.cs` |
| Trazas DPC | `Infrastructure\Windows\Etw\WprDpcCollector.cs` |
| Diagnóstico térmico | `Infrastructure\SystemInterop\ThermalDiagnosticsProvider.cs` |
| Diagnóstico de rendimiento | `Infrastructure\SystemInterop\PerformanceDiagnosticsProvider.cs` |
| Canal de aplicación | `Infrastructure\Windows\Execution\SystemCommandGateway.cs` |
| Comandos elevados | `Infrastructure\PowerShell\ElevatedCommandCatalog.cs` |
| Registro | `Infrastructure\Windows\SystemRegistry\RegistryAccessor.cs` |
| Journal | `Infrastructure\Persistence\FileTransactionJournal.cs` |
| Snapshots | `Infrastructure\Persistence\FileSnapshotStore.cs` |
| Repositorio | `Infrastructure\Persistence\SnapshotRepository.cs` |
| Transacción | `Core\Rollback\OptimizationTransaction.cs` |
| Recuperación post-crash | `Core\Rollback\CrashRecoveryService.cs` |

**Corrección de F7:** *`BenchmarkAsync` **no** está en `OptimizationEngine`.* Es un método con implementación por defecto en la interfaz **`IOptimization`** (`Core\Interfaces\IOptimization.cs:88`) y lo invoca `OptimizationTransaction.cs:327`. Las fases `BenchmarkStarted` / `BenchmarkCompleted` **sí existen** (`Shared\Contracts\Lifecycle.cs:83-84`).

## 2.13 Cultura de evidencia (ya presente en CA-O)

`README.md:452`, **verificado carácter a carácter**:

> **Evidencia honesta:** `Official` (Microsoft Docs) / `Vendor` (NVIDIA/AMD/Intel) / `Benchmark` (reproducible) / `Empirical` (controlado) / `Heuristic` (histórico). Ninguna `Unknown` en `Recommended`.

El enum que lo materializa es **`EvidenceLevel`** en `src\CA-O.Shared\Enums\Enums.cs:33` y tiene **123 consumidores**. Sus valores son exactamente los seis del texto.

> **Cuidado con una colisión de nombres.** `Benchmark` es simultáneamente (a) el nombre de una **página** y (b) un **nivel de evidencia** del enum. Retirar la página **no retira el nivel de evidencia**: `EvidenceLevel.Benchmark` sigue siendo el vocabulario correcto para «medido de forma reproducible», y lo será también para el motor de OC. El §2.15/§6.16 del borrador los confundía al listar `docs\OPTIMIZATION-CATALOG.md:17` (que es la **leyenda del enum de evidencia**) como línea a actualizar por la retirada de la página.

**Es la misma disciplina que Hypertune no tiene** y sigue siendo el argumento de diferenciación más fuerte.

## 2.14 Conteo de tests de referencia

**Suite de referencia del commit 2.2.3: 1278 tests (Release, 0 fallos)** — declarado de forma consistente en `docs\ARCHITECTURE.md:99-109` y `README.md:752-760`.

| Proyecto | Documentado (2.2.3) | **Medido en el árbol de trabajo** |
|---|---|---|
| `CA-O.Core.Tests` | 786 | **786** ✅ |
| `CA-O.Security.Tests` | 248 | **248** ✅ |
| `CA-O.UI.Tests` | 109 | **145** ⚠️ **+36** |
| `CA-O.Integration.Tests` | 60 | **60** ✅ |
| `CA-O.Infrastructure.Tests` | 58 | **58** ✅ |
| `CA-O.Benchmark.Tests` | 17 | **17** ✅ |
| **Total** | **1278** | **1314** |

**Verificación ejecutada el 2026-10-10 sobre el árbol de trabajo:** `dotnet test CA-O.sln -c Release` → **`Failed: 0` en las seis suites, 1314 tests, exit code 0.**

> **Hallazgo (nuevo, y confirma §2.17):** cinco de las seis suites coinciden **exactamente** con lo documentado. La única desviación es `CA-O.UI.Tests`, con **36 tests más** (145 vs 109) — que corresponden a los cinco ficheros `Tour*Tests.cs` **sin commitear** del trabajo del tutorial. Es decir: **la baseline de 1278 es correcta para el commit 2.2.3, y el árbol de trabajo tiene 1314 tests verdes.** Cualquier afirmación del tipo «hemos dejado los 1278 verdes» debe decir sobre qué commit, o partir de una baseline de **1314**.

Eliminar `Benchmark.Tests` obliga a redistribuir o retirar sus **17** tests — pero ver §6.16 para el destino correcto, que **no** es la retirada.

## 2.15 Contratos invariantes — lo que CUALQUIER cambio debe respetar

> Esta sección no estaba en el borrador y es la que más ha costado aprender al repositorio. Se deriva de código y tests, no de opinión.

### Arquitectura

| Contrato | Dónde | Qué prohíbe |
|---|---|---|
| La UI no lanza procesos ni hospeda el pipe | `ArchitectureDependencyTests.UiNeverSpawnsProcessesOrHostsThePipe` | En `CA-O.UI`: `Process.Start`, `new Process`, `NamedPipeServerStream`, `using CAO.Privileged`, **`using System.Management`**. Excepciones nombradas y justificadas: `SettingsPage.xaml.cs`, `ExplorerRecovery.cs`, `DriversPage.xaml.cs` |
| Core no conoce UI ni Infrastructure | `ArchitectureDependencyTests.CoreNeverReferencesUiOrInfrastructure` (**L52**) | `using CAO.UI`, `using CAO.Infrastructure` en `CA-O.Core`. **Esta es la regla que sostiene §6.2** — el borrador citaba L40, que dice otra cosa |
| Infrastructure y Privileged no conocen UI | `ArchitectureDependencyTests.InfrastructureAndPrivilegedNeverReferenceUi` | `using CAO.UI` |
| `Process.Start` solo en el gateway | misma clase | Cualquier ejecución de proceso fuera de `SystemCommandGateway` |

### Consistencia documentación ↔ código

| Contrato | Dónde | Qué prohíbe |
|---|---|---|
| Todo id de `docs\OPTIMIZATION-CATALOG.md` existe en el catálogo compilado | `DocumentationCodeConsistencyTests.EveryCatalogIdInDocsMatchesTheCompiledCatalog` | Documentar una optimización que no existe. **«This test FAILS when docs drift from code»** — el propio encabezado lo declara |

> Consecuencia para este documento: si se crea `docs\OVERCLOCK-…md` con ids de knobs, hay que decidir si el test debe cubrirlo. Y si se renombra la página Benchmark, `README.md` y `docs\ARCHITECTURE.md` deben moverse en el mismo commit o el test de contratos falla.

### Transacciones y recuperación

| Contrato | Dónde | Valor exacto |
|---|---|---|
| Fases | `docs\TRANSACTIONS.md`; `Shared\Contracts\Lifecycle.cs:75` | `PRECHECK → COMPATIBILITY → SNAPSHOT → APPLY → VERIFY → COMMIT → POST-COMMIT BENCHMARK` |
| **Conjunto terminal** | `Shared\DTO\TransactionEvent.cs:26` `IsTerminal` | `{ Commit, RolledBack, Failed, CancelledBeforeApply, RecoveryCompleted }` — **exactamente estos cinco** |
| Cómo se detecta una transacción incompleta | `Core\Rollback\ITransactionJournal.cs:31` | El **último evento APILADO** (última línea del journal, nunca el reloj de pared) no es terminal |
| Efecto de una incompleta | `docs\TRANSACTIONS.md`; `CrashRecoveryService` | Bloquea nuevas mutaciones con `CAO-TXN-004` vía `HasPendingRecovery` |
| Commit es terminal y se journaliza el último | `OptimizationTransaction.cs:308-347` | El bug CAO-BUG-2026-10-06 fue journalizar `Commit` **antes** de las fases de benchmark, dejando 88 de 98 entradas abiertas para siempre |

### IPC

| Contrato | Dónde | Valor exacto |
|---|---|---|
| Operaciones | `Shared\IPC\IpcProtocol.cs:21` | `PrivilegedOperationKind` tiene **18 miembros** (0..17) — `docs\IPC_PROTOCOL.md` dice «17 miembros» y `operation: 0..16`: **está desactualizado** desde que 2.2.3 añadió `RecoverTransaction` |
| Versión de protocolo | `docs\IPC_PROTOCOL.md` | `ProtocolVersion = 2`, coincidencia **exacta** (`CAO-IPC-001`) |
| Límites duros | `IpcProtocol.Max*Bytes` | request **≤ 64 KB**, response **≤ 256 KB** |
| Cadena de validación | `docs\IPC_PROTOCOL.md` | 9 pasos en orden; incluye `OptimizationId` con esquema `[a-z0-9-]{1,80}` y allowlist por operación en el servicio |
| Cada operación tiene SU payload | `IpcProtocol.cs:43-45` | «no grab-bag parameter bags» — prohibido un payload comodín |

### Convenciones de código

| Contrato | Dónde |
|---|---|
| Rutas conocidas centralizadas | `Shared\CaOPaths.cs` (`ProgramDataRoot`, `HistoryFile`, `SnapshotsDirectory`, `BenchmarksDirectory`, `SettingsFile`, `UiLogsDirectory`, `UiCrashLogFile`, `ServiceLogsDirectory`) — **nada de rutas literales** |
| Versión centralizada | `Version.props` (fuente única) |
| Timeouts centralizados | `TimeoutProfile` (fuente única) |
| Identidad del producto | `Directory.Build.props` |
| Navegación | `RouteTable` (fuente única de tags) |
| Escritura atómica | `tmp → flush → Move(overwrite)` con cuarentena `.corrupt.timestamp` (`AnalysisStateStore`) |
| Exclusión de snapshots | mutex — ojo: 2.2.3 arregló que «el mutex de snapshots perdía la exclusión en silencio al agotarse la espera» |
| Diálogos | `Helpers\UiDialogs.cs` admite **un solo** `ContentDialog` a la vez (`SemaphoreSlim Gate = new(1,1)`, `WaitAsync(0)`); añadir otro concurrente reproduce CAO-BUG-2026-10-06, que **mataba la app** |
| Enumeración recursiva | `EnumerationOptions { IgnoreInaccessible = true }` y sin reparse points — **nunca `SearchOption.AllDirectories`** en este repo (`RecursiveEnumerationGuardTests` lo vigila) |
| i18n | `Localizer.Get(key)`; claves, nunca literales de UI |

## 2.16 Superficies documentales a actualizar — **ahora mínimas (D5)**

### La decisión D5 elimina casi todo este trabajo

El borrador necesitaba tocar **26 sitios** en README, ARCHITECTURE y OPTIMIZATION-CATALOG, porque sustituía la sección Benchmark. Con **D5** — añadir `Overclock` como página y tag nuevos **conservando `Benchmark` intacto** — la lista se reduce a **añadir**, no a renombrar ni borrar.

| Fichero | Acción con D5 | Motivo |
|---|---|---|
| `README.md` L25 (índice), L58-70 (pilares), L99/L164/L187-198 (árboles), L533-558 (§6), L610 (páginas), L624-635 (capturas) | **Añadir** las entradas de Overclock | `Benchmark` sigue existiendo; el pilar `BENCHMARK` **no se renombra** |
| `README.md` L209-215 | **Arreglar el duplicado** de `CA-O.Benchmark.Tests` (aparece dos veces, en L213 y L215) | Deriva preexistente, no causada por Overclock |
| `README.md` L752-760 / `ARCHITECTURE.md` L99-109 | Actualizar la tabla de suites **solo cuando cambien los conteos** | Los 17 de `Benchmark.Tests` no se tocan (D6) |
| `docs\ARCHITECTURE.md` **L1** | Título «Arquitectura CA-O **2.1**» con la versión 2.2.3 en curso | **Deriva preexistente**, ajena a Overclock. Buen candidato a arreglar de paso |
| `docs\IPC_PROTOCOL.md` | **Sí hay que actualizarlo**: añadir las operaciones de OC y corregir «17 miembros» / `operation: 0..16` | Ya estaba desactualizado desde que 2.2.3 añadió `RecoverTransaction` (hoy son **18**) |
| `docs\THREAT-MODEL.md` | Añadir la frontera nueva (UI elevada haciendo OC de GPU) | Es una frontera de confianza nueva |
| `docs\TRANSACTIONS.md` | Añadir las fases de OC **no terminales** | No cambia el conjunto terminal |
| `THIRD-PARTY-NOTICES.md` | Añadir NVAPI/ADLX como «invocado si existe, nunca redistribuido» | §6.18 |
| `PRIVACY.md` | Aclarar que invocar una DLL local del driver no es telemetría | §6.18 |
| `capturas\` | **Añadir** `10-overclock.png` | No se retira `06-benchmark.png` |
| `CHANGELOG.md` | **Añadir entrada nueva** | No se reescribe el pasado |
| `docs\OPTIMIZATION-CATALOG.md` **L17, L218, L259** | **NO TOCAR** | L17 es la leyenda de `EvidenceLevel`; L218/L259 son campos del catálogo. `Benchmark` como **nivel de evidencia** no desaparece (§2.13) |

> **Regla de oro que se conserva: el CHANGELOG no se reescribe.** El borrador proponía tocar L100, L301 y L303 — notas de release históricas (`[2.1.33]`, `[2.0.0]`). Reescribirlas falsifica el historial. **Se añade una entrada nueva.**

> **El otro gran ahorro de D5:** el pilar fundacional `DIAGNOSE → OPTIMIZE → BENCHMARK → RECOVER` (`README.md:64`, `ARCHITECTURE.md:7`) **no se toca**. El borrador habría obligado a renombrarlo a `MEASURE` en dos documentos y en las capturas.

## 2.17 Estado del árbol de trabajo (importante para planificar)

`git status --short` sobre `eb6c561`:

```
 M src/CA-O.Shared/DTO/AppSettings.cs
?? .playwright-mcp/
?? docs/HYPERTUNE-INVESTIGATION-CA-O.md        ← este documento
?? "memoria HOLA/"
?? src/CA-O.UI/Tutorial/
?? tests/CA-O.UI.Tests/TourCatalogTests.cs
?? tests/CA-O.UI.Tests/TourProgressTests.cs
?? tests/CA-O.UI.Tests/TourSettingsTests.cs
?? tests/CA-O.UI.Tests/TourStateTests.cs
?? tests/CA-O.UI.Tests/TourTargetRegistryTests.cs
```

**Consecuencias, y la más importante es un bloqueo real:**

1. Hay **trabajo sin commitear** de la feature *tutorial/recorrido guiado* (2026-10-08). Su spec y su plan están en `docs/superpowers/`, y su proceso en `.superpowers/sdd/2026-10-08-tutorial-recorrido-guiado/`.
2. **Ese tutorial está al 44 %.** El plan tiene **9 tareas** (`docs/superpowers/plans/2026-10-08-tutorial-recorrido-guiado.md`, 1793 líneas) y el ledger del proceso demuestra qué está hecho:

   | Tarea | Contenido | Estado |
   |---|---|---|
   | 1 | Tour persistence flags | ✅ implementada, con `report` y `review` |
   | 2 | Tour catalog (pure data) | ✅ implementada, con `report` y `review` |
   | 3 | Tour state machine | ✅ implementada, con `report`, `review` y `rereview` |
   | 4 | Guard de que todo target de spotlight existe | ⚠️ el test existe (`TourTargetRegistryTests.cs`), pero **no hay `report` ni `review`** |
   | 5 | El control de overlay de spotlight | ❌ **sin brief, sin código** |
   | 6 | Coordinador del tour y cableado con el shell | ❌ **sin brief, sin código** |
   | 7 | Página Tutorial, ruta y entrada de navegación | ❌ **sin brief, sin código** |
   | 8 | Pantalla de bienvenida de primer arranque | ❌ **sin brief, sin código** |
   | 9 | Docs y trazabilidad con la spec | ❌ **sin brief** |

   **Prueba material de que la feature no es visible:** `src/CA-O.UI/Tutorial/` contiene solo **3 ficheros** (`TourCatalog.cs`, `TourProgress.cs`, `TourState.cs`). No hay overlay, no hay coordinador, no hay `TutorialPage.xaml`, y `RouteTable` **no tiene** tag `tutorial`. Lo que existe es el sustrato lógico, no el recorrido guiado.
3. **El ledger del proceso prohíbe cualquier operación git.** `progress.md` lo dice literalmente en sus *Standing operator constraints*: *«**GIT IS OFF LIMITS.** No `git add`, no `git commit`, no `git push`, no branch, no worktree, nothing. All 11 commit steps in the plan are DEFERRED / DO NOT RUN and stay that way until the operator explicitly lifts `no subir nada todavia`.»* Es decir: **terminar el tutorial no basta para dejar la baseline limpia; además hay que levantar ese bloqueo.** Es el prerrequisito P0 (§6.21).
4. **«1278 tests verdes» es el estado del commit 2.2.3, no el del árbol.** Medido: el árbol tiene **1314 tests, todos verdes**, y los 5 ficheros `Tour*Tests.cs` aportan exactamente **36** de ellos (`CA-O.UI.Tests` pasa de 109 a 145 — ver §2.14).
5. **`.gitignore` excluye `docs/superpowers/` y `.superpowers/`.** El proceso documental del repo **no está versionado**. Este documento, en cambio, **sí está en `docs/` y sin ignorar** — se commiteará si nadie lo excluye, a diferencia de los specs del tutorial.
6. `memoria HOLA/` está untracked; al commitear hay que excluirla con pathspec `:!memoria HOLA`.
7. **Restricción operativa del repo:** CA-O.UI corre **elevada**, así que hay que **cerrar la app a mano antes de un build Release**, o el build falla con **MSB3027/MSB3021** (ficheros bloqueados). Un fallo con esos códigos es entorno, no código.

---

# PARTE 3 — RESTRICCIÓN TÉCNICA REAL

> Esta parte está reescrita con documentación oficial y fuentes primarias. Es la que más cambia respecto al borrador, porque el borrador contenía errores que habrían llevado a construir sobre arena.

## 3.0 Las tres rutas, y la que no existe

Para hacer overclock desde una aplicación de usuario **sin firmar driver**:

| Componente | ¿Posible en modo usuario sin driver? | Vía real |
|---|---|---|
| **GPU NVIDIA** — relojes, power limit, límite térmico, ventilador, reset | **Sí, pero con API privada no documentada** | `nvapi64.dll` (P/Invoke + `nvapi_QueryInterface`) |
| **GPU AMD** — power limit, reloj máximo, ventilador, VRAM | **Sí, y documentado** | **ADLX** (`adlx.dll`), no `amdocl64.dll` |
| **CPU Intel** — ratio turbo, offset de voltaje | **No** | XTU / IPF (no hay SDK redistribuible) o BIOS |
| **CPU AMD** — reloj/voltaje | **No** | Ryzen Master o BIOS (el Monitoring SDK es **read-only**) |
| **CPU (cualquiera)** — frecuencia/voltaje absolutos | **No** | `wrmsr` (ring 0), BIOS, o driver firmado del fabricante |
| **CPU (política)** — boost mode, tope de frecuencia, plan | **Sí** | `powercfg` (`PERFBOOSTMODE`, max processor frequency) — **limita, nunca supera el stock** |
| **Memoria** — leer velocidad nominal vs configurada | **Sí** | `Win32_PhysicalMemory` (SMBIOS Type 17) |
| **Memoria** — leer perfil XMP/EXPO, cambiar timings | **No** | EEPROM SPD vía SMBus (ring 0); timings los fija el UEFI en el POST |

## 3.1 GPU NVIDIA — viable hoy, pero sobre API privada

**Lo que es cierto:**

- `nvapi64.dll` **se instala con el driver** y se puede P/Invoke desde modo usuario ([NVAPI Driver Settings Programming Guide](https://developer.nvidia.com/downloads/nvapi-driver-settings-programming-guide); [foro NVIDIA, 2024-03-14](https://forums.developer.nvidia.com/t/getting-started-with-nvapi-c/286078)). No añade ningún driver kernel propio.
- Los **headers y librerías** del SDK están en [NVIDIA/nvapi](https://github.com/NVIDIA/nvapi) bajo **MIT**; la implementación la pone el driver instalado.
- Existe **una única exportación útil**: `nvapi_QueryInterface`. Desde C# hay que resolver cada función por su **ID de 32 bits** (`NvAPI_GPU_GetPstates20 = 1878528531`, `NvAPI_GPU_SetPstates20 = 256749163`).
- Requiere **elevación**: existe `NVAPI_INVALID_USER_PRIVILEGE` («requires Administrator privileges»).

**Lo que el borrador daba por soportado y no lo está:**

| Capacidad | Documentada en la referencia pública | Función real |
|---|---|---|
| Leer p-states / relojes | ✅ `NvAPI_GPU_GetPstates20` | [grupo P-State](https://docs.nvidia.com/nvapi/group__gpupstate.html) |
| Leer temperatura GPU | ✅ `NvAPI_GPU_GetThermalSettings` | [grupo Thermal](https://docs.nvidia.com/nvapi/group__gputhermal.html) |
| Escribir p-states / offsets | ❌ **no aparece** | `NvAPI_GPU_SetPstates20`, `SetPstatesInfo`, `EnableOverclockedPstates`, `SetPerfClocks` — **privadas** |
| Power limit | ❌ **no aparece** | `NvAPI_GPU_ClientPowerPoliciesSetStatus` — **privada** |
| Límite térmico | ❌ **no aparece** | `NvAPI_GPU_ClientThermalPoliciesSetLimit`, `NvAPI_GPS_SetThermalLimit` — **privadas** |
| Ventilador | ❌ **no aparece** (el grupo Cooler solo tiene `GetTachReading`) | `NvAPI_GPU_SetCoolerLevels` — **«Undocumented function»** |
| Reset | parcial | `RestoreCoolerSettings`, `RestoreCoolerPolicyTable` (privadas); la vía **soportada** es **Debug Mode** ([KB 5802](https://nvidia.custhelp.com/app/answers/detail/a_id/5802), actualizado 2026-02-25) |

Y la confirmación oficial del propio fabricante, en su foro de desarrolladores (2025-08-19), respondiendo a un desarrollador que pedía documentación «possibly under NDA»:

> *«**NVAPI SDK currently does not provide full GPU overclocking and undervolting support**»* — [foro NVIDIA](https://forums.developer.nvidia.com/t/overclocking/342592)

**El riesgo no es teórico, es de hace seis semanas:**

> *«With the latest driver update **616.56**, however, it seems as though NVIDIA has blocked the core power control functionality. […] any attempt to input any extra current results in a **black screen**, reminiscent of a driver crash.»* — [TechPowerUp, 2026-08-30](https://www.techpowerup.com/352157/nvidia-shuts-down-rtx-5000-power-limit-control-oc-in-latest-driver-update)

El artículo afecta a **mVolt+** y a **RTX 50**; Tom's Hardware reporta que *«The latest driver… seems to be blocking the app from making changes, hard crashing anytime you try to adjust the core power limit»* y que Hydra 2.3B seguía funcionando. Antes, en la misma familia, el driver 595.71 limitó el voltaje/OC en algunas RTX 50 y el **hotfix 595.76 lo restauró**.

**Estado real: VIABLE, FRÁGIL Y NO CONTRATADO.** No es «verificable» en el sentido de estable. Es exactamente la clase de dependencia que exige una prueba de capacidad en cada arranque y una degradación honesta, no una promesa.

**Anti-cheat:** usar NVAPI en modo usuario **no** dispara por sí mismo EAC/BattlEye. BattlEye lo dice: *«Generally we only ever ban for the use of actual cheats/hacks… non-cheat overlays and visual enhancement tools… are generally supported»*, y bloquea *«certain software that is using kernel drivers which contain known security issues»* ([FAQ BattlEye](https://www.battleye.com/support/faq)). El disparador real es un **driver kernel** con IOCTLs de MSR/memoria física ([secret.club, 2020-04-28](https://secret.club/2020/04/28/anticheat_blocking_overclocking_tools.html)). La fricción documentada es de **overlays** (RTSS/RICOCHET, EAC cerrando VRChat por un fichero «unt trusted»).

> **Consecuencia de diseño:** un cliente NVAPI en modo usuario es de bajo riesgo. **No añadir nunca** OSD, inyección, handles a procesos protegidos ni ningún componente kernel. Ese es el invariante.

## 3.2 GPU AMD — la vía es ADLX, no ADL2 ni OpenCL

**Corrección de B3.** El borrador decía «GPU AMD — vía `amdocl64.dll` (ADL2)». Es un error doble:

- `amdocl64.dll` es el **ICD de OpenCL** de AMD (runtime de cómputo), no una librería de display/tuning. Se registra como Installable Client Driver ([ptrumpis/OpenCL-AMD-GPU](https://github.com/ptrumpis/OpenCL-AMD-GPU)).
- La **ADL** real es `atiadlxx.dll` (x64) / `atiadlxy.dll` (x86) ([GPUOpen display-library, issue #14](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/issues/14)). Su documentación pública (v18.1) es de display/adaptador y **no publica entrada de overdrive**.
- La vía de las *soft power play tables* está **muerta** en las generaciones modernas: *«There will be no MPT for RDNA3. AMD really screwed it up this time… everything is double and triple secured»* ([WCCFTech](https://wccftech.com/community-driven-morepowertool-for-amd-gpus-will-not-support-rdna-3-due-to-hard-lock-users-to-pay-for-power-limits-features)).

**La vía correcta y documentada es ADLX** (v2.0, septiembre de 2026): *«ADLX is a modern library and SDK designed to access features and functionality of AMD systems in the categories of Display, 3D graphics, Performance Monitoring, **GPU Tuning**»* ([gpuopen.com/adlx](https://gpuopen.com/adlx/)). Con setters públicos documentados:

- `IADLXManualPowerTuning::SetPowerLimit`, `SetTDCLimit`, `GetPowerLimitRange` ([manual](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxmanualpowertuning/))
- `IADLXManualGraphicsTuning2::SetGPUMaxFrequency` — *«Sets the maximum frequency (offset) on a GPU»* — marcado *«for RDNA ASIC family»* ([manual](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxmanualgraphicstuning2/setgpumaxfrequency/))
- Tuning manual de ventilador y de VRAM, más muestras de auto/preset ([repo ADLX](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX))

**Requisitos y puertas:** exige la **instalación completa** de AMD Software: Adrenalin ([FAQ DH3-020](https://www.amd.com/en/resources/support-articles/faqs/DH3-020.html)); el tuning manual/VRAM está disponible *«for Radeon RX 5000 Series and later»*; hay que sondear con `IsSupported*` y los `Get*Range`. Las RDNA1/2 pasaron a una rama de driver «de mantenimiento» en Adrenalin 25.10.2 ([AMD blog, 2025-11-02](https://www.amd.com/en/blogs/2025/continued-support-for-every-radeon-gamer.html)).

**Estado: viable y mejor que lo que decía el borrador — pero se implementa en Fase 6, no antes, y con ADLX.**

## 3.3 CPU — por qué el «XTU SDK» no es la respuesta que el borrador creía

### Lo que es cierto

El overclock real de CPU exige **escribir MSR** (`wrmsr`), que es ring 0: *«These model-specific registers and the intrinsics to read/write them are only available when operating at a higher privilege level such as ring-0»* ([secret.club](https://secret.club/2020/04/28/anticheat_blocking_overclocking_tools.html)). No hay atajo ACPI. Y un driver tipo WinRing0/rwdrv/iomap está **muerto** (§3.4).

### Lo que el borrador daba por hecho y no se sostiene

| Supuesto del borrador | Realidad |
|---|---|
| «Intel XTU SDK» es un SDK redistribuible | **No hay SDK público ni concesión de redistribución.** `IntelOverclockingSDK.dll` es un componente **dentro de XTU**; el namespace `Intel.Overclocking.SDK.Tuning` aparece en DLLs de XTU; lo consumen servicios OEM (Alienware OCControl, OMEN Intel Overclocking SDK Service). Intel lo describe como *«enabling **motherboard vendors** for early enabling of Intel OC features»*: canal partner/OEM bajo la EULA de XTU |
| Funciona si XTU está instalado | **Depende de la versión y de la plataforma.** XTU **10.0.0.76** es **exclusivo de Arrow Lake / Core Ultra 200S** y *«Replaced XTU drivers with the Intel Innovation Platform Framework (IPF)»*; XTU **7.14.2.14** sirve a 14ª gen y anteriores. Core Ultra 200S requiere **IPF** instalado |
| Cualquier CPU Intel vale | **No.** *«supported with unlocked processors (K- and X-series) only»*; hace falta chipset de OC completo (Z890/Z790/Z690…) |
| Funciona siempre | **No.** XTU no corre bajo VBS salvo que se habilite «Undervolt Protection» en BIOS (12ª gen+) |
| Intel Performance Maximizer como plan B | **Descatalogado el 2023-04-24**: *«Intel Performance Maximizer will be discontinued as of April 24, 2023»* |

### La corrección de fondo (B2)

El borrador justificaba la Decisión D diciendo que Hypertune usa el XTU SDK **para evitar el bloqueo de anti-cheat de un driver kernel**. **Hypertune carga drivers kernel de fabricante** (§1.4). Es decir:

- La elección de XTU de Hypertune no es una lección de limpieza; **Hypertune hace las dos cosas**.
- CA-O **no puede** integrar el SDK de Intel legítimamente (no hay licencia de redistribución), y **no va a** cargar un driver kernel.
- Por tanto, para CPU en 2026, las opciones honestas son exactamente tres:
  1. **Detectar** XTU/IPF y ofrecer un flujo guiado que el usuario ejecute (CA-O mide y verifica; el usuario aplica). `EvidenceTier: Vendor`.
  2. **Tuning por política** en modo usuario: `PERFBOOSTMODE` (GUID `be337238-0d82-4146-a960-4f3749d470c7`), tope de frecuencia de procesador, plan de energía, core parking. **Limita y permite, nunca supera el stock ni fija voltaje.** Es real, medible y honesto — y es exactamente el tipo de «ajuste» que el catálogo de 93 ya sabe hacer con transacción y revert.
  3. **Driver firmado propio** (futuro, §6.14) con EV + HLK, y aun así expuesto a la blocklist de Microsoft.

> **Decisión D revisada:** *«XTU SDK cuando esté disponible»* pasa a *«detección + flujo guiado de XTU/IPF, más tuning por política en modo usuario; arquitectura preparada para un driver firmado propio en el futuro»*. Sin reescribir nada más.

## 3.4 La ruta de driver kernel: por qué está cerrada

| Hecho | Fuente |
|---|---|
| **La blocklist de drivers vulnerables está activa por defecto desde la actualización de Windows 11 de 2022**, y también se aplica con HVCI, Smart App Control o modo S. Se actualiza trimestralmente vía Windows Update | [Microsoft recommended driver block rules](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/design/microsoft-recommended-driver-block-rules) (actualizado 2026-05-04) |
| WinRing0 está clasificado como vulnerabilidad conocida: **CVE-2020-14979**, permite a procesos de baja integridad *«read and write to arbitrary memory locations»*. Defender lo detecta como `VulnerableDriver:WinNT/Winring0` desde ~marzo de 2025 | [MS support](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0), [NVD](https://nvd.nist.gov/vuln/detail/cve-2020-14979), [Neowin 2025-03-13](https://www.neowin.net/news/windows-1110-is-flagging-winring0-on-your-pc-monitoring-fan-control-apps-heres-why/) |
| Además existe la regla ASR *«Block abuse of exploited vulnerable signed drivers»* | MS Learn (arriba) |
| BattlEye bloquea software con drivers kernel con problemas de seguridad conocidos; FACEIT bloquea **firmantes enteros** (bloqueó PawnIO por su certificado) | [BattlEye FAQ](https://www.battleye.com/support/faq), [PawnIO issue #1](https://github.com/namazso/PawnIO.Setup/issues/1) |
| El ecosistema migró: LibreHardwareMonitor, FanControl y OpenRGB adoptaron **PawnIO**, un driver firmado y restringido, el **2025-09-16** | [commit eb5e1a2](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/commit/eb5e1a20be996d4865170b13bab97af43d97f341) |
| Firmar exige certificado **EV** y pasar **HLK** | [LHM issue #984](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/issues/984) |
| Los drivers de fabricante no son reutilizables: `AMDRyzenMasterDriver.sys` lleva **CVE-2023-20564** y está en LOLDrivers; la antigua extensión de rendimiento firmada de Intel llegó a incluir WinRing0 y hoy la marca Defender | [LOLDrivers](https://www.loldrivers.io/drivers/13973a71-412f-4a18-a2a6-476d3853f8de/), [Intel Community](https://community.intel.com/t5/Mobile-and-Desktop-Processors/Intel-needs-to-fix-performancedriverextension-inf-which-causes/m-p/1709376) |

> **Consecuencia, y es la más importante de todo el documento:** la decisión de CA-O de **no cargar driver kernel** no es una preferencia estética. Está **impuesta por el sistema operativo y por los anti-cheat**. El borrador ya llegaba a esta conclusión; ahora está demostrada, y la conclusión se refuerza: **hay que decirlo como ventaja**, porque es la única arquitectura que funciona.

## 3.5 Memoria — la honestidad exige recortar la promesa

**Corrección de F16.** El borrador proponía «Detectar perfiles XMP/EXPO presentes — Lectura SMBIOS vía WMI (`Win32_PhysicalMemory`)». **SMBIOS Type 17 no tiene campo de XMP ni de EXPO**: los perfiles viven en la EEPROM SPD, y leerlos exige SMBus, que exige un driver kernel temporal ([Thaiphoon Burner](https://www.thaiphoonburner.com/blog/fix-no-smbus-driver-found-error)).

**Lo que Windows sí expone, en modo usuario y sin driver** ([`Win32_PhysicalMemory`](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory)):

| Propiedad | Significado |
|---|---|
| `Speed` | Velocidad **nominal** del módulo (la del JEDEC/XMP anunciado por el fabricante) |
| `ConfiguredClockSpeed` | Velocidad **configurada** en la que está corriendo |
| `ConfiguredVoltage`, `SMBIOSMemoryType`, `Manufacturer`, `PartNumber` | Datos del módulo |

**La fila honesta que sustituye al borrador:**

| Lo que CA-O puede decir | Cómo | Nivel de evidencia |
|---|---|---|
| «Tu kit es de 6400 MT/s y está corriendo a 4800» | `Speed` vs `ConfiguredClockSpeed` | `Official` (SMBIOS/CIM documentado) |
| «Tu kit es de 6400 y está corriendo a 6400» → **inferencia**: muy probablemente XMP/EXPO está activo | misma comparación | `Empirical` — **es una inferencia, no una lectura del perfil**, y debe presentarse como tal |
| «Este equipo **no** tiene el perfil activo; actívalo en la BIOS» | Guía enlazada al fabricante de la placa | `Vendor` |
| «Los timings reales (tCL, tRCD, tRP, tRAS) y los voltajes de DRAM» | **No disponibles en modo usuario** | — **no se muestra, no se promete** |

**Y el texto de UI que el borrador escribió sigue siendo correcto, ahora con la razón precisa:**

> «No podemos leer ni aplicar perfiles XMP/EXPO ni timings de memoria: el perfil vive en la EEPROM SPD del módulo y Windows solo lo expone al firmware durante el POST. No hay API de Windows que lo permita a una aplicación sin firmar un driver, y los timings los fija la UEFI. Lo que sí hacemos: leer la velocidad nominal y la configurada en SMBIOS, decirte si coinciden (y por tanto si el perfil parece activo), y enlazarte al fabricante de tu placa para el resto.»

Cada fila lleva su `EvidenceLevel`. Si algo es `Heuristic`, se dice. Si es `Unknown`, **no aparece en el perfil por defecto** — misma regla que el catálogo de 93.

## 3.6 Telemetría y medición — lo que hay que reescribir

### WHEA — la clase WMI no existe (corrige F13)

`Win32_WHEAHardwareError` **no existe** en la documentación CIM/Win32 de Microsoft. El modelo de Microsoft es **eventos**: *«WHEA raises an Event Tracing for Windows (ETW) event whenever a hardware error occurs. These hardware error events are recorded in the system event log»* ([WHEA Hardware Error Events](https://learn.microsoft.com/en-us/windows-hardware/drivers/whea/whea-hardware-error-events)).

| Qué usar | Canal / proveedor | IDs |
|---|---|---|
| Errores de hardware corregidos y fatales | `Microsoft-Windows-WHEA-Logger` (registro **System**) | **17** corregido · **18** fatal/no corregido · **19** corregido · **47** corregido con contador |
| Detalle por hardware | `Microsoft-Windows-Kernel-WHEA/Errors` y `/Operational` | — |

Accesible desde modo usuario con `EventLogWatcher` / `Get-WinEvent`. **Cero driver.** (Nota adicional: `wmic` está siendo retirado de Windows 11 24H2/25H2 — usar CIM.)

### Bugcheck — el proveedor no se llama `BugCheck` (corrige F14)

| ID | Proveedor | Texto |
|---|---|---|
| **1001** | **`WER-SystemErrorReporting`** (moderno: `Microsoft-Windows-WER-SystemErrorReporting`) | «The computer has rebooted from a bugcheck. The bugcheck was: 0x…» |
| **41** | `Kernel-Power` | «The system has rebooted without cleanly shutting down first» |

Fuente: [Troubleshoot unexpected reboots using system event logs](https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/troubleshoot-unexpected-reboots-system-event-logs). `BugCheck` es el nombre legado de la era Windows 7: **aceptar ambos**, pero no depender del literal.

### Deriva de reloj — el QPC es ciego al OC (corrige F15)

> *«Is QPC accuracy affected by processor frequency changes caused by power management or Turbo Boost technology? **No.** If the processor has an invariant TSC, the QPC is not affected by these sort of changes.»* — [Acquiring high-resolution time stamps](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)

Es decir: **`Stopwatch`/QPC no detecta un overclock** en una máquina con TSC invariante — que es casi cualquier equipo moderno. La métrica `ClockDrift` del borrador, tal como estaba descrita, **no mediría nada**.

**Alternativas reales:**

| Objetivo | Método correcto | Nota |
|---|---|---|
| Frecuencia efectiva de CPU | `\Processor Information(_Total)\% Processor Performance` × `Win32_Processor.MaxClockSpeed` | Es la receta estándar (la usa OSHI). El contador `\Processor Information\Processor Frequency` **no es fiable**: «the value is (almost always) constant» |
| Relojes de GPU | NVAPI `NvAPI_GPU_GetAllClockFrequencies` / `GetPstates20`; ADLX PerfGPU metrics | Modo usuario, sin driver ni admin |
| Temperatura de GPU | NVAPI `NvAPI_GPU_GetThermalSettings`; ADLX | Documentado |
| Temperatura de CPU | **No disponible** | Ver abajo |
| Consumo | NVAPI/ADLX para GPU; para CPU, no hay vía de usuario fiable | — |

### Temperatura de CPU — el hueco que no se cierra (corrige B4)

| Vía | Estado |
|---|---|
| `MSAcpi_ThermalZoneTemperature` (WMI) | Devuelve «not supported» en muchos equipos; donde funciona, *«typically returns motherboard or chassis ambient temperatures rather than actual CPU core temperatures»* |
| LibreHardwareMonitor / OpenHardwareMonitor / HWiNFO | **Usan un driver kernel firmado.** LHM: *«This is a 64-bit Windows kernel driver identified as WinRing0. While it can be used by legitimate hardware monitoring or overclocking utilities, it provides a user-mode interface to perform highly privileged, low-level hardware operations»*; migrado a PawnIO el 2025-09-16 |
| Intel Power Gadget | **Descatalogado** (INTEL-SA-01037, 2024-05-14); cubría solo 2ª–10ª gen |
| AMD Ryzen Master Monitoring SDK | **Read-only** y **autoinstala el driver de AMD** (`AMDRyzenMasterDriver.inf/.cat`) |
| Task Manager de Windows 11 | No muestra temperatura de CPU; sí la de GPU, porque el driver gráfico la expone |

**No hay API nativa de Windows para temperatura de CPU.** Esto tiene una consecuencia dura sobre el diseño:

> **El guardarraíl térmico del §6.5 no puede basarse en temperatura de CPU en modo usuario.** Si el diseño exige temperatura de CPU para permitir OC de CPU, entonces **el OC de CPU queda desactivado en la mayoría de equipos**, y eso hay que declararlo en la UI, no esconderlo.

**La salida elegante, y es mejor que la del borrador:** para GPU, **no estimar la temperatura en software, sino fijar el límite térmico en el firmware** mediante la API del fabricante (`NvAPI_GPU_ClientThermalPoliciesSetLimit` / `IADLXManualPowerTuning` + límites). El hardware aplica el límite aunque la app muera. Eso es un guardarraíl **más fuerte** que «el motor vigila un número» y elimina la dependencia de sensores. Para CPU, el guardarraíl es de **política** (tope de frecuencia, boost mode, plan), no térmico.

## 3.7 Tabla resumen: qué es realmente posible en modo usuario (Windows 11, 2026)

| Capacidad | ¿Sin driver? | API correcta | Notas |
|---|---|---|---|
| NVIDIA offsets de reloj, power limit, límite térmico | **Sí, no documentado** | `NvAPI_GPU_SetPstates20`, `ClientPowerPoliciesSetStatus`, `ClientThermalPoliciesSetLimit` | API privada; admin; rotura probada (615.56/616.56, 2026-08) |
| NVIDIA ventilador | **Parcial** | `NvAPI_GPU_SetCoolerLevels` («undocumented») | Dependiente de EC/portátil |
| NVIDIA reset | **Sí** | `RestoreCoolerSettings`, p-states de stock, o **Debug Mode** | Debug Mode es la vía soportada |
| NVIDIA lectura (temp/reloj/power) | **Sí, documentado** | `GetThermalSettings`, `GetPstates20`, `GetAllClockFrequencies` | sin driver ni admin |
| AMD tuning de GPU (reloj, power, thermal, ventilador, VRAM) | **Sí, documentado** | **ADLX**: `IADLXManualPowerTuning::SetPowerLimit`, `IADLXManualGraphicsTuning2::SetGPUMaxFrequency` | requiere Adrenalin completo; gating por ASIC RDNA |
| CPU Intel OC | **No** | XTU 7.x (≤14ª gen) / XTU 10.x + IPF (Core Ultra 200S) | SDK embebido en XTU, canal OEM/partner, no redistribuible; K/X + placa Z |
| CPU AMD OC | **No** | Ryzen Master / BIOS | Monitoring SDK **read-only** y con driver propio |
| Frecuencia/voltaje de CPU (cualquier marca) | **No, ring 0 o BIOS** | `wrmsr`, BIOS, o driver firmado del fabricante | Confirmado por Intel, AMD y secret.club |
| Política de CPU (boost mode, tope de frecuencia, plan) | **Sí** | `powercfg` (`PERFBOOSTMODE`, max processor frequency) | Limita/permite; nunca supera el stock |
| Memoria: nominal vs configurada, part number | **Sí** | `Win32_PhysicalMemory` / SMBIOS Type 17 | Sin XMP/EXPO, sin timings |
| XMP/EXPO: leer perfil o cambiar timings | **No** | — | SPD/SMBus exige ring 0; los timings los fija la UEFI |
| WHEA | **Sí** | `Microsoft-Windows-WHEA-Logger` 17/18/19/47 | `Win32_WHEAHardwareError` no existe |
| Historial de bugcheck | **Sí** | Evento 1001, proveedor `WER-SystemErrorReporting` (+ Kernel-Power 41) | El origen no es «BugCheck» |
| Frecuencia efectiva de CPU | **Sí** | `% Processor Performance` × `MaxClockSpeed` | `Processor Frequency` no fiable; el QPC es ciego al OC |
| Telemetría de GPU | **Sí** | NVAPI / ADLX | sin driver ni admin |
| **Temperatura de CPU** | **No (no de forma fiable)** | — | LHM/OHM/HWiNFO usan driver firmado; Intel Power Gadget descatalogado |
| Temperatura de GPU | **Sí** | NVAPI `GetThermalSettings`, ADLX | — |
| Ruta ring-0 MSR (WinRing0/rwdrv/iomap) | n/a (driver) | — | Blocklist por defecto desde Win11 2022; Defender desde ~mar-2025; bloqueado por BattlEye/EAC; FACEIT bloquea firmantes |

## 3.8 Consecuencias de la Parte 3 sobre el diseño

1. **La sección Overclock arranca como sección de GPU NVIDIA + política de CPU + memoria honesta.** No hay un «OC de CPU» que prometer.
2. **Toda escritura se prueba antes de ofrecerse.** `Probe()` no devuelve «soportado» teórico, devuelve lo que **esta máquina, este driver y esta versión** aceptan.
3. **El motor de medición es lo que hace creíble el resultado**, no el OC. Y ya existe (§2.8).
4. **El guardarraíl térmico se delega al firmware** donde sea posible (GPU), y en CPU se declara la limitación.
5. **Todo dato que no se pueda obtener se declara como no obtenido.** Nunca se estima un número de temperatura, frecuencia o voltaje que no se ha leído.

---

## 3.9 Qué NO se puede validar con el hardware disponible — y cómo se declara

> Sección nueva. Es la aplicación al propio CA-O de la disciplina de evidencia que este documento exige a todo lo demás.

**Hardware disponible confirmado (2026-10-10):** **un escritorio con GPU NVIDIA RTX 40/30/20.**

| Capacidad del diseño | ¿Validable en hardware real? | Cómo se declara |
|---|---|---|
| Backend NVIDIA: lectura (temp, relojes, p-states) | ✅ Sí | `EvidenceLevel.Vendor` |
| Backend NVIDIA: **escritura** (offsets, power limit, límite térmico) | ✅ **Sí** — las RTX 40/30/20 no están afectadas por el bloqueo del driver que golpeó a las RTX 50 | `EvidenceLevel.Vendor`, con la versión de driver registrada en la evidencia |
| Estado `DriverBlocked` (caso 616.56 en RTX 50) | ❌ **No** — no hay RTX 50 a mano | Solo reproducible con `DriverBlockedBackend`. La UI debe decir «puede ocurrir con otras GPU y drivers» |
| **Backend AMD (ADLX)** | ❌ **No** — no hay GPU AMD con Adrenalin | **`D9`: se implementa y se declara sin validar en hardware real.** `EvidenceLevel.Heuristic` en el propio módulo, no en el ajuste. La UI lo dice en el estado de sección |
| Flujo guiado XTU/IPF | ⚠️ **Parcial** — depende de qué CPU tenga ese escritorio | Si no hay K/X en placa Z, el camino feliz no se prueba |
| Tuning por política de CPU (`powercfg`) | ✅ Sí, en cualquier x86-64 | `EvidenceLevel.Official` |
| Memoria: nominal vs configurada | ✅ Sí | `EvidenceLevel.Official` |
| WHEA, bugcheck, reloj efectivo | ✅ Sí (provocables con `ScriptedProbe` y con eventos reales) | `Empirical` |
| Regla D4 completa (dos arranques sucios) | ⚠️ **Parcial** — el test es determinista contra el store, pero la prueba end-to-end exige arrancar la máquina dos veces con un OC inestable real | Se prueba en el motor; la prueba de humo queda documentada como no ejecutada |
| Superficie de la UI en GPU AMD y RTX 50 | ❌ No | Tests contra backend falso + captura con estado simulado |

**Regla que se deriva, y es la consecuencia de diseño más honesta de esta revisión:**

> **CA-O debe aplicar su propio `EvidenceLevel` a sus módulos, no solo a los ajustes.** Un backend que no se ha ejecutado en hardware real **no puede presentarse como soportado**. La Fase 6 (AMD) se entrega con su estado de sección diciéndolo, exactamente igual que CA-O exige al catálogo de 93 optimizaciones.

**Consecuencia sobre la Fase 3:** el hardware disponible es suficiente para el objetivo G1. Las RTX 40/30/20 están en el rango donde los setters de NVAPI funcionan; el riesgo de R1 (que NVIDIA los rompa) es real pero **no impide validar la fase hoy**.

---

# PARTE 4 — DECISIONES DE BRAINSTORMING (revisadas)

## 4.1 Decisión 1 — CPU — **CERRADA (D1)**

| | |
|---|---|
| **Original** | Opción D: XTU SDK cuando esté disponible; deshabilitado con explicación clara cuando no; arquitectura preparada para un driver firmado propio. |
| **Problema** | Se apoyaba en un supuesto falso: que el XTU SDK es un SDK redistribuible con el que se puede integrar (no lo es), y en una lectura falsa de Hypertune (que usa XTU *en lugar de* un driver, cuando usa **ambos**). |
| **Decisión final** | **Opción D′, en tres niveles:** **(a)** detección de XTU/IPF + flujo guiado que el usuario ejecuta, con `EvidenceLevel.Vendor`; **(b)** **tuning por política** en modo usuario (`PERFBOOSTMODE`, tope de frecuencia, plan, core parking) — real, medible, reversible, y encaja en el catálogo transaccional existente; **(c)** arquitectura lista para un driver firmado propio, sin reescribir el motor. |
| **Coste asumido** | El OC real de CPU no existe en CA-O, y no es un problema de ingeniería: es una decisión de Microsoft y de los anti-cheat (§3.4). |
| **Lo que NO se hará** | Integrar el SDK de Intel (sin licencia), cargar WinRing0/rwdrv/iomap, ni prometer «+X MHz / +Y mV» de CPU. |

## 4.2 Decisión 2 — Alcance — **CERRADA (D2), con un ajuste**

| | |
|---|---|
| **Original** | Opción B: CPU + GPU + Memoria + perfiles de juego. Network / Windows Features / Cleaning **no se duplican**. |
| **Veredicto** | **Correcta y bien razonada.** Hypertune tiene cinco secciones; CA-O ya tiene las otras cuatro en forma superior y repartidas con transacción, journal y revert. Duplicarlas dentro de Overclock sería una regresión. |
| **Ajuste** | Hay que añadir explícitamente a la lista de **no duplicados** el **sistema de medición A/B** (§2.8): no se reimplementa, se **reutiliza**. Y el **pilar BENCHMARK del README/ARCHITECTURE no se renombra** — D5 lo conserva. |

## 4.3 Decisiones nuevas — **CERRADAS**

> El borrador dejó estas cinco implícitas. Están resueltas y sus consecuencias aplicadas al resto del documento. El resumen ejecutivo está en §0.5.

| # | Decisión | Opciones consideradas | **Elección** |
|---|---|---|---|
| **D3** | **¿En qué proceso se ejecutan las escrituras de OC?** | (a) proceso de UI elevado; (b) servicio SYSTEM (sesión 0); (c) helper elevado | **(a) para el OC de GPU; (b) para política de CPU, journal, checkpoints y puerta anti-cheat.** La UI ya corre `requireAdministrator` y NVAPI/ADLX operan sobre el estado de display de la **sesión interactiva**; el servicio (sesión 0) no lo ve. Coste asumido: el OC de GPU es de **sesión**. Ver §5.3 |
| **D4** | **¿Cómo se reconcilia el revert automático tras cuelgue con la decisión de 2.2.3?** | (a) regla estricta de 2.2.3 siempre; (b) excepción acotada; (c) revert al primer arranque sucio | **(b), con umbral explícito.** Primer arranque sucio: **avisa y ofrece** revertir. **Segundo consecutivo: revierte** al último bueno y marca `Interrupted`. Cubre el caso en que el usuario no puede llegar a la UI sin violar la regla general. Ver §6.6 |
| **D5** | **¿Qué pasa con la sección Benchmark?** | (a) añadir Overclock y conservar Benchmark; (b) renombrar la página y conservar el pilar; (c) renombrar el pilar a `MEASURE` y fusionar | **(a).** Se **añade** `OverclockPage` y el tag `"overclock"`; `Benchmark` queda **intacto** (página, tag, motor y pilar). Coste: dos secciones coexisten. A cambio: **cero migración, cero riesgo documental, cero pérdida de tests**. Ver §6.16 |
| **D6** | **¿Qué pasa con los 17 tests de `Benchmark.Tests`?** | (a) redistribuir; (b) conservar donde están; (c) retirar | **(b): no se retiran ni se mueven.** Prueban el motor de medición, que se conserva. Con D5(a) no hay nada que mover |
| **D7** | **¿Se adopta un driver firmado propio?** | (a) nunca; (b) solo con puertas escritas; (c) prioritario | **(b), y hoy la respuesta es no.** Las seis puertas están escritas en §6.14. La blocklist de drivers vulnerables está activa por defecto desde Win11 2022 y FACEIT bloquea firmantes enteros: ni un driver firmado y restringido es seguro |
| **D8** | **¿En qué orden se entrega?** | (a) Fase 1 de solo lectura; (b) escritura de GPU ya en Fase 1; (c) motor puro primero | **(a).** El primer incremento **no depende de una API privada** y es demostrable en cualquier máquina, incluida una sin GPU NVIDIA. La escritura de GPU es la **Fase 3**. Ver §6.13 |
| **D9** | **¿Qué hacemos con el backend AMD sin hardware para validarlo?** | (a) implementar y declarar sin validar; (b) aparcar hasta tener GPU AMD; (c) marcar como experimental | **(a).** Se implementa, se prueba contra backend falso, y **se declara sin validar en hardware real con su `EvidenceLevel` honesto**: la UI lo dice. Es la misma regla que CA-O exige al catálogo de 93. Ver §3.9 |

---

# PARTE 5 — ARQUITECTURA

## 5.1 Los tres enfoques considerados (conservado)

### Enfoque 1 — Adaptadores por fabricante + motor de estabilidad compartido
Un `IOverclockBackend` por fabricante (`NvidiaNvapiBackend`, `AmdAdlxBackend`, `IntelPolicyBackend`), cada uno devolviendo una lista uniforme de capacidades, con un motor de estabilidad común por encima.
- **A favor:** simple de razonar, backends pequeños.
- **En contra:** la interfaz común se queda corta. El power limit de GPU, el tope de frecuencia de CPU y el reset de VBIOS no se expresan bien en un tipo común. Acabas con campos opcionales y un «eventualmente-tipo-específico».

### Enfoque 2 — Catálogo declarativo de knobs + motor genérico ✅ **ELEGIDO**
Cada ajuste es **DATO** (id, componente, backend, rango, paso, unidad, riesgo, evidencia, lector, aplicar/restaurar). Un motor genérico recorre el catálogo, escala, valida y journaliza. Los backends son finos: leer/escribir un valor.
- **A favor:** encaja con la cultura de `docs\OPTIMIZATION-CATALOG.md` (que ya tiene 93 entradas de datos con evidencia/riesgo/reversibilidad); la UI se genera desde el catálogo; el motor es trivialmente testeable; el driver firmado futuro = una fila más + un backend.
- **En contra:** riesgo de API de mínimo común denominador. Se resuelve con `KnobDependency` (§6.3).

### Enfoque 3 — Rebanadas verticales por fabricante
NVIDIA completa, AMD completa, Intel completa, cada una con su camino nativo; solo se unifican UI, transacciones y reporting.
- **A favor:** fidelidad máxima.
- **En contra:** lógica triplicada — y la recuperación tras cuelgue y los deltas honestos, justo donde Hypertune falla, habría que reescribirlos tres veces.

## 5.2 Recomendación — Enfoque 2 con extensiones de capacidad

> **ESTADO: APROBADO por el usuario el 2026-10-10, con las correcciones de esta revisión.**

El catálogo como espina dorsal, con un contrato de backend que admite extensiones donde el modelo genérico no alcanza — y **reutilizando los enums que ya existen**:

```csharp
// CA-O.Core/Overclock/Abstractions/IOverclockBackend.cs
public interface IOverclockBackend
{
    string Id { get; }                              // "nvidia-nvapi" | "amd-adlx" | "intel-policy" | "msr-signed"
    OverclockCapabilities Probe(CancellationToken ct);   // qué soporta ESTA máquina, ESTE driver, AHORA
    ValueTask<KnobRead>        ReadAsync(KnobId id, CancellationToken ct);
    ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue v, CancellationToken ct);
    ValueTask<UnitSnapshot>    SnapshotAsync(CancellationToken ct);   // para revert
    ValueTask<RestoreOutcome>  RestoreAsync(UnitSnapshot s, CancellationToken ct);
}

// Lo que el catálogo no puede expresar: reset de VBIOS, activación de XMP, política de CPU
public interface IOverclockExtension
{
    string Id { get; }
    bool Handles(OverclockOperation op);            // Op.GpuVbiosReset, Op.CpuBoostPolicy, ...
    ValueTask<ExtensionResult> ExecuteAsync(OverclockOperation op, CancellationToken ct);
}
```

**Las cuatro reglas de reutilización (nuevas, corrigen B5/B6/B10):**

| Regla | En vez de… | Usar… |
|---|---|---|
| Evidencia | `enum EvidenceTier` nuevo | **`CAO.Shared.EvidenceLevel`** (`Enums.cs:33`, 123 consumidores) |
| Riesgo | `enum KnobRisk { Safe, Moderate, High }` | **`CAO.Shared.RiskLevel`** (`Safe, Low, Moderate, High, Critical`) |
| Fases | `enum TuningPhase` nuevo | **`CAO.Shared.TransactionPhase`** (`Contracts/Lifecycle.cs:75`) |
| Rutas | `C:\ProgramData\CA-O\overclock\runs\` literal | **`CaOPaths`** con una propiedad nueva `OverclockRunsDirectory` |

## 5.3 Dónde se ejecuta la escritura — **DECIDIDO (D3)**

> **Resuelve B9. Es la decisión arquitectónica número uno, y está cerrada: la escritura de OC de GPU ocurre en el proceso de UI elevado.**

CA-O tiene hoy una regla fuerte: **toda mutación cruza el pipe al servicio SYSTEM**. Es la regla correcta para registro, servicios, BCD, energía y red. **No sirve para el tuning de GPU**, por tres razones:

1. El servicio corre en **sesión 0**, sin estación de trabajo ni escritorio. El estado de OC de NVAPI/ADLX está asociado a la sesión de display del usuario.
2. Un servicio no debe depender de que haya un usuario con sesión iniciada para mantener un OC. Un OC aplicado desde sesión 0 sobrevive de forma impredecible y no es reversible por el usuario.
3. `ArchitectureDependencyTests.UiNeverSpawnsProcessesOrHostsThePipe` prohíbe `Process.Start` en `CA-O.UI`, así que lanzar un helper desde la UI **rompería un test de contrato** salvo excepción documentada (como ya se hizo, con justificación, para `SettingsPage`, `ExplorerRecovery` y `DriversPage`). **La opción (c) queda descartada por este motivo.**

**Reparto decidido, que respeta los contratos existentes:**

| Responsabilidad | Proceso | Por qué |
|---|---|---|
| **Escritura de OC de GPU** (NVAPI/ADLX) | **Proceso de UI elevado** (`CA-O.UI`, `requireAdministrator`) | Es la sesión interactiva; el usuario ve y puede abortar; el OC muere con la sesión, que es el comportamiento seguro |
| **Lectura de telemetría de GPU** | Proceso de UI elevado | Mismo motivo |
| **Política, validación, planificación, máquina de estados, evaluación de estabilidad** | `CA-O.Core` (invocado por quien escribe) | La política vive una vez, testeable sin hardware |
| **Journal y checkpoints** | Servicio SYSTEM vía IPC **y** fichero local de respaldo | El journal debe sobrevivir al cierre de la UI |
| **Tuning por política de CPU** (`powercfg`) | Servicio SYSTEM vía IPC | Es una mutación de sistema clásica; ya existe el camino y el catálogo |
| **Detección de anti-cheat y de juegos** | Servicio SYSTEM vía IPC (ya existe) | El servicio ve todos los procesos |
| **Puerta anti-cheat y autorización** | `CA-O.Core` en el servicio | Es política |

**Consecuencias que hay que escribir en el diseño:**

1. El proceso de UI **sí** puede P/Invoke (`DllImport`), pero **no** puede añadir `using System.Management` (lo prohíbe el test) ni `Process.Start`. Los backends de GPU deben invocar NVAPI/ADLX por `DllImport` **sin** System.Management de por medio.
2. El backend de GPU vive en `CA-O.Infrastructure` (referenciable por la UI) — **la UI ya referencia `CA-O.Infrastructure`** (`README.md:121`). No hace falta ninguna frontera nueva.
3. El estado de OC de GPU es **de sesión**. El `KnobScope { Session, Persistent }` del borrador es correcto y ahora tiene una razón concreta: la mayoría de knobs de GPU son `Session`, y eso debe ser explícito en la UI («este ajuste se pierde al cerrar la sesión»).
4. Si en el futuro se quiere OC persistente, la vía **no** es el servicio: es un driver firmado (§6.14), y D7 ya dice que hoy no.
5. **Consecuencia sobre el modo solo lectura:** si el servicio no está instalado, la UI opera en modo solo lectura (`README.md:48`). Con D3, **Overclock sigue funcionando para GPU sin el servicio**, porque escribe en su propio proceso. Solo pierde el journal en el servicio y el tuning de CPU. **Es una ventaja de D3 que conviene documentar en la UI.**

## 5.4 Contratos IPC — lo que hay que añadir

> **Resuelve B8.** Toda mutación cruza el pipe, y el pipe tiene reglas duras.

| Qué | Estado actual | Qué hace falta |
|---|---|---|
| `PrivilegedOperationKind` | **18 miembros** (0..17), `Shared\IPC\IpcProtocol.cs:21` | Añadir los nuevos **al final** (el índice es el valor en el wire) |
| `docs\IPC_PROTOCOL.md` | Dice «17 miembros», `operation: 0..16` | **Actualizar** (ya está desactualizado por `RecoverTransaction`) |
| Payloads | «cada operación tiene SU tipo; no grab-bag» | Un payload por operación nueva |
| Validación | 9 pasos en orden; `OptimizationId` `[a-z0-9-]{1,80}`; allowlist por operación | Extender el validador + `CA-O.Security.Tests` |
| Límites | request **≤ 64 KB**, response **≤ 256 KB** | **El resultado de un tuning NO cabe en una respuesta IPC si incluye el historial completo de puntos.** El historial se persiste y se lee por streaming, o la respuesta lleva solo el resumen |

**Operaciones nuevas propuestas (mínimas):**

```
OverclockProbe          → capacidades + knobs de esta máquina (sin tocar nada)
OverclockPrepare        → valida un perfil contra las capacidades; devuelve el plan de pasos
OverclockApplyKnob      → aplica un solo knob (journalizado)
OverclockRestoreUnit    → restaura un snapshot de unidad
OverclockFinalize       → cierra el run (Commit | RolledBack | Failed)
OverclockGetRunState    → estado del run en curso o interrumpido (para la reanudación)
```

> **Nota crítica sobre nombres:** `OverclockFinalize` debe cerrar con uno de los **cinco eventos terminales existentes**. **No se añade ninguna fase terminal nueva** (corrige B5). Las fases de OC nuevas son **no terminales** y se añaden a `TransactionPhase` como miembros nuevos **antes** del cierre:

```csharp
// Extensión de Shared/Contracts/Lifecycle.cs — NO se crea un enum paralelo
public enum TransactionPhase
{
    // ... los 18 existentes, sin reordenar (el valor va al journal) ...
    OverclockStarted,          // nuevos, todos NO terminales
    OverclockCheckpointTaken,
    OverclockStepApplied,
    OverclockStepRejected,
    OverclockTestPassed,
    OverclockTestFailed,
    OverclockInterrupted,
    // IsTerminal() NO cambia: sigue siendo
    // { Commit, RolledBack, Failed, CancelledBeforeApply, RecoveryCompleted }
}
```

**Invariante:** todo run de OC termina en `Commit`, `RolledBack`, `Failed` o `CancelledBeforeApply`. Si termina en cualquier otra cosa, `Incomplete()` lo detecta — que es exactamente lo que se quiere.

---

# PARTE 6 — DISEÑO DE LA SECCIÓN OVERCLOCK

> Deriva del Enfoque 2 (PARTE 5.2) con las correcciones de las Partes 0 y 3.

## 6.1 Objetivos y no-objetivos

### Objetivos

| # | Objetivo | Cómo se cumple |
|---|---|---|
| G1 | Tuning de GPU NVIDIA funcional hoy, con degradación honesta cuando el driver lo bloquee | Backend NVAPI sobre `nvapi64.dll`, con `Probe()` real y estados de no-soportado |
| G2 | Tuning de CPU **honesto**: guiado cuando hay XTU/IPF, por política siempre | Detección + `powercfg`; **nunca** se promete «+X MHz» |
| G3 | Sobrevivir a cuelgues sin intervención humana cuando el usuario no puede intervenir | Checkpoints persistentes + política de recuperación reconciliada (§6.6) |
| G4 | Nunca dejar la máquina inestable | Restauración al último bueno + límite térmico delegado al firmware |
| G5 | Deltas honestos, no un score opaco | **Reutilizar** el motor de medición existente (§2.8), no reimplementarlo |
| G6 | Anti-cheat-safe por construcción | Solo APIs de fabricante en modo usuario; **cero componentes kernel**; jamás ficheros de juego |
| G7 | Ser estrictamente mejor que Hypertune | Cada línea del §1.9 convertida en requisito |
| G8 | Testeable sin hardware | Motor genérico + backend falso + sonda guionizada |
| G9 | **(NUEVO)** Cero componentes residentes | Sin servicios, drivers ni tareas programadas propias |
| G10 | **(NUEVO)** Ningún dato no leído se presenta como leído | Sin temperatura estimada, sin frecuencia inventada, sin perfil XMP «detectado» |

### No-objetivos (fuera de alcance, con la razón corregida)

| No-objetivo | Razón |
|---|---|
| Firmar y distribuir un driver kernel propio | **No es solo coste**: la blocklist de drivers vulnerables está activa por defecto desde Win11 2022, y FACEIT bloquea firmantes. No resuelve el problema (§3.4) |
| Timings de memoria reales (tCL, tRCD, voltajes) | No hay API de usuario. **Pero además el borrador prometía leer XMP/EXPO y eso tampoco es posible** (§3.5) |
| Integrar el SDK de Intel XTU | No hay licencia de redistribución; es un componente OEM/partner de XTU (§3.3) |
| **Temperatura de CPU** (salvo que el usuario tenga un monitor con driver propio) | No hay API de usuario sin driver firmado (§3.6). **La temperatura de GPU sí se usa** |
| Reimplementar Network / Windows Features / Cleaning | Ya existen con transacción y revert, muy por encima de Hypertune |
| Soporte de GPU AMD en la primera entrega | Se añade en Fase 6 con **ADLX** (no ADL2) |
| Modificar ficheros dentro de juegos | Anti-cheat y riesgo de baneo |
| Overlays, OSD, inyección en procesos de juego | Es lo que **sí** dispara anti-cheat |
| Aplicar OC persistente sin driver | El estado de OC de GPU es de sesión; persistirlo exige driver |

## 6.2 Arquitectura en capas

```
┌──────────────────────────────────────────────────────────────────────────┐
│  CA-O.UI  (elevado, sesión interactiva)                                   │
│    OverclockPage.xaml + OverclockViewModel                                │
│    Wizard por pasos, chips de estado, techos por eje, revert por knob     │
│    ── ÚNICO sitio donde se escribe OC de GPU (NVAPI/ADLX) ──              │
├──────────────────────────────────────────────────────────────────────────┤
│  CA-O.Core                                                               │
│    DOMINIO PURO — toda la política, una sola vez                          │
│    OverclockKnob, catálogo, motor de tuning, motor de estabilidad,        │
│    máquina de estados, planificador de checkpoints, puerta anti-cheat     │
│    → 100 % testeable sin hardware                                        │
├──────────────────────────────────────────────────────────────────────────┤
│  CA-O.Infrastructure                                                     │
│    BACKENDS (delgados)                                                    │
│      NvidiaNvapiBackend   (Fase 1)   — DllImport nvapi64.dll             │
│      IntelPolicyExtension (Fase 2)   — powercfg, no OC real              │
│      XmpExpoReporter      (Fase 3)   — SMBIOS, solo lectura               │
│      AmdAdlxBackend       (Fase 6)   — adlx.dll, no amdocl64.dll          │
│      MsrSignedBackend     (futuro)   — requiere driver firmado            │
│    Sensors                                                                │
│      GpuTelemetryProvider (NVAPI/ADLX)                                   │
│      WheaEventProbe, BugcheckProbe, EffectiveClockProbe, ThermalProbe     │
├──────────────────────────────────────────────────────────────────────────┤
│  CAO.Privileged  (servicio Windows, sesión 0)                             │
│    Operaciones que exigen SYSTEM: policy-cfg de CPU, journal, anti-cheat  │
│    Policy SIEMPRE en Core, nunca aquí                                     │
└──────────────────────────────────────────────────────────────────────────┘
```

**Regla de dependencia, verificada por tests:**

- `CA-O.UI` no referencia `CAO.Privileged`, no usa `System.Management`, no lanza procesos — `ArchitectureDependencyTests.UiNeverSpawnsProcessesOrHostsThePipe`.
- `CA-O.Core` no referencia `CAO.UI` ni `CAO.Infrastructure` — `CoreNeverReferencesUiOrInfrastructure` (**L52**, corregido respecto al borrador).
- `CA-O.Infrastructure` y `CA-O.Privileged` no referencian `CAO.UI`.
- El servicio llama a Core, nunca al revés.

**Precedente que se respeta:** toda la política —incluidas las fases de transacción del OC— vive en `CA-O.Core`, igual que `OperationResultCodes`, `SafeErrorMessages` y `TimeoutProfile`.

## 6.3 Modelo de dominio (`CA-O.Core/Overclock/`)

### Enums — **todos preexistentes salvo dos**

```csharp
// REUTILIZADOS (no se duplican)
using CAO.Shared;   // EvidenceLevel, RiskLevel, PerformanceImpact, ...

// EvidenceLevel { Official, Vendor, Benchmark, Empirical, Heuristic, Unknown }   // Enums.cs:33
// RiskLevel     { Safe, Low, Moderate, High, Critical }                          // Enums.cs:46

// NUEVOS — solo los que no existen
public enum OverclockComponent { Cpu, Gpu, Memory }
public enum KnobUnit   { Hertz, Millivolt, Milliwatt, Percent, Bits, Ratio, Enum, Celsius }
public enum KnobKind   { Scalar, Enum, Curve }
public enum DependencyKind { PairsWith, Requires, IncompatibleWith }
public enum KnobScope  { Session, Persistent }   // GPU = Session por diseño (§5.3)
```

### El knob — el catálogo como dato

```csharp
public sealed record OverclockKnob(
    KnobId                  Id,              // "nvidia.gpu.powerLimit"
    OverclockComponent      Component,
    KnobKind                Kind,
    KnobRange               Range,           // Min, Max, valores discretos si Kind == Enum
    double                  Step,
    KnobUnit                Unit,
    RiskLevel               Risk,            // ← reutilizado
    EvidenceLevel           Evidence,        // ← reutilizado; NUNCA Unknown en el perfil por defecto
    string                  LabelKey,        // clave de Localizer, nunca literal
    string                  DescriptionKey,
    bool                    RequiresReboot,
    KnobScope               Scope,
    string                  BackendId,       // qué backend lo sirve
    IReadOnlyList<KnobPreset>   Presets,     // Conservative / Balanced / Performance / Custom
    IReadOnlyList<KnobDependency> DependsOn);
```

`KnobDependency` es lo que **resuelve la objeción principal al Enfoque 2**:

```csharp
public sealed record KnobDependency(KnobId Other, DependencyKind Kind);
// PairsWith        → "gpu.clockOffset" PairsWith "gpu.voltageOffset": subir uno sin el otro
//                     no da rendimiento y sube el riesgo. El motor lo valida.
// Requires         → "memory.report" Requires "smbios.available"
// IncompatibleWith → "gpu.aggressiveFanCurve" IncompatibleWith "gpu.silentProfile"
```

El motor valida dependencias **antes** de aplicar, y detecta **ciclos** (test `Catalog_Validate_RejectsCircularDependencies`). Si se viola una dependencia, el cambio se rechaza con motivo legible y se journaliza como `OverclockStepRejected`.

### Capacidades — el resultado del sondeo

```csharp
public enum CpuBackendKind    { None, XtuGuided, PolicyOnly, MsrSigned }
public enum GpuBackendKind    { None, NvidiaNvapi, AmdAdlx }
public enum MemoryBackendKind { None, SmbiosReportOnly }   // ← corregido: no existe XmpExpo

public sealed record BackendAvailability(
    string      BackendId,
    bool        Available,
    string      UnavailableReasonKey,   // clave de Localizer
    string?     RemedyHintKey,
    bool        RequiresRestart,
    string?     DriverVersion,          // ← NUEVO: crítico para NVAPI (§3.1)
    EvidenceLevel LastProbeEvidence);

public sealed record OverclockCapabilities(
    CpuBackendKind    Cpu,
    GpuBackendKind    Gpu,
    MemoryBackendKind Memory,
    IReadOnlyList<BackendAvailability> Details,
    IReadOnlyList<OverclockKnob>      Knobs);
```

> **Punto clave.** `Probe()` devuelve el estado del hardware **real de esta máquina con esta versión de driver**. Una RTX 4070 Ti y una RTX 4090 exponen listas distintas sin que el motor lo sepa. Si XTU no está, `Cpu = PolicyOnly` con su razón — **la sección sigue funcionando** con el tuning por política. Si el driver bloqueó los setters de OC (como 616.56 en RTX 50), `Gpu = None` con la razón real y la versión de driver: **la sección sigue funcionando en modo lectura**.

## 6.4 Contratos de backend

```csharp
public interface IOverclockBackend
{
    string Id { get; }
    OverclockCapabilities Probe(CancellationToken ct);
    ValueTask<KnobRead>        ReadAsync(KnobId id, CancellationToken ct);
    ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue v, CancellationToken ct);
    ValueTask<UnitSnapshot>    SnapshotAsync(CancellationToken ct);
    ValueTask<RestoreOutcome>  RestoreAsync(UnitSnapshot s, CancellationToken ct);
}

public interface IOverclockExtension
{
    string Id { get; }
    bool Handles(OverclockOperation op);
    ValueTask<ExtensionResult> ExecuteAsync(OverclockOperation op, CancellationToken ct);
}

public interface IOverclockBackendRegistry
{
    OverclockCapabilities ProbeAll(CancellationToken ct);
}
```

### Implementaciones previstas (corregidas)

| Backend | Fase | Acceso real | Cubre |
|---|---|---|---|
| `NvidiaNvapiBackend` | **1** | `nvapi64.dll` P/Invoke (usuario, elevado) | Lectura (docs): p-states, relojes, temperatura. Escritura (**privada**): offsets de reloj, power limit, límite térmico, ventilador, reset |
| `IntelPolicyExtension` | **2** | `powercfg` vía el servicio (ya existe el camino) | `PERFBOOSTMODE`, tope de frecuencia, plan, core parking. **No es OC de reloj** |
| `XtuGuidedFlow` | **2** | Detección de XTU/IPF + guía al usuario | Turbo ratio y voltajes **los aplica el usuario en XTU**; CA-O mide y verifica |
| `XmpExpoReporter` | **3** | `Win32_PhysicalMemory` (SMBIOS Type 17) | Velocidad nominal vs configurada. **Solo lectura. No hay `XmpExpoExtension` que active nada** |
| `AmdAdlxBackend` | **6** | **`adlx.dll`** (no `amdocl64.dll`, no ADL2) | Power limit, reloj máximo, ventilador, VRAM — gated por ASIC |
| `MsrSignedBackend` | Futuro | Driver firmado propio | MSR crudos. Requiere EV + HLK + verificación anti-cheat (§6.14) |

## 6.5 Motor de estabilidad y de tuning (`CA-O.Core/Overclock/Tuning/`)

### Sondas de estabilidad — con los proveedores corregidos

```csharp
public enum ProbeMetric { WheaEvents, KernelBugcheck, EffectiveClockStall, GpuThermalLimit, Composite }

public sealed record ProbeRequest(ProbeMetric Metric, TimeSpan Window);
public sealed record ProbeSample(bool Healthy, double? ObservedValue, IReadOnlyList<ProbeEvidence> Signals);

public interface IStabilityProbe
{
    ValueTask<ProbeSample> SampleAsync(ProbeRequest req, CancellationToken ct);
}
```

| Sonda | Implementación real | Corrección aplicada |
|---|---|---|
| **WHEA** | `EventLogWatcher` sobre **`Microsoft-Windows-WHEA-Logger`** (registro System), IDs **17/18/19/47**; canal `Microsoft-Windows-Kernel-WHEA/Errors` | ❌ `Win32_WHEAHardwareError` **no existe** |
| **Bugcheck** | Evento **1001** con proveedor **`WER-SystemErrorReporting`** / `Microsoft-Windows-WER-SystemErrorReporting`; apoyo en `Kernel-Power` **41** | ❌ el origen no es `BugCheck` (nombre legado Win7) |
| **Estancamiento de reloj efectivo** | `\Processor Information(_Total)\% Processor Performance` × `Win32_Processor.MaxClockSpeed`; para GPU, `GetAllClockFrequencies` / ADLX | ❌ `ClockDrift` con QPC **no detecta OC** (TSC invariante) |
| **Límite térmico** | **No se estima en software.** Para GPU se **fija el límite en el firmware** (`ClientThermalPoliciesSetLimit` / ADLX) y se **lee** `GetThermalSettings`. Para CPU se declara `NoThermalSensor` y el guardarraíl es de política | ❌ no hay temperatura de CPU en modo usuario (§3.6) |
| **WHEA «Composite»** | Agregado de las anteriores | — |

> En la suite de tests, `ScriptedProbe` devuelve una secuencia programada. **Todos los tests de estabilidad y tuning son deterministas y corren sin GPU, sin XTU, sin sensores.**

### Veredicto y puntos de estabilidad

```csharp
public enum StabilityVerdict { Pending, Passed, FailedUnstable, Aborted }   // nombres sin colisión

public sealed record StabilityPoint(
    KnobId            Knob,
    KnobValue         Value,
    TimeSpan          Duration,
    StabilityVerdict  Verdict,
    IReadOnlyList<ProbeEvidence> Evidence);
```

> **Colisión de nombres evitada (nueva):** el borrador usaba `StabilityVerdict.Failed` y `StabilityVerdict.Unstable` junto a `TransactionPhase.Failed`. Se renombran a `FailedUnstable` y `Aborted` para que `grep Failed` no confunda dos máquinas de estados distintas.

`ProbeEvidence` es un tagged union (`Whea(id, count)`, `Bugcheck(code)`, `ClockStall(deltaPercent)`, `ThermalLimitReached(sensor, observedC, limitC)`). Se muestra al usuario. **Nunca se colapsa a un score.**

### Algoritmo de tuning

Bisección sobre el eje del knob, con predicado de estabilidad:

```
run = TuningRun(profile)                        // punto de partida: Conservative
checkpoint = TakeCheckpoint()                   // ← persiste ANTES de tocar nada
while (cursor < candidates.Count) and (not budgetExceeded):
    candidate = NextCandidate(cursor)           // el motor elige, no el usuario
    if violatesDependencies(candidate):         // §6.3 KnobDependency
        journal(OverclockStepRejected); continue
    Apply(candidate)                            // journal: OverclockStepApplied
    verdict = Probe(stabilityWindow)
    match verdict:
        Passed        -> checkpoint = Advance(candidate); journal(OverclockTestPassed)
        FailedUnstable-> RestoreToCheckpoint()      // journal: OverclockTestFailed + revert
                         MarkCeiling(candidate.Value)
        Aborted       -> journal(OverclockTestFailed)
                         // NO se cierra aquí: se marca Interrupted y se ofrece reanudar
journal(Commit)   // ← TERMINAL, siempre el último (regla CAO-BUG-2026-10-06)
```

**Decisiones tomadas (revisadas):**

1. **El motor elige los puntos, el usuario aprueba el perfil.** El usuario nunca teclea un valor de reloj. Puede abortar y fijar un techo manual.
2. **El techado térmico se delega al firmware.** Para GPU: se fija el límite térmico del hardware y se lee la temperatura como evidencia. Se **anuncia antes de empezar**: «tu GPU tiene un límite de 83 °C configurado en el driver; no lo voy a subir». Para CPU: `NoThermalSensor` se declara y el guardarraíl es de política. **Corrige la decisión 2 del borrador, que era inimplementable.**
3. **`FailedUnstable` revierte y continúa; `Aborted` interrumpe y ofrece reanudar.** Un bugcheck deja el run en `Interrupted`, no lo cierra: cerrarlo a ciegas es lo que hacía el bug de 2.2.3.
4. **Presupuesto de tiempo.** El tuning respeta `DispatchHeavy` (20 min) y `ClientResponseHeavy` (21 min). El id del tuning se añade **al `HashSet` de `TimeoutProfile.cs`**, nunca copiando la lista (regla dura, §2.10).
5. **(NUEVA) Guardia de versión de driver.** El run registra la versión del driver de GPU en el checkpoint. Si cambia entre el apply y el revert, el revert se intenta igualmente pero el resultado se marca como no verificado.

## 6.6 Checkpoints y recuperación — reconciliado con 2.2.3

> **Resuelve B7 y B10. DECIDIDO (D4).** El invariante del borrador («la máquina arranca estable sin intervención humana») **contradecía una decisión explícita de 2.2.3**. Aquí se reconcilia en lugar de sobrescribirla.

### La decisión que hay que respetar

CHANGELOG `[2.2.3] - 2026-10-07`:

> *«El servicio revisa al arrancar si hay recuperaciones pendientes y lo deja en su log (`RecoveryStartupReporter`), **sin revertir: revertir es mutar el sistema y debe ser decisión del usuario, no un efecto secundario de un reinicio**.»*

Y la recuperación ya existe como operación IPC `RecoverTransaction` con botones **Revertir** / **Descartar** en Restaurar, con `RecoveryStartupReporter`.

### Por qué el OC merece una excepción, y de qué tamaño

El razonamiento de 2.2.3 es correcto para una optimización de registro: el usuario puede abrir la app y decidir. **No lo es para un OC de GPU que impide el arranque**: si la máquina no arranca, el usuario **no puede** pulsar «Revertir». Ese es el único caso que justifica automatismo, y hay que acotarlo.

### Regla propuesta (decisión D4)

```
al arrancar, para cada checkpoint de OC con Phase ∉ terminal:

  sucio = (el arranque anterior NO fue limpio)

  si NOT sucio:
      → el proceso terminó limpiamente: Commit o Revert según el journal.
        Sin acción automática.

  si sucio Y es el PRIMER arranque sucio de este run:
      → NO revertir automáticamente.
      → Marcar el run como Interrupted y persistir el contador de arranques sucios.
      → Avisar en la UI: "el tuning se interrumpió; la máquina arrancó con el
        último ajuste conocido. ¿Reanudar, revertir o descartar?"
        (tres botones, mismo patrón que RecoverTransaction)

  si sucio Y es el SEGUNDO arranque sucio consecutivo del mismo run:
      → REVERTIR al LastKnownGood. La máquina arranca estable.
      → Marcar el run como Interrupted + AutoReverted.
      → Emitir evento; el usuario ve el aviso al abrir la app y puede
        reanudar o descartar.
```

**Invariante resultante:** *un OC que impide arrancar se auto-revierte en el segundo intento fallido; un OC que solo se interrumpe pide decisión al usuario.* Respeta 2.2.3 y resuelve el caso que 2.2.3 no podía resolver.

**Test obligatorio:** `Recovery_DoesNotAutoRevert_OnFirstUncleanBoot` y `Recovery_AutoReverts_OnSecondConsecutiveUncleanBoot`.

### Qué se persiste

`CaOPaths.OverclockRunsDirectory` → `%ProgramData%\CA-O\overclock\runs\<runId>.json`

```csharp
public sealed record TuningCheckpoint(
    Guid                RunId,
    TransactionPhase    Phase,          // ← reutilizado, NO un enum nuevo
    OverclockProfile    Profile,
    KnobId              CurrentKnob,
    KnobValue           LastKnownGood,  // el valor estable confirmado
    UnitSnapshot        Snapshot,       // estado previo completo, para el revert
    int                 UncleanBootCount,   // ← NUEVO: sustenta la regla de D4
    string              GpuDriverVersion,   // ← NUEVO: guardia de versión (§6.5.5)
    DateTimeOffset      UpdatedAt,
    bool                BootWasClean);
```

### Contrato de persistencia (corrige B10)

| Requisito | Cómo |
|---|---|
| Ruta | `CaOPaths.OverclockRunsDirectory`, nunca literal |
| Escritura atómica | `tmp → flush → Move(overwrite)`, con cuarentena `.corrupt-<timestamp>`, igual que `AnalysisStateStore` |
| Exclusión mutua | Mutex de un solo escritor, **comprobando el retorno de `WaitOne`** (2.2.3 arregló justo ese bug) |
| Cota de tamaño | `History` con **ventana deslizante** (p. ej. últimos 200 puntos) + resumen agregado. **Corrige el crecimiento ilimitado del journal que ya ocurrió** (88 de 98 entradas) |
| Retención | Poda de runs cerrados, igual que el journal de transacciones (`TransactionJournalRetentionTests` es el precedente) |
| ACL | Endurecidas por `scripts/harden-data-acls.ps1`, igual que el resto de `%ProgramData%\CA-O` |
| Datos personales | Nada de contenido de usuario; coherente con `PRIVACY.md` |

### Extensión del journal — **sin nuevas fases terminales**

```
(nuevas, todas NO terminales)
OverclockStarted
OverclockCheckpointTaken
OverclockStepApplied
OverclockStepRejected
OverclockTestPassed
OverclockTestFailed
OverclockInterrupted
(cierre, con los cinco terminales que YA existen)
Commit  |  RolledBack  |  Failed  |  CancelledBeforeApply  |  RecoveryCompleted
```

`Incomplete()` sigue siendo correcto sin cambios: **el último evento apilado debe ser terminal** (`ITransactionJournal.cs:31`). Y `Commit` se journaliza **al final**, después de cualquier fase de benchmark o de verificación, respetando el arreglo de CAO-BUG-2026-10-06.

## 6.7 Capa de memoria — honestidad con el alcance real

Hypertune vende «memory overclocking» y entrega un checkbox de XMP. Es su mayor sobreventa. **Y el borrador overclaimed en la dirección opuesta**: prometía *detectar* perfiles XMP/EXPO, y eso tampoco es posible (§3.5).

### Lo que SÍ es posible sin driver

| Acción | Mecanismo | Honestidad |
|---|---|---|
| Leer velocidad **nominal** del módulo | `Win32_PhysicalMemory.Speed` | Real, `Official` |
| Leer velocidad **configurada** | `Win32_PhysicalMemory.ConfiguredClockSpeed` | Real, `Official` |
| Comparar ambas → inferir si el perfil está activo | Aritmética sobre lo anterior | **Inferencia**, `Empirical`. La UI dice «parece activo», nunca «hemos leído tu perfil XMP» |
| Leer fabricante, part number, tipo SMBIOS, voltaje | mismas propiedades | Real, `Official` |
| Guiar a la BIOS para activar el perfil | Enlace al fabricante de la placa | Informativo, `Vendor` |

### Lo que NO es posible, y se dice explícitamente en la UI

> **«No podemos leer ni aplicar perfiles XMP/EXPO ni timings de memoria. El perfil vive en la EEPROM SPD del módulo, y Windows solo lo expone al firmware durante el arranque. No hay ninguna API de Windows que lo permita a una aplicación sin firmar un driver, y los timings los fija la UEFI. Lo que sí hacemos: comparar la velocidad que tu memoria anuncia con la que está usando ahora mismo — si no coinciden, tienes el perfil desactivado y te decimos dónde activarlo.»**

Cada afirmación lleva su `EvidenceLevel`. Si algo es `Heuristic`, se dice. **Si es `Unknown`, no aparece en el perfil por defecto** — la misma regla que ya aplica al catálogo de 93.

## 6.8 Deltas honestos — sin score opaco, reutilizando lo que ya existe

### El problema que Hypertune resuelve mal

Un score de escala desconocida no es explicable, no es comparable y no responde a *«¿qué gané y por qué?»*.

### Lo que CA-O mide y lo que NO atribuye

**Regla dura de honestidad:** el overclock **no es aditivo**. Subir reloj y power limit no suman. Por tanto **es técnicamente incorrecto** atribuir un delta individual a cada knob.

| Dato | Cómo se obtiene | Honestidad |
|---|---|---|
| **Delta total del perfil** | El motor de medición **existente** (`SystemBenchmarkRunner`: 3 trials, mediana, warmup) antes y después, misma carga, y `BenchmarkAnalyzer` con su suelo del 3 % | Real, reproducible, con veredicto `Improvement` / `NoMeasurableImprovement` / `Regression` |
| **Techo que limitó cada eje** | El motor sabe dónde dejó de poder subir | Real y muy informativo |
| **Límite térmico configurado** | Se lee del driver; no se estima | Real |
| **Estabilidad por punto** | `IStabilityProbe` | Real, con señales nombradas |
| **Reloj efectivo alcanzado** | `% Processor Performance` × MaxClockSpeed (CPU) / NVAPI-ADLX (GPU) | Real |

> **La novedad de esta revisión:** nada de esto se construye. El §6.16 del borrador lo borraba. Aquí se **conserva y se consume**.

**UI de resultado — la diferencia visible frente a Hypertune:**

```
RESULTADO

  Rendimiento        +7,4 %      vs. línea base medida en este equipo
                                  3 cargas, mediana, suelo de ruido 3 %
                                  Veredicto: Mejora medible

  Techos encontrados
    Reloj GPU        2.750 MHz   limitado por power limit (320 W al 100 %)
    Reloj memoria    +500 MHz    espacio disponible, no aplicado
    Turbo CPU        —           requiere Intel XTU; no disponible en este equipo
                                 CA-O sí puede: modo boost y tope de frecuencia

  Estabilidad        9 de 9 puntos pasaron
                     WHEA 0 · Bugcheck 0 · Límite térmico del driver: 83 °C (respetado)

  Sin datos           Temperatura de CPU: Windows no la expone sin un driver
                      de monitorización. No se estima. No se inventa.

  Revertir           [ Deshacer todos los cambios ]
```

Frente al *«Optimization score: 649»* de Hypertune, esto es auditable, explicable y accionable — y **la fila «Sin datos» es la que más credibilidad construye**.

## 6.9 Perfiles de juego

Decisión 2: **CPU + GPU + Memoria + perfiles de juego**. Network / Windows / Cleaning no se duplican.

| Necesidad | Componente existente |
|---|---|
| Detección de juegos instalados | `Gaming\GameDetectionProvider` |
| Anti-cheat | `Gaming\AntiCheatScanProvider` |

```csharp
public sealed record GameProfile(
    string    GameId,              // id estable de GameDetectionProvider
    string    DisplayName,
    OverclockProfile Overclock,    // referencia a un perfil YA validado
    bool      PreferHighPerformanceGpu,
    EvidenceLevel GpuPreferenceEvidence);
```

> **Nota de alcance (nueva):** el borrador incluía `LaunchOptions` con `EvidenceTier LaunchOptionsEvidence` y `ProcessPriority`. **Se retiran.** Inyectar argumentos de arranque y elevar la prioridad de un proceso de juego es exactamente la clase de manipulación que los anti-cheat vigilan, y no es overclock. `set-games-high-performance-gpu` ya existe en el catálogo de 93 como optimización transaccional. Un perfil de juego aquí **solo referencia un perfil de OC validado** — nada más.

**Nunca se crea un perfil de OC nuevo para un juego.** Un perfil de OC solo existe tras pasar el tuning completo con evidencia. Esto elimina por construcción la posibilidad de aplicar algo no validado a un título con anti-cheat.

### Anti-cheat: puerta obligatoria

Antes de aplicar cualquier cambio:

```
1. AntiCheatScanProvider detecta anti-cheat activo        → BLOQUEAR con el nombre
2. AntiCheatScanProvider detecta EAC/BattlEye/Vanguard    → BLOQUEAR con el nombre
3. Juego en ejecución sin anti-cheat                       → PERMITIR, registrar
4. Ningún cambio toca ficheros dentro del directorio del juego → por construcción
5. Ningún componente kernel, ningún overlay, ningún handle a proceso de juego
```

Se añade un test que lo verifica: **guard estático que falla la build si un backend de OC referencia una ruta bajo un directorio de juego, o si aparece cualquier P/Invoke a un driver kernel o cualquier `OpenProcess`**.

## 6.10 Modelo de la UI

**Decisión: se conserva el tema de CA-O. Se adopta la arquitectura de información de Hypertune.**

| Se adopta de Hypertune | Se rechaza de Hypertune |
|---|---|
| Secciones numeradas en micro-caps | El fondo `#0a0b0b` y el acento mint (choca con el tema de CA-O) |
| Chips de estado que dicen **qué se aplicó** | El score de escala desconocida |
| Tarjetas con texto izquierda / visual derecha | «Memory overclocking» que es un checkbox |
| Cards por juego con estado | «just restart and let it continue cooking» |
| Colores casi negros | Las afirmaciones sin evidencia |
| — | Drivers kernel persistentes que sobreviven a la desinstalación |

### Secciones

```
01  PROCESADOR       backend detectado (XTU/IPF guiado o solo política), knobs, presets
02  GRÁFICOS         knobs NVIDIA/AMD, power limit, límite térmico, curva de ventilador
03  MEMORIA          velocidad nominal vs configurada, con la explicación honesta de §6.7
04  PERFILES DE JUEGO  cards de GameDetectionProvider + perfil OC validado
05  ESTABILIDAD      puntos, señales WHEA/bugcheck/reloj, botón de reanudar
06  RESULTADO        deltas honestos y botón de revertir
```

### Estados obligatorios de cada sección

Cada sección renderiza **uno** de estos seis estados:

| Estado | Cuándo | Qué muestra |
|---|---|---|
| `Available` | Backend presente y knobs disponibles | Controles |
| `Unsupported` | Hardware no soportado por ningún backend | Motivo real + enlace |
| `DependencyMissing` | Falta XTU / IPF / Adrenalin / placa con OC | Motivo real + enlace al remedio |
| `NoPermission` | Falta elevación | Explicación de por qué se necesita |
| `Applying` | Operación en curso | Progreso indeterminado + límite térmico ya anunciado |
| **`DriverBlocked`** | **(NUEVO)** El driver instalado rechazó los setters (caso 616.56 en RTX 50) | Versión de driver detectada, motivo, y qué queda disponible (solo lectura) |

> **Regla del repo que se respeta:** un componente que no pudo inspeccionar **no puede mostrarse como «aplicado»**. Si `Probe()` falló, la sección cae a `DependencyMissing` con el motivo real.

### Guardas de la página

- `_busy` — guard de reentrada por página, como `LimpiezaPage` y `OptimizePage`.
- Un solo `ContentDialog` a la vez, vía `Helpers\UiDialogs.cs` (añadir uno concurrente reproduce CAO-BUG-2026-10-06, que **mataba la app**).
- Si un juego con anti-cheat está corriendo, el botón de aplicar se deshabilita **con el motivo**.
- La sección de Estabilidad aparece en rojo solo con evidencia real de fallo.
- Nunca `break` en el primer fallo de un lote de operaciones.
- Progreso indeterminado + techo anunciado («puede tardar hasta 20 minutos»).
- **Ningún `async void` sin `try/catch`** y siempre esperado por quien llama.
- **Ningún proceso lanzado ni stdout leído en el hilo de UI** (el CHANGELOG de 2.2.3 documenta una congelación de 13 s por `sc.exe` en el hilo de UI).

## 6.11 Flujo de datos de extremo a extremo (con el pipe)

```
Usuario pulsa "Iniciar tuning" en un preset
        │
        ▼
OverclockPage / OverclockViewModel        (UI, elevada, sesión interactiva)
        │  valida: ¿juego con anti-cheat? ¿elevación? ¿_busy?
        │
        ├─► IPC OverclockProbe ──────────► servicio SYSTEM ──► Core: ProbeAll()
        │                                       │                    │
        │                                       │      NVIDIA: DllImport nvapi64.dll
        │                                       │      (o directo desde la UI, ver §5.3)
        │                                       ▼
        │                              Capabilities + Knobs + DriverVersion
        │
        ├─► IPC OverclockPrepare ────────► Core: OverclockPolicy.Validate(profile, caps)
        │                                       → plan de pasos + dependencias resueltas
        │
        ▼
TuningEngine.RunAsync(plan, probe, checkpointStore, journal)      ← Core
        │
        ├─► checkpointStore.Save(Phase=Preparing)      ← %ProgramData%\CA-O\overclock\runs\
        │                                                 ESCRITO ANTES DE TOCAR NADA
        ├─► journal(OverclockStarted) + journal(OverclockCheckpointTaken)
        │
        ├─► por cada candidato:
        │     journal(OverclockStepApplied)
        │     backend.WriteAsync(candidate)            ← NVAPI/ADLX en la UI elevada
        │     probe.SampleAsync(window)                ← WHEA / bugcheck / reloj / térmico
        │     journal(OverclockTestPassed | OverclockTestFailed)
        │     si inestable → journal(OverclockStepRejected) + RestoreToCheckpoint
        │
        ├─► IPC OverclockFinalize ───────► journal(Commit | RolledBack | Failed)
        │                                  ← TERMINAL, SIEMPRE EL ÚLTIMO
        ▼
OverclockResult  ──►  UI muestra delta + techos + evidencia + "sin datos"
        │
        ▼
Reversible en cualquier momento vía el snapshot persistido
```

**Nota de tamaño:** la respuesta de `OverclockGetRunState` **no** devuelve el historial completo (límite de 256 KB). Devuelve el resumen y los últimos N puntos; el historial completo se lee del checkpoint persistido.

## 6.12 Manejo de errores (corregido)

| Situación | Comportamiento | Evidencia declarada |
|---|---|---|
| XTU/IPF ausente | `CpuBackendKind.PolicyOnly` + sección `DependencyMissing` con motivo y enlace | `Vendor` |
| GPU AMD en Fases 1-5 | `GpuBackendKind.None` + motivo «soporte AMD llega en la Fase 6» | `Vendor` |
| **Sin sensor térmico de CPU** | **Se declara `NoThermalSensor`.** El OC de CPU por reloj no se ofrece; el tuning por política sí. **No se estima ninguna temperatura** | `Official` |
| **Driver bloqueó los setters de NVAPI** (caso 616.56) | Estado **`DriverBlocked`**: se muestra la versión de driver y se degrada a solo lectura. **No se reintenta a ciegas** | `Vendor` |
| Power limit ya al 100 % | El motor marca ese eje como «limitado por power limit» en vez de fallar | `Benchmark` |
| WHEA detectado | `FailedUnstable` → revert + techo | `Empirical` |
| Bugcheck | `Aborted` → el run queda `Interrupted`; **no se cierra a ciegas**; se ofrece reanudar tras el reinicio (regla D4) | `Official` |
| Cuelgue a mitad del tuning | Checkpoint sobrevive; primer arranque sucio avisa, segundo revierte (regla D4) | `Empirical` |
| `nvapi64.dll` ausente | El backend se autoexcluye en `ProbeAll`, sin lanzar excepción | `Vendor` |
| `adlx.dll` ausente o Adrenalin no completo | Igual: autoexclusión silenciosa con motivo | `Vendor` |
| Permiso insuficiente | `NoPermission` con la razón concreta (`NVAPI_INVALID_USER_PRIVILEGE`) | `Official` |
| Anti-cheat activo | Bloqueo con el nombre del anti-cheat detectado | `Official` |
| Error de journal | El tuning **aborta y revierte**. No se continúa a ciegas | `Official` |
| Checkpoint corrupto | Cuarentena `.corrupt-<timestamp>`; el run se marca `Interrupted`; **nunca se borra evidencia** | `Official` |
| Snapshot de revert no disponible | El run se marca `Interrupted` y **no se cierra**: no se puede garantizar la reversión, así que no se declara cerrado | `Official` |

> **Regla del repo que se respeta:** un `Detect` que no pudo inspeccionar **no puede responder «applied»**.

## 6.13 Estrategia de fases (resecuenciada)

> **Corrección.** El borrador ponía en la Fase 1 *«`NvidiaNvapiBackend` + UI completa + migración de Benchmark»* a la vez. Eso junta la superficie externa más frágil (§3.1), toda la UI y una migración documental, en un solo incremento. Se resecuencia para que **el primer incremento entregue valor verificable con riesgo externo cero**.

| Fase | Contenido | Verificable con | Riesgo externo |
|---|---|---|---|
| **0** | Modelo de dominio, catálogo, motor de tuning, motor de estabilidad, checkpoints, **backend falso**, `ScriptedProbe`. Fases nuevas en `TransactionPhase` (no terminales). Contratos IPC. | `dotnet test CA-O.sln -c Release` verde, **sin hardware** | **Ninguno** |
| **1** | **Sección Overclock de solo lectura**: UI completa, `OverclockPage` + tag `overclock`, telemetría de GPU (NVAPI lectura), memoria honesta (SMBIOS), perfiles de juego **sin OC**, estados de sección, `Probe()` real | `dotnet test` verde + prueba manual: la sección muestra la verdad en cualquier máquina, **incluida una sin GPU NVIDIA** | Bajo (solo lectura) |
| **2** | **Tuning por política de CPU** (`powercfg`: boost mode, tope de frecuencia) + **flujo guiado XTU/IPF**. Reutiliza el catálogo transaccional que ya existe | `dotnet test` verde + prueba manual | Bajo (son mutaciones de sistema ya soportadas) |
| **3** | **Escritura de OC de GPU NVIDIA** (`NvAPI_GPU_SetPstates20`, power limit, límite térmico). Guardia de versión de driver. Estado `DriverBlocked`. Delta honesto sobre el motor de medición existente | `dotnet test` verde + **prueba manual en GPU NVIDIA, verificando el revert** | **Alto** — API privada; rotura de driver probada |
| **4** | **Reanudación tras cuelgue** (regla D4) + techos por eje + deltas en la UI | Test de arranque sucio simulado (primero y segundo) | Ninguno |
| **5** | **Perfiles de juego** + puerta anti-cheat + guard estático de rutas | `dotnet test` verde | Bajo |
| **6** | **`AmdAdlxBackend`** con ADLX | `dotnet test` verde + prueba en GPU AMD con Adrenalin completo | Medio |
| **Futuro** | `MsrSignedBackend` | Requiere driver firmado — **sin refactor** (§6.14) | **Muy alto** |

**Criterio de aceptación de cada fase:** `dotnet build CA-O.sln -c Release` con **0 warnings** (son errores), y `dotnet test CA-O.sln -c Release` verde, **incluidos los 1278 existentes**.

## 6.14 Cómo se activa el driver firmado (y por qué probablemente no)

Esta es la razón de fondo del Enfoque 2. Cuando —y **solo si**— llegue el momento:

1. Escribir `MsrSignedBackend : IOverclockBackend` con `Id = "msr-signed"`.
2. Registrarlo en el `IOverclockBackendRegistry`.
3. Añadir las filas de catálogo de MSR a `OverclockCatalog`.
4. **Nada más cambia.**

El motor de tuning, el de estabilidad, los checkpoints, la recuperación, el journal, los deltas, la UI y los perfiles de juego son los mismos. Esa es la prueba de que la arquitectura es la correcta.

**Pero el criterio de decisión debe estar escrito antes de empezar** (decisión D7), porque la evidencia dice que probablemente no compense:

| Puerta | Criterio de paso |
|---|---|
| Valor | Sin driver no se puede hacer OC real de CPU. ¿Es eso un objetivo del producto o un extra? |
| Coste | Certificado **EV** + **HLK** + cadena de suministro documentada |
| **Blocklist** | La blocklist de drivers vulnerables está **activa por defecto desde Win11 2022** y se actualiza trimestralmente vía Windows Update. Un driver que exponga MSR es candidato natural a entrar |
| **Anti-cheat** | FACEIT bloquea **firmantes enteros**. BattlEye bloquea drivers con IOCTLs peligrosos. Un driver propio puede ser bloqueado por firma |
| Residuo | Un driver `AUTOSTART` que sobrevive a la desinstalación es exactamente el pecado que le reprochamos a Hypertune (§1.4) |
| Alternativa | El tuning por política cubre la parte honesta sin ningún riesgo |

**Conclusión provisional: no. El techo técnico del OC de CPU en Windows no es un problema de ingeniería de CA-O, es una decisión de Microsoft y de los anti-cheat.** Decirlo así es más honesto y más defendible que prometer un driver.

## 6.15 Estrategia de pruebas

### El principio

> **Ninguna prueba del motor requiere hardware.** El motor se testea contra backends falsos y sondas guionizadas. Eso es lo que permite que la suite siga verde en cualquier máquina de CI.

### Pirámide (revisada)

| Capa | Qué | Mínimo | Hardware |
|---|---|---|---|
| **Dominio** | Catálogo válido, dependencias (incluidos ciclos), rangos, presets, `EvidenceLevel` nunca `Unknown` en el perfil por defecto | ~60 | No |
| **Motor de tuning** | Bisección, dependencias, presupuesto de tiempo, techo por eje | ~40 | No |
| **Estabilidad** | Veredictos, transiciones, techo marcado, `DriverBlocked` | ~30 | No |
| **Checkpoints** | Guardar/cargar, corrupción + cuarentena, cota de ventana, run interrumpido | ~30 | No |
| **Recuperación** | Regla D4 completa: primer arranque sucio **no** revierte; segundo **sí** | ~20 | No |
| **Transacciones** | Orden de fases, **`IsTerminal` sin cambios**, `Incomplete()` correcto con las fases nuevas | ~20 | No |
| **Backends falsos** | `FakeOverclockBackend`, `ScriptedProbe`, `FlakyBackend` | ~25 | No |
| **Contratos** | Estabilidad de `.Probe()`, idempotencia de `.RestoreAsync()`, `DriverBlocked` | ~15 | No |
| **IPC** | Nuevas operaciones: payload correcto, rechazo de cruces, límites de 64/256 KB, allowlist | ~15 | No |
| **Integración** | Pipe completo con backend falso | ~15 | No |
| **Seguridad** | Anti-cheat, guard de rutas de juego, guard de «cero kernel», frontera SYSTEM | ~20 | No |
| **UI** | ViewModel de Overclock (el code-behind no tiene seam, igual que el resto del repo) | ~30 | No |
| **Humo en hardware real** | Detección de GPU NVIDIA, lectura de knobs, **y revert verificado** | ~5 | **Sí** |

**Presupuesto:** **~325 tests nuevos**, de los cuales **~320 sin hardware**. Denominación explícita (el borrador usaba «NVD (necesario)», que no significa nada).

**Restricciones sobre los conteos existentes:** los 1278 de referencia deben seguir verdes. Los 17 de `Benchmark.Tests` **no se retiran** (decisión D6): prueban el motor de medición, que se conserva.

### Fixtures

```csharp
public sealed class FakeOverclockBackend : IOverclockBackend { /* knobs en memoria,
    fallos inyectables por knob, registro de escrituras para aserciones de orden */ }

public sealed class ScriptedProbe : IStabilityProbe { /* secuencia programada:
    "estable hasta 2700 MHz, inestable a partir de 2750" — sin GPU */ }

public sealed class FlakyBackend : IOverclockBackend { /* falla la escritura N veces,
    para probar reintentos y journal */ }

public sealed class DriverBlockedBackend : IOverclockBackend { /* simula 616.56:
    Probe() devuelve Gpu=None con motivo y versión de driver */ }
```

### Tests que cubren específicamente las debilidades de Hypertune

| Test | Qué garantiza |
|---|---|
| `Tuning_RestoresToLastKnownGood_WhenProbeReportsUnstable` | Nunca deja la máquina inestable |
| `Recovery_DoesNotAutoRevert_OnFirstUncleanBoot` | Respeta la decisión de 2.2.3 |
| `Recovery_AutoReverts_OnSecondConsecutiveUncleanBoot` | Y resuelve el caso en que el usuario no puede decidir |
| `Recovery_DoesNotRun_WhenLastBootWasClean` | No hace revert innecesario |
| `Knobs_NeverExposeUnknownEvidence_InDefaultProfile` | Coherencia con `README.md:452` |
| `Overclock_Backends_NeverReferenceGameDirectories` | Guard estático anti-cheat |
| `Overclock_IntroducesNoKernelComponents` | **Guard estático nuevo**: sin servicios, drivers, tareas programadas ni P/Invoke a drivers |
| `Overclock_IntroducesNoOverlaysOrProcessInjection` | **Guard estático nuevo**: sin OSD, sin `OpenProcess` sobre juegos |
| `Catalog_Validate_RejectsCircularDependencies` | El catálogo no se auto-bloquea |
| `OverclockKnobRequiresReboot_IsSurfaced` | El reinicio se avisa, no se sufre |
| `Tuning_ResultFitsIpcResponseLimit` | El resultado **no** supera los 256 KB |
| `Transaction_OverclockPhases_DoNotChangeIsTerminal` | `IsTerminal` sigue con sus cinco valores |
| `Transaction_OverclockRunEndsInTerminalEvent` | Todo run cierra en `Commit`/`RolledBack`/`Failed`/`CancelledBeforeApply` |
| `Timeout_HeavyId_IsRegisteredNotCopied` | Regresión del bug de `HeavyOptimizationIds` |
| `Telemetry_NeverReportsAnUnreadSensor` | Ningún dato no leído se presenta como leído (G10) |

## 6.16 Migración desde la sección Benchmark — **reescrita (corrige B1)**

> **ESTADO: DECIDIDO Y CERRADO el 2026-10-10.** El usuario eligió explícitamente **«añadir una nueva pestaña»**: Overclock es una pestaña nueva y `BenchmarkPage` se conserva íntegra. Se adopta la «opción (b)» del Paso 2 y queda **descartada** la fusión de `Benchmark` dentro de `Overclock`. Esto coincide con el criterio de muerte (4) del §6.20.

> **El §6.16 del borrador era el error más caro del documento.** Proponía borrar `BenchmarkPolicy`, `BenchmarkAnalyzer` y `BenchmarkModels` por ser «el score heurístico». **No son un score**: son el suelo de ruido del 3 %, la mediana de trials y los percentiles de frame-time con veredicto A/B. En CA-O **no existe ningún score 0–1000**.

### Qué NO se toca, y por qué

| Ruta | Por qué se conserva |
|---|---|
| `src\CA-O.Core\Benchmark\BenchmarkPolicy.cs` | Es la **fuente única de verdad metodológica** (suelo 3 %, 3 trials, 1 warmup). Borrarla reintroduce la contradicción «docs 3 % vs analyzer 1 %» que su propio comentario dice haber eliminado |
| `src\CA-O.Core\Benchmark\BenchmarkAnalyzer.cs` | Percentiles de frame-time y veredicto A/B. **Es el §6.8 hecho código** |
| `src\CA-O.Shared\DTO\BenchmarkModels.cs` | `BenchmarkVerdict { InsufficientData, NoMeasurableImprovement, Improvement, Regression }` — el sistema de veredicto honesto que el propio documento alaba |
| `src\CA-O.Infrastructure\Benchmarking\SystemBenchmarkRunner.cs` | El motor de medición. El §6.8 lo necesita literalmente |
| `src\CA-O.Infrastructure\Benchmarking\{BenchmarkStore,BootInfoProvider,IFrameCapture,DxgiFrameCapture}.cs` | Soporte del anterior |
| `tests\CA-O.Benchmark.Tests\` (5 ficheros, 17 tests) | Prueban lo anterior. **No se retiran** |
| `tests\CA-O.Infrastructure.Tests\DnsBenchmarkProviderTests.cs` | DNS, ortogonal |
| `src\CA-O.Infrastructure\Networking\DnsBenchmarkProvider.cs` | Se queda donde está |
| `docs\OPTIMIZATION-CATALOG.md` L17, L218, L259 | L17 es la leyenda de `EvidenceLevel`; L218/L259 son campos del catálogo. **`Benchmark` como nivel de evidencia no desaparece** |

### Qué cambia realmente (decisión D5)

**Recomendación: opción (b) + (c).**

**Paso 1 — añadir, no sustituir.**

| Fichero | Antes | Después |
|---|---|---|
| `src\CA-O.UI\CA-O.UI.csproj:38` | `<Page Include="BenchmarkPage.xaml" />` | **sin cambio** |
| `src\CA-O.UI\CA-O.UI.csproj` | — | `+ <Page Include="OverclockPage.xaml" />` |
| `RouteTable.cs:17` | `["benchmark"] = typeof(Pages.BenchmarkPage)` | **sin cambio** |
| `RouteTable.cs` | — | `+ ["overclock"] = typeof(Pages.OverclockPage)` |
| `RouteTable.cs:30` | `Order` con `"benchmark"` | `+ "overclock"` en la posición que decida el producto |
| `TimeoutProfile.cs` | 15 ids | `+ "overclock-tune"` **añadido al `HashSet` del fichero** |

**Paso 2 — decidir el nombre a largo plazo.**

| Opción | Coste | Riesgo |
|---|---|---|
| **Conservar `Benchmark` como página y pilar, añadir `overclock`** | Cero | Bajo. Coexisten: Benchmark mide, Overclock ajusta. Es coherente con «no duplicar secciones»: no se duplica nada, se **añade** la que falta |
| Renombrar el pilar a `MEASURE` y fusionar Benchmark dentro de Overclock | 8+ referencias en README/ARCHITECTURE + capturas | Medio. Toca el pilar fundacional del producto |

**Recomendación: empezar por la primera y evaluar la segunda cuando la sección Overclock tenga uso real.** Si se hace la segunda, el trabajo documental es el inventario de §2.16, y **el CHANGELOG no se reescribe**.

**Corrección conceptual que debe quedar escrita:** el delta honesto **ya existe**. El trabajo de Overclock no es construirlo, es **consumirlo**. Eso reduce el alcance real de la Fase 0-1 de forma significativa.

## 6.17 Seguridad y anti-cheat

| Garantía | Cómo |
|---|---|
| Nunca se tocan ficheros de juego | Ningún backend de OC acepta una ruta de juego. Guard estático en tests |
| Solo APIs de fabricante en modo usuario | NVAPI, ADLX, `powercfg`. **Cero componentes kernel** |
| Sin driver en las Fases 0-6 | Todo el acceso es a DLLs de usuario ya instaladas por el driver/Adrenalin |
| Sin overlays, sin inyección, sin handles a juegos | Guard estático nuevo |
| SYSTEM solo cuando es imprescindible | Canal privilegiado v2 existente, con token y SessionId |
| La política vive en Core | `CoreNeverReferencesUiOrInfrastructure` (**L52**) lo garantiza estructuralmente |
| Cero componentes residentes | Sin servicios propios, sin tareas programadas, sin `AUTOSTART`. **Es la diferencia con Hypertune (§1.4)** |
| No se toca nada ajeno al OC | Ni Windows Update ni políticas del sistema |

### El driver firmado, si alguna vez llega

Firmado con EV, cadena de suministro documentada en `THIRD-PARTY-NOTICES.md`, threat model actualizado en `docs\THREAT-MODEL.md` (STRIDE), **verificación explícita contra la lista de drivers bloqueados por EAC y BattlEye documentada como evidencia del propio módulo**, y las puertas de D7/§6.14 superadas y escritas.

## 6.18 Licencias y cadena de suministro — **sección nueva**

> El borrador solo mencionaba `THIRD-PARTY-NOTICES.md` para el futuro driver. Pero el diseño P/Invokea DLLs de terceros desde la Fase 1. **Esto hay que resolverlo antes de escribir código.**

| Componente | Régimen | Qué implica para CA-O |
|---|---|---|
| **Headers/libs del NVAPI SDK** ([NVIDIA/nvapi](https://github.com/NVIDIA/nvapi)) | **MIT** | Se pueden incluir los headers. La implementación la aporta el driver instalado |
| **`nvapi64.dll`** (runtime) | Propiedad de NVIDIA, distribuida con el driver | **No se redistribuye.** CA-O solo la invoca si ya está. Nunca se empaqueta |
| **ADLX** ([repo](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX)) | Licencia propia de AMD (GPUOpen) | **Leer la licencia antes de la Fase 6** y confirmar si `adlx.dll` se puede invocar/depurar. **No se redistribuye `adlx.dll`** |
| **ADL** (`atiadlxx.dll`) | Propiedad de AMD | **Descartada del diseño** (no expone overdrive) |
| **Intel XTU / `IntelOverclockingSDK.dll`** | EULA de XTU, canal OEM/partner | **No se integra ni se redistribuye.** Solo se **detecta** su presencia y se guía al usuario |
| **Intel IPF** | Distribución de Intel | Solo detección |
| **`powercfg`** | Herramienta del SO | Ya está en `ElevatedCommandCatalog` con contrato probado (`ElevatedCommandCatalogTests`) |

**Acciones obligatorias:**

1. `THIRD-PARTY-NOTICES.md` debe distinguir, para cada uno: **distribuido en runtime** vs. **solo compilación** vs. **nunca distribuido, solo invocado si existe**.
2. Añadir un test o una revisión de contrato que falle si un instalador empaqueta una DLL de fabricante.
3. Documentar en `PRIVACY.md` que invocar una DLL local del driver **no es telemetría** ni conexión de red — sigue cumpliendo el claim de «únicamente conexiones locales declaradas».
4. **No añadir ninguna dependencia NuGet nueva** si se puede evitar: todo el diseño se apoya en `DllImport` y `System.Diagnostics.EventLog` (`Microsoft.Extensions.Logging`/`EventLog` ya está disponible vía `Microsoft.Extensions.Hosting`). Un paquete NuGet nuevo es una decisión de cadena de suministro, no un detalle.

## 6.19 Registro de riesgos — **sección nueva**

| # | Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|---|
| R1 | **NVIDIA retira o rompe los setters de NVAPI OC en un driver futuro** (ya pasó con 595.71 y 615.56/616.56) | **Alta** | Alto | Estado `DriverBlocked`; guardia de versión de driver en el checkpoint; la sección degrada a solo lectura sin romperse; el valor de la sección no depende del OC |
| R2 | El tuning por política no da ganancia medible en muchos equipos | Media | Medio | Se dice con el veredicto A/B: `NoMeasurableImprovement` es un resultado legítimo y honesto |
| R3 | Coste real de la UI y del motor mayor de lo estimado | Media | Medio | Fase 1 sin escritura; fases con valor independiente |
| R4 | La regla D4 (auto-revert al segundo arranque sucio) resulta agresiva en la práctica | Media | Medio | Los tests la cubren; el contador está en el checkpoint y es auditable; se puede subir el umbral |
| R5 | Confusión de nombres entre `EvidenceLevel.Benchmark` y la página Benchmark | Alta | Bajo | Se documenta explícitamente (§2.13) y se mantiene el nivel de evidencia |
| R6 | Deriva documental: 8+ referencias al pilar BENCHMARK | Alta | Bajo | Inventario de §2.16 + `DocumentationCodeConsistencyTests` |
| R7 | Se introduce sin querer un componente kernel o un overlay | Baja | **Muy alto** | Guards estáticos nuevos + revisión de contrato |
| R8 | El checkpoint crece sin límite y ralentiza el arranque | Media | Medio | Ventana deslizante + poda + el precedente del bug del journal (88 de 98 entradas) |
| R9 | Trabajo previo sin commitear (tutorial) interfiere con la verificación de la suite | **Alta (ya ocurre)** | Bajo | §2.17: commitear o aislar antes de empezar |
| R10 | Se promete más de lo que el hardware permite y se repite el pecado de Hypertune | Baja | **Muy alto** | G10 y el test `Telemetry_NeverReportsAnUnreadSensor` |

## 6.20 Criterios de muerte del plan — **sección nueva**

Condiciones bajo las cuales el diseño debe **pararse y replantearse**, no seguir:

1. **Si la Fase 3 no logra aplicar un offset de reloj de GPU y revertirlo de forma verificable en al menos una máquina NVIDIA de prueba** → el plan pierde su objetivo G1 y hay que replantear el alcance de la sección (medición + política + memoria, sin OC de GPU).
2. **Si el guard estático de «cero componentes kernel» no se puede escribir de forma que falle de verdad** → el invariante no es verificable y hay que declararlo como aspiración en lugar de garantía.
3. **Si la regla D4 no se puede testear de forma determinista sin arrancar la máquina** → hay que rediseñar la detección de arranque sucio o renunciar al automatismo y quedarse con la regla de 2.2.3.
4. **Si el número de referencias documentales al pilar BENCHMARK resulta inmanejable sin reescribir el CHANGELOG** → se abandona D5(b) y se conserva el nombre `Benchmark` como pilar, añadiendo Overclock como página.
5. **Si aparece un requisito de OC persistente** (que sobreviva al reinicio sin sesión) → **no se resuelve con el servicio**: es la puerta de D7 y obliga a reconsiderar el driver firmado, con el coste de §3.4.

---

# PARTE 7 — REGLAS DURADERAS DEL REPOSITORIO

Reglas aprendidas en rondas anteriores de bugfixing y **verificadas contra el código de 2.2.3**. No deben violarse.

| Regla | Origen / verificación |
|---|---|
| Contar ficheros **excluyendo `obj/` y `bin/`** | El borrador de este documento se equivocó por no hacerlo (§2.2) |
| **Nunca copiar `TimeoutProfile.HeavyOptimizationIds` a mano** | Bug real, **dos veces**. El comentario del fichero documenta que vivían triplicados y que el gateway mataba un DISM a mitad |
| **`Commit` se journaliza el último** | CAO-BUG-2026-10-06: 88 de 98 entradas quedaron abiertas para siempre |
| **No añadir fases terminales nuevas** sin cambiar `IsTerminal` y `Incomplete()` con tests | `TransactionEvent.cs:26`, `ITransactionJournal.cs:31` |
| **Reutilizar los enums existentes.** Nunca duplicar `EvidenceLevel`, `RiskLevel`, `TransactionPhase`, `TimeoutProfile`, `CaOPaths`, `RouteTable`, `Version.props` | Convención de fuente única de verdad del repo |
| Nunca `SearchOption.AllDirectories` sin `EnumerationOptions { IgnoreInaccessible = true }` y sin reparse points | `RecursiveEnumerationGuardTests` |
| Un `Detect` que no pudo inspeccionar **no puede responder «applied»** | Contrato de diagnóstico (bug real en limpiezas) |
| Guard de reentrada `_busy` por página | Reentrada de UI |
| Nunca `break` en el primer fallo de un lote | BUG 2.2.3: «El lote de recomendados paraba en el primer fallo» |
| Los cuerpos `async void` necesitan `try/catch` **y** deben ser esperados por quien llama | BUG 2.2.3: «Un segundo diálogo cerraba la aplicación» |
| **Un solo `ContentDialog` a la vez** vía `UiDialogs` | CAO-BUG-2026-10-06: mataba la app |
| No lanzar procesos ni leer stdout en el hilo de UI | BUG 2.2.3: congelación de 13 s por `sc.exe` |
| Progreso indeterminado + techo anunciado para trabajo largo por ítem | UX de operaciones largas |
| **Nunca una ruta literal**: usar `CaOPaths` | Convención; el borrador proponía `C:\ProgramData\CA-O\overclock\...` |
| Escritura atómica `tmp → flush → Move` + cuarentena `.corrupt-<timestamp>`; **comprobar el retorno del mutex** | BUG 2.2.3: «el mutex de snapshots perdía la exclusión en silencio» |
| Verificar los negativos de una delegación antes de actuar | Disciplina de herramientas |
| **El CHANGELOG no se reescribe**; se añade entrada | Regla de `Keep a Changelog`, que el repo declara seguir |
| **`docs/superpowers/` y `.superpowers/` están en `.gitignore`** | Verificado en `.gitignore` |

**Precedente arquitectónico clave:** la POLÍTICA vive en `CA-O.Core` (testeable) y la llama el servicio privilegiado. Ejemplos: `OperationResultCodes`, `SafeErrorMessages`, `TimeoutProfile`.

**Fronteras verificadas por tests:** `ArchitectureDependencyTests` — UI sin procesos ni pipe (**L40**), Core sin UI ni Infrastructure (**L52**), Infrastructure/Privileged sin UI.
**Consistencia documental verificada por test:** `DocumentationCodeConsistencyTests` — «**This test FAILS when docs drift from code**».

---

# PARTE 8 — COMANDOS DE VERIFICACIÓN

```powershell
# Build (Release, ~1m40s medido; 0 warnings es obligatorio: son errores)
dotnet build CA-O.sln -c Release --nologo -v minimal

# Test — DEBE incluir el build
dotnet test CA-O.sln -c Release

# Gates de verificación del repo
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

> **Aviso verificado en este repositorio:** ejecutar `dotnet test --no-build` sobre assemblies obsoletos da conteos incorrectos. Nunca usar `--no-build` para verificar.

**Conteo reproducible de código** (usar siempre este patrón):

```bash
find src tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -exec cat {} + | wc -l
```

**Comprobar antes de tocar nada:**

```bash
git status --short                     # ¿hay trabajo sin commitear? (§2.17)
git log --oneline -3                   # ¿estamos sobre eb6c561?
grep -n 'Page Include' src/CA-O.UI/CA-O.UI.csproj
grep -n 'benchmark' src/CA-O.UI/Navigation/RouteTable.cs
```

**Herramientas disponibles:** `gh` autenticado como **Pyromesis**; Inno Setup Compiler en `C:/Program Files (x86)/Inno Setup 6/ISCC.exe`.

**Git:** `memoria HOLA/` está untracked — excluir de commits con pathspec `:!memoria HOLA`.

---

# PARTE 9 — ESTADO Y PRÓXIMOS PASOS

## 9.1 Estado actual

| Ítem | Estado |
|---|---|
| Investigación de Hypertune | **COMPLETA Y VERIFICADA** (con fuentes fechadas; 8 correcciones aplicadas) |
| Mapa del repositorio CA-O | **COMPLETO Y VERIFICADO** (cifras medidas, 12 correcciones aplicadas) |
| Verificación técnica de viabilidad | **COMPLETA** (con documentación de fabricante; 6 supuestos del borrador refutados) |
| Decisión 1 — CPU | **REVISADA** → Opción D′ (§4.1) |
| Decisión 2 — alcance | **CONSERVADA**, con ajuste (§4.2) |
| Decisión 3 — Enfoque 2 con extensiones | **APROBADO**, con contratos corregidos (§5.2) |
| Decisiones 4-7 | **NUEVAS Y PENDIENTES DE APROBACIÓN** (§4.3) |
| Revisión de esta revisión por el usuario | **PENDIENTE** |
| **Código escrito** | **0** |
| Operaciones git | **0** |

## 9.2 Preguntas de diseño — estado revisado

| # | Pregunta | Resolución | Sección |
|---|---|---|---|
| 1 | Estabilidad: ¿quién elige el punto? | El motor elige; el usuario aprueba el perfil, puede abortar y fijar techo manual. Presupuesto `DispatchHeavy` (20 min) | §6.5 |
| 2 | ¿Dónde vive el checkpoint? | `CaOPaths.OverclockRunsDirectory`, escrito **antes** de tocar nada, con escritura atómica, mutex verificado y **ventana deslizante** de historial | §6.6 |
| 3 | ¿Se conserva `BenchmarkAsync`? | **Sí, y además se conservan `BenchmarkPolicy`, `BenchmarkAnalyzer` y `BenchmarkModels`.** No son un score: son el motor de deltas honestos. **La premisa del borrador para borrarlos era falsa** | §2.8, §6.8, §6.16 |
| 4 | Los 17 tests de `Benchmark.Tests` | **No se retiran** (decisión D6). Si se renombra el proyecto, se mueven los 5 ficheros completos, incluido `FrameCaptureTests.cs` | §6.16 |
| 5 | Ubicación del código | Política y dominio en `CA-O.Core\Overclock\`; backends finos en `CA-O.Infrastructure\Overclock\` (invocables desde la UI elevada); presentación en `CA-O.UI\OverclockPage` | §6.2 |
| 6 | Nomenclatura | **D5(b):** añadir tag `"overclock"` y `OverclockPage` **sin retirar** `"benchmark"` en la primera entrega. Revisar el renombrado del pilar después | §6.16 |
| 7 | Visual | Se conserva el tema de CA-O; se adopta la arquitectura de información de Hypertune. Se rechaza el score y el copy sin evidencia | §6.10 |
| 8 | **(NUEVA)** ¿En qué proceso se escribe? | GPU en la UI elevada (sesión interactiva); política de CPU y journal en el servicio | §5.3 |
| 9 | **(NUEVA)** ¿Cómo se reconcilia el revert automático con 2.2.3? | Primer arranque sucio: avisar. Segundo consecutivo: revertir. | §6.6 |
| 10 | **(NUEVA)** ¿Se adopta un driver firmado? | Hoy no; criterio de decisión escrito en §6.14 y D7 | §6.14 |
| 11 | **(NUEVA)** ¿Qué se promete de CPU? | Detección + guiado XTU/IPF, y tuning por política. **Nunca «+X MHz»** | §3.3, §4.1 |
| 12 | **(NUEVA)** ¿Qué se promete de memoria? | Nominal vs configurada + enlace a la BIOS. **Nunca leer el perfil XMP/EXPO** | §6.7 |

## 9.3 Flujo restante

1. **Revisión de esta Revisión 2 por el usuario.** Puntos que requieren decisión explícita: **D3** (§5.3), **D4** (§6.6), **D5** (§6.16), **D6** (§6.16), **D7** (§6.14).
2. **Antes de escribir código, fijar la baseline.** El árbol tiene hoy **1314 tests verdes** (medido): 1278 del commit 2.2.3 + 36 del tutorial sin commitear. O se **commitea el tutorial**, o se **declara la baseline de 1314** en el plan y en el CHANGELOG. Ambas son válidas; lo que no lo es es verificar contra un número que no corresponde al árbol (§2.14, §2.17).
3. Extraer la spec a `docs\superpowers\specs\2026-10-10-overclock-design.md` — **con la cabecera del formato del repo** (`Fecha`, `Estado`, `Ruta del proceso`), siguiendo el precedente de `2026-10-08-tutorial-recorrido-guiado-design.md`. **Nota: `docs/superpowers/` está en `.gitignore`**, así que esa spec no se versionará; si se quiere versionada, va en `docs/`.
4. Self-review de la spec: placeholders, coherencia interna, alcance, ambigüedad.
5. Producir el plan de implementación por fases en `.superpowers\sdd\<fecha>-overclock\`, con el patrón del repo: `progress.md` + `task-N-brief.md` + `task-N-report.md` + `task-N-review.md`.
6. Implementación **Fase 0 → Fase 6**, con `dotnet build CA-O.sln -c Release` (**0 warnings**) y `dotnet test CA-O.sln -c Release` **verde al final de cada fase**, sin perder ningún test de la baseline fijada en el paso 2.

## 9.4 Cómo leer este documento

| Si quieres… | Ve a |
|---|---|
| Saber qué estaba mal en el borrador | **Parte 0** (§0.2) |
| Entender qué hace Hypertune y por qué es mejorable | Parte 1 (§1.1–§1.10) |
| **Saber que Hypertune instala drivers kernel** | **§1.4** |
| Saber qué tiene CA-O hoy, con cifras fiables | Parte 2 (§2.1–§2.17) |
| **Saber qué contratos no se pueden romper** | **§2.15** |
| Saber qué es realmente posible en modo usuario | **Parte 3** (§3.7 es la tabla resumen) |
| Entender por qué el OC de CPU no va a existir | §3.3, §3.4, §4.1 |
| Ver los enfoques descartados y por qué | Parte 5 (§5.1) |
| Ver los contratos de backend y de IPC | §5.2, §5.4 |
| **Decidir dónde se ejecuta la escritura** | **§5.3** |
| **Implementar** | **Parte 6** — empezar por §6.3 (dominio) y §6.15 (tests primero) |
| Saber cuánto cuesta de verdad | §6.13 (fases), §6.19 (riesgos), §6.20 (criterios de muerte) |
| No romper nada | §2.15 y Parte 7 |

---

## Anexo — Procedencia de las cifras de esta revisión

| Cifra | Comando o fuente |
|---|---|
| Ficheros y líneas por proyecto | `find <dir> -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \| wc -l` y `-exec cat {} + \| wc -l` |
| 12 proyectos, `Uninstaller` en la `.sln` | `grep -o '= "[^"]*", "[^"]*\.csproj"' CA-O.sln` |
| 5 ficheros y 17 tests de `Benchmark.Tests` | `grep -c '\[Fact\]\|\[Theory\]'` por fichero |
| 18 miembros de `PrivilegedOperationKind` | `sed -n '21,41p' src/CA-O.Shared/IPC/IpcProtocol.cs` |
| 18 miembros de `TransactionPhase` | `src/CA-O.Shared/Contracts/Lifecycle.cs:75` |
| Conjunto terminal | `src/CA-O.Shared/DTO/TransactionEvent.cs:26` |
| `Incomplete()` = último evento apilado | `src/CA-O.Core/Rollback/ITransactionJournal.cs:31` |
| `EvidenceLevel` con 6 valores, 123 consumidores | `src/CA-O.Shared/Enums/Enums.cs:33`; `grep -rn 'EvidenceLevel' src \| wc -l` |
| `ArchitectureDependencyTests` L40 y L52 | `grep -n 'DoesNotContain' tests/CA-O.Integration.Tests/ArchitectureDependencyTests.cs` |
| Build verde | `dotnet build CA-O.sln -c Release` → 0 warnings, 0 errors, 1m40s |
| **1314 tests verdes, 0 fallos** | `dotnet test CA-O.sln -c Release` → `Failed: 0` en las seis suites (786+248+145+60+58+17), exit code 0 |
| Precios y citas de Hypertune | Fuentes enlazadas en §1.2, §1.5, §1.6, §1.7 |
| Viabilidad técnica | Fuentes enlazadas en la Parte 3 completa |
