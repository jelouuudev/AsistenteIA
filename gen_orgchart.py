from PIL import Image, ImageDraw, ImageFont

W, H = 1200, 820
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)

reg = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
bold = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 15)
banner = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 24)

def round_rect(x, y, w, h, r, fill, outline=None, ow=0):
    d.rounded_rectangle([x, y, x+w, y+h], radius=r, fill=fill, outline=outline, width=ow)

def centered(text, x, y, w, font, fill):
    tw = d.textlength(text, font=font)
    d.text((x + (w - tw)/2, y), text, font=font, fill=fill)

# Banner
round_rect(330, 20, 540, 56, 8, "#1f3a5f")
centered("DIRECCIÓN GENERAL  ·  Gerencia General", 330, 38, 540, banner, "white")
d.line([(600, 76), (600, 120)], fill="#888", width=2)

areas = [
    ("Área de Desarrollo y Tecnología", "#2f6fb0", [
        "Desarrollo de sistemas empresariales",
        "Diseño e implementación de soluciones tecnológicas",
        "Gestión de bases de datos",
        "Integración de sistemas",
        "Investigación e innovación tecnológica",
    ]),
    ("Área de Automatización y\nSoluciones Empresariales", "#2e8b57", [
        "Automatización de procesos empresariales",
        "Implementación de sistemas de gestión",
        "Optimización de procesos",
        "Integración de herramientas tecnológicas",
    ]),
    ("Área de Soporte y Servicios TI", "#7d5ba6", [
        "Soporte técnico",
        "Mantenimiento de sistemas",
        "Atención de incidencias",
        "Monitoreo y mantenimiento de aplicaciones",
    ]),
    ("Área Administrativa y Financiera", "#e08a2b", [
        "Contabilidad y finanzas",
        "Recursos humanos",
        "Gestión administrativa",
        "Compras y logística",
    ]),
    ("Área Comercial", "#2a9d8f", [
        "Ventas de soluciones tecnológicas",
        "Gestión de clientes",
    ]),
]

x0, y0 = 40, 130
cw, ch = 360, 300
gx, gy = 30, 40
n_cols = 3

for i, (title, color, items) in enumerate(areas):
    r, c = divmod(i, n_cols)
    x = x0 + c * (cw + gx)
    y = y0 + r * (ch + gy)
    round_rect(x, y, cw, 52, 8, color)
    # título (puede ser 2 líneas)
    for k, ln in enumerate(title.split("\n")):
        centered(ln, x, y + 10 + k*18, cw, bold, "white")
    iy = y + 66
    for it in items:
        round_rect(x, iy, cw, 34, 6, "#f4f6f8", outline=color, ow=1)
        d.text((x + 14, iy + 9), "•", font=bold, fill=color)
        d.text((x + 32, iy + 9), it, font=reg, fill="#333")
        iy += 40

d.text((W/2 - d.textlength("Figura 1. Organigrama de AUTOMATION DEVELOPER SYSTEM SAC", font=reg)/2, H-30),
       "Figura 1. Organigrama de AUTOMATION DEVELOPER SYSTEM SAC", font=reg, fill="#666")

img.save("Figura1_Organigrama.png")
print("PNG generado", img.size)
