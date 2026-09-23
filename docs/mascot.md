# Mascota CA-O — silueta de gato frame a frame

La mascota del Panel y de Benchmark es la silueta de un gato animada **fotograma a
fotograma** (flipbook). No hay interpolación, morphing ni vectores animados: la app
muestra PNG ya dibujados, en bucle.

## Qué se ve

- 5 moods: `Idle`, `Working`, `Celebrate`, `Warn`, `Sleep` (los mismos de `CatMoodCatalog`).
- 8 fotogramas por mood y ciclo cerrado (el fotograma 7 empalma con el 0).
- 2 conjuntos de arte: `light` (silueta oscura) y `dark` (silueta clara). El control
  elige según el tema real de la ventana (`ActualTheme`).
- Lienzo 256×256 con transparencia, la silueta apoyada en la misma línea de suelo en
  todos los fotogramas (la animación no "tiembla").

## Dónde vive

```
assets/mascot/frames/<tema>/<mood>/f00.png .. f07.png
```

`CA-O.UI.csproj` los copia a `Assets/mascot/frames/...` en la salida, y el control
`CaoCat` los carga con URI `ms-appx:///` primero y ruta de archivo como fallback
(`MascotFlipbook.AppxUri` construye la URI; el catálogo es puro y testeable
headless). `ApplyMood` es idempotente: reaplicar el mismo tema/mood no reinicia el
ciclo. Los fallos de carga van al log `Debug` (`ImageFailed`, conteo de frames
cargados) sin romper el contrato de nunca fallar.

| Mood | Velocidad | Qué hace |
|------|-----------|----------|
| Idle | 130 ms/frame | respira, la cola se mece |
| Working | 90 ms/frame | da golpecitos con la pata, la cola azota |
| Celebrate | 80 ms/frame | salto de alegría |
| Warn | 110 ms/frame | orejas atrás, cola erizada |
| Sleep | 300 ms/frame | dormido, cola enrollada |

`Accessibility.ReducedMotion` (Windows > Accesibilidad > Efectos visuales) deja el
fotograma 0 fijo, sin bucle.

## Cómo se generan

El arte se produce con el motor local de **Open Generative AI**
(`%APPDATA%\open-generative-ai\local-ai`) — `stable-diffusion.cpp` (`sd-cli.exe`) con
**DreamShaper 8** — y luego se limpia con GDI+ para que sea una silueta plana real.

```powershell
# Regenera los crudos con el motor local y reconstruye las hojas finales
pwsh -File scripts/generate-mascot-frames.ps1 -Regenerate

# Solo reconstruye las hojas desde los crudos existentes (sin GPU, determinista)
pwsh -File scripts/generate-mascot-frames.ps1

# Un solo mood
pwsh -File scripts/generate-mascot-frames.ps1 -Regenerate -Mood Celebrate
```

Pipeline:

1. **Silueta base** — text2img con el prompt de la referencia (gato sentado, perfil,
   **una sola cola**), 512², 28 pasos. Es la pose de la que parten los ciclos suaves.
2. **Poses clave por mood** — los movimientos grandes que el img2img no alcanza
   (saltar, aterrizar, dormir enroscado, orejas atrás, arqueo defensivo) se dibujan
   de cero con text2img. Sin esto la animación se queda en "mover la cola".
3. **Intermedios** — cada fotograma que no es pose clave se genera por img2img desde
   la pose clave correspondiente (`--strength 0.25-0.5`). Este es el paso que da la
   sensación de dibujo fotograma a fotograma: la pose avanza poco a poco y el estilo
   se mantiene.
4. **Limpieza (GDI+)** — umbral → se conserva la mayor región conexa (elimina motas y
   fondos sueltos) → relleno de huecos (ojos, bigotes, oreja) → alineación por línea de
   suelo y centroide de la base → color del tema con alfa suavizado → PNG 256². La
   silueta se escala a 200 px de alto con el suelo en y=246 para que llene el lienzo
   sin recortarse.
5. **Espejado** — la silueta se espeja para mirar a la derecha, como la referencia.

Plan de fotogramas (8 por mood, el último empalma con el primero):

| Mood | Fotogramas | Poses clave |
|------|-----------|-------------|
| Idle | los 8 son intermedios de la base | — (solo la cola se mece) |
| Working | 0, 5 clave + 6 intermedios | `pawup`, `pawup2` |
| Celebrate | 0, 2, 6 clave + 5 intermedios | `crouch`, `jump`, `land` |
| Warn | 0, 2, 4 clave + 5 intermedios | `alert`, `flat`, `arch` |
| Sleep | 0 clave + 7 intermedios | `loaf` |

Notas:

- La imagen inicial de cada cadena **debe tener fondo blanco** (los crudos). Un PNG con
  transparencia se aplana a negro dentro del motor y rompe la detección de silueta; el
  post-procesado lo avisa con un warning en vez de escupir un frame negro.
- Si la VRAM está ocupada por otras apps, `sd-cli` falla con *"cannot make enough memory
  available"*; el generador reintenta automáticamente con `--offload-to-cpu --vae-tiling`
  y el flujo es reanudable (los fotogramas ya hechos no se repiten).
- El prompt lleva negativos fuertes contra el fondo: el modelo tiende a dibujar un suelo
  o un pedestal, que acabaría dentro de la silueta.
- Los crudos viven en `artifacts/mascot-preview/gen` (ignorado por git): el repo solo
  versiona las hojas finales.

## Pruebas

`tests/CA-O.UI.Tests/MascotFlipbookTests.cs` verifica el catálogo puro y que **todos los
assets existen**, son PNG 256×256 y de tamaño razonable, en los dos temas y los 5 moods
(80 ficheros). Así un mood sin arte rompe el build de tests, no la app.