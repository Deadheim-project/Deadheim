"""Gera os seis ícones dos itens de pet em estilo de medalhão Valheim."""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter

SIZE = 512
C = SIZE / 2
OUT = os.path.dirname(__file__)


def radial(inner, outer):
    image = Image.new("RGBA", (SIZE, SIZE))
    px = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            t = min(1.0, math.hypot(x - (C - 80), y - (C - 100)) / 520)
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(inner, outer))
    return image


def medal(field):
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((34, 44, 482, 496), fill=(0, 0, 0, 180))
    image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(13)))
    d = ImageDraw.Draw(image)
    d.ellipse((24, 18, 488, 482), fill=(50, 38, 27, 255), outline=(20, 20, 18, 255), width=14)
    d.ellipse((38, 31, 474, 467), fill=(151, 112, 57, 255), outline=(198, 157, 87, 255), width=7)
    d.ellipse((52, 45, 460, 453), fill=(37, 34, 28, 255), outline=(80, 62, 39, 255), width=11)
    d.ellipse((66, 59, 446, 439), outline=(177, 139, 76, 255), width=5)
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).ellipse((77, 70, 435, 428), fill=255)
    texture = radial(field[0], field[1])
    image.paste(texture, (0, 0), mask)
    d = ImageDraw.Draw(image)
    # Speckled stone/paint grain, subdued so it survives the 128 px inventory icon.
    grain = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grain)
    rng = random.Random(sum(field[0]))
    for _ in range(2600):
        x, y = rng.randrange(82, 430), rng.randrange(75, 423)
        if mask.getpixel((x, y)) < 180:
            continue
        tone = rng.choice(((207, 183, 135, 22), (12, 16, 15, 30), (157, 141, 110, 18)))
        gd.point((x, y), fill=tone)
    image.alpha_composite(grain)
    d = ImageDraw.Draw(image)
    d.ellipse((79, 72, 433, 426), outline=(25, 26, 23, 255), width=10)
    for angle in range(8):
        a = math.radians(angle * 45 + 22.5)
        x, y = C + math.cos(a) * 218, C + math.sin(a) * 218
        d.ellipse((x - 8, y - 8, x + 8, y + 8), fill=(183, 143, 77, 255), outline=(45, 34, 23, 255), width=4)
    # Small engraved rune-like marks along the inner rim.
    for angle in range(16):
        a = math.radians(angle * 22.5)
        x, y = C + math.cos(a) * 202, C + math.sin(a) * 202
        tangent = a + math.pi / 2
        dx, dy = math.cos(tangent) * 8, math.sin(tangent) * 8
        ink = (104, 79, 47, 255)
        d.line((x - dx, y - dy, x + dx, y + dy), fill=ink, width=4)
        d.line((x, y, x + math.cos(a) * 9 + dx, y + math.sin(a) * 9 + dy), fill=ink, width=3)
    return image


def poly(d, points, fill, outline=(48, 35, 24, 255), width=9):
    d.polygon(points, fill=fill)
    d.line(points + [points[0]], fill=outline, width=width, joint="curve")


def make_pet(name, field, draw_pet):
    image = medal(field)
    creature = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(creature)
    draw_pet(d)
    # Soft top-left light, cool shadow and fine painted grain break up the flat vector fills.
    rng = random.Random(name)
    px = creature.load()
    for y in range(SIZE):
        for x in range(SIZE):
            r, g, b, a = px[x, y]
            if not a:
                continue
            nx, ny = x / SIZE, y / SIZE
            shade = 1.2 - 0.47 * (0.58 * nx + 0.42 * ny)
            grain = rng.randrange(-9, 10)
            px[x, y] = (
                max(0, min(255, int(r * shade + grain + 4))),
                max(0, min(255, int(g * shade + grain + 2))),
                max(0, min(255, int(b * shade + grain - 2))),
                a,
            )
    image.alpha_composite(creature)
    d = ImageDraw.Draw(image)
    image = image.resize((128, 128), Image.Resampling.LANCZOS)
    image.save(os.path.join(OUT, f"pet-{name}.png"), optimize=True)


def chicken(d):
    d.ellipse((145, 257, 344, 370), fill=(184, 171, 139, 255), outline=(42, 36, 28, 255), width=12)
    d.ellipse((263, 189, 359, 291), fill=(194, 180, 145, 255), outline=(42, 36, 28, 255), width=11)
    poly(d, [(159, 279), (111, 243), (171, 250)], (151, 136, 108, 255), width=8)
    poly(d, [(181, 285), (135, 325), (190, 315)], (151, 136, 108, 255), width=8)
    poly(d, [(311, 205), (326, 167), (339, 191), (357, 167), (363, 206)], (131, 51, 35, 255), width=7)
    poly(d, [(350, 225), (402, 246), (353, 263)], (177, 105, 43, 255), width=7)
    d.ellipse((323, 217, 339, 233), fill=(29, 26, 22, 255))
    d.line((204, 354, 196, 390), fill=(121, 82, 38, 255), width=10)
    d.line((296, 356, 300, 390), fill=(121, 82, 38, 255), width=10)


def chick(d):
    d.ellipse((159, 258, 345, 374), fill=(190, 143, 48, 255), outline=(48, 37, 23, 255), width=11)
    d.ellipse((265, 197, 353, 294), fill=(205, 160, 57, 255), outline=(48, 37, 23, 255), width=10)
    poly(d, [(295, 207), (309, 173), (321, 197), (338, 178), (350, 215)], (139, 50, 30, 255), width=7)
    poly(d, [(344, 232), (401, 251), (349, 266)], (180, 97, 34, 255), width=7)
    d.ellipse((318, 222, 333, 237), fill=(30, 25, 19, 255))
    poly(d, [(177, 288), (137, 258), (178, 268)], (160, 119, 40, 255), width=7)
    d.line((221, 360, 213, 388), fill=(126, 83, 32, 255), width=9)
    d.line((298, 359, 302, 388), fill=(126, 83, 32, 255), width=9)


def piglet(d):
    d.ellipse((141, 235, 351, 356), fill=(147, 91, 82, 255), outline=(46, 33, 28, 255), width=11)
    d.ellipse((290, 214, 365, 315), fill=(163, 103, 91, 255), outline=(46, 33, 28, 255), width=10)
    poly(d, [(308, 225), (303, 171), (344, 207)], (141, 73, 68, 255), width=8)
    d.ellipse((340, 250, 407, 293), fill=(177, 116, 100, 255), outline=(50, 34, 29, 255), width=8)
    d.ellipse((362, 259, 377, 270), fill=(51, 33, 28, 255))
    d.ellipse((385, 260, 399, 270), fill=(51, 33, 28, 255))
    d.ellipse((316, 232, 330, 246), fill=(29, 25, 22, 255))
    for x in (184, 274, 324):
        d.line((x, 329, x - 7, 378), fill=(80, 52, 40, 255), width=13)


def wolf(d):
    d.ellipse((132, 249, 355, 364), fill=(91, 97, 92, 255), outline=(31, 33, 30, 255), width=12)
    poly(d, [(158, 279), (105, 221), (160, 243)], (80, 86, 82, 255), width=9)
    poly(d, [(171, 288), (120, 333), (178, 319)], (80, 86, 82, 255), width=9)
    d.ellipse((289, 204, 369, 297), fill=(108, 112, 105, 255), outline=(31, 33, 30, 255), width=10)
    poly(d, [(299, 220), (294, 163), (332, 207)], (85, 91, 85, 255), width=8)
    poly(d, [(345, 243), (414, 261), (350, 280), (327, 268)], (133, 131, 117, 255), width=8)
    d.ellipse((336, 224, 350, 238), fill=(27, 26, 22, 255))
    d.ellipse((399, 254, 412, 266), fill=(29, 25, 22, 255))
    for x in (190, 285, 337):
        d.line((x, 333, x - 5, 383), fill=(54, 57, 52, 255), width=12)
    d.line((161, 263, 224, 283), fill=(147, 143, 126, 255), width=8)


def lox(d):
    d.ellipse((129, 235, 366, 370), fill=(125, 83, 52, 255), outline=(40, 30, 23, 255), width=12)
    d.ellipse((288, 199, 383, 306), fill=(145, 99, 60, 255), outline=(40, 30, 23, 255), width=10)
    d.arc((194, 105, 302, 251), 174, 300, fill=(196, 162, 101, 255), width=22)
    d.arc((284, 104, 411, 250), 234, 360, fill=(196, 162, 101, 255), width=22)
    poly(d, [(346, 264), (408, 279), (352, 301), (328, 285)], (166, 126, 84, 255), width=8)
    d.ellipse((331, 224, 346, 239), fill=(28, 24, 20, 255))
    d.ellipse((393, 274, 405, 285), fill=(33, 26, 20, 255))
    for x in (175, 274, 337):
        d.line((x, 337, x - 6, 382), fill=(72, 48, 34, 255), width=14)
    d.line((160, 260, 232, 283), fill=(177, 130, 84, 255), width=8)


def crow(d):
    d.ellipse((131, 246, 357, 362), fill=(34, 41, 45, 255), outline=(18, 22, 24, 255), width=12)
    poly(d, [(160, 275), (102, 223), (129, 295), (205, 319)], (48, 57, 60, 255), width=9)
    d.ellipse((278, 188, 369, 291), fill=(42, 51, 54, 255), outline=(18, 22, 24, 255), width=10)
    poly(d, [(355, 218), (418, 241), (356, 259)], (158, 93, 36, 255), width=8)
    d.ellipse((337, 210, 353, 226), fill=(182, 138, 58, 255))
    d.ellipse((343, 214, 351, 222), fill=(20, 22, 22, 255))
    d.line((199, 279, 296, 330), fill=(89, 99, 99, 255), width=10)
    d.line((244, 348, 239, 383), fill=(74, 55, 38, 255), width=8)
    d.line((302, 350, 308, 383), fill=(74, 55, 38, 255), width=8)


make_pet("galinha", ((83, 93, 76, 255), (28, 43, 37, 255)), chicken)
make_pet("pintinho", ((121, 100, 50, 255), (42, 42, 29, 255)), chick)
make_pet("leitao", ((105, 73, 70, 255), (42, 37, 33, 255)), piglet)
make_pet("wolf_cub", ((78, 87, 85, 255), (29, 39, 40, 255)), wolf)
make_pet("lox_cub", ((104, 78, 52, 255), (40, 37, 31, 255)), lox)
make_pet("corvo", ((68, 83, 88, 255), (25, 34, 39, 255)), crow)
