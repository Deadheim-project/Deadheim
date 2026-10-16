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


# ------------------------------------------------------------------ Dead Token
def dead():
    """O token unico: valknut de ouro (o no dos mortos de Odin) num campo escuro com brilho de alma.

    Tres triangulos entrelacados como os aneis de Borromeo: 1 passa por cima do 0, o 2 por cima
    do 1 e o 0 por cima do 2. Cada triangulo e desenhado na propria camada e o 0 volta por cima
    so onde cruza com o 2.
    """
    img = coin((34, 74, 84, 255), (5, 10, 16, 255))
    R, off, width = 106, 46, 22

    def raw(k):
        # Centros num triangulo de ponta para baixo: com eles de ponta para cima, os lados de dois
        # triangulos caiam na mesma reta e o no sumia.
        a = math.radians(90 + 120 * k)
        cx, cy = math.cos(a) * off, math.sin(a) * off
        return [(cx + math.cos(math.radians(t)) * R, cy + math.sin(math.radians(t)) * R) for t in (-90, 30, 150)]

    # Centraliza a figura pela caixa que ela ocupa.
    pts = [p for k in range(3) for p in raw(k)]
    mx = (min(p[0] for p in pts) + max(p[0] for p in pts)) / 2
    my = (min(p[1] for p in pts) + max(p[1] for p in pts)) / 2

    def triangle(k):
        return [(C + x - mx, C + y - my) for x, y in raw(k)]

    def stroke(points):
        layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        closed = points + [points[0], points[1]]
        d.line(closed, fill=(62, 34, 6, 255), width=width + 12, joint='curve')
        d.line(closed, fill=(240, 188, 72, 255), width=width, joint='curve')
        d.line(closed, fill=(255, 238, 176, 255), width=width // 3, joint='curve')
        return layer

    layers = [stroke(triangle(k)) for k in range(3)]
    knot = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    for layer in layers:
        knot = Image.alpha_composite(knot, layer)
    over = ImageChops.multiply(layers[0].split()[3], layers[2].split()[3])
    top = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    top.paste(layers[0], (0, 0), over)
    knot = Image.alpha_composite(knot, top)

    knot = glow(knot, (90, 230, 255), blur=16, strength=2)
    img = Image.alpha_composite(img, knot)
    return finish(img, 'deadtoken.png')


# ----------------------------------------------------------------- Ficha Fundador
def fundador():
    """Estrela de 5 pontas: quem chegou na abertura (titulo Fundador)."""
    img = coin((255, 221, 140, 255), (120, 78, 10, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    pts = []
    for i in range(10):
        r = 128 if i % 2 == 0 else 54
        a = math.radians(i * 36 - 90)
        pts.append((C + math.cos(a) * r, C + math.sin(a) * r))
    d.polygon(pts, fill=(255, 238, 176, 255), outline=(90, 52, 10, 255))
    d.line(pts + [pts[0]], fill=(90, 52, 10, 255), width=5)
    em = glow(em, (255, 210, 110), blur=14, strength=2)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichafundador.png')


# ----------------------------------------------------------------- Ficha Barbeiro
def barbeiro():
    """Tesoura aberta: troca de cabelo/barba no Barbeiro."""
    img = coin((60, 90, 150, 255), (14, 22, 42, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    silver = (225, 232, 240, 255)
    dark = (40, 46, 56, 255)
    # Duas laminas em X, cada uma um triangulo fino saindo do centro.
    for sign in (-1, 1):
        a = math.radians(35 * sign)
        tip = (C + math.sin(a) * 150, C - 150)
        base1 = (C - 14 * math.cos(a), C - 14 * math.sin(a) + 8)
        base2 = (C + 14 * math.cos(a), C + 14 * math.sin(a) + 8)
        d.polygon([tip, base1, base2], fill=silver, outline=dark)
    # Aneis do cabo, embaixo.
    for sign in (-1, 1):
        d.ellipse((C + sign * 50 - 34, C + 58, C + sign * 50 + 34, C + 126), outline=dark, width=14)
    d.ellipse((C - 16, C + 2, C + 16, C + 34), fill=dark)
    em = glow(em, (150, 190, 255), blur=10, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichabarbeiro.png')


# --------------------------------------------------------------- Ficha Halloween
def halloween():
    """Abobora com brasa verde: pacote de Halloween."""
    img = coin((255, 150, 40, 255), (60, 20, 4, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    orange = (255, 140, 30, 255)
    dark = (60, 18, 4, 255)
    for dx in (-70, 0, 70):
        d.ellipse((C + dx - 70, C - 110, C + dx + 70, C + 130), fill=orange, outline=dark, width=6)
    d.rectangle((C - 16, C - 170, C + 16, C - 100), fill=(80, 130, 50, 255), outline=dark)
    glowcol = (140, 255, 90, 255)
    d.polygon([(C - 60, C - 50), (C - 20, C - 50), (C - 40, C), ], fill=glowcol)
    d.polygon([(C + 60, C - 50), (C + 20, C - 50), (C + 40, C), ], fill=glowcol)
    d.polygon([(C - 55, C + 55), (C + 55, C + 55), (C + 30, C + 95), (C, C + 70), (C - 30, C + 95)], fill=glowcol)
    em = glow(em, (140, 255, 90), blur=14, strength=2)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichahalloween.png')


# -------------------------------------------------------------- Ficha Temporada
def temporada():
    """Ampulheta: passe de temporada cosmetico."""
    img = coin((70, 150, 170, 255), (10, 30, 42, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    gold = (255, 214, 120, 255)
    edge = (90, 54, 10, 255)
    glass = (220, 245, 250, 180)
    d.rounded_rectangle((C - 90, C - 140, C + 90, C - 118), radius=8, fill=gold, outline=edge, width=5)
    d.rounded_rectangle((C - 90, C + 118, C + 90, C + 140), radius=8, fill=gold, outline=edge, width=5)
    top = [(C - 80, C - 118), (C + 80, C - 118), (C + 14, C - 6), (C - 14, C - 6)]
    bottom = [(C - 14, C + 6), (C + 14, C + 6), (C + 80, C + 118), (C - 80, C + 118)]
    d.polygon(top, fill=glass, outline=edge)
    d.polygon(bottom, fill=glass, outline=edge)
    d.polygon([(C - 60, C - 100), (C + 60, C - 100), (C + 10, C - 24), (C - 10, C - 24)], fill=(200, 140, 70, 255))
    d.polygon([(C - 10, C + 18), (C + 10, C + 18), (C + 36, C + 92), (C - 36, C + 92)], fill=(200, 140, 70, 255))
    em = glow(em, (120, 220, 240), blur=12, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichatemporada.png')


# ---------------------------------------------------------------- Ficha Batizar
def batizar():
    """Marcador de mapa (pin): batizar um lugar."""
    img = coin((70, 150, 90, 255), (10, 36, 16, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    gold = (255, 214, 120, 255)
    edge = (90, 54, 10, 255)
    cx, top, r = C, C - 150, 95
    d.ellipse((cx - r, top, cx + r, top + 2 * r), fill=gold, outline=edge, width=8)
    d.polygon([(cx - r * 0.62, top + 1.55 * r), (cx + r * 0.62, top + 1.55 * r), (cx, C + 150)], fill=gold, outline=edge)
    d.ellipse((cx - 36, top + r - 36, cx + 36, top + r + 36), fill=(10, 36, 16, 255))
    em = glow(em, (255, 210, 110), blur=12, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichabatizar.png')


# ----------------------------------------------------------------- Ficha Brasao
def brasao():
    """Escudo: vela/brasao de guilda sob encomenda."""
    img = coin((150, 30, 36, 255), (34, 6, 8, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    gold = (255, 214, 120, 255)
    edge = (70, 14, 10, 255)
    shield = [(C - 100, C - 130), (C + 100, C - 130), (C + 100, C + 30), (C, C + 150), (C - 100, C + 30)]
    d.polygon(shield, fill=gold, outline=edge, width=0)
    d.line(shield + [shield[0]], fill=edge, width=8)
    inner = (34, 6, 8, 255)
    d.rectangle((C - 16, C - 95, C + 16, C + 70), fill=inner)
    d.rectangle((C - 65, C - 20, C + 65, C + 12), fill=inner)
    em = glow(em, (255, 190, 90), blur=10, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichabrasao.png')


# ------------------------------------------------------------------- Ficha Pet
def pet():
    """Pata de animal: pet cosmético."""
    img = coin((180, 130, 90, 255), (60, 40, 10, 255))
    em = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(em)
    brown = (180, 140, 100, 255)
    dark = (60, 40, 10, 255)
    # Pata: dedo grande no centro, dois pequenos em cima
    # Dedo grande
    d.ellipse((C - 45, C - 40, C + 45, C + 70), fill=brown, outline=dark, width=6)
    # Dedos pequenos
    d.ellipse((C - 95, C - 100, C - 45, C - 40), fill=brown, outline=dark, width=6)
    d.ellipse((C + 45, C - 100, C + 95, C - 40), fill=brown, outline=dark, width=6)
    # Almofadas
    d.ellipse((C - 20, C + 30, C + 20, C + 70), fill=dark)
    d.ellipse((C - 70, C - 60, C - 30, C - 20), fill=dark)
    d.ellipse((C + 30, C - 60, C + 70, C - 20), fill=dark)
    em = glow(em, (255, 210, 140), blur=12, strength=1)
    img = Image.alpha_composite(img, em)
    return finish(img, 'fichapet.png')


TOKENS = {
    'portal': portal, 'spawner': spawner, 'territory': territory, 'dead': dead,
    'fundador': fundador, 'barbeiro': barbeiro, 'halloween': halloween,
    'temporada': temporada, 'batizar': batizar, 'brasao': brasao, 'pet': pet,
}

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    # Sem nomes depois da pasta, desenha todos: python gerar-tokens.py assets dead
    names = sys.argv[2:] or list(TOKENS)
    tiles = [TOKENS[n]() for n in names]
    # Folha de previa: cada um em 128 e em 64 (tamanho do inventario), em fundo escuro.
    n = len(tiles)
    sheet = Image.new('RGBA', (n * 140 + n * 72 + 20, 150), (38, 34, 30, 255))
    for i, t in enumerate(tiles):
        sheet.alpha_composite(t, (10 + i * 140, 11))
        sheet.alpha_composite(t.resize((64, 64), Image.LANCZOS), (10 + n * 140 + i * 72, 43))
    sheet.save(os.path.join(OUT, 'previa.png'))
    print('previa ok')
