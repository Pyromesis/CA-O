# CA-O Windows Optimizer

**CA-O** (Configuración Avanzada y Optimización) es una herramienta de optimización para Windows 10/11. Está construida como una aplicación web con Next.js que se distribuye como aplicación de escritorio mediante un lanzador de Electron. Ejecuta cambios reales en el registro de Windows, servicios y configuración del sistema a través de PowerShell, con verificación obligatoria después de cada cambio y rutas de reversión verificadas.

- Repositorio: <https://github.com/Pyromesis/CA-O>
- Versión actual: **0.2.1**
- Plataforma: Windows 10 (1903+) y Windows 11

---

## Índice

1. [Estado del proyecto](#estado-del-proyecto)
2. [Arquitectura v2 y documentación técnica](#arquitectura-v2-y-documentación-técnica)
3. [Características](#características)
3. [Modelo de seguridad y verificación](#modelo-de-seguridad-y-verificación)
4. [Categorías de optimización](#categorías-de-optimización)
5. [Solución de problemas integrada](#solución-de-problemas-integrada)
6. [Perfiles](#perfiles)
7. [Arquitectura](#arquitectura)
8. [Stack tecnológico](#stack-tecnológico)
9. [Requisitos del sistema](#requisitos-del-sistema)
10. [Instalación](#instalación)
11. [Scripts disponibles](#scripts-disponibles)
12. [API interna](#api-interna)
13. [Persistencia de datos](#persistencia-de-datos)
14. [Estructura del proyecto](#estructura-del-proyecto)
15. [Pruebas](#pruebas)
16. [Atajos de teclado](#atajos-de-teclado)
17. [Solución de problemas comunes](#solución-de-problemas-comunes)
18. [Limitaciones conocidas](#limitaciones-conocidas)
19. [Hoja de ruta](#hoja-de-ruta)
20. [Licencia](#licencia)

---

## Estado del proyecto

| Métrica | Valor |
|---|---|
| Optimizaciones ejecutables y verificables | **147** |
| IDs definidos en el catálogo interno de comandos | 156 |
| Cambios irreversibles (requieren confirmación explícita) | 9 |
| Elementos de solo guía (no automatizables) | 5 |
| Rutinas de reparación (troubleshooting) | 12 |
| Perfiles predefinidos | 4 |
| Idiomas de la interfaz | Español (por defecto) e Inglés |
| Pruebas de contrato | 5 suites (`npm run test:contracts`) |

El proyecto está en fase beta funcional. La versión web (modo desarrollo) y la versión de escritorio (instalador NSIS) comparten el mismo código de aplicación.

## Arquitectura v2 y documentación técnica

Tras una auditoría completa, el catálogo funciona con un modelo basado en evidencia: cada optimización declara impacto esperado, confianza, fuentes, condiciones de aplicabilidad y efectos adversos. Las acciones de reparación/mantenimiento/cosmética están separadas de las de rendimiento, los trade-offs de seguridad viven en su propia categoría con confirmación dedicada, y la API local exige token de sesión, origen 127.0.0.1, rate limiting y audit log.

### Qué NO promete CA-O

- Milagros de FPS ni "+20 FPS" sin medición reproducible.
- Reducciones universales de latencia o de ping.
- Mejoras de red automáticas (Cloudflare no es "más rápido para juegos").
- Garantías absolutas de compatibilidad con anti-cheats.

### Qué SÍ ofrece CA-O

- Diagnóstico primero: térmico, memoria, almacenamiento, red (con bufferbloat), drivers problemáticos, estado de seguridad.
- Configuración consciente del contexto: cada tweak se evalúa contra TU equipo antes de poder aplicarse.
- Optimización segura: gates de prerrequisitos/conflictos, snapshots estructurados, verificación conductual, reversión.
- Benchmarking honesto: DNS benchmark, muestreo del sistema, bufferbloat; los datos de FPS/frame-time requieren el helper nativo futuro y no se inventan.
- Health score basado en problemas medidos, no en número de tweaks aplicados.
- Gestión de privacidad/seguridad separada del rendimiento.

Documentación detallada:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — capas, flujo de Apply, seguridad local, modelo de privilegios objetivo.
- [docs/OPTIMIZATION-CATALOG.md](docs/OPTIMIZATION-CATALOG.md) — metodología de clasificación, reclasificaciones realizadas, matriz anti-cheat, scoring.
- [docs/OPTIMIZATION-AUDIT.md](docs/OPTIMIZATION-AUDIT.md) — auditoría individual de los 155 IDs con disposición final (generada).

Endpoints nuevos relevantes: `/api/system/context`, `/api/diagnostics/{overview|thermal|input|network}`, `/api/benchmark/dns`, `/api/benchmark/system`, `/api/profiles/plan`, `/api/maintenance/temp`.

---

## Características

### Motor de optimización

- Ejecución real mediante PowerShell: registro (`HKLM` / `HKCU`), servicios (`Stop-Service`, `Set-Service`), `powercfg`, `netsh`, entre otros.
- Verificación post-aplicación obligatoria: cada optimización tiene un script de verificación que lanza error si el cambio no quedó aplicado. Si falla, no se marca como aplicado.
- Captura de instantánea del estado original antes de aplicar, usada luego por la reversión.
- Reversión con verificación propia para la mayoría de los elementos (142+ comandos de verificación de reversión).
- Reverificación del estado real contra Windows con caché de 30 segundos y procesamiento por lotes; los registros obsoletos se corrigen automáticamente.
- Elementos repetibles de mantenimiento (limpiar temporales, vaciar DNS, etc.) y elementos de sesión (no persistidos).

### Seguridad

- Clasificación de riesgo por elemento: `safe` / `warning` / `dangerous`.
- Los 9 cambios irreversibles están bloqueados salvo confirmación explícita (`confirmDangerous`).
- Punto de restauración de Windows opcional antes de aplicar.
- Razones de riesgo documentadas bilingües por elemento.
- Advertencias de impacto en seguridad y de compatibilidad con anti-cheats cuando aplica.

### Interfaz

- Dashboard con monitoreo en tiempo real (CPU, RAM, disco, GPU, adaptadores de red, SO, uptime) vía sondeo cada 5 segundos, pausado cuando la ventana no está visible.
- Indicador de salud (health score) calculado a partir de las optimizaciones aplicadas.
- Historial de cambios (últimos 100 eventos) y cola de deshacer.
- Asistente de bienvenida (onboarding) y pantalla de carga.
- Temas claro/oscuro, efectos de sonido opcionales, notificaciones.
- Búsqueda y filtrado de optimizaciones.
- Exportación e importación de configuración.
- Programador de tareas para aplicar perfiles automáticamente (funciona mientras la aplicación esté abierta).
- Interfaz completamente bilingüe español/inglés con fallback tolerante a claves faltantes.

---

## Modelo de seguridad y verificación

Para que una optimización sea ejecutable desde la interfaz debe cumplir **todas** estas condiciones:

1. Tener comando de aplicación definido.
2. Tener comando de verificación post-aplicación.
3. Tener ruta de reversión con verificación de reversión, **o** estar clasificada explícitamente como irreversible.
4. No estar en la lista de elementos no ejecutables.

Consecuencias de este modelo sobre el catálogo de 156 IDs:

| Conjunto | Cantidad | Comportamiento |
|---|---|---|
| Ejecutables verificados | 147 | Visibles y aplicables desde la interfaz |
| Irreversibles | 9 | Aplicables solo con `confirmDangerous`; ejemplos: eliminar OneDrive, reset de Winsock, reset de red |
| Solo guía | 5 | Se muestran aparte con la razón documentada de por qué no se automatizan |
| Sin verificación post-aplicación | 4 | Excluidos del catálogo ejecutable |

Además:

- La captura de estado original es obligatoria: si falla, la aplicación se rechaza con error 503.
- Los cambios ya aplicados se rechazan (409) salvo elementos repetibles o de sesión.
- El endpoint `/api/registry` está deshabilitado deliberadamente (responde 501) hasta que exista un adaptador real de registro; no se simulan cambios.

---

## Categorías de optimización

Recuentos previos al filtro de verificación; el total ejecutable publicado por la API es 147.

| Categoría | Descripción | Elementos |
|---|---|---|
| Sistema | Telemetría, servicios en segundo plano, limpieza de temporales, Cortana, Xbox, indexación | 26 |
| Red | Reducción de latencia estilo ExitLag: TCP, DNS, QoS, Wi-Fi, Winsock | 19 |
| Entrada | Ratón, teclado, polling USB, Game Bar, latencia de dispositivos HID | 21 |
| Tweaks | Explorador, barra de tareas, menú contextual, ajustes visuales de Windows | 30 |
| Potentes | Planes de energía, GPU, VBS/HVCI, memoria virtual, procesos en segundo plano | 30 |
| Privacidad | Telemetría avanzada, publicidad, seguimiento, permisos, Copilot | 30 |

Cada elemento incluye nombre y descripción bilingües, explicación de qué hace y a qué afecta, claves de registro, comandos y servicios involucrados, nivel de riesgo, impacto en rendimiento, impacto en seguridad, advertencias de anti-cheat y requisitos de reinicio.

---

## Solución de problemas integrada

La vista de troubleshooting ejecuta 12 rutinas reales de reparación mediante PowerShell, cada una con pasos, estado, problemas corregidos, recomendaciones y aviso de reinicio:

| Acción | Función |
|---|---|
| `create-restore-point` | Crea un punto de restauración de Windows |
| `restore-audio` | Reinicia servicios y dispositivos de audio |
| `restore-bluetooth` | Repara la pila Bluetooth |
| `restore-network` | Restablece adaptadores y configuración de red |
| `restore-windows-update` | Repara componentes de Windows Update |
| `restore-display` | Reinicia controladores gráficos |
| `restore-all` | Restauración general del sistema |
| `repair-system-files` | SFC / DISM para archivos del sistema |
| `reset-store-cache` | Limpia la caché de Microsoft Store |
| `restart-explorer` | Reinicia el proceso Explorer |
| `flush-dns-cache` | Vacía la caché DNS |
| `clean-temp-junk` | Elimina archivos temporales |

---

## Perfiles

Cuatro perfiles predefinidos con aplicación secuencial, creación de punto de restauración previa y progreso en vivo por elemento:

| Perfil | Enfoque |
|---|---|
| Gaming | Latencia de entrada, red y rendimiento en juegos |
| Productividad | Limpieza de sistema y fluidez general |
| Power Saver | Ahorro de energía para portátiles |
| Privacidad | Bloqueo de telemetría y seguimiento |

El programador permite ejecutar cualquier perfil en fecha/hora concreta o de forma recurrente. La programación persiste localmente y se evalúa cada minuto; solo se ejecuta con la aplicación abierta.

---

## Arquitectura

```
Electron (lanzador)
  |- ventana splash
  |- reserva puerto local (preferido: 38957, fallback: puerto libre)
  |- inicia servidor Next.js standalone con node.exe portable
  |- espera respuesta HTTP (electron net)
  |- abre ventana principal (1400x900) -> http://127.0.0.1:<puerto>
        |
        v
Next.js (App Router, output standalone)
  |- React 19 + Zustand + Tailwind 4
  |- API Routes (/api/...)
  |- lib/optimization-commands.ts (catálogo de comandos PowerShell)
  |- lib/powershell-runner.ts (ejecución de scripts)
  |- lib/db.ts (persistencia JSON crash-safe)
```

Detalles relevantes del lanzador (`electron-main.js`):

- Una sola instancia activa (single instance lock).
- `nodeIntegration: false` y `contextIsolation: true`.
- Los enlaces externos se abren en el navegador del sistema; la navegación fuera de la URL local se bloquea.
- Registro del lanzador en `%TEMP%\ca-o-launcher.log`.
- El puerto preferido es fijo (38957) para mantener estable el origen de `localStorage` entre sesiones; si está ocupado se usa cualquier puerto libre.
- Reintenta la carga de la página hasta 10 veces si el servidor tarda en responder.

---

## Stack tecnológico

| Capa | Tecnología | Uso |
|---|---|---|
| Framework | Next.js 16 (App Router, salida standalone) | Servidor y enrutamiento |
| UI | React 19 | Componentes |
| Lenguaje | TypeScript 5 | Tipado estático |
| Estilos | Tailwind CSS 4 | Estilos utilitarios |
| Componentes | shadcn/ui + Radix UI | Primitivas accesibles |
| Estado | Zustand 5 (con middleware persist) | Estado global del cliente |
| Escritorio | Electron 43 + electron-builder 26 | Lanzador y empaquetado NSIS |
| Hardware | systeminformation | Métricas reales de CPU/RAM/disco/GPU/red |
| Gráficos | Recharts | Visualización de datos |
| Animaciones | Framer Motion | Transiciones |
| Iconos | Lucide React | Iconografía |
| Notificaciones | Sonner | Toasts |

Nota: el proyecto **no usa Prisma ni SQLite**. La capa de persistencia es un archivo JSON gestionado por `src/lib/db.ts`.

---

## Requisitos del sistema

### Para usar la versión de escritorio (instalador)

| Componente | Requisito |
|---|---|
| Sistema operativo | Windows 10 (1903+) o Windows 11 |
| Privilegios | Administrador (el instalador lo exige; los cambios tocan HKLM y servicios) |
| RAM | 4 GB mínimo |
| Espacio en disco | ~500 MB |
| Pantalla | 1024x700 mínimo |

No requiere Node.js instalado: el instalador empaqueta un runtime portable de Node.

### Para desarrollo (versión web)

| Componente | Requisito |
|---|---|
| Node.js | 18 o superior |
| npm | Incluido con Node.js |
| Sistema | Windows (las optimizaciones ejecutan PowerShell real) |

---

## Instalación

### Opción A: modo desarrollo (web)

```bash
git clone https://github.com/Pyromesis/CA-O.git
cd CA-O
npm install
npm run dev
```

Abrir <http://localhost:3000> (enlazado a 127.0.0.1).

Recomendado tras instalar dependencias:

```bash
npm run security:check   # auditoría de vulnerabilidades
npm run lint             # verificación de código
npm run test:contracts   # pruebas de contrato
```

### Opción B: compilación de escritorio (instalador NSIS)

```bash
npm run dist
```

Este comando ejecuta en orden:

1. `next build` (salida standalone).
2. `node build-electron.js`: copia `node.exe` portable del equipo hacia `dist/CA-O-Windows-Optimizer/`, junto con el servidor standalone, assets estáticos, carpeta `public` y dependencias trazadas (`server-dependencies/`).
3. `electron-builder --win`: genera el instalador NSIS en `dist-electron/`.

El instalador se declara con `requestedExecutionLevel: requireAdministrator`, necesario porque la mayoría de optimizaciones escriben en HKLM y gestionan servicios.

Salida típica: `dist-electron/CA-O Windows Optimizer Setup <versión>.exe`.

---

## Scripts disponibles

| Comando | Descripción |
|---|---|
| `npm run dev` | Servidor de desarrollo en 127.0.0.1:3000 |
| `npm run build` | Compilación de producción Next.js |
| `npm start` | Sirve el build standalone (`node .next/standalone/server.js`) |
| `npm run lint` | ESLint sobre el proyecto |
| `npm run security:check` | Auditoría de dependencias (`npm audit`) |
| `npm run audit:fix` | Corrección automática de vulnerabilidades |
| `npm run electron:dev` | Abre la app en Electron en modo desarrollo |
| `npm run electron:build` | Empaqueta con electron-builder |
| `npm run dist` | Build completo de distribución (build + empaquetado NSIS) |
| `npm run test:contracts` | Ejecuta las 5 suites de pruebas de contrato |
| `npm run test:optimization-contract` | Valida integridad del catálogo de optimizaciones |
| `npm run test:troubleshoot-contract` | Valida las 12 rutinas de reparación |
| `npm run test:auxiliary-integrity` | Detecta comportamiento simulado prohibido en componentes auxiliares |
| `npm run test:api-security-contract` | Valida validación de entradas y stubs de seguridad en la API |
| `npm run test:persistence-recovery` | Prueba real de recuperación ante corrupción del JSON de estado |

---

## API interna

Todas las rutas viven bajo `src/app/api`:

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/optimization` | Catálogo completo (147 ejecutables + metadatos + elementos de solo guía), fusionado con el estado aplicado. Admite `?category=` |
| GET | `/api/optimization/state` | Mapa `{id: boolean}` con reverificación real contra Windows (caché 30 s, lotes paralelos) |
| POST | `/api/optimization/apply` | Aplica una optimización. Body: `{optimizationId, createBackup?, confirmDangerous?}`. Bloquea desconocidos (404), irreversibles sin confirmar (409) y duplicados (409) |
| POST | `/api/optimization/apply-all` | Aplicación por lotes. Body: `{ids[], createBackup?, confirmDangerous?}`. Devuelve resultado por ID |
| POST | `/api/optimization/revert` | Revierte una optimización usando su comando de reversión y verificación, restaurando la instantánea cuando existe |
| POST | `/api/optimization/revert-all` | Reversión por lotes con resultado por ID |
| GET/POST | `/api/app-state` | Banderas persistentes de UI (onboarding completado, splash visto) |
| GET | `/api/system/info` | Información real de hardware: CPU, RAM, discos, GPU, red, pantallas, SO, uptime |
| POST/GET | `/api/troubleshoot/execute` | Ejecuta una de las 12 rutinas de reparación (POST) o lista sus descriptores (GET) |
| GET/POST | `/api/registry` | Deshabilitado: responde siempre 501 hasta implementar un adaptador real |
| GET | `/api` | Stub de comprobación |

---

## Persistencia de datos

La aplicación no usa base de datos SQL. Todo el estado de optimizaciones se guarda en un archivo JSON con escritura atómica y copia de seguridad:

- Desarrollo: `optimization-state.json` en la raíz del proyecto.
- Empaquetado: `%APPDATA%\<userData>\optimization-state.json` (la ruta se comunica al servidor con la variable `CAO_STATE_PATH`).

Formato:

```json
{
  "disable-telemetry": {
    "applied": true,
    "updatedAt": "2026-01-01T12:00:00.000Z",
    "snapshot": "{ ... estado original capturado ... }"
  }
}
```

Garantías de `src/lib/db.ts`:

- Escritura segura ante fallos: copia a `.bak`, escritura a archivo temporal y renombrado atómico.
- Lecturas validadas; si el JSON principal está corrupto se recupera desde `.bak` automáticamente (cubierto por `test:persistence-recovery`).
- Firma de métodos compatible con Prisma (`findUnique`, `upsert`, etc.) para simplificar los handlers.

Otros datos locales (localStorage):

- `ca-o-storage`: estado de la interfaz (vistas, ajustes, historial) vía persist middleware de Zustand.
- `ca-o-schedules`: programaciones del planificador.

---

## Estructura del proyecto

```
CA-O/
├── electron-main.js              # Lanzador Electron (servidor local + ventana)
├── build-electron.js             # Preparación del paquete portable para electron-builder
├── next.config.ts                # Output standalone, externals de systeminformation
├── optimization-state.json       # Estado persistido (desarrollo)
├── src/
│   ├── app/
│   │   ├── page.tsx              # Punto de entrada de la SPA
│   │   └── api/                  # Rutas de API (ver tabla anterior)
│   ├── components/
│   │   ├── ca-o/                 # Vistas y paneles de la aplicación
│   │   │   ├── DashboardView.tsx
│   │   │   ├── FullOptimizationPanel.tsx
│   │   │   ├── TroubleshootingView.tsx
│   │   │   ├── SettingsView.tsx
│   │   │   ├── ProfileSelector.tsx
│   │   │   ├── Scheduler.tsx
│   │   │   ├── OptimizationHistory.tsx
│   │   │   └── ...
│   │   └── ui/                   # Primitivas shadcn/ui
│   ├── hooks/                    # useKeyboardShortcuts, use-toast
│   ├── lib/
│   │   ├── optimization-commands.ts   # Catálogo: apply/verify/revert/revert-verify
│   │   ├── powershell-runner.ts       # Ejecución de PowerShell
│   │   ├── db.ts                      # Persistencia JSON crash-safe
│   │   ├── verify-cache.ts            # Caché de reverificación
│   │   ├── optimization-descriptions.ts / -details.ts
│   │   └── i18n/                      # Diccionarios es/en
│   ├── store/useAppStore.ts      # Estado global (Zustand + persist)
│   └── types/                    # Tipos de optimizaciones
├── tests/                        # Suites de contrato (.mjs)
└── public/assets/                # Logotipos y recursos
```

---

## Pruebas

```bash
npm run test:contracts
```

Las pruebas son scripts Node sin framework que validan contratos del código fuente y comportamiento real de persistencia:

| Suite | Qué verifica |
|---|---|
| `optimization-contract` | Coherencia entre catálogo de comandos y catálogo expuesto por la API (puertas de verificación) |
| `troubleshoot-contract` | Que las 12 acciones de reparación existen con implementación real |
| `auxiliary-integrity` | Ausencia de comportamiento simulado (patrones `/simulat/i`, `Math.random()`) en exportación, sincronización en la nube y perfil de usuario |
| `api-security-contract` | Validación de entradas en apply/apply-all/revert/revert-all y stub 501 de `/api/registry` |
| `persistence-recovery` | Upserts reales contra un JSON temporal y recuperación automática tras corrupción |

---

## Atajos de teclado

| Tecla | Acción |
|---|---|
| `1` | Ir al Dashboard |
| `2` | Ir al centro de optimización |
| `3` | Ir a solución de problemas |
| `4` | Ir a ajustes |
| `Esc` | Cerrar modal de ayuda o volver al Dashboard |
| `?` | Abrir/cerrar ayuda de atajos |

Los atajos se ignoran mientras se escribe en campos de texto y cuando se usan modificadores Ctrl/Alt/Meta.

---

## Solución de problemas comunes

| Problema | Solución |
|---|---|
| La app no abre en http://localhost:3000 | Verifica que el puerto 3000 esté libre y que Node.js sea 18+. Borra `.next` y repite `npm run dev` |
| Las optimizaciones fallan al aplicar | Ejecuta el terminal como administrador; muchos cambios requieren HKLM y servicios |
| "El servidor no respondió a tiempo" (app de escritorio) | Consulta `%TEMP%\ca-o-launcher.log`; otro proceso puede ocupar el puerto 38957 |
| Un cambio dejó algo funcionando mal | Usa la reversión individual desde el historial, "Restauración completa" en troubleshooting, o un punto de restauración de Windows |
| Problemas de red tras optimizar | Ejecuta `netsh winsock reset`, `netsh int ip reset`, `ipconfig /flushdns` y reinicia; luego revierte la categoría Red |
| Estado inconsistente tras actualizar | La API `/api/optimization/state` reverifica contra Windows y auto-corrige registros obsoletos |

Antes de aplicar cambios masivos: crea un punto de restauración (botón dedicado en troubleshooting o dentro del flujo de perfiles) y revisa la descripción de cada optimización.

---

## Limitaciones conocidas

Documentadas de forma explícita para transparencia:

1. **Sincronización en la nube**: la interfaz existe pero es una simulación local; no hay backend de nube.
2. **Programador**: solo ejecuta perfiles mientras la aplicación está abierta (no hay servicio en segundo plano).
3. **Perfil de usuario**: la gamificación (XP, nivel, rachas) es local derivada del historial; no hay cuentas ni autenticación.
4. **Elementos de solo guía**: 5 optimizaciones no se automatizan por seguridad (por ejemplo, gestión de programas de inicio o polling de ratón que depende del hardware); se muestran con instrucciones manuales.
5. **`/api/registry`**: deshabilitado a propósito; el acceso al registro ocurre únicamente a través de los comandos del catálogo.
6. **Algunas reversiones manuales**: ciertos cambios (Winsock, DNS, limpieza de temporales) no tienen reversión automática porque su naturaleza es de mantenimiento o reset.
7. **Windows únicamente**: el motor depende de PowerShell y del registro de Windows.

---

## Hoja de ruta

| Objetivo | Descripción |
|---|---|
| Completar verificación del catálogo | Recuperar los 4 elementos excluidos por falta de verificación y evaluar nuevos tweaks |
| Servicio en segundo plano | Permitir que el programador ejecute sin la app abierta |
| Actualizador automático | Distribución de nuevas versiones del instalador |
| Ampliación de idiomas | Nuevos diccionarios además de ES/EN |
| Cobertura de pruebas | Ampliar las suites de contrato a nuevos módulos |

---

## Licencia

Proyecto bajo licencia MIT.

---

*Última actualización de este documento: agosto de 2026, correspondiente a la versión 0.2.1.*
