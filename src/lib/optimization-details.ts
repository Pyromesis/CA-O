type OptimizationCategory = 'system' | 'network' | 'input' | 'tweaks' | 'powerful' | 'privacy';
type SecurityImpact = 'none' | 'low' | 'medium' | 'high' | 'reduces-security';
type PerformanceImpact = 'low' | 'medium' | 'high' | 'very-high';

import { optimizationTexts } from './optimization-descriptions';

const riskReasonById: Record<string, string> = {
  'dns-optimization': 'Fuerza Cloudflare y puede ignorar DNS corporativo, parental o de una VPN.',
  'disable-search-indexing': 'Las búsquedas de archivos serán más lentas al perder el índice.',
  'disable-superfetch': 'Puede empeorar el inicio de aplicaciones, sobre todo en discos mecánicos.',
  'disable-print-spooler': 'Impide imprimir hasta reactivar el servicio Spooler.',
  'disable-bits': 'Puede interrumpir Windows Update y descargas que usan BITS.',
  'disable-hibernation': 'Elimina hiberfil.sys y también afecta Inicio rápido.',
  'remove-onedrive': 'Elimina sincronización y recuperación de OneDrive; respalda archivos primero.',
  'disable-ipv6-transition': 'Puede romper redes que dependan de mecanismos de transición IPv6.',
  'disable-llmnr': 'Algunos recursos antiguos dejarán de resolver nombres localmente.',
};

export interface OptimizationDetail {
  whatIsItEs: string;
  whatIsItEn: string;
  whatDoesEs: string;
  whatDoesEn: string;
  whatItAppliesEs: string;
  whatItAppliesEn: string;
  securityExplanationEs: string;
  securityExplanationEn: string;
  performanceExplanationEs: string;
  performanceExplanationEn: string;
  limitationsEs: string;
  limitationsEn: string;
}

const categoryContext: Record<OptimizationCategory, OptimizationDetail> = {
  system: {
    whatIsItEs: 'Un ajuste de servicios, privacidad o comportamiento general de Windows.', whatIsItEn: 'A Windows service, privacy, or general system behavior setting.',
    whatDoesEs: 'Cambia el componente indicado y después comprueba su estado real.', whatDoesEn: 'Changes the selected component and then checks its actual state.',
    whatItAppliesEs: 'Servicios, políticas y procesos del sistema operativo.', whatItAppliesEn: 'Operating-system services, policies, and processes.',
    securityExplanationEs: 'No concede permisos nuevos por sí mismo; el riesgo depende de la función que se desactive.', securityExplanationEn: 'It does not grant new permissions by itself; risk depends on the disabled function.',
    performanceExplanationEs: 'Impacto normalmente bajo o medio: reduce actividad en segundo plano, pero el resultado depende del equipo.', performanceExplanationEn: 'Usually low or medium impact: it reduces background activity, but results depend on the PC.',
    limitationsEs: 'Puede cambiar funciones de Windows que otras aplicaciones esperan encontrar activas.', limitationsEn: 'It may change Windows functions that other applications expect to be enabled.',
  },
  network: {
    whatIsItEs: 'Un ajuste de la pila de red, resolución de nombres, energía o transporte TCP/IP.', whatIsItEn: 'A network-stack, name-resolution, power, or TCP/IP transport setting.',
    whatDoesEs: 'Modifica la configuración de red indicada y verifica el estado que Windows devuelve.', whatDoesEn: 'Changes the selected network setting and verifies the state reported by Windows.',
    whatItAppliesEs: 'Adaptadores, DNS, TCP/IP, Winsock y servicios de red del equipo local.', whatItAppliesEn: 'Adapters, DNS, TCP/IP, Winsock, and network services on the local PC.',
    securityExplanationEs: 'No sustituye al firewall ni al antivirus; algunos cambios pueden romper controles DNS, IPv6 o de red corporativa.', securityExplanationEn: 'It does not replace the firewall or antivirus; some changes can bypass or break DNS, IPv6, or corporate-network controls.',
    performanceExplanationEs: 'El beneficio esperado es bajo a medio; una menor latencia solo aparece si el cuello de botella coincide con este ajuste.', performanceExplanationEn: 'Expected benefit is low to medium; lower latency occurs only when this setting matches the actual bottleneck.',
    limitationsEs: 'El proveedor, el router, el controlador y el servidor remoto pueden hacer que no haya mejora o que empeore.', limitationsEn: 'The ISP, router, driver, and remote server may provide no improvement or make results worse.',
  },
  input: {
    whatIsItEs: 'Un ajuste de entrada de Windows para ratón, teclado, touchpad o USB.', whatIsItEn: 'A Windows input setting for the mouse, keyboard, touchpad, or USB.',
    whatDoesEs: 'Cambia cómo Windows procesa la entrada y comprueba la configuración resultante.', whatDoesEn: 'Changes how Windows processes input and checks the resulting configuration.',
    whatItAppliesEs: 'Cola de entrada, periféricos, accesibilidad y administración de energía USB.', whatItAppliesEn: 'Input queues, peripherals, accessibility, and USB power management.',
    securityExplanationEs: 'No mejora ni desactiva la protección contra malware; puede afectar accesibilidad o la disponibilidad de un periférico.', securityExplanationEn: 'It does not improve or disable malware protection; it may affect accessibility or peripheral availability.',
    performanceExplanationEs: 'La mejora esperada es baja: puede reducir esperas percibidas, pero no aumenta la frecuencia física del periférico.', performanceExplanationEn: 'Expected improvement is low: it may reduce perceived delay but cannot increase a peripheral’s physical polling rate.',
    limitationsEs: 'El firmware y el software del fabricante pueden ignorar ajustes globales de Windows.', limitationsEn: 'Firmware and vendor software may ignore global Windows settings.',
  },
  tweaks: {
    whatIsItEs: 'Un ajuste visual o de interfaz de Windows.', whatIsItEn: 'A Windows visual or interface setting.',
    whatDoesEs: 'Cambia la presentación o el contenido de la interfaz y verifica el valor aplicado.', whatDoesEn: 'Changes interface presentation or content and verifies the applied value.',
    whatItAppliesEs: 'Explorador, barra de tareas, escritorio, animaciones y notificaciones.', whatItAppliesEn: 'Explorer, taskbar, desktop, animations, and notifications.',
    securityExplanationEs: 'Normalmente no cambia la seguridad; ocultar extensiones o archivos puede dificultar identificar archivos peligrosos.', securityExplanationEn: 'It normally does not change security; hiding extensions or files can make dangerous files harder to identify.',
    performanceExplanationEs: 'Impacto bajo: reduce renderizado o contenido en segundo plano, no acelera el hardware.', performanceExplanationEn: 'Low impact: it reduces rendering or background content, but does not make hardware faster.',
    limitationsEs: 'Es un cambio de experiencia de usuario y puede requerir reiniciar Explorer o volver a iniciar sesión.', limitationsEn: 'This is a user-experience change and may require restarting Explorer or signing in again.',
  },
  powerful: {
    whatIsItEs: 'Un ajuste avanzado de energía, memoria, programación, servicios o políticas de Windows.', whatIsItEn: 'An advanced Windows power, memory, scheduling, service, or policy setting.',
    whatDoesEs: 'Aplica el cambio avanzado indicado y ejecuta una verificación específica después.', whatDoesEn: 'Applies the selected advanced change and runs a targeted verification afterward.',
    whatItAppliesEs: 'Planificador, energía, memoria, GPU, servicios y políticas del sistema.', whatItAppliesEn: 'Scheduler, power, memory, GPU, services, and system policies.',
    securityExplanationEs: 'Los ajustes marcados como advertencia pueden reducir defensas, aumentar superficie de ataque o impedir actualizaciones.', securityExplanationEn: 'Warning-level settings may reduce defenses, increase attack surface, or prevent updates.',
    performanceExplanationEs: 'El impacto puede ser medio o alto, pero una cifra universal de FPS o latencia no es técnicamente fiable.', performanceExplanationEn: 'Impact can be medium or high, but no universal FPS or latency figure is technically reliable.',
    limitationsEs: 'El beneficio depende del hardware, controladores, carga y aplicaciones; mide antes y después.', limitationsEn: 'Benefits depend on hardware, drivers, workload, and applications; measure before and after.',
  },
  privacy: {
    whatIsItEs: 'Un control de privacidad de Windows para telemetría, permisos, personalización o sincronización.', whatIsItEn: 'A Windows privacy control for telemetry, permissions, personalization, or synchronization.',
    whatDoesEs: 'Reduce la recopilación o el acceso indicado y verifica la política local resultante.', whatDoesEn: 'Reduces the selected collection or access and verifies the resulting local policy.',
    whatItAppliesEs: 'Datos de diagnóstico, identificadores, permisos de aplicaciones y servicios en la nube.', whatItAppliesEn: 'Diagnostic data, identifiers, app permissions, and cloud services.',
    securityExplanationEs: 'Suele mejorar privacidad, pero puede quitar funciones útiles como localizar el dispositivo, dictado o sincronización.', securityExplanationEn: 'It usually improves privacy, but may remove useful features such as device location, dictation, or sync.',
    performanceExplanationEs: 'Impacto bajo: reduce tareas y comunicaciones concretas, sin prometer una mejora medible de FPS.', performanceExplanationEn: 'Low impact: it reduces specific tasks and communications without promising measurable FPS gains.',
    limitationsEs: 'La política local no controla necesariamente datos enviados por aplicaciones de terceros.', limitationsEn: 'A local policy does not necessarily control data sent by third-party applications.',
  },
};

const overrides: Record<string, Partial<OptimizationDetail>> = {
  'disable-vbs': { securityExplanationEs: 'Reduce la protección VBS, Credential Guard y HVCI; un exploit de kernel tendría menos aislamiento.', securityExplanationEn: 'Reduces VBS, Credential Guard, and HVCI protection; a kernel exploit would have less isolation.' },
  'disable-defender': { securityExplanationEs: 'Deja el equipo sin protección antivirus en tiempo real y aumenta directamente el riesgo de malware.', securityExplanationEn: 'Leaves the PC without real-time antivirus protection and directly increases malware risk.' },
  'disable-smartscreen': { securityExplanationEs: 'Elimina comprobaciones de reputación para descargas, aplicaciones y sitios; aumenta el riesgo de phishing y malware.', securityExplanationEn: 'Removes reputation checks for downloads, apps, and sites; increases phishing and malware risk.' },
  'disable-ransomware-protection': { securityExplanationEs: 'Desactiva Acceso controlado a carpetas y deja documentos sensibles más expuestos al ransomware.', securityExplanationEn: 'Disables Controlled folder access and leaves sensitive documents more exposed to ransomware.' },
  'disable-pagefile': { securityExplanationEs: 'No reduce malware, pero puede causar pérdida de disponibilidad y evitar volcados útiles para investigar fallos.', securityExplanationEn: 'It does not reduce malware, but can reduce availability and prevent useful crash dumps.' },
  'remove-onedrive': { securityExplanationEs: 'Reduce integración en la nube, pero elimina sincronización y recuperación; conserva copias antes de aplicarlo.', securityExplanationEn: 'Reduces cloud integration but removes sync and recovery; keep backups before applying it.' },
};

function humanize(id: string): string { return id.replace(/-/g, ' '); }

export function getOptimizationDetail(
  id: string,
  category: OptimizationCategory,
  securityImpact: SecurityImpact,
  performanceImpact: PerformanceImpact,
  implementation: string,
): OptimizationDetail {
  const base = categoryContext[category];
  const detail = overrides[id] || {};
  const texts = optimizationTexts[id];
  const name = humanize(id);
  const securityReason = riskReasonById[id];
  const securityText = securityImpact === 'reduces-security'
    ? 'Reduce una defensa del sistema y aumenta la exposición a ataques; úsalo solo con una razón concreta y durante el menor tiempo posible.'
    : securityReason || base.securityExplanationEs;
  const securityTextEn = securityImpact === 'reduces-security'
    ? 'It reduces a system defense and increases attack exposure; use it only for a concrete reason and for the shortest possible time.'
    : base.securityExplanationEn;
  const performanceText = `Clasificación: ${performanceImpact}. ${base.performanceExplanationEs} El cambio concreto es: ${implementation}.`;
  const performanceTextEn = `Rating: ${performanceImpact}. ${base.performanceExplanationEn} The concrete change is: ${implementation}.`;
  return { ...base, ...detail,
    whatIsItEs: texts?.isItEs || detail.whatIsItEs || `Un ajuste de Windows para ${name}.`,
    whatIsItEn: texts?.isItEn || detail.whatIsItEn || `A Windows setting for ${name}.`,
    whatItAppliesEs: texts?.appliesEs || detail.whatItAppliesEs || `El componente concreto modificado es: ${implementation}.`,
    whatItAppliesEn: texts?.appliesEn || detail.whatItAppliesEn || `The specific component modified is: ${implementation}.`,
    securityExplanationEs: detail.securityExplanationEs || securityText,
    securityExplanationEn: detail.securityExplanationEn || securityTextEn,
    performanceExplanationEs: detail.performanceExplanationEs || performanceText,
    performanceExplanationEn: detail.performanceExplanationEn || performanceTextEn,
    whatDoesEs: texts?.doesEs || detail.whatDoesEs || `Aplica el ajuste «${name}» y comprueba con el verificador asociado que Windows haya cambiado realmente.`,
    whatDoesEn: texts?.doesEn || detail.whatDoesEn || `Applies “${name}” and uses the associated verifier to confirm that Windows actually changed.`,
  };
}