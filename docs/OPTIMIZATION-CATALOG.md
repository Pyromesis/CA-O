# Catálogo de optimizaciones CA-O 2.0

Calidad sobre cantidad (spec 131): cada entrada responde qué cambia, por qué, con qué evidencia, qué riesgo y seguridad afecta, si es reversible y cómo se verifica. Las clasificaciones alimentan el motor de recomendaciones; ninguna optimización queda sin clasificar (contrato automatizado en `OptimizationCatalogContractTests`).

## Tabla maestra

| Id | Categoría | Impacto esperado | Evidencia | Riesgo | Compatibilidad | Seguridad | Reversible | Flags |
|---|---|---|---|---|---|---|---|---|
| disable-game-bar-dvr | Gaming | WorkloadDependent | Vendor | Low | NoKnownConflict | None | Sí | — |
| enable-gpu-scheduling | Gaming | WorkloadDependent | Vendor | Moderate | Conditional | None | Sí | RequiresReboot |
| normalize-tcp-autotuning | Network | WorkloadDependent | Official | Low | Conditional | None | Sí | — |
| disable-background-apps | Performance | Small | Official | Low | NoKnownConflict | PrivacyOnly | Sí | — |
| disable-search-indexing | Performance | WorkloadDependent | Empirical | Moderate | Conditional | None | Sí | RecommendedOnSsd |
| disable-transparency | Performance | Tiny | Empirical | Safe | Compatible | None | Sí | — |
| disable-vbs | Performance | WorkloadDependent | Vendor | Critical | PotentialConflict | ReducedProtection | Sí | ExpertOnly, SecurityTradeoff, RequiresReboot |
| disable-visual-effects | Performance | Tiny | Empirical | Safe | Compatible | None | Sí | — |
| maximum-power-plan | Performance | Small | Official | Low | Compatible | None | Sí | — |
| zero-menu-delay | Performance | Tiny | Heuristic | Safe | Compatible | None | Sí | — |
| disable-copilot | PrivacySecurity | None | Vendor | Low | NoKnownConflict | PrivacyOnly | Sí | — |
| disable-cortana | PrivacySecurity | None | Official | Low | NoKnownConflict | PrivacyOnly | Sí | — |
| disable-onedrive-autostart | PrivacySecurity | Tiny | Official | Low | Conditional | PrivacyOnly | Sí | ExpertOnly |
| disable-suggestions | PrivacySecurity | None | Official | Low | Compatible | PrivacyOnly | Sí | — |
| disable-telemetry | PrivacySecurity | None | Official | Low | Compatible | PrivacyOnly | Sí | — |
| disable-widgets | PrivacySecurity | Tiny | Official | Low | NoKnownConflict | PrivacyOnly | Sí | — |
| disable-hibernate | Storage | None | Official | Moderate | NoKnownConflict | None | Sí | — |
| optimize-system-drive | Storage | None | Official | Low | Compatible | None | No (mantenimiento) | NotReversible |

## Notas por optimización

- **disable-vbs**: reduce la seguridad del kernel. Con Vanguard u otro anti-cheat presente queda **bloqueado por defecto** (`blocked-by-default`/`blocked-anticheat`). Rompe WSL2/Docker/Sandbox al desactivar el hipervisor. Requiere modo Expert + doble confirmación + reinicio. Rollback restaura `hypervisorlaunchtype` previo.
- **enable-gpu-scheduling (HAGS)**: beneficio dependiente de GPU/driver/juego (spec 21). El motor lo trata como Conditional+WorkloadDependent; recomendación sólo tras análisis, nunca automática.
- **disable-game-bar-dvr**: si el usuario graba con Game Bar, NO aplicar (spec 25). Clasificado como captura-dependiente.
- **maximum-power-plan**: cambio de esquema de energía documentado por Microsoft. En portátiles con batería, los perfiles Gaming/Competitive lo bloquean por impacto.
- **zero-menu-delay**: tweak cosmético heredado (MenuShowDelay). Evidence=Heuristic → cae automáticamente en bucket Experimental.
- **disable-search-indexing**: requiere SSD (flag RecommendedOnSsd); sin SSD el bucket es NotApplicable. Riesgo Moderate porque afecta la búsqueda del sistema.
- **normalize-tcp-autotuning**: restaura el valor estándar cuando un tweak externo lo alteró; no aplica "optimizaciones TCP" aleatorias (spec 55).
- **optimize-system-drive**: mantenimiento irreversible (defrag HDD / retrim SSD según medio real). Siempre bucket Optional, jamás auto-aplicado.
- **Privacidad (telemetry/cortana/widgets/copilot/suggestions/onedrive)**: gestión de funciones y privacidad (spec 61, 63, 97); su puntuación nunca alimenta métricas de rendimiento.

## Bloqueos por defecto (spec 95)

`AntiCheatGuard.NeverAutoRecommend` contiene además ids reservados para tweaks que CA-O **no ofrece hoy** pero reconoce como peligrosos si aparecieran importados: CPU min state 100%, core parking off, memory compression off, MPO off global, FSO global, pagefile estático, NetworkThrottlingIndex/SvcHostSplit hacks, borrado de Prefetch, hypervisorlaunchtype off directo, etc.

## Informe final por optimización (spec 141)

| Id | Comportamiento actual | Tras aplicar | Verificación | Benchmark |
|---|---|---|---|---|
| disable-game-bar-dvr | Game DVR activo o ausente | Política GameDVR DisableWrite / AllowGameDVR=0 | Lectura registro en vivo | n/a (captura-dependiente) |
| enable-gpu-scheduling | HAGS según driver/build | HwSchMode=2 | Registro + PendingReboot | A/B manual recomendado |
| normalize-tcp-autotuning | normal / disabled / experimental | normal (restore) | `netsh int tcp` vía estado | n/a |
| disable-background-apps | Apps en segundo plano activas | GlobalUserDisabled=1 | Registro en vivo | n/a |
| disable-search-indexing | WSearch Auto | WSearch Disabled (+captura estado) | ServiceManager observado | n/a |
| disable-transparency | Transparencia activa | EnableTransparency=0 | Registro en vivo | n/a |
| disable-vbs | Hypervisor Auto/On | hypervisorlaunchtype Off | bcdedit enum (Unknown→reboot) | Workload-dependent |
| disable-visual-effects | Efectos visuales por defecto | VisualFXSetting/animaciones off | Registro en vivo | n/a |
| maximum-power-plan | Esquema activo actual | SCHEME_MIN/MAX | powercfg getactivescheme | n/a |
| zero-menu-delay | MenuShowDelay 400 | MenuShowDelay=0 | Registro en vivo | n/a |
| disable-copilot | Copilot presente | WindowsCopilotPolicy/UserDisabled=1 | Registro en vivo | n/a |
| disable-cortana | Cortana permitida | AllowCortana=0 | Registro en vivo | n/a |
| disable-onedrive-autostart | OneDrive arranca con sesión | Run key OneDrive eliminado | Registro en vivo | n/a |
| disable-suggestions | Sugerencias activas | SubscribedContent-338388Enabled=0 etc. | Registro en vivo | n/a |
| disable-telemetry | Telemetría completa | AllowTelemetry=0(+servicios) | Registro/servicio observado | n/a |
| disable-widgets | Widgets habilitados | Dsh Allowed=0 | Registro en vivo | n/a |
| disable-hibernate | Hibernación activa | powercfg /h off (captura previa) | Estado powercfg | n/a |
| optimize-system-drive | — (mantenimiento) | Optimize-Volume -Defrag/-ReTrim | Exit code + salida | Duración informada |

> "n/a" significa que la optimización no tiene ruta de benchmark propia; el benchmark de sistema (BenchmarkPage) sirve para comparar antes/después a nivel máquina.
