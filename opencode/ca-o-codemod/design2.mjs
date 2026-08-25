import fs from 'node:fs';

// ── Header: refinado elegante ──
{
  const p = 'src/components/ca-o/Header.tsx';
  let s = fs.readFileSync(p, 'utf8');

  // Fondo más sutil: menos blur pesado, borde más fino, sombra en capas suave
  s = s.replace(
    `background: isDark
            ? 'rgba(25, 25, 45, 0.7)'
            : 'rgba(255, 255, 255, 0.75)',
          backdropFilter: 'blur(20px)',
          WebkitBackdropFilter: 'blur(20px)',
          border: \`1px solid \${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}\`,
          boxShadow: isDark
            ? '0 8px 32px rgba(0, 0, 0, 0.3), inset 0 1px 0 rgba(255, 255, 255, 0.05)'
            : '0 8px 32px rgba(0, 0, 0, 0.08), inset 0 1px 0 rgba(255, 255, 255, 0.8)',`,
    `background: isDark
            ? 'rgba(18, 18, 35, 0.72)'
            : 'rgba(255, 255, 255, 0.82)',
          backdropFilter: 'blur(24px) saturate(1.3)',
          WebkitBackdropFilter: 'blur(24px) saturate(1.3)',
          border: \`1px solid \${isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)'}\`,
          boxShadow: isDark
            ? '0 1px 2px rgba(0,0,0,0.15), 0 4px 16px rgba(0,0,0,0.12), inset 0 1px 0 rgba(255,255,255,0.04)'
            : '0 1px 2px rgba(0,0,0,0.05), 0 4px 16px rgba(0,0,0,0.06), inset 0 1px 0 rgba(255,255,255,0.9)',`
  );

  // Título más refinado: tracking más apretado, subtítulo más sutil
  s = s.replace(
    `className="text-lg font-bold tracking-tight"`,
    `className="text-base font-semibold tracking-tight"`
  );

  fs.writeFileSync(p, s);
  console.log('Header refinado');
}

// ── Dock: más limpio, menos peso visual ──
{
  const p = 'src/components/ca-o/Dock.tsx';
  let s = fs.readFileSync(p, 'utf8');

  // Fondo del dock: menos opaco, sombra más sutil
  s = s.replace(
    `background: 'rgba(30, 30, 50, 0.75)',
          backdropFilter: 'blur(20px)',
          WebkitBackdropFilter: 'blur(20px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          boxShadow: \`
            0 25px 50px -12px rgba(0, 0, 0, 0.5),
            0 0 0 1px rgba(255, 255, 255, 0.05),
            inset 0 1px 0 rgba(255, 255, 255, 0.05)
          \`,`,
    `background: 'rgba(22, 22, 40, 0.72)',
          backdropFilter: 'blur(24px) saturate(1.3)',
          WebkitBackdropFilter: 'blur(24px) saturate(1.3)',
          border: '1px solid rgba(255, 255, 255, 0.06)',
          boxShadow: \`
            0 2px 8px rgba(0, 0, 0, 0.15),
            0 8px 24px rgba(0, 0, 0, 0.2),
            inset 0 1px 0 rgba(255, 255, 255, 0.04)
          \`,`
  );

  fs.writeFileSync(p, s);
  console.log('Dock refinado');
}

// ── FullOptimizationPanel: tarjetas más refinadas ──
{
  const p = 'src/components/ca-o/FullOptimizationPanel.tsx';
  let s = fs.readFileSync(p, 'utf8');

  // Fondo del panel: menos saturado
  s = s.replace(
    "'bg-[#0a0a1a]'",
    "'bg-[#0b0b18]'"
  );

  fs.writeFileSync(p, s);
  console.log('Panel refinado');
}
