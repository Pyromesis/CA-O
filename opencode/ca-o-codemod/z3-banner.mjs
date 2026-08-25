import fs from 'node:fs';
const p = 'src/components/ca-o/ProfileSelector.tsx';
let s = fs.readFileSync(p, 'utf8');

// Banner de resultado tras aplicar un perfil
const anchor = '  return (\n    <div className={cn("space-y-4", className)}>';
const banner = `  return (
    <div className={cn("space-y-4", className)}>
      {applyNotice && (
        <motion.div
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          className="rounded-2xl p-4 text-sm"
          style={{
            background: applyNotice.failed.length ? 'rgba(239, 68, 68, 0.12)' : 'rgba(16, 185, 129, 0.12)',
            border: \`1px solid \${applyNotice.failed.length ? 'rgba(239, 68, 68, 0.35)' : 'rgba(16, 185, 129, 0.35)'}\`,
          }}
        >
          <div className="font-semibold mb-1" style={{ color: applyNotice.failed.length ? '#EF4444' : '#10B981' }}>
            {applyNotice.failed.length
              ? \`Perfil \${applyNotice.profileName}: \${applyNotice.ok}/\${applyNotice.total} aplicadas — \${applyNotice.failed.length} fallaron\`
              : \`Perfil \${applyNotice.profileName}: \${applyNotice.total}/\${applyNotice.total} aplicadas correctamente\`}
          </div>
          {applyNotice.failed.length > 0 && (
            <ul className="mt-2 space-y-1" style={{ color: applyNotice.failed.length ? '#fca5a5' : undefined }}>
              {applyNotice.failed.map((f) => (
                <li key={f.id} className="truncate">
                  • {humanizeId(f.id)} — <span className="opacity-80">{f.reason}</span>
                </li>
              ))}
            </ul>
          )}
          <button
            onClick={() => setApplyNotice(null)}
            className="mt-2 text-xs underline opacity-70 hover:opacity-100"
          >
            Cerrar
          </button>
        </motion.div>
      )}`;
s = s.replace(anchor, banner);
fs.writeFileSync(p, s);
console.log('banner añadido:', s.includes('applyNotice && ('));
