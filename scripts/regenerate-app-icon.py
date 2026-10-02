"""Regenera el icono de CA-O con alto contraste (determinista, PIL).

Problema: el icono anterior era full-bleed casi negro (#0B0F14) con texto
fino blanco: invisible sobre la taskbar oscura e ilegible a 16-32px.
Fix: badge solido cian de marca #00C9B6 (CaoOpsCyanBrush) + texto "CA-O"
oscuro #062A24 (tinta del boton ops primario) en bold grueso, full-bleed
para que el solido teal recorte sobre taskbar clara Y oscura.

Genera:
  assets/app-icon-minimal.png (1024x1024 RGBA, badge redondeado)
  assets/app-icon-minimal.ico (16/24/32/48/64/128/256, PNG-comprimido,
    variante cuadrada 100% opaca para taskbar/bandeja: sin alfa en esquinas)
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw, ImageFont

SIZE = 1024
BADGE = (0x00, 0xC9, 0xB6, 0xFF)   # #00C9B6 cian de marca
INK = (0x06, 0x2A, 0x24, 0xFF)     # #062A24 tinta oscura de marca
RADIUS_RATIO = 0.22
EDGE_WIDTH = 12
TEXT = "CA-O"
TEXT_MAX_WIDTH_RATIO = 0.88

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PNG_PATH = os.path.join(REPO, "assets", "app-icon-minimal.png")
ICO_PATH = os.path.join(REPO, "assets", "app-icon-minimal.ico")
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]

FONT_CANDIDATES = [
    r"C:\Windows\Fonts\segoeuib.ttf",
    r"C:\Windows\Fonts\arialbd.ttf",
]


def load_bold_font(pixel_size: int) -> ImageFont.FreeTypeFont:
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            return ImageFont.truetype(path, pixel_size)
    raise FileNotFoundError("sin fuente bold: " + ", ".join(FONT_CANDIDATES))


def fit_font(draw: ImageDraw.ImageDraw, text: str, max_width: int) -> ImageFont.FreeTypeFont:
    size = 420
    while size > 40:
        font = load_bold_font(size)
        bbox = draw.textbbox((0, 0), text, font=font)
        if bbox[2] - bbox[0] <= max_width:
            return font
        size -= 8
    return load_bold_font(40)


def build_master() -> Image.Image:
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    radius = int(SIZE * RADIUS_RATIO)
    # Badge full-bleed: recorta en teal solido sobre taskbar clara y oscura.
    draw.rounded_rectangle([0, 0, SIZE - 1, SIZE - 1], radius=radius, fill=BADGE)
    # Borde oscuro fino: define el borde sobre fondos claros.
    draw.rounded_rectangle(
        [EDGE_WIDTH // 2, EDGE_WIDTH // 2, SIZE - 1 - EDGE_WIDTH // 2, SIZE - 1 - EDGE_WIDTH // 2],
        radius=radius,
        outline=INK,
        width=EDGE_WIDTH,
    )
    font = fit_font(draw, TEXT, int(SIZE * TEXT_MAX_WIDTH_RATIO))
    stroke = max(2, font.size // 110)
    draw.text((SIZE / 2, SIZE / 2 + SIZE * 0.01), TEXT, font=font, fill=INK, anchor="mm",
              stroke_width=stroke, stroke_fill=INK)
    return img


def build_taskbar_master(rounded: Image.Image) -> Image.Image:
    """Variante full-bleed 100% opaca para el ICO (taskbar/bandeja/ventana).

    El master redondeado deja alfa 0 en las esquinas (radio 0.22 sobre
    fondo transparente) y a 16-32px Windows compone el fondo de la
    taskbar a traves: el slot se lee vacio/transparente. Al componer el
    badge sobre fondo solido del mismo teal, las esquinas quedan opacas
    con el glifo identico. El PNG redondeado se mantiene para el
    TitleBar IconSource dentro de la ventana.
    """
    solid = Image.new("RGBA", (SIZE, SIZE), BADGE)
    solid.alpha_composite(rounded)
    return solid


def luminance(rgb: tuple[int, int, int]) -> float:
    def ch(c: int) -> float:
        c /= 255.0
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

    r, g, b = (ch(v) for v in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def verify(master: Image.Image, taskbar: Image.Image) -> None:
    assert master.size == (SIZE, SIZE), master.size
    assert taskbar.size == (SIZE, SIZE), taskbar.size
    # Contraste tinta vs badge (WCAG): debe ser alto.
    lb, li = luminance(BADGE[:3]), luminance(INK[:3])
    ratio = (max(lb, li) + 0.05) / (min(lb, li) + 0.05)
    print(f"contraste tinta/badge: {ratio:.2f}:1")
    assert ratio >= 4.5, f"contraste bajo: {ratio:.2f}"
    # Legibilidad a 16px: el glifo debe seguir presente (fraccion oscura
    # significativa, no ni todo-fondo ni todo-tinta).
    tiny = master.resize((16, 16), Image.LANCZOS).convert("RGB")
    raw = tiny.tobytes()
    dark = sum(1 for i in range(0, len(raw), 3) if luminance((raw[i], raw[i + 1], raw[i + 2])) < 0.25)
    frac = dark / 256.0
    print(f"pixeles oscuros a 16px: {dark}/256 ({frac:.1%})")
    assert 0.10 <= frac <= 0.70, f"glifo ilegible a 16px: {frac:.1%}"
    # Badge visible sobre taskbar oscura (#1F1F1F) y clara (#F3F3F3):
    # distancia de luminancia del teal a ambos extremos.
    for name, bg in (("oscura", (0x1F, 0x1F, 0x1F)), ("clara", (0xF3, 0xF3, 0xF3))):
        dl = abs(luminance(BADGE[:3]) - luminance(bg))
        print(f"distancia luminancia vs taskbar {name}: {dl:.3f}")
        assert dl >= 0.15, f"badge indistinguible sobre taskbar {name}"
    # Variante taskbar: cero pixeles semitransparentes (slot opaco).
    alpha = taskbar.getchannel("A")
    transparent = sum(1 for v in alpha.getdata() if v < 255)
    print(f"pixeles no-opacos en variante taskbar: {transparent}/{SIZE * SIZE}")
    assert transparent == 0, f"variante taskbar con alfa: {transparent} px"


def main() -> int:
    master = build_master()
    taskbar = build_taskbar_master(master)
    verify(master, taskbar)
    master.save(PNG_PATH, "PNG")
    taskbar.save(ICO_PATH, "ICO", sizes=[(s[0], s[1]) for s in ICO_SIZES])
    with Image.open(ICO_PATH) as ico:
        ico_sizes = sorted(ico.info.get("sizes", []))
    print(f"PNG: {PNG_PATH} {os.path.getsize(PNG_PATH)} B")
    print(f"ICO: {ICO_PATH} {os.path.getsize(ICO_PATH)} B sizes={ico_sizes}")
    assert set(ico_sizes) == set(ICO_SIZES), f"ICO incompleto: {ico_sizes}"
    print("OK icono regenerado y verificado")
    return 0


if __name__ == "__main__":
    sys.exit(main())
