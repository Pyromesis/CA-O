import fs from 'node:fs';
const p = 'src/app/globals.css';
let s = fs.readFileSync(p, 'utf8');
if (s.includes('CA-O Design Refinement')) { console.log('already present'); process.exit(0); }

const block = `
/* ─── CA-O Design Refinement Layer ─── */

/* Tipografía refinada */
body {
  letter-spacing: -0.011em;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-rendering: optimizeLegibility;
}

h1, h2, h3, h4 {
  letter-spacing: -0.02em;
  line-height: 1.25;
}

/* Glass refinado: bordes más suaves, sin dureza */
.glass-ultra, .glass-liquid, .glass-strong {
  backdrop-filter: blur(24px) saturate(1.4) !important;
  -webkit-backdrop-filter: blur(24px) saturate(1.4) !important;
  transition: background 0.3s ease, border-color 0.3s ease, box-shadow 0.3s ease;
}

/* Sombras en capas (más profundidad, menos peso) */
[class*="rounded-2xl"], [class*="rounded-xl"] {
  transition: box-shadow 0.25s ease, transform 0.2s ease, border-color 0.2s ease;
}

/* Scrollbar fino y elegante */
::-webkit-scrollbar { width: 5px; height: 5px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb {
  background: rgba(255, 107, 53, 0.2);
  border-radius: 99px;
}
::-webkit-scrollbar-thumb:hover { background: rgba(255, 107, 53, 0.35); }
* { scrollbar-width: thin; scrollbar-color: rgba(255,107,53,0.2) transparent; }

/* Focus ring sutil y elegante */
:focus-visible {
  outline: 2px solid rgba(255, 107, 53, 0.4);
  outline-offset: 2px;
  border-radius: 4px;
}

/* Selección de texto con el acento */
::selection {
  background: color-mix(in srgb, var(--ca-o-accent, #ffa94d) 30%, transparent);
  color: inherit;
}

/* Transiciones suaves universales (solo propiedades baratas) */
button, a, input, select, [role="button"] {
  transition: color 0.15s ease, background-color 0.15s ease, border-color 0.15s ease, opacity 0.15s ease;
}

/* Enlaces y botones: cursor refinado */
button:not(:disabled), [role="button"]:not(:disabled) { cursor: pointer; }
button:disabled { cursor: not-allowed; }

/* Inputs refinados */
input, textarea, select {
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}
input:focus, textarea:focus, select:focus {
  border-color: rgba(255, 107, 53, 0.35) !important;
}

/* Números tabulares para contadores y estadísticas */
.tabular-nums, [class*="font-mono"] { font-variant-numeric: tabular-nums; }

/* Sutiles líneas divisorias más elegantes */
hr, [class*="border-t"], [class*="border-b"] {
  opacity: 0.7;
}
`;
s += block;
fs.writeFileSync(p, s);
console.log('design refinement layer added');
