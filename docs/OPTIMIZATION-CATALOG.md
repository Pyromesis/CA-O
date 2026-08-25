# CA-O — Catálogo y metodología de evidencia (v3, 2026)

> Tabla completa de auditoría individual: [docs/OPTIMIZATION-AUDIT.md](OPTIMIZATION-AUDIT.md) (155 IDs, generada automáticamente).

## Cómo se clasifica una optimización

Cada ID del catálogo tiene:

1. **Taxonomía** (`src/lib/catalog/taxonomy.ts`): `group/subgroup/kind`.
2. **Evidencia** (`src/lib/catalog/evidence.ts`): impacto esperado, confianza, fuentes, racional bilingüe, efectos adversos.
3. **Aplicabilidad** (`src/lib/catalog/applicability.ts`): prerrequisitos de contexto, confirmaciones extra, sensibilidad anti-cheat.

### Tipos de acción (`kind`)

| kind | Significado |
|---|---|
| optimization | Cambio con objetivo de rendimiento, con evidencia declarada |
| maintenance | Mantenimiento/higiene del sistema (no vende FPS) |
| repair-action | Herramienta de reparación o troubleshooting |
| security-hardening | Mejora la seguridad (SMBv1 off, LLMNR off...) |
| security-tradeoff | Reduce seguridad a cambio de otra cosa; confirmación dedicada |
| privacy-control | Control de telemetría/privacidad |
| cosmetic | Preferencia visual/de comportamiento |
| diagnostic | Diagnóstico/experimental; requiere reconocer su naturaleza |
| guidance | Solo guía: no automatizable de forma segura |

## Reclasificación destacada (auditoría v2)

| ID | Antes | Ahora | Motivo |
|---|---|---|---|
| disable-memory-integrity | Powerful ("very-high") | Security / hvci-vbs / security-tradeoff | Reducción crítica de seguridad; nunca en perfiles gaming |
| memory-compression | Powerful gaming default | Experimental / diagnostics | Solo justificable con RAM 32 GB+ y presión medida baja |
| disable-superfetch | System | Experimental / contested | En SSD modernos raramente es cuello de botella |
| static-pagefile | Powerful | Experimental / hardware-experimental | Requiere dimensionar commit pico; riesgo de fallos de commit |
| timer-resolution-0-5ms | Input | Experimental / advanced-power | No persistente, coste energético; helper con vida limitada |
| dns-optimization | Network "optimización" | Experimental + benchmark | El DNS no afecta al ping en partida; se mide antes de tocar |
| disable-network-throttling | Network | Experimental / contested | Escenario afectado prácticamente inexistente hoy |
| winsock-reset / flush-dns / reset-network / flush-arp-cache | Network performance | Repair / network-repair | Acciones de reparación, no optimizaciones |
| clear-temp-files | System "optimización" | Repair / troubleshooting / maintenance | Liberar espacio no es ganancia de FPS |
| registry-cleanup | Powerful | Repair / troubleshooting / maintenance | MRU lists; mantenimiento menor |
| disable-fullscreen-optimizations | Powerful global toggle | Repair / troubleshooting | Global off solo como diagnóstico; preferencia por-juego |
| disable-multiplane-overlay | Powerful global toggle | Repair / troubleshooting | MPO off solo para flickering concreto de driver/GPU |
| disable-smb1, disable-llmnr, disable-netbios, disable-wpad, restrict-point-and-print, admin-shares, RDP/NLA... | Network/System perf | Security / hardening | Reducción de superficie de ataque, no rendimiento |
| disable-cpu-idle / enable-core-parking / disable-power-throttling | Powerful universales | Performance+gates (desktop/AC) o experimental | Contexto obligatorio; contraproducente en batería |
| disable-modern-standby | Powerful | Experimental | Puede romper el sueño del equipo; solo diagnóstico |
| disable-tablet-input-service / disable-windows-ink | Input/System | System services + gate sin táctil | Rompería entrada táctil si existe pantalla táctil |

## Scoring

```
score = evidence(0-25) + confidence(0-25) + expectedBenefit(0-20)
      + safety(0-20)   + compatibility(0-10)

labels: recommended >=75 · conditional >=55 · caution >=35 · else not-recommended
```

Un tweak sin evidencia no puede puntuar alto aunque sea inocuo.

## Matriz anti-cheat (resumen operativo)

| Anti-cheat | Detección | Conflictos con catálogo CA-O |
|---|---|---|
| Riot Vanguard | servicios vgc/vgk, proceso vgtray | Secure Boot exigido en Win11; HVCI recomendada ON → `disable-memory-integrity` genera bloqueo/aviso contextual |
| Easy Anti-Cheat | servicio EasyAntiCheat(_EOS) | Sin conflictos conocidos con este catálogo |
| BattlEye | servicio/proceso BEService | Sin conflictos conocidos |
| FACEIT AC | servicio faceit, proceso faceitclient | Estricto con kernel/debuggers; el catálogo no toca nada de eso |
| nProtect GameGuard | servicio/proceso GameMon | Sin conflictos conocidos |

La detección vive en `/api/system/context`; la evaluación contextual en `evaluateApplicability`.

## Elementos de solo-guía (no ejecutables)

`optimize-startup`, `mouse-polling`, `enable-mouse-raw-input`, `enable-msi-gpu`, `disable-keyboard-filter`: automatizarlos de forma segura no es posible (dependen de hardware/aplicaciones); se muestran con instrucciones manuales.
