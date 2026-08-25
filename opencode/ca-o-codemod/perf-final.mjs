import fs from 'node:fs';

// ── 1) SERVIDOR: cachear generateOptimizations (el catálogo no cambia en runtime) ──
{
  const p = 'src/app/api/optimization/route.ts';
  let s = fs.readFileSync(p, 'utf8');
  if (!s.includes('let catalogCache')) {
    // Insertar caché antes de generateOptimizations
    s = s.replace(
      '// Generate full optimization items from commands',
      `// Catálogo cacheado a nivel de módulo: los items base nunca cambian en runtime.
// Solo isApplied cambia, y eso se aplica después desde la DB.
let catalogCache: OptimizationItem[] | null = null;

// Generate full optimization items from commands`
    );
    // Cachear el resultado
    s = s.replace(
      'function generateOptimizations(): OptimizationItem[] {\n  const items: OptimizationItem[] = [];',
      'function generateOptimizations(): OptimizationItem[] {\n  if (catalogCache) return catalogCache;\n  const items: OptimizationItem[] = [];'
    );
    s = s.replace(
      '  return items;\n}',
      '  catalogCache = items;\n  return items;\n}',
    );
    fs.writeFileSync(p, s);
    console.log('servidor: catálogo cacheado');
  }
}

// ── 2) CLIENTE: React.memo en tarjetas + sin stagger en listas grandes ──
{
  const p = 'src/components/ca-o/FullOptimizationPanel.tsx';
  let s = fs.readFileSync(p, 'utf8');

  // Memoizar tarjetas
  if (!s.includes('const OptimizationCard = memo')) {
    s = s.replace('function OptimizationCard({', 'const OptimizationCard = memo(function OptimizationCard({');
    // Cierre de OptimizationCard: buscar la última '}' antes de OptimizationCardGrid
    const gridIdx = s.indexOf('function OptimizationCardGrid');
    // El cierre es '}' justo antes del comentario de OptimizationCardGrid
    const beforeGrid = s.lastIndexOf('}', gridIdx);
    s = s.slice(0, beforeGrid) + '});' + s.slice(beforeGrid + 1);
  }
  if (!s.includes('const OptimizationCardGrid = memo')) {
    s = s.replace('function OptimizationCardGrid({', 'const OptimizationCardGrid = memo(function OptimizationCardGrid({');
    // El cierre es al final del archivo antes de export
    const exportIdx = s.indexOf('// Default Export');
    const beforeExport = s.lastIndexOf('}', exportIdx);
    s = s.slice(0, beforeExport) + '});' + s.slice(beforeExport + 1);
  }

  // Importar memo si no está
  if (!s.includes('const { memo } = require') && !s.includes('import { memo')) {
    s = s.replace("import { useState, useEffect, useCallback, useMemo, useRef } from 'react';",
                  "import { useState, useEffect, useCallback, useMemo, useRef, memo } from 'react';");
  }

  // Eliminar stagger: delay 0 en vez de index * 0.03
  s = s.replace('transition={{ delay: index * 0.03 }}', 'transition={{ duration: 0.2 }}');

  fs.writeFileSync(p, s);
  console.log('cliente: memo + sin stagger');
}

// ── 3) CLIENTE: debounce en búsqueda (200ms) ──
{
  const p = 'src/components/ca-o/FullOptimizationPanel.tsx';
  let s = fs.readFileSync(p, 'utf8');
  if (!s.includes('debouncedSearch')) {
    // Añadir estado de debounce después del searchQuery
    s = s.replace(
      "const [searchQuery, setSearchQuery] = useState('');",
      `const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchQuery), 200);
    return () => clearTimeout(timer);
  }, [searchQuery]);`
    );
    // Usar el valor debouncado en el filtro
    s = s.replace(
      "if (searchQuery.trim()) {\n      const query = searchQuery.toLowerCase();",
      "if (debouncedSearch.trim()) {\n      const query = debouncedSearch.toLowerCase();"
    );
    // El key del contenedor usa searchQuery → cambiar a debouncedSearch
    s = s.replace("key={`list-${activeCategory}-${searchQuery}-", "key={`list-${activeCategory}-${debouncedSearch}-");
    s = s.replace("key={`grid-${activeCategory}-${searchQuery}-", "key={`grid-${activeCategory}-${debouncedSearch}-");
    fs.writeFileSync(p, s);
    console.log('cliente: búsqueda con debounce 200ms');
  }
}

// ── 4) ELECTRON: límite de memoria del servidor Node ──
{
  const p = 'electron-main.js';
  let s = fs.readFileSync(p, 'utf8');
  if (!s.includes('--max-old-space-size')) {
    s = s.replace(
      "serverProcess = spawn(nodeExe, [serverJs], {",
      "serverProcess = spawn(nodeExe, ['--max-old-space-size=256', serverJs], {"
    );
    fs.writeFileSync(p, s);
    console.log('electron: heap limitado a 256MB');
  }
}

// ── 5) Dashboard: pausar sondeo cuando la ventana no está visible ──
{
  const p = 'src/components/ca-o/DashboardView.tsx';
  let s = fs.readFileSync(p, 'utf8');
  if (!s.includes('document.hidden')) {
    s = s.replace(
      "const interval = setInterval(fetchData, 5000); // Fetch every 5 seconds",
      `const interval = setInterval(() => {
        if (!document.hidden) fetchData();
      }, 5000); // Pausado cuando la ventana está minimizada`
    );
    fs.writeFileSync(p, s);
    console.log('dashboard: sondeo pausado en background');
  }
}
