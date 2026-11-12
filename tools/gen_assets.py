#!/usr/bin/env python3
"""Generate all pixel-art and store assets for Cavern Odyssey.

Everything is authored procedurally so the art is reproducible and tweakable.
Run from the repository root:  python3 tools/gen_assets.py
"""
from __future__ import annotations

import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "src", "SuperSqij.Shared", "Assets")
os.makedirs(ASSETS, exist_ok=True)

# Palette -------------------------------------------------------------------
INK = (12, 16, 28, 255)
DEEP = (11, 13, 26, 255)
ROCK_D = (44, 54, 82, 255)
ROCK_M = (68, 82, 118, 255)
ROCK_L = (102, 122, 162, 255)
ROCK_H = (140, 164, 206, 255)
MOSS_D = (40, 100, 70, 255)
MOSS_M = (72, 152, 104, 255)
MOSS_L = (140, 220, 150, 255)
Sqij_D = (38, 92, 52, 255)
Sqij_M = (79, 174, 74, 255)
Sqij_L = (143, 227, 106, 255)
Sqij_H = (204, 255, 160, 255)
EYE_W = (244, 255, 224, 255)
PUPIL = (16, 26, 18, 255)
BEAK = (242, 181, 68, 255)
BEAK_D = (174, 112, 32, 255)
FOOT = (196, 148, 84, 255)
PINK = (255, 111, 174, 255)
PINK_D = (196, 52, 118, 255)
PINK_L = (255, 190, 214, 255)
PURPLE = (176, 111, 227, 255)
PURPLE_D = (98, 58, 148, 255)
PURPLE_L = (224, 186, 255, 255)
GOLD = (255, 209, 102, 255)
CYAN = (87, 217, 255, 255)
TEAL = (53, 183, 168, 255)
WHITE = (255, 255, 255, 255)

DOOR_COLORS = [
    (255, 92, 92),   # ember
    (255, 200, 87),  # amber
    (87, 217, 255),  # azure
    (176, 124, 255), # violet
    (155, 255, 87),  # lime
    (255, 155, 87),  # copper
]


def save(img: Image.Image, name: str) -> None:
    path = os.path.join(ASSETS, name)
    img.save(path)
    print("wrote", os.path.relpath(path, ROOT), img.size)


def outline(img: Image.Image, color=INK, width: int = 1) -> Image.Image:
    """Add a hard pixel outline around opaque pixels."""
    a = img.getchannel("A")
    grown = a
    for _ in range(width):
        grown = grown.filter(ImageFilter.MaxFilter(3))
    ring = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ring.putalpha(grown)
    ring.paste(color, (0, 0), grown)
    base = Image.new("RGBA", img.size, (0, 0, 0, 0))
    base.alpha_composite(ring)
    base.alpha_composite(img)
    return base


def px_rect(d: ImageDraw.ImageDraw, box, fill) -> None:
    d.rectangle(box, fill=fill)


def dither(img: Image.Image, box, color, step=2, offset=0) -> None:
    x0, y0, x1, y1 = box
    p = img.load()
    for y in range(max(0, y0), min(img.height, y1)):
        for x in range(max(0, x0), min(img.width, x1)):
            if (x + y + offset) % step == 0:
                if p[x, y][3] > 0:
                    p[x, y] = color


# ---------------------------------------------------------------- tiles -----
def make_tiles() -> None:
    """8 x 4 grid of 16x16 tiles."""
    rng = random.Random(1986)
    atlas = Image.new("RGBA", (8 * 16, 4 * 16), (0, 0, 0, 0))
    for variant in range(6):
        t = Image.new("RGBA", (16, 16), ROCK_M)
        d = ImageDraw.Draw(t)
        # blocky rock facets
        d.polygon([(0, 0), (15, 0), (15, 3), (11, 3), (11, 6), (0, 6)], fill=ROCK_L)
        d.polygon([(0, 11), (5, 11), (5, 14), (0, 14)], fill=ROCK_D)
        d.polygon([(9, 8), (15, 8), (15, 12), (12, 12), (12, 15), (9, 15)], fill=ROCK_D)
        for _ in range(14):
            x, y = rng.randrange(16), rng.randrange(16)
            d.point((x, y), fill=ROCK_D if rng.random() < 0.6 else ROCK_H)
        # a couple of cracks
        cx = rng.randrange(2, 13)
        d.line([(cx, 3), (cx + 1, 7), (cx, 10)], fill=ROCK_D)
        atlas.paste(t, (variant * 16, 0))

    # mossy top tile
    for variant in range(2):
        t = Image.new("RGBA", (16, 16), ROCK_M)
        d = ImageDraw.Draw(t)
        d.rectangle((0, 0, 15, 3), fill=MOSS_M)
        d.rectangle((0, 0, 15, 1), fill=MOSS_L)
        for x in range(0, 16, 3):
            d.point((x, 4), fill=MOSS_D)
        for _ in range(8):
            d.point((rng.randrange(16), rng.randrange(5, 16)), fill=ROCK_D)
        atlas.paste(t, (variant * 16, 16))

    # one-way ledges (row 2)
    for variant in range(2):
        t = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        d = ImageDraw.Draw(t)
        d.rectangle((0, 0, 15, 4), fill=ROCK_M)
        d.rectangle((0, 0, 15, 1), fill=MOSS_M)
        d.point((0, 2), fill=MOSS_D)
        d.rectangle((0, 5, 15, 7), fill=ROCK_D)
        for x in range(variant, 16, 4):
            d.point((x, 8), fill=ROCK_D)
        atlas.paste(t, (variant * 16, 32))

    # hazard: lava frames row 3 (4 frames) at cols 0-3
    for f in range(4):
        t = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        d = ImageDraw.Draw(t)
        d.rectangle((0, 5, 15, 15), fill=(191, 58, 30, 255))
        d.rectangle((0, 8, 15, 15), fill=(240, 105, 30, 255))
        for x in range(16):
            y = 4 + int(round(1.6 * math.sin((x + f * 4) / 16 * math.pi * 2)))
            d.point((x, y), fill=(255, 224, 120, 255))
            d.point((x, y + 1), fill=(255, 177, 64, 255))
        d.point((4 + f, 11), fill=(255, 236, 160, 255))
        d.point((11 - f, 13), fill=(255, 236, 160, 255))
        atlas.paste(t, (f * 16, 48))

    # spikes cols 4-5
    for variant in range(2):
        t = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        d = ImageDraw.Draw(t)
        for i in range(3):
            x = i * 5 + variant
            d.polygon([(x, 16), (x + 2, 5), (x + 5, 16)], fill=ROCK_H)
            d.polygon([(x, 16), (x + 2, 5), (x + 2, 16)], fill=ROCK_L)
        atlas.paste(t, ((4 + variant) * 16, 48))

    # crystals cols 6-7 row 3 (animated colour)
    for variant in range(2):
        t = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        d = ImageDraw.Draw(t)
        d.polygon([(5, 16), (8, 3), (11, 16)], fill=TEAL)
        d.polygon([(5, 16), (8, 3), (8, 16)], fill=(126, 240, 208, 255))
        d.polygon([(10, 16), (13, 8), (15, 16)], fill=(38, 137, 148, 255))
        d.line([(8, 6), (8, 10)], fill=WHITE)
        atlas.paste(t, ((6 + variant) * 16, 48))
    save(atlas, "tiles.png")


# ---------------------------------------------------------------- sqij ------
def make_sqij() -> None:
    """6 columns x 2 rows of 24x24 frames."""
    sheet = Image.new("RGBA", (6 * 24, 2 * 24), (0, 0, 0, 0))
    for row in range(2):
        for col in range(6):
            fr = row * 6 + col
            img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
            d = ImageDraw.Draw(img)
            bob = [0, -1, 0, -1, 0, -2][fr % 6]
            leg = [0, 1, 0, -1, 0, 0][fr % 6]
            wing_up = fr in (4, 5)
            # feet
            fy = 20 + leg
            d.rectangle((8, fy, 10, fy + 1), fill=FOOT)
            d.rectangle((14, fy, 16, fy + 1), fill=FOOT)
            d.rectangle((7, fy + 2, 11, fy + 2), fill=BEAK_D)
            d.rectangle((13, fy + 2, 17, fy + 2), fill=BEAK_D)
            # tail feathers
            d.polygon([(3, 11 + bob), (0, 8 + bob), (4, 14 + bob)], fill=Sqij_D)
            d.polygon([(3, 13 + bob), (0, 14 + bob), (5, 16 + bob)], fill=Sqij_D)
            # body
            d.ellipse((4, 6 + bob, 20, 21 + bob), fill=Sqij_M)
            d.ellipse((4, 15 + bob, 20, 21 + bob), fill=Sqij_D)
            d.ellipse((6, 7 + bob, 18, 17 + bob), fill=Sqij_L)
            # head
            d.ellipse((7, 1 + bob, 21, 14 + bob), fill=Sqij_M)
            d.ellipse((8, 2 + bob, 20, 11 + bob), fill=Sqij_L)
            # crest
            d.polygon([(10, 3 + bob), (11, 0 + bob), (13, 4 + bob)], fill=Sqij_M)
            d.polygon([(14, 3 + bob), (16, 0 + bob), (17, 4 + bob)], fill=Sqij_M)
            # wing
            if wing_up:
                d.ellipse((3, 8 + bob, 12, 16 + bob), fill=Sqij_D)
                d.polygon([(4, 10 + bob), (0, 4 + bob), (8, 9 + bob)], fill=Sqij_M)
            else:
                d.ellipse((4, 11 + bob, 13, 19 + bob), fill=Sqij_D)
                d.ellipse((6, 12 + bob, 12, 17 + bob), fill=Sqij_M)
            # eye
            d.ellipse((12, 4 + bob, 19, 11 + bob), fill=EYE_W)
            d.ellipse((15, 6 + bob, 18, 9 + bob), fill=PUPIL)
            d.point((16, 6 + bob), fill=WHITE)
            # beak
            d.polygon([(19, 8 + bob), (24, 10 + bob), (19, 12 + bob)], fill=BEAK)
            d.polygon([(19, 10 + bob), (23, 10 + bob), (19, 12 + bob)], fill=BEAK_D)
            d.point((20, 9 + bob), fill=WHITE)
            # shading speckles
            d.point((9, 17 + bob), fill=Sqij_M)
            d.point((15, 18 + bob), fill=Sqij_M)
            img = outline(img)
            sheet.paste(img, (col * 24, row * 24))
    save(sheet, "sqij.png")


# ------------------------------------------------------------- enemies -----
def make_enemies() -> None:
    """4 columns x 2 rows of 24x24 (two animation frames each)."""
    sheet = Image.new("RGBA", (4 * 24, 2 * 24), (0, 0, 0, 0))
    for frame in range(2):
        # 0: crawler - armoured beetle
        img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        for i, x in enumerate((3, 8, 13, 18)):
            off = 0 if (i + frame) % 2 else 2
            d.line([(x, 18), (x - 2 + off, 22)], fill=(30, 32, 48, 255), width=1)
        d.ellipse((3, 9, 21, 20), fill=(120, 70, 60, 255))
        d.ellipse((4, 9, 20, 16), fill=(176, 100, 72, 255))
        d.rectangle((4, 14, 20, 18), fill=(96, 52, 52, 255))
        for x in (7, 11, 15):
            d.point((x, 12), fill=(232, 164, 96, 255))
        d.ellipse((15, 11, 18, 14), fill=(255, 220, 120, 255))
        d.point((16, 12), fill=INK)
        d.point((5, 15), fill=(255, 200, 140, 255))
        sheet.paste(outline(img), (0, frame * 24))

        # 1: floater - one-eyed spore
        img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        for i, x in enumerate((5, 9, 13, 17, 20)):
            yy = 18 + (i % 2) * 2 + frame
            d.line([(x, 17), (x + (1 if i % 2 else -1), yy)], fill=(90, 61, 130, 255))
        d.ellipse((3, 6 + frame, 21, 19 + frame), fill=PURPLE_D)
        d.ellipse((5, 7 + frame, 19, 17 + frame), fill=PURPLE)
        d.ellipse((7, 8 + frame, 16, 15 + frame), fill=EYE_W)
        d.ellipse((10, 10 + frame, 14, 14 + frame), fill=(60, 20, 60, 255))
        d.point((12, 10 + frame), fill=WHITE)
        d.point((6, 9 + frame), fill=PURPLE_L)
        sheet.paste(outline(img), (24, frame * 24))

        # 2: turret - crystal cannon
        img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.rectangle((5, 14, 19, 21), fill=(58, 70, 92, 255))
        d.rectangle((7, 15, 17, 18), fill=(92, 110, 140, 255))
        d.polygon([(12, 3), (18, 11), (12, 17), (6, 11)], fill=(28, 101, 112, 255))
        d.polygon([(12, 5), (16, 11), (12, 15), (8, 11)], fill=(71, 183, 174, 255))
        d.ellipse((10, 8 + (frame % 2), 14, 12 + (frame % 2)), fill=(180, 255, 230, 255))
        d.rectangle((4, 20, 20, 22), fill=(40, 48, 66, 255))
        sheet.paste(outline(img), (48, frame * 24))

        # 3: bat - winged eye
        img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        wy = 9 + (0 if frame else 2)
        d.polygon([(11, 12), (1, wy), (2, wy + 7), (10, 14)], fill=(58, 40, 78, 255))
        d.polygon([(13, 12), (23, wy), (22, wy + 7), (14, 14)], fill=(58, 40, 78, 255))
        d.ellipse((7, 7, 17, 18), fill=(112, 70, 142, 255))
        d.ellipse((9, 9, 15, 15), fill=EYE_W)
        d.ellipse((11, 10, 14, 14), fill=(220, 40, 90, 255))
        d.point((12, 11), fill=WHITE)
        d.point((8, 8), fill=(196, 150, 230, 255))
        sheet.paste(outline(img), (72, frame * 24))
    save(sheet, "enemies.png")


# --------------------------------------------------------------- items -----
def make_items() -> None:
    sheet = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
    # fruit
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((3, 5, 13, 15), fill=PINK_D)
    d.ellipse((4, 4, 12, 13), fill=PINK)
    d.ellipse((6, 5, 9, 8), fill=PINK_L)
    d.rectangle((8, 1, 9, 5), fill=(84, 60, 30, 255))
    d.polygon([(9, 4), (14, 2), (12, 6)], fill=MOSS_M)
    d.point((5, 10), fill=WHITE)
    sheet.paste(outline(img), (0, 0))

    # enertree fragment
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((7, 7, 8, 14), fill=(224, 200, 160, 255))
    d.polygon([(7, 3), (12, 6), (8, 8), (4, 6)], fill=MOSS_L)
    d.polygon([(3, 6), (7, 5), (6, 9), (2, 9)], fill=MOSS_M)
    d.polygon([(12, 6), (14, 10), (9, 9), (8, 6)], fill=MOSS_M)
    d.point((8, 4), fill=WHITE)
    d.rectangle((5, 13, 10, 15), fill=(120, 100, 72, 255))
    sheet.paste(outline(img), (16, 0))

    # pass (white, tinted per rune colour in game)
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((1, 3, 14, 13), radius=2, fill=(236, 242, 250, 255))
    d.rounded_rectangle((2, 4, 13, 12), radius=1, fill=WHITE)
    d.rectangle((3, 6, 6, 10), fill=(150, 160, 180, 255))
    d.rectangle((8, 6, 12, 7), fill=(150, 160, 180, 255))
    d.rectangle((8, 9, 11, 10), fill=(150, 160, 180, 255))
    d.ellipse((3, 12, 5, 14), fill=(180, 190, 205, 255))
    sheet.paste(outline(img), (32, 0))

    # heart/energy pickup (used for emergency energy drops)
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(2, 5), (6, 2), (8, 5), (10, 2), (14, 5), (8, 14)], fill=(235, 76, 104, 255))
    d.polygon([(4, 5), (7, 4), (8, 6), (9, 4), (12, 5), (8, 11)], fill=(255, 130, 150, 255))
    sheet.paste(outline(img), (48, 0))
    save(sheet, "items.png")


# --------------------------------------------------------------- props -----
def make_props() -> None:
    # monsta (2 frames, 32x32)
    sheet = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
    for f in range(2):
        img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.ellipse((3, 8, 29, 30), fill=PURPLE_D)
        d.ellipse((4, 6, 28, 26), fill=PURPLE)
        d.ellipse((8, 4, 24, 18), fill=PURPLE_L)
        # mouth chute
        d.rounded_rectangle((10, 17, 22, 24), radius=2, fill=(42, 20, 51, 255))
        mouth = 2 + ((f * 2) % 3)
        d.rectangle((11, 20 - mouth // 2, 21, 21 + mouth // 2), fill=(20, 10, 26, 255))
        for x in (12, 15, 18):
            d.point((x, 19), fill=PURPLE_L)
        # eyes on stalks
        for ex in (8, 20):
            d.rectangle((ex, 3, ex + 3, 8), fill=PURPLE_D)
            d.ellipse((ex - 1, 1, ex + 4, 6), fill=EYE_W)
            d.point((ex + 1 + f, 3), fill=INK)
        # feeding slot / fruit dish
        d.rounded_rectangle((4, 25, 28, 31), radius=2, fill=(70, 40, 86, 255))
        for i in range(3):
            d.ellipse((7 + i * 7, 26, 11 + i * 7, 30), fill=PINK if i != f else PINK_D)
        img = outline(img)
        sheet.paste(img, (f * 32, 0))
    save(sheet, "monsta.png")

    # altar / great platform 96x64
    img = Image.new("RGBA", (96, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((4, 44, 91, 60), fill=ROCK_M)
    d.rectangle((4, 44, 91, 48), fill=ROCK_L)
    d.rectangle((10, 52, 85, 56), fill=ROCK_D)
    d.polygon([(16, 44), (24, 30), (72, 30), (80, 44)], fill=ROCK_D)
    d.rectangle((24, 30, 72, 34), fill=MOSS_M)
    for x in range(28, 72, 8):
        d.rectangle((x, 35, x + 2, 40), fill=MOSS_D)
    # bas-relief tree
    d.rectangle((46, 18, 49, 32), fill=MOSS_D)
    d.ellipse((38, 8, 58, 24), fill=MOSS_M)
    d.ellipse((41, 11, 55, 21), fill=MOSS_L)
    d.point((48, 6), fill=WHITE)
    # glow runes
    for x in (20, 74):
        d.ellipse((x, 35, x + 4, 39), fill=TEAL)
    img = outline(img)
    save(img, "altar.png")

    # door barrier 32x48 (white/masked, tinted per colour in game)
    img = Image.new("RGBA", (32, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((2, 0, 5, 47), fill=(210, 230, 245, 255))
    d.rectangle((26, 0, 29, 47), fill=(210, 230, 245, 255))
    d.rectangle((6, 4, 25, 44), fill=(120, 190, 220, 120))
    d.rectangle((12, 8, 19, 40), fill=(230, 250, 255, 90))
    for y in range(4, 44, 8):
        d.line([(6, y), (25, y + 3)], fill=(255, 255, 255, 180))
    d.ellipse((11, 18, 20, 27), fill=(245, 255, 255, 230))
    d.polygon([(15, 20), (17, 25), (19, 20)], fill=(30, 60, 70, 255))
    save(img, "door.png")

    # torch 16x32, two frames
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for f in range(2):
        t = Image.new("RGBA", (16, 32), (0, 0, 0, 0))
        d = ImageDraw.Draw(t)
        d.rectangle((7, 14, 9, 30), fill=(120, 80, 48, 255))
        d.rectangle((5, 12, 11, 15), fill=(176, 130, 80, 255))
        d.ellipse((5, 5 - f, 11, 13), fill=(255, 132, 42, 255))
        d.ellipse((6, 6 - f, 10, 12), fill=(255, 214, 92, 255))
        d.ellipse((7, 7 - f, 9, 11), fill=(255, 255, 210, 255))
        img.paste(outline(t), (f * 16, 0))
    save(img, "torch.png")


# ------------------------------------------------------------ backgrounds ---
def silhouette_layer(width: int, height: int, seed: int, base_y: float, color, jag: int) -> Image.Image:
    rng = random.Random(seed)
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # tileable jagged cave ridge using a periodic random walk
    top = int(height * base_y)
    points = []
    x = 0
    y = top
    while x < width:
        points.append((x, y))
        x += rng.randrange(18, 46)
        y += rng.randrange(-jag, jag + 1)
        y = max(int(height * 0.18), min(height - 40, y))
    points.append((width, top))
    d.polygon([(0, height)] + points + [(width, height)], fill=color)
    # stalactites
    for _ in range(16):
        cx = rng.randrange(0, width)
        w = rng.randrange(10, 36)
        ln = rng.randrange(20, 110)
        d.polygon([(cx - w // 2, 0), (cx + w // 2, 0), (cx, ln)], fill=color)
    # stalagmites
    for _ in range(14):
        cx = rng.randrange(0, width)
        w = rng.randrange(14, 50)
        ln = rng.randrange(20, 120)
        d.polygon([(cx - w // 2, height), (cx + w // 2, height), (cx, height - ln)], fill=color)
    return img


def make_backgrounds() -> None:
    w, h = 1024, 360
    far = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # vertical gradient sky
    for y in range(h):
        t = y / h
        col = (int(22 + 28 * t), int(28 + 34 * t), int(52 + 42 * t), 255)
        ImageDraw.Draw(far).line([(0, y), (w, y)], fill=col)
    far.alpha_composite(silhouette_layer(w, h, 7, 0.60, (36, 44, 72, 255), 26))
    far.alpha_composite(silhouette_layer(w, h, 21, 0.75, (27, 33, 56, 255), 20))
    # distant glow spots
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    rng = random.Random(3)
    for _ in range(26):
        x, y = rng.randrange(w), rng.randrange(40, h - 20)
        r = rng.randrange(1, 4)
        gd.ellipse((x - r, y - r, x + r, y + r), fill=(90, 190, 190, rng.randrange(30, 90)))
    glow = glow.filter(ImageFilter.GaussianBlur(3))
    far.alpha_composite(glow)
    save(far, "bg_far.png")

    near = silhouette_layer(w, h, 55, 0.86, (12, 15, 27, 255), 34)
    save(near, "bg_near.png")

    # tiled cave wall that fills the space between platforms
    wall = Image.new("RGBA", (512, 512), (30, 38, 60, 255))
    wd = ImageDraw.Draw(wall)
    wrng = random.Random(4242)
    for _ in range(2600):
        x, y = wrng.randrange(512), wrng.randrange(512)
        shade = wrng.randrange(0, 3)
        col = [(36, 46, 72), (42, 53, 82), (25, 32, 52)][shade]
        r = 1 + wrng.randrange(3)
        wd.ellipse((x, y, x + r, y + r), fill=(*col, 255))
    for _ in range(26):
        x, y = wrng.randrange(512), wrng.randrange(512)
        pts = [(x, y)]
        for _ in range(wrng.randrange(3, 7)):
            x += wrng.randrange(-18, 19)
            y += wrng.randrange(6, 26)
            pts.append((x, y))
        wd.line(pts, fill=(18, 24, 40, 255), width=1)
    for _ in range(18):
        x, y = wrng.randrange(512), wrng.randrange(512)
        wd.ellipse((x, y, x + 2, y + 2), fill=(80, 160, 150, 40))
    wall = wall.filter(ImageFilter.GaussianBlur(0.4))
    save(wall, "bg_wall.png")

    # radial glow
    g = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    p = g.load()
    for y in range(128):
        for x in range(128):
            dx, dy = (x - 63.5) / 64, (y - 63.5) / 64
            a = max(0.0, 1.0 - math.sqrt(dx * dx + dy * dy))
            a = int((a ** 2.2) * 255)
            p[x, y] = (255, 255, 255, a)
    save(g, "glow.png")

    # vignette + scanlines (virtual 640x360)
    v = Image.new("RGBA", (640, 360), (0, 0, 0, 0))
    p = v.load()
    for y in range(360):
        for x in range(640):
            dx, dy = (x - 319.5) / 320, (y - 179.5) / 180
            d = min(1.0, math.sqrt(dx * dx + dy * dy))
            a = int(max(0, (d - 0.62)) * 260)
            p[x, y] = (0, 0, 0, min(110, a))
    save(v, "vignette.png")

    s = Image.new("RGBA", (640, 360), (0, 0, 0, 0))
    d = ImageDraw.Draw(s)
    for y in range(0, 360, 3):
        d.line([(0, y), (640, y)], fill=(0, 0, 0, 20))
    save(s, "scanlines.png")


# ------------------------------------------------------- text and logo -----
def load_font(paths, size):
    for p in paths:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


FONT_BOLD = [
    "/System/Library/Fonts/Supplemental/Arial Black.ttf",
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/HelveticaNeue.ttc",
]


def fit_font(text, size, max_width):
    """Largest bold size that keeps the wordmark inside max_width."""
    while size > 12:
        font = load_font(FONT_BOLD, size)
        box = font.getbbox(text)
        if box[2] - box[0] <= max_width:
            return font
        size -= 4
    return load_font(FONT_BOLD, 12)


def text_with_glow(base: Image.Image, xy, text, font, fill, glow_color, glow_radius=18, stroke=0, stroke_fill=None):
    glow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.text(xy, text, font=font, fill=glow_color, stroke_width=stroke, stroke_fill=glow_color)
    glow = glow.filter(ImageFilter.GaussianBlur(glow_radius))
    base.alpha_composite(glow)
    d = ImageDraw.Draw(base)
    d.text(xy, text, font=font, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


def make_logo() -> None:
    W, H = 1024, 340
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # layered chrome-green wordmark, sized to sit between the two emblems
    title = "CAVERN ODYSSEY"
    font = fit_font(title, 150, W - 420)
    # measure
    tmp = Image.new("RGBA", (10, 10))
    td = ImageDraw.Draw(tmp)
    box = td.textbbox((0, 0), title, font=font)
    tw = box[2] - box[0]
    x = (W - tw) // 2
    y = 60
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.text((x, y), title, font=font, fill=(60, 255, 170, 160), stroke_width=2, stroke_fill=(60, 255, 170, 160))
    glow = glow.filter(ImageFilter.GaussianBlur(24))
    img.alpha_composite(glow)
    d = ImageDraw.Draw(img)
    d.text((x + 6, y + 8), title, font=font, fill=(4, 20, 14, 255))
    d.text((x, y), title, font=font, fill=(24, 74, 46, 255), stroke_width=10, stroke_fill=(11, 18, 26, 255))
    d.text((x, y), title, font=font, fill=(143, 227, 106, 255), stroke_width=4, stroke_fill=(26, 58, 40, 255))
    d.text((x, y - 4), title, font=font, fill=(210, 255, 170, 255), stroke_width=1, stroke_fill=None)
    # gradient sheen clipped to text
    sheen = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sheen)
    for yy in range(y, y + 170):
        t = (yy - y) / 170
        sd.line([(0, yy), (W, yy)], fill=(255, 255, 255, int(90 * (1 - abs(t - 0.35) * 2))))
    mask = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(mask)
    md.text((x, y - 4), title, font=font, fill=255, stroke_width=1)
    sheen.putalpha(Image.composite(sheen.getchannel("A"), Image.new("L", (W, H), 0), mask))
    img.alpha_composite(sheen)
    # subtitle
    f2 = load_font(FONT_BOLD, 30)
    sub = "REBUILD THE ENERTREE"
    sb = td.textbbox((0, 0), sub, font=f2)
    d.text(((W - (sb[2] - sb[0])) // 2, 248), sub, font=f2, fill=(126, 232, 202, 255))
    # bird emblem
    sq = Image.open(os.path.join(ASSETS, "sqij.png")).crop((0, 0, 24, 24)).resize((132, 132), Image.NEAREST)
    img.alpha_composite(sq, (40, 30))
    img.alpha_composite(sq.transpose(Image.FLIP_LEFT_RIGHT), (W - 172, 30))
    save(img, "logo.png")


def cave_icon(size=1024) -> Image.Image:
    SS = 4
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # rounded square gradient background
    bg = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bg)
    for y in range(s):
        t = y / s
        col = (int(9 + 26 * t), int(16 + 54 * t), int(30 + 54 * t), 255)
        bd.line([(0, y), (s, y)], fill=col)
    # cave rings
    for i in range(7):
        inset = int(s * (0.08 + i * 0.035))
        bd.ellipse((inset, inset + s * 0.02, s - inset, s - inset + s * 0.02),
                   outline=(30 + i * 7, 70 + i * 12, 78 + i * 10, 255), width=int(SS * 4))
    mask = Image.new("L", (s, s), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle((0, 0, s, s), radius=int(s * 0.22), fill=255)
    img.paste(bg, (0, 0), mask)
    # glow behind bird
    glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((s * 0.22, s * 0.18, s * 0.78, s * 0.82), fill=(90, 255, 160, 120))
    glow = glow.filter(ImageFilter.GaussianBlur(s // 20))
    img.alpha_composite(glow)
    # Sqij head, from the sprite sheet, enlarged
    sq = Image.open(os.path.join(ASSETS, "sqij.png")).crop((6, 1, 24, 22))
    sq = sq.resize((int(s * 0.56), int(s * 0.56)), Image.NEAREST)
    sq = outline(sq, (8, 20, 16, 255), SS)
    img.alpha_composite(sq, (int(s * 0.24), int(s * 0.24)))
    # enertree rune at the base
    tree = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    td = ImageDraw.Draw(tree)
    cx, cy = s * 0.5, s * 0.8
    td.rectangle((cx - s * 0.012, cy, cx + s * 0.012, cy + s * 0.085), fill=(236, 214, 168, 255))
    td.ellipse((cx - s * 0.09, cy - s * 0.07, cx + s * 0.09, cy + s * 0.045), fill=MOSS_M)
    td.ellipse((cx - s * 0.06, cy - s * 0.055, cx + s * 0.065, cy + s * 0.025), fill=MOSS_L)
    td.ellipse((cx - s * 0.018, cy - s * 0.105, cx + s * 0.018, cy - s * 0.07), fill=(210, 255, 170, 255))
    td.ellipse((cx - s * 0.11, cy + s * 0.015, cx + s * 0.11, cy + s * 0.105), outline=(126, 240, 208, 200), width=SS * 3)
    img.alpha_composite(tree)
    # rim light
    rim = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rim)
    rd.rounded_rectangle((SS, SS, s - SS, s - SS), radius=int(s * 0.22), outline=(120, 240, 200, 90), width=SS * 3)
    img.alpha_composite(rim)
    return img.resize((size, size), Image.LANCZOS)


def make_icons_and_splash() -> None:
    icon = cave_icon(1024)
    save(icon, "icon_1024.png")

    # in-game splash / publisher card
    W = H = 1024
    sp = Image.new("RGBA", (W, H), (8, 12, 22, 255))
    d = ImageDraw.Draw(sp)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=(int(8 + 18 * t), int(12 + 26 * t), int(22 + 34 * t), 255))
    # stars/dust
    rng = random.Random(9)
    for _ in range(160):
        x, y = rng.randrange(W), rng.randrange(H)
        a = rng.randrange(20, 110)
        d.point((x, y), fill=(150, 230, 220, a))
    big = icon.resize((360, 360), Image.LANCZOS)
    sp.alpha_composite(big, ((W - 360) // 2, 210))
    title = "CAVERN ODYSSEY"
    f1 = fit_font(title, 96, W - 140)
    text_with_glow(sp, (0, 0), "", f1, WHITE, (60, 255, 160, 90))
    tb = d.textbbox((0, 0), title, font=f1)
    text_with_glow(sp, ((W - (tb[2] - tb[0])) // 2, 620), title, f1, (200, 255, 170, 255), (60, 255, 160, 120),
                   glow_radius=22, stroke=6, stroke_fill=(18, 50, 32, 255))
    f2 = load_font(FONT_BOLD, 34)
    sub = "AN ALL-THE-JOHNSON PRODUCTION"
    sb = d.textbbox((0, 0), sub, font=f2)
    d.text(((W - (sb[2] - sb[0])) // 2, 760), sub, font=f2, fill=(126, 232, 202, 255))
    f3 = load_font(FONT_BOLD, 24)
    sub2 = "PROGRAMMED AND DRAWN BY PAUL F. JOHNSON"
    sb = d.textbbox((0, 0), sub2, font=f3)
    d.text(((W - (sb[2] - sb[0])) // 2, 810), sub2, font=f3, fill=(130, 155, 170, 255))
    save(sp, "splash.png")

    # macOS .icns + Windows .ico + platform icons
    icns_dir = os.path.join(ROOT, "build", "iconset")
    os.makedirs(icns_dir, exist_ok=True)
    for sz in (16, 32, 64, 128, 256, 512, 1024):
        icon.resize((sz, sz), Image.LANCZOS).save(os.path.join(icns_dir, f"icon_{sz}x{sz}.png"))
    # .ico
    ico_path = os.path.join(ROOT, "build", "Icon.ico")
    os.makedirs(os.path.dirname(ico_path), exist_ok=True)
    icon.save(ico_path, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("wrote", ico_path)
    # mac .icns via iconutil
    iconset = os.path.join(ROOT, "build", "AppIcon.iconset")
    os.makedirs(iconset, exist_ok=True)
    for base in (16, 32, 128, 256, 512):
        icon.resize((base, base), Image.LANCZOS).save(os.path.join(iconset, f"icon_{base}x{base}.png"))
        icon.resize((base * 2, base * 2), Image.LANCZOS).save(os.path.join(iconset, f"icon_{base}x{base}@2x.png"))
    # iOS icon (opaque, no alpha)
    icon.convert("RGB").save(os.path.join(ROOT, "build", "AppIcon-ios-1024.png"))
    print("wrote platform icon sources")

    # Android icons + splash
    for name, size in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        folder = os.path.join(ROOT, "src", "SuperSqij.Android", "Resources", "mipmap-" + name)
        os.makedirs(folder, exist_ok=True)
        icon.resize((size, size), Image.LANCZOS).save(os.path.join(folder, "ic_launcher.png"))
        icon.resize((size, size), Image.LANCZOS).save(os.path.join(folder, "ic_launcher_round.png"))
    # adaptive foreground with safe padding
    for name, size in (("mdpi", 108), ("hdpi", 162), ("xhdpi", 216), ("xxhdpi", 324), ("xxxhdpi", 432)):
        folder = os.path.join(ROOT, "src", "SuperSqij.Android", "Resources", "mipmap-" + name)
        fg = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        inner = icon.resize((int(size * 0.66), int(size * 0.66)), Image.LANCZOS)
        fg.alpha_composite(inner, ((size - inner.width) // 2, (size - inner.height) // 2))
        fg.save(os.path.join(folder, "ic_launcher_foreground.png"))
    # splash drawable
    folder = os.path.join(ROOT, "src", "SuperSqij.Android", "Resources", "drawable")
    os.makedirs(folder, exist_ok=True)
    splash = Image.new("RGBA", (1080, 1920), (8, 12, 22, 255))
    inner = icon.resize((420, 420), Image.LANCZOS)
    splash.alpha_composite(inner, ((1080 - 420) // 2, 660))
    save(splash, "android_splash.png")
    splash.save(os.path.join(folder, "splash.png"))
    icon.resize((432, 432), Image.LANCZOS).save(os.path.join(folder, "splash_logo.png"))

    # iOS launch icon
    icon.resize((512, 512), Image.LANCZOS).save(os.path.join(ROOT, "src", "SuperSqij.iOS", "LaunchLogo.png"))
    icon.resize((1024, 1024), Image.LANCZOS).convert("RGB").save(os.path.join(ROOT, "src", "SuperSqij.iOS", "GameThumbnail.png"))


def make_font() -> None:
    """16 x 6 grid of 8x10 monospace glyphs (ASCII 32..127)."""
    font = load_font(["/System/Library/Fonts/Monaco.ttf", "/System/Library/Fonts/Menlo.ttc"], 11)
    cw, ch = 8, 10
    atlas = Image.new("RGBA", (cw * 16, ch * 6), (0, 0, 0, 0))
    # Every glyph shares one baseline; centring each glyph in its cell separately
    # trimmed the top row off the capitals and left the baseline ragged.
    text_y = -ImageDraw.Draw(Image.new("L", (1, 1))).textbbox((0, 0), "E", font=font)[1]
    for i in range(96):
        cell = Image.new("L", (cw, ch), 0)
        d = ImageDraw.Draw(cell)
        ch_ = chr(32 + i)
        bbox = d.textbbox((0, 0), ch_, font=font)
        gw = bbox[2] - bbox[0]
        x = (cw - gw) // 2 - bbox[0]
        d.text((x, text_y), ch_, font=font, fill=255)
        cell = cell.point(lambda v: 255 if v > 100 else 0)
        rgba = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        rgba.paste((255, 255, 255, 255), (0, 0), cell)
        atlas.paste(rgba, ((i % 16) * cw, (i // 16) * ch))
    save(atlas, "font.png")


def main() -> None:
    make_font()
    make_tiles()
    make_sqij()
    make_enemies()
    make_items()
    make_props()
    make_backgrounds()
    make_logo()
    make_icons_and_splash()
    print("assets complete")


if __name__ == "__main__":
    main()
