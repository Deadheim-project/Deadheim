"""Icones dos tokens do Deadheim: moeda com aro de ouro, campo colorido e um emblema.

Desenha em 512 px e reduz para 128 px (LANCZOS) para ter borda suave.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 512
C = S / 2
OUT = sys.argv[1] if len(sys.argv) > 1 else '.'


def radial(size, inner, outer, center=None, radius=None, power=1.0):
    """Gradiente radial RGBA de inner (centro) para outer (borda)."""
    img = Image.new('RGBA', (size, size))
    px = img.load()
    cx, cy = center or (size / 2, size / 2)
    r = radius or size / 2
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - cx, y - cy) / r) ** power
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(4))
    return img


def circle_mask(r, cx=C, cy=C, size=S):
    m = Image.new('L', (size, size), 0)
    ImageDraw.Draw(m).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return m


def coin(field_in, field_out):
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))

    # Sombra leve embaixo da moeda.
    shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((22, 30, S - 18, S - 10), fill=(0, 0, 0, 150))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(10)))

    # Aro de ouro: gradiente deslocado para cima-esquerda (luz vindo de la).
    rim = radial(S, (255, 226, 140, 255), (120, 72, 18, 255), center=(C - 70, C - 80), radius=S * 0.72, power=1.2)
    img.paste(rim, (0, 0), circle_mask(240))
    # Contorno escuro do aro.
    d = ImageDraw.Draw(img)
    d.ellipse((C - 240, C - 240, C + 240, C + 240), outline=(60, 34, 8, 255), width=8)
    # Chanfro interno: anel escuro e anel claro.
    d.ellipse((C - 200, C - 200, C + 200, C + 200), outline=(92, 54, 12, 255), width=10)
    d.ellipse((C - 190, C - 190, C + 190, C + 190), outline=(255, 222, 130, 255), width=4)

    # Campo colorido.
    field = radial(S, field_in, field_out, center=(C - 30, C - 40), radius=200, power=1.1)
    img.paste(field, (0, 0), circle_mask(186))

    # Pontinhos no aro (rebites), oito em volta.
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        x, y = C + math.cos(a) * 220, C + math.sin(a) * 220
        d.ellipse((x - 9, y - 9, x + 9, y + 9), fill=(255, 236, 170, 255), outline=(90, 52, 10, 255), width=3)
    return img


def glow(layer, color, blur=14, strength=2):
    """Halo da cor dada atras do emblema."""
    alpha = layer.split()[3]
    halo = Image.new('RGBA', layer.size, color + (0,))
    halo.putalpha(alpha.filter(ImageFilter.GaussianBlur(blur)))
    out = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    for _ in range(strength):
        out = Image.alpha_composite(out, halo)
    return Image.alpha_composite(out, layer)


def shine(img):
    """Brilho em arco no alto-esquerda da moeda."""
    hl = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(hl).arc((C - 222, C - 222, C + 222, C + 222), start=200, end=255, fill=(255, 255, 230, 200), width=10)
    return Image.alpha_composite(img, hl.filter(ImageFilter.GaussianBlur(3)))


def finish(img, name):
    img = shine(img)
    small = img.resize((128, 128), Image.LANCZOS)
    path = os.path.join(OUT, name)
    small.save(path)
    print('gravado', path)
    return small


# ---------------------------------------------------------------- Portal Token
def portal():
    img = coin((70, 40, 140, 255), (18, 8, 48, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    # Redemoinho: tres bracos em espiral, brancos no centro, ciano e depois violeta para fora.
    arms, steps = 3, 140
    for k in range(arms):
        prev = None
        for i in range(steps + 1):
            t = i / steps
            r = 10 + t * 158
            a = t * 2.3 * math.pi + k * 2 * math.pi / arms
            p = (C + math.cos(a) * r, C + math.sin(a) * r)
            if prev:
                col = (int(235 - 95 * t), int(250 - 150 * t), 255, int(255 - 70 * t))
                d.line((prev, p), fill=col, width=int(26 - 18 * t))
            prev = p
    d.ellipse((C - 26, C - 26, C + 26, C + 26), fill=(245, 252, 255, 255))
    em = glow(em, (120, 200, 255), blur=12, strength=2)
    img = Image.alpha_composite(img, em)
    return finish(img, 'portaltoken.png')


# --------------------------------------------------------------- Spawner Token
def spawner():
    img = coin((60, 110, 50, 255), (12, 32, 12, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    bone = (236, 228, 200, 255)
    dark = (20, 26, 16, 255)
    # Cranio.
    d.ellipse((C - 105, C - 130, C + 105, C + 70), fill=bone)
    # Mandibula.
    d.rounded_rectangle((C - 62, C + 30, C + 62, C + 118), radius=22, fill=bone)
    # Olhos e nariz.
    d.ellipse((C - 78, C - 50, C - 18, C + 12), fill=dark)
    d.ellipse((C + 18, C - 50, C + 78, C + 12), fill=dark)
    d.polygon([(C, C + 22), (C - 16, C + 52), (C + 16, C + 52)], fill=dark)
    # Dentes.
    for i in range(-2, 3):
        x = C + i * 22
        d.line((x, C + 74, x, C + 112), fill=dark, width=6)
    d.line((C - 60, C + 74, C + 60, C + 74), fill=dark, width=6)
    # Brasa verde nos olhos.
    d.ellipse((C - 58, C - 30, C - 38, C - 10), fill=(140, 255, 120, 255))
    d.ellipse((C + 38, C - 30, C + 58, C - 10), fill=(140, 255, 120, 255))
    em = glow(em, (110, 255, 90), blur=10, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'spawnertoken.png')


# ------------------------------------------------------------- Territory Token
def territory():
    img = coin((165, 30, 30, 255), (48, 6, 8, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    gold = (255, 210, 90, 255)
    edge = (90, 50, 8, 255)
    # Torre com ameias.
    tower = [(C - 70, C + 120), (C - 70, C - 40), (C - 70, C - 70), (C - 44, C - 70), (C - 44, C - 46),
             (C - 13, C - 46), (C - 13, C - 70), (C + 13, C - 70), (C + 13, C - 46), (C + 44, C - 46),
             (C + 44, C - 70), (C + 70, C - 70), (C + 70, C + 120)]
    d.polygon(tower, fill=gold, outline=edge)
    d.line(tower + [tower[0]], fill=edge, width=5)
    # Porta em arco.
    d.rectangle((C - 24, C + 60, C + 24, C + 120), fill=(40, 6, 6, 255))
    d.ellipse((C - 24, C + 36, C + 24, C + 84), fill=(40, 6, 6, 255))
    # Janela.
    d.rectangle((C - 9, C - 18, C + 9, C + 18), fill=(40, 6, 6, 255))
    # Base (chao).
    d.rounded_rectangle((C - 105, C + 112, C + 105, C + 134), radius=8, fill=gold, outline=edge, width=4)
    # Mastro e bandeira.
    d.line((C, C - 70, C, C - 150), fill=edge, width=8)
    d.polygon([(C + 4, C - 150), (C + 82, C - 132), (C + 4, C - 110)], fill=(250, 245, 230, 255), outline=edge)
    em = glow(em, (255, 200, 80), blur=10, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'territorytoken.png')


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    tiles = [portal(), spawner(), territory()]
    # Folha de previa: os tres em 128 e em 64 (tamanho do inventario), em fundo escuro.
    sheet = Image.new('RGBA', (3 * 140 + 3 * 72 + 20, 150), (38, 34, 30, 255))
    for i, t in enumerate(tiles):
        sheet.alpha_composite(t, (10 + i * 140, 11))
        sheet.alpha_composite(t.resize((64, 64), Image.LANCZOS), (10 + 3 * 140 + i * 72, 43))
    sheet.save(os.path.join(OUT, 'previa.png'))
    print('previa ok')
