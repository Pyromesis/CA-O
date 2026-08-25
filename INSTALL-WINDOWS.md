# 🖥️ CA-O Windows Optimizer - Guía de Instalación Windows

## 📋 Requisitos Previos

Antes de instalar, asegúrate de tener instalado:

### 1. Node.js (v18 o superior)
```powershell
# Verificar si tienes Node.js instalado
node --version

# Si no lo tienes, descárgalo de:
# https://nodejs.org/ (Versión LTS recomendada)
```

### 2. npm (viene con Node.js)
```powershell
# Verificar versión de npm
npm --version
```

---

## 🚀 Instalación Paso a Paso

### Paso 1: Descomprimir el Archivo
```
1. Navega a la carpeta donde descargaste el archivo
2. Extrae: CA-O-Windows-Optimizer-FINAL.tar.gz
3. Te quedará una carpeta llamada "my-project"
```

### Paso 2: Abrir Terminal
```powershell
# En Windows Explorer, navega a la carpeta my-project
# Haz clic derecho en un espacio vacío → "Abrir en Terminal"
# O presiona Shift + clic derecho → "Abrir ventana de PowerShell aquí"
```

### Paso 3: Instalar Dependencias
```powershell
npm install
```

### Paso 4: 🔒 Solucionar Vulnerabilidades de Seguridad (IMPORTANTE)

Después de `npm install`, ejecuta:

```powershell
# Verificar vulnerabilidades
npm audit

# Solucionar automáticamente (recomendado)
npm run audit:fix

# Si hay vulnerabilidades persistentes, usar force:
npm run audit:fix:force
```

**Nota:** `audit:fix:force` puede causar cambios breaking pero es seguro para este proyecto.

### Paso 5: Iniciar Aplicación
```powershell
npm run dev
```

### Paso 6: Abrir en Navegador
```
Abre: http://localhost:3000
```

---

## 🔒 Vulnerabilidades de Seguridad - Guía Detallada

El proyecto incluye actualizaciones para las siguientes vulnerabilidades:

| Vulnerabilidad | Severidad | Paquete Afectado | Solución |
|---------------|-----------|------------------|----------|
| deepmerge-ts Stack Exhaustion | High | prisma | Actualizado a ^6.14.0 |
| js-yaml DoS (merge key) | High | @mdxeditor/editor | Actualizado a ^4.1.0 |
| prismjs DOM Clobbering | Moderate | react-syntax-highlighter | Actualizado a ^16.1.1 |
| sharp libvips CVEs | High | sharp | Actualizado a ^0.35.3 |

### Comandos Rápidos de Seguridad:

```powershell
# Verificar estado de seguridad
npm run security:check

# Fix automático (sin breaking changes)
npm run audit:fix

# Fix completo (con breaking changes si es necesario)
npm run audit:fix:force
```

---

## 🛠️ Solución de Problemas Comunes

### Error: "npm no se reconoce"
```powershell
# Solución: Reiniciar terminal o reiniciar PC después de instalar Node.js
# O usa la ruta completa:
C:\Program Files\nodejs\npm.cmd install
```

### Error: "Permiso denegado" (EACCES)
```powershell
# Ejecutar PowerShell como Administrador
# Click derecho en PowerShell → "Ejecutar como administrador"
```

### Error: Puerto 3000 en uso
```powershell
# Matar procesos en puerto 3000
netstat -ano | findstr :3000
taskkill /PID <PID> /F

# O usar otro puerto:
npm run dev -- -p 3001
```

### Error: Dependencias faltantes después de npm audit fix
```powershell
# Limpiar e reinstalar todo
rmdir /s /q node_modules
del package-lock.json
npm install
```

### Error: prisma generate falla
```powershell
# Regenerar cliente Prisma
npx prisma generate
```

---

## 📦 Estructura del Proyecto

```
my-project/
├── src/                    # Código fuente principal
│   ├── app/               # Páginas Next.js (App Router)
│   │   ├── api/           # Endpoints API
│   │   └── globals.css    # Estilos globales
│   ├── components/        # Componentes React
│   │   ├── ca-o/          # Componentes CA-O (35+ archivos)
│   │   └── ui/            # shadcn/ui components
│   ├── hooks/             # Custom React hooks
│   ├── lib/               # Utilidades y configuración
│   ├── store/             # Zustand state management
│   └── types/             # TypeScript type definitions
├── public/                # Assets estáticos
├── prisma/                # Base de datos schema
├── package.json           # Dependencias y scripts
├── README.md              # Documentación completa
└── INSTALL-WINDOWS.md      # Este archivo
```

---

## ✅ Verificación Post-Instalación

Después de instalar, verifica que todo funciona:

```powershell
# 1. Verificar lint (debe mostrar 0 errores)
npm run lint

# 2. Verificar seguridad (debe mostrar 0 vulnerabilidades o solo info)
npm audit

# 3. Iniciar servidor
npm run dev
```

Luego abre http://localhost:3000 y deberías ver:
- ✅ Dashboard con métricas del sistema
- ✅ Centro de Optimización con 5 categorías
- ✅ Score de salud del sistema
- ✅ Panel de navegación lateral

---

## 🎮 Uso Rápido

1. **Dashboard**: Vista general del estado del sistema
2. **Optimizations (🔧)**: Centro de optimización completo
   - **Categoría A**: Sistema Windows (20 items)
   - **Categoría B**: Red/ExitLag (20 items)
   - **Categoría C**: Mouse/Teclado (20 items)
   - **Categoría D**: Visual/Tweaks (20 items)
   - **Categoría E**: Avanzadas ⚠️ (20 items)
3. **Troubleshoot (🔧)**: Herramientas de diagnóstico
4. **Settings (⚙️)**: Configuración y personalización

---

## 💡 Tips para Windows

### Rendimiento Óptimo
- Usa **Windows Terminal** en lugar de CMD tradicional
- Para desarrollo, desactiva Windows Defender Real-time temporalmente en la carpeta del proyecto
- Usa SSD para mejor rendimiento de node_modules

### Atajos Útiles
- `Ctrl+C` en terminal = Detener servidor
- `cls` = Limpiar terminal (equivalente a `clear`)
- `dir` = Listar archivos (equivalente a `ls`)
- `code .` = Abrir carpeta en VS Code

### Variables de Entorno (Opcional)
```powershell
# Agregar al PATH de Windows para acceso global
[System.Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";C:\ruta\a\my-project", "User")
```

---

## 🆘 Soporte

Si tienes problemas:

1. Revisa los logs en `dev.log` (se crea al ejecutar `npm run dev`)
2. Ejecuta `npm run lint` para verificar errores de código
3. Abre una issue en GitHub con:
   - Versión de Node.js (`node --version`)
   - Versión de npm (`npm --version`)
   - Sistema operativo (`ver`)
   - Error completo (captura de pantalla)

---

## 📄 Licencia

MIT License - Libre para uso personal y comercial.

---

**¡Disfruta optimizando tu Windows!** 🚀⚡
