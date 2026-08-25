# CA-O — Arquitectura (v2)

Estado: refactor basado en evidencia. Este documento describe la arquitectura real del código tras la auditoría y las piezas que quedan como trabajo futuro declarado.

## 1. Principios

1. **Evidencia antes que folklore**: ninguna afirmación de rendimiento sin fuente (`microsoft | vendor | benchmark | empirical | heuristic`), confianza y efectos adversos documentados.
2. **Separación de intenciones**: rendimiento, seguridad, privacidad, gaming, reparación, mantenimiento y cosmética son grupos distintos. Una acción de limpieza nunca se presenta como optimización.
3. **Verificación sobre confianza**: cada cambio se verifica contra el estado real de Windows; la verificación es obligatoria, no opcional.
4. **Mínimo privilegio como dirección**: hoy el proceso del servidor corre elevado; el diseño prepara el aislamiento (ver §7).
5. **La API acepta IDs, no scripts**: jamás se ejecuta PowerShell arbitrario proveniente de la UI.

## 2. Procesos

```
Electron (lanzador, sandbox activo en el renderer)
  ├─ genera CAO_SESSION_SECRET (crypto.randomBytes)
  ├─ lo pasa al servidor por entorno y a la UI por hash de URL (#tk=...)
  └─ ventana principal -> http://127.0.0.1:<puerto>
          │
          ▼
Next.js standalone (servidor local, solo 127.0.0.1)
  ├─ API Routes con guardia de sesión/origen/rate-limit/audit
  ├─ catálogo + taxonomía + evidencia (lib/catalog/*)
  ├─ contexto del sistema (lib/system-context.ts)
  └─ persistencia JSON crash-safe (lib/db.ts)
```

## 3. Capas del catálogo

| Módulo | Responsabilidad |
|---|---|
| `lib/optimization-commands.ts` | Registro inmutable de comandos: apply / verify / revert / revert-verify / original-state por ID |
| `lib/catalog/taxonomy.ts` | Clasificación de los 156 IDs en grupo/subgrupo/tipo de acción |
| `lib/catalog/evidence.ts` | Evidencia (impacto esperado, confianza, fuentes), scoring 0-100 |
| `lib/catalog/applicability.ts` | Prerrequisitos contextuales, conflictos, confirmaciones requeridas |
| `lib/system-context.ts` | Detección real: SO, hardware, energía, Secure Boot/TPM/VBS/HVCI, anti-cheats |

El endpoint `/api/optimization` fusiona las capas y añade, por ítem: `group`, `subgroup`, `kind`, evidencia completa, `scoreTotal/scoreLabel`, `applicable`, `blockers[]`, `warnings[]`.

## 4. Flujo de Apply (defensa en profundidad)

1. Guardia de seguridad (token en producción/packaged, Host 127.0.0.1, Origin, rate-limit, audit log).
2. Validación de cuerpo; ID debe existir en el registro (`isExecutableOptimizationId`).
3. Confirmación explícita para irreversibles (`confirmDangerous`, 409 si falta).
4. **Gate de aplicabilidad** contra el contexto real (422 con bloqueos bilingües): factor de forma, fuente de energía, RAM, táctil, anti-cheat, confirmación `confirmSecurityChange` para trade-offs de seguridad, `acknowledgeExperimental` para experimentales.
5. Punto de restauración opcional.
6. Snapshot del estado original (obligatorio si existe comando de captura) + metadatos estructurados (`meta`: build, edición, huella de hardware, fecha, elevación).
7. Ejecución secuencial; primer error detiene.
8. Verificación primaria obligatoria.
9. Verificación conductual adicional cuando existe (p. ej., HVCI vía `Win32_DeviceGuard`, conectividad DNS real).
10. Persistencia solo si todo lo anterior tuvo éxito.

## 5. Estado deseado vs real

`GET /api/optimization/state` re-verifica cada fila aplicada contra Windows (caché 30 s, lotes paralelos) y devuelve:

- `data`: mapa `{id: actual}` (compatibilidad v1);
- `meta.details`: `{desired, actual, lastVerifiedAt, lastError, drift}` por ID;
- `meta.drifted`: IDs donde Windows difiere del registro; se reconcilian automáticamente a `actual`.

## 6. Perfiles adaptativos

`POST /api/profiles/plan {profileId, safeMode?}` evalúa el perfil contra la máquina:

- `apply`: aplicable según contexto y reglas;
- `skip`: con motivo documentado (no aplicable / trade-off de seguridad / excluido por modo seguro).

El store (`applyProfile`) consume el plan y solo aplica los `apply`; nunca envía confirmaciones en ciego. Sin planificador disponible usa una lista conservadora que excluye `security` y `experimental`. Los perfiles **nunca** incluyen cambios de seguridad críticos (`disable-memory-integrity` fue eliminado del perfil Fortnite).

## 7. Modelo de privilegios (objetivo)

Hoy: el instalador NSIS exige administrador y el servidor hereda elevación (necesario para HKLM/servicios). El runner etiqueta implícitamente operaciones privilegiadas y falla con mensajes claros si Windows deniega acceso.

Objetivo declarado (pendiente de implementación nativa):

```
UI (renderer, sandbox)  →  servidor sin privilegios  →  helper privilegiado
                            valida token/rate/IDs       ejecuta comandos firmados
                                                        del registro interno
```

La migración requiere un binario auxiliar propio (también resolvería timer resolution nativo y captura de FPS). El helper actual de timer resolution es un hijo controlado con vida máxima de 2 h, cierre garantizado (`finally` restaura la resolución), PID journal y sin `-ExecutionPolicy Bypass`.

## 8. Seguridad de la API local

- Token de sesión aleatorio por arranque (Electron ↔ servidor ↔ renderer).
- Comparación timing-safe; token solo exigido en producción/packaged (`next dev` queda usable).
- Solo `127.0.0.1`/`localhost`; Origin rechazado si no coincide.
- Rate limiting en memoria (12/min rutas pesadas, 90/min resto).
- Audit log append-only (`audit.log` junto al archivo de estado): intentos bloqueados incluidos.
- CSP estricta en Next (`frame-ancestors 'none'`, `object-src 'none'`, etc.), `sandbox: true`, permisos denegados, webviews prohibidos, navegación externa al navegador del sistema.

## 9. Benchmarks

- DNS: `/api/benchmark/dns` mide mediana, jitter y timeouts por resolvedor; recomienda pero **no cambia** nada.
- Sistema: `/api/benchmark/system` muestrea CPU, RAM, commit y compresión de memoria (antes/después manual).
- Mantenimiento: `/api/maintenance/temp` estima espacio por objetivo ANTES de limpiar; Prefetch no se limpia como "optimización".
- FPS/frame-time: requiere helper nativo; NO se simula.

## 10. Compatibilidad Windows 11 / anti-cheat

`/api/system/context` expone build, edición, forma factor, batería, Secure Boot, TPM (versión), VBS/HVCI, hipervisor, blocklist de drivers vulnerables y anti-cheats detectados (Vanguard, EAC, BattlEye, FACEIT, nProtect) con estado de compatibilidad honesto (`potential-conflict`, `no-known-conflict`). Nunca se afirma "anti-cheat safe".
