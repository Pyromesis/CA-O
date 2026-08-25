import fs from 'node:fs';
const p = 'src/components/ca-o/ProfileSelector.tsx';
let s = fs.readFileSync(p, 'utf8');
if (!s.includes('function ElapsedTicker')) {
  const anchor = '// ============================================\n// Confirmation Dialog Component';
  const comp = `function ElapsedTicker() {
  const [seconds, setSeconds] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setSeconds((s) => s + 1), 1000);
    return () => clearInterval(t);
  }, []);
  const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
  const ss = String(seconds % 60).padStart(2, '0');
  return <span className="font-mono opacity-70">({mm}:{ss})</span>;
}

`;
  s = s.replace(anchor, comp + anchor);
  fs.writeFileSync(p, s);
  console.log('ElapsedTicker added');
} else console.log('already present');
