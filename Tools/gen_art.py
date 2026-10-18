import json
import math
import os
import random

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")

CLEAR = (0, 0, 0, 0)
INK = (43, 27, 18, 255)
WHITE = (255, 255, 255, 255)


def H(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (c[3],)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), CLEAR)
        self.px = self.img.load()

    def p(self, x, y, c):
        if c is None:
            return
        x, y = int(math.floor(x)), int(math.floor(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return CLEAR

    def filled(self, x, y):
        return self.get(x, y)[3] > 0

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.p(x, y, c)

    def ell(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    self.p(x, y, c)

    def line(self, x0, y0, x1, y1, c, thick=1):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / max(1, n)
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            self.p(x, y, c)
            if thick > 1:
                self.p(x + 1, y, c)
                self.p(x, y + 1, c)

    def recolor_inside(self, cond, c):
        for y in range(self.h):
            for x in range(self.w):
                if self.filled(x, y) and cond(x, y):
                    self.px[x, y] = c

    def outline(self, col=INK):
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] > 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] > 0:
                        self.px[x, y] = col
                        break
        return self

    def save(self, rel):
        path = os.path.join(ART, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.save(path)

def ing_icon(ing):
    c = Canvas(20, 20)
    c1, c2 = H(ing["color"]), H(ing["color2"])
    shape, iid = ing["icon"], ing["id"]
    dark = shade(c1, 0.75)
    lite = mix(c1, WHITE, 0.45)
    plate = H("#F2F2F2")

    if shape == "nest":
        c.ell(10, 11, 7.5, 5.5, c1)
        for y in range(6, 17, 2):
            for x in range(2, 19):
                if c.filled(x, y) and int(x + math.sin(x * 0.9 + y) * 1.5) % 3 == 0:
                    c.p(x, y, c2)
                    c.p(x, y + 1, c2)
        for x in range(4, 17, 4):
            c.p(x, 8, lite)
    elif shape == "cylinders":
        for x0, y0 in ((3, 4), (5, 9), (2, 14)):
            c.rect(x0, y0, x0 + 11, y0 + 2, c1)
            c.rect(x0, y0 + 2, x0 + 11, y0 + 2, c2)
    elif shape == "pot":
        c.rect(4, 9, 15, 16, c2)
        c.rect(5, 17, 14, 17, shade(c2, 0.7))
        c.rect(3, 8, 16, 8, mix(c2, WHITE, 0.4))
        c.ell(10, 8.5, 6, 1.4, c1)
        for y in (10, 11):
            c.p(2, y, c2); c.p(17, y, c2)
        for x, y in ((7, 5), (8, 4), (7, 3), (12, 5), (13, 4), (12, 3)):
            c.p(x, y, (255, 255, 255, 210))
    elif shape == "jar":
        c.rect(5, 8, 14, 17, c1)
        c.p(5, 8, CLEAR); c.p(14, 8, CLEAR)
        c.rect(6, 5, 13, 7, H("#8A5A3A"))
        c.rect(6, 11, 13, 14, c2)
        c.p(6, 9, lite); c.p(6, 10, lite)
    elif shape == "powder":
        c.ell(10, 13, 7.5, 4.5, c2)
        c.rect(3, 10, 16, 11, c2)
        c.ell(10, 10, 6, 3, c1)
        for x, y in ((7, 9), (10, 8), (12, 10), (9, 11)):
            c.p(x, y, dark)
    elif shape == "pile":
        c.ell(10, 12, 7.5, 5, c1)
        for x, y in ((6, 10), (8, 13), (11, 9), (13, 12), (9, 15), (14, 14), (5, 13)):
            c.p(x, y, c2); c.p(x + 1, y, c2)
        c.p(10, 7, lite)
    elif shape == "slices":
        for i, y in enumerate((6, 10, 14)):
            c.ell(10 + (i - 1) * 1.5, y, 7, 2.3, c1)
            for x in range(5, 15, 2):
                c.p(x + i, y, c2)
    elif shape == "sausage":
        for i in range(12):
            t = i / 11
            c.ell(4 + t * 11, 15 - t * 10, 2.4, 2.4, c1)
        c.line(5, 12, 13, 4, c2)
    elif shape == "balls":
        for x, y in ((7, 8), (13, 9), (10, 14)):
            c.ell(x, y, 3.3, 3.3, c1)
            c.p(x - 1, y - 1, WHITE)
            c.p(x + 1, y + 1, c2)
    elif shape == "chunk":
        c.ell(7, 9, 4, 3, c1); c.ell(13, 12, 4, 3, c1); c.ell(8, 14, 3.5, 2.5, c1)
        for x, y in ((8, 11), (14, 14), (9, 16), (6, 10)):
            c.p(x, y, c2)
    elif shape == "rib":
        c.ell(9, 11, 7, 5.5, c1)
        c.rect(14, 4, 15, 9, c2)
        c.ell(14.5, 3.5, 2, 1.5, c2)
        for k in range(3):
            c.line(4 + k * 4, 14, 7 + k * 4, 8, dark)
    elif shape == "cheese":
        c.rect(4, 5, 15, 15, c1)
        c.ell(7.5, 8.5, 1.5, 1.5, c2); c.ell(12, 12, 1.7, 1.7, c2); c.ell(12, 7, 1, 1, c2)
        c.p(15, 5, CLEAR); c.p(14, 5, c2); c.p(15, 6, c2)
    elif shape == "egg":
        c.ell(10, 11, 5, 6.5, c1)
        c.p(8, 7, WHITE); c.p(8, 8, WHITE)
        c.p(12, 15, shade(c1, 0.88)); c.p(13, 14, shade(c1, 0.88))
    elif shape == "shrimp":
        for a in range(-40, 200, 6):
            r = math.radians(a)
            th = 2.6 - (a + 40) / 240 * 1.4
            c.ell(10 + math.cos(r) * 5, 10 + math.sin(r) * 5, th, th, c1)
        r = math.radians(200)
        tx, ty = 10 + math.cos(r) * 5, 10 + math.sin(r) * 5
        c.rect(int(tx) - 2, int(ty) - 3, int(tx), int(ty) - 1, c2)
        for a in range(0, 180, 40):
            r = math.radians(a)
            c.p(10 + math.cos(r) * 5, 10 + math.sin(r) * 5, c2)
        c.p(10 + math.cos(math.radians(-40)) * 5, 10 + math.sin(math.radians(-40)) * 5, INK)
    elif shape == "rings":
        for x, y in ((7, 7), (13, 9), (9, 14)):
            c.ell(x, y, 3.6, 3.6, c1)
            c.ell(x, y, 1.8, 1.8, CLEAR)
            c.p(x + 2, y + 2, c2)
    elif shape == "octopus":
        c.ell(10, 7, 5, 4.5, c1)
        for k in range(4):
            x0 = 6 + k * 2.7
            for y in range(10, 18):
                x = x0 + math.sin(y * 0.9 + k) * 1
                c.p(x, y, c1); c.p(x + 1, y, c1)
                if y % 3 == 0:
                    c.p(x, y, c2)
        c.p(8, 7, INK); c.p(12, 7, INK)
    elif shape == "enoki":
        for x in range(5, 15, 2):
            c.rect(x, 7, x, 15, c1)
            c.ell(x + 0.5, 6, 1.3, 1.3, mix(c1, c2, 0.5))
        c.rect(4, 15, 15, 17, c2)
    elif shape == "cabbage":
        c.ell(10, 10, 7.5, 7, c1)
        c.ell(10, 11, 4.5, 4.5, c2)
        c.line(10, 4, 10, 16, c2)
        c.line(5, 8, 10, 12, lite); c.line(15, 8, 10, 12, lite)
    elif shape == "scallion":
        for dx in (-3, 0, 3):
            for i in range(15):
                t = i / 14
                x, y = 6 + dx + 6 * t, 17 - 14 * t
                col = c2 if t < 0.35 else c1
                c.p(x, y, col); c.p(x + 1, y, col)
    elif shape == "pickles":
        c.ell(10, 14, 8, 3.5, plate)
        c.line(5, 12, 9, 6, c1, 2); c.line(8, 12, 12, 6, c1, 2)
        c.ell(13.5, 11, 2.6, 2.6, c2); c.p(13, 10, lite)
    elif shape == "rice":
        c.ell(10, 13, 8, 5, c2)
        c.ell(10, 10, 7, 4.5, c1)
        for x, y in ((6, 9), (9, 8), (12, 10), (8, 11), (13, 8)):
            c.p(x, y, shade(c1, 0.88))
    elif shape == "chop":
        c.ell(10, 10.5, 7.5, 5.2, c2)
        c.ell(10, 11.5, 7, 4.8, c1)
        for k in range(-2, 3):
            c.line(7 + k * 3, 15, 10 + k * 3, 8, dark)
        c.rect(16, 8, 17, 10, H("#F8F0E0"))
    elif shape == "strands":
        c.ell(10, 14, 8, 3.5, plate)
        rnd = random.Random(5)
        for k in range(8):
            x, y = 4 + rnd.random() * 10, 8 + rnd.random() * 5
            c.line(x, y, x + 5, y + rnd.random() * 3 - 1.5, c1)
        for x, y in ((7, 10), (11, 12), (13, 9)):
            c.p(x, y, c2)
    elif shape == "eggcake":
        c.rect(4, 7, 15, 15, c1)
        c.rect(4, 7, 15, 9, c2)
        for x, y in ((6, 11), (10, 13), (13, 11), (8, 14)):
            c.p(x, y, dark)
    elif shape == "dish":
        c.ell(10, 12, 8, 4.5, plate)
        c.ell(10, 11.5, 6.5, 3, c1)
        for x, y in ((8, 11), (12, 12), (10, 10)):
            c.p(x, y, c2)
    elif shape == "drumstick":
        c.ell(8, 8, 5.5, 5, c1)
        c.line(11, 11, 15, 15, c2, 2)
        c.ell(16, 16, 1.7, 1.7, c2)
        for x, y in ((6, 7), (9, 10), (7, 10)):
            c.p(x, y, dark)
    elif shape == "bottle":
        c.rect(8, 4, 11, 6, c1)
        c.rect(8, 2, 11, 3, c2)
        c.rect(6, 7, 13, 17, c1)
        c.rect(6, 10, 13, 13, c2 if iid != "nuoc_suoi" else H("#F0F8FF"))
        for y in range(8, 17):
            c.p(7, y, (255, 255, 255, 170))
    elif shape == "cup":
        for y in range(4, 18):
            w = 5 - (y - 4) * 0.15
            for x in range(int(10 - w), int(10 + w)):
                c.p(x, y, c1 if y >= 7 else (235, 245, 255, 200))
        c.p(7, 9, (255, 255, 255, 220)); c.p(12, 10, (255, 255, 255, 220))
    elif shape == "sugar":
        c.ell(10, 13, 8, 4.5, plate)
        c.ell(10, 10, 6, 3.5, c1)
        for x, y in ((8, 9), (11, 10), (10, 8)):
            c.p(x, y, c2)
    elif shape == "lemon":
        c.ell(10, 10, 7.5, 5.5, c1)
        c.p(2, 10, c1); c.p(18, 10, c1)
        c.p(7, 7, c2); c.p(8, 7, c2)
    elif shape == "kumquat":
        for x, y in ((7, 12), (13, 12), (10, 7)):
            c.ell(x, y, 3.4, 3.4, c1)
            c.p(x - 1, y - 1, mix(c1, WHITE, 0.5))
        c.p(10, 3, c2); c.p(11, 3, c2); c.p(11, 2, c2)
    elif shape == "ice":
        for x, y in ((3, 9), (10, 5), (9, 12)):
            c.rect(x, y, x + 5, y + 5, c1)
            c.rect(x, y, x + 5, y, WHITE)
            c.rect(x, y, x, y + 5, WHITE)
            c.rect(x + 5, y + 1, x + 5, y + 5, c2)
    elif shape == "can":
        c.rect(5, 4, 14, 17, c1)
        c.rect(5, 3, 14, 3, H("#C8C8D0"))
        c.rect(5, 9, 14, 12, c2)
        for y in range(4, 17):
            c.p(6, y, mix(c1, WHITE, 0.5))
    elif shape == "bag":
        c.rect(4, 6, 15, 17, c1)
        for x in range(4, 16, 2):
            c.p(x, 5, c1)
        c.rect(6, 10, 13, 14, c2)
    elif shape == "pearls":
        for y in range(7, 17, 3):
            for x in range(4 + (y % 2) * 2, 17, 3):
                if abs(x - 10) < (y - 4):
                    c.ell(x, y, 1.6, 1.6, c1)
                    c.p(x - 1, y - 1, c2)
    elif shape == "fruit":
        c.ell(10, 11, 6.5, 6.5, c1)
        if iid == "cam":
            for x, y in ((8, 9), (12, 12), (10, 14), (13, 8)):
                c.p(x, y, dark)
            c.p(10, 4, c2); c.p(11, 3, c2); c.p(12, 3, c2)
        elif iid == "vai_ngam":
            for y in range(5, 18):
                for x in range(4, 17):
                    if c.filled(x, y) and (x + y * 2) % 4 == 0:
                        c.p(x, y, c2)
        else:
            c.ell(12.5, 13, 3.5, 3.5, c2)
            c.p(10, 4, H("#4A9A3A")); c.p(11, 3, H("#4A9A3A"))
        c.p(7, 8, mix(c1, WHITE, 0.6))
    elif shape == "carton":
        c.rect(5, 8, 14, 17, c1)
        for y in range(4, 8):
            d = 7 - y
            c.rect(5 + d, y, 14 - d, y, c1)
        c.rect(5, 11, 14, 13, c2)
    elif shape == "phin":
        c.rect(6, 12, 13, 17, (220, 235, 245, 220))
        c.rect(6, 15, 13, 17, c2)
        c.rect(5, 8, 14, 11, c1)
        c.rect(7, 5, 12, 7, c1)
        c.p(9, 4, c1); c.p(10, 4, c1)
        c.p(9, 12, c2); c.p(10, 13, c2)
    else:
        c.ell(10, 10, 7, 7, c1)
    c.outline()
    return c

TOPPING = {
    "bo": ("slice", H("#9C3A2C")), "bo_my": ("slice_m", H("#D0606A")), "tom": ("shrimp", H("#F07840")),
    "muc": ("ring", H("#F4F0E8")), "bach_tuoc": ("blob", H("#C8506C")), "xuc_xich": ("stick", H("#C8402C")),
    "ca_vien": ("ball", H("#F2E6C8")), "kim_chi": ("patch", H("#E0502C")), "nam_kim_cham": ("lines", H("#F6F2E2")),
    "cai_thao": ("patch", H("#B8E070")), "trung": ("egg", H("#F8F8F0")), "pho_mai": ("square", H("#F8D040")),
    "ga": ("chunk", H("#E8C49A")), "suon_sun": ("chunk", H("#B8704A")), "ba_chi": ("slice_m", H("#E8A090")),
}


def draw_topping(c, kind, col, x, y):
    if kind == "slice":
        c.rect(x, y, x + 3, y + 1, col); c.p(x + 1, y, shade(col, 1.3))
    elif kind == "slice_m":
        c.rect(x, y, x + 3, y + 1, col); c.p(x + 1, y, WHITE); c.p(x + 3, y + 1, WHITE)
    elif kind == "shrimp":
        c.p(x, y, col); c.p(x + 1, y - 1, col); c.p(x + 2, y - 1, col); c.p(x + 3, y, col); c.p(x + 3, y + 1, shade(col, 0.8))
    elif kind == "ring":
        c.rect(x, y, x + 2, y + 2, col); c.p(x + 1, y + 1, CLEAR)
    elif kind == "blob":
        c.rect(x, y, x + 2, y + 1, col); c.p(x, y + 2, col); c.p(x + 2, y + 2, col); c.p(x + 1, y, WHITE)
    elif kind == "stick":
        c.line(x, y + 2, x + 3, y, col, 2)
    elif kind == "ball":
        c.rect(x, y, x + 1, y + 1, col)
    elif kind == "patch":
        c.rect(x, y, x + 2, y + 1, col); c.p(x + 3, y + 1, col)
    elif kind == "lines":
        for k in range(3):
            c.rect(x + k, y - 1, x + k, y + 2, col)
    elif kind == "egg":
        c.ell(x + 1.5, y + 1, 2, 1.6, col); c.p(x + 1, y + 1, H("#F0B020")); c.p(x + 2, y + 1, H("#F0B020"))
    elif kind == "square":
        c.rect(x, y, x + 2, y + 2, col); c.p(x + 1, y + 3, col)
    elif kind == "chunk":
        c.rect(x, y, x + 2, y + 1, col); c.p(x + 1, y + 2, shade(col, 0.8))


def food_icon(rec, ing_by_id):
    c = Canvas(24, 24)
    ids = [i["ing"] for i in rec["items"]]
    cat = rec["category"]
    spots = [(6, 10), (14, 9), (10, 12), (16, 12), (5, 13), (11, 8), (8, 9), (13, 13)]

    if cat == "Noodle":
        if "banh_gao" in ids:
            c.ell(12, 15, 10.5, 5.5, H("#F4F4F4"))
            c.ell(12, 13.5, 8, 4, H("#D8341C"))
            for x, y in ((7, 12), (11, 14), (14, 11), (16, 14), (9, 15)):
                c.rect(x, y, x + 3, y + 1, H("#F8F2E2"))
            if "pho_mai" in ids:
                c.rect(10, 10, 14, 11, H("#F8D040")); c.p(12, 12, H("#F8D040"))
        elif "nuoc_dung" not in ids:
            sauce = H("#3A2010") if "sot_tuong_den" in ids else H("#D8341C")
            c.ell(12, 15, 10, 6, H("#F4F4F4"))
            c.rect(2, 12, 21, 13, H("#F4F4F4"))
            c.ell(12, 11.5, 8, 4, H("#F0D070"))
            for y in range(8, 15):
                for x in range(4, 21):
                    if c.get(x, y) == H("#F0D070") and (x + y * 2) % 4 == 0:
                        c.p(x, y, H("#C89A40"))
            c.ell(11, 10.5, 4.5, 2, sauce)
            for k, ing in enumerate([i for i in ids if i in TOPPING][:4]):
                kind, col = TOPPING[ing]
                draw_topping(c, kind, col, *[(14, 9), (6, 11), (13, 13), (8, 8)][k])
            if "dua_chua" in ids:
                c.rect(16, 12, 18, 12, H("#9AD060")); c.rect(17, 13, 19, 13, H("#F08A30"))
        else:
            pot = H("#3C3A40")
            c.ell(12, 15, 10.5, 6.5, pot)
            c.rect(1, 11, 22, 13, pot)
            c.rect(0, 11, 1, 12, pot); c.rect(22, 11, 23, 12, pot)
            broth = H("#E8782C") if "sot_lau_thai" in ids else H("#D8341C")
            c.ell(12, 10.5, 9, 3.6, mix(pot, WHITE, 0.25))
            c.ell(12, 10.8, 8.2, 3, broth)
            for x, y in ((5, 10), (9, 12), (17, 11), (13, 9)):
                c.p(x, y, H("#F0D070")); c.p(x + 1, y, H("#F0D070"))
            tops = [i for i in ids if i in TOPPING]
            for k, ing in enumerate(tops[:7]):
                kind, col = TOPPING[ing]
                x, y = spots[k]
                draw_topping(c, kind, col, x, y - 1)
            for x, y in ((8, 4), (9, 3), (8, 2), (15, 5), (16, 4), (15, 3)):
                c.p(x, y, (255, 255, 255, 200))
    elif cat == "Rice":
        c.ell(12, 15, 11.2, 6.3, H("#8AA8C8"))
        c.ell(12, 14.6, 10.4, 5.6, H("#FAFAFA"))
        rice = H("#E89040") if "kim_chi" in ids else (H("#F6F0DC") if "gao_tam" in ids else WHITE)
        c.ell(8, 13, 6, 3.8, rice)
        for x, y in ((5, 12), (8, 11), (10, 13), (7, 14)):
            c.p(x, y, shade(rice, 0.88))
        if "kim_chi" in ids:
            for x, y in ((6, 12), (9, 13)):
                c.p(x, y, H("#C8281C"))
        if "suon_cot_let" in ids:
            c.ell(16, 13, 5.2, 3.5, H("#A85A30"))
            c.ell(16, 12.4, 4.6, 2.6, H("#B86A38"))
            for k in range(3):
                c.line(13 + k * 2, 15, 15 + k * 2, 11, H("#7A3A18"))
        if "dui_ga" in ids:
            c.ell(16, 12, 4.5, 3.6, H("#D89040"))
            c.line(19, 14, 21, 16, H("#F8F0E0"), 2)
        if "ba_chi" in ids and "kim_chi" not in ids:
            for k in range(3):
                c.rect(13 + k * 2, 11 + k, 17 + k * 2, 12 + k, H("#B86A48" if k % 2 else "#E8A090"))
        if "bi" in ids:
            for k in range(4):
                c.line(4 + k, 17, 9 + k, 16, H("#F0E0B0"))
        if "cha_trung" in ids:
            c.rect(10, 16, 13, 18, H("#F0B040")); c.rect(10, 16, 13, 16, H("#F8E080"))
        if "trung" in ids:
            c.ell(10 if "kim_chi" in ids else 16, 8, 3.5, 2.6, WHITE)
            c.ell(10 if "kim_chi" in ids else 16, 8, 1.4, 1.2, H("#F0A020"))
        if "dua_chua" in ids:
            c.rect(18, 17, 20, 17, H("#F08A30")); c.rect(17, 16, 18, 16, H("#9AD060"))
        if "hanh" in ids:
            for x, y in ((6, 11), (9, 12), (8, 14)):
                c.p(x, y, H("#4CB040"))
        if "nuoc_mam" in ids:
            c.ell(20, 6, 3.2, 2.2, H("#F4F4F4")); c.ell(20, 5.8, 2.2, 1.2, H("#E0A040")); c.p(20, 5, H("#F04030"))
    else:  # Drink
        rid = rec["id"]
        if "nuoc_suoi" in ids:
            c.rect(10, 2, 13, 3, H("#2060C0"))
            c.rect(10, 4, 13, 6, H("#A8D8F0"))
            c.rect(7, 7, 16, 21, H("#A8D8F0"))
            c.rect(7, 11, 16, 15, H("#F0F8FF"))
            c.rect(9, 12, 14, 13, H("#2060C0"))
            for y in range(8, 21):
                c.p(8, y, (255, 255, 255, 180))
        elif "lon_ngot" in ids:
            c.rect(6, 4, 17, 21, H("#D82020"))
            c.rect(6, 3, 17, 3, H("#C8C8D0")); c.rect(6, 22, 17, 22, H("#C8C8D0"))
            for x in range(7, 17):
                c.p(x, 11 + int(math.sin(x * 0.8) * 1.5), WHITE)
            for y in range(4, 21):
                c.p(7, y, mix(H("#D82020"), WHITE, 0.5))
        else:
            colors = {
                "tra_da": "#C89A40", "tra_chanh": "#E0C048", "tra_tac": "#E8A838", "tra_sua": "#C8A070",
                "tra_dao": "#F0A060", "tra_vai": "#F0D8C0", "nuoc_cam": "#F59A20", "cf_sua": "#8A5030",
                "soda_vq": "#7A40B0", "sua_tuoi_tcdd": "#F8F4EC",
            }
            liquid = H(colors[rid]) if rid in colors else next(
                (H(ing_by_id[i]["color"]) for i in ids if i in ing_by_id and ing_by_id[i]["category"] == "Drink"), H("#C89A40"))
            short = rid in ("tra_da", "cf_sua")
            top = 9 if short else 5
            for y in range(top, 22):
                w = 6.5 - (y - top) * 0.12
                for x in range(int(12 - w), int(12 + w)):
                    c.p(x, y, liquid if y >= top + 3 else (235, 245, 255, 210))
            for x, y in ((8, top + 3), (12, top + 4), (14, top + 3)):
                c.rect(x, y, x + 1, y + 1, (235, 250, 255, 255))
            if rid == "cf_sua":
                for y in range(18, 22):
                    for x in range(6, 18):
                        if c.filled(x, y):
                            c.p(x, y, H("#F0E8D0"))
            if rid == "sua_tuoi_tcdd":
                for k in range(4):
                    c.line(7 + k * 3, 19, 10 + k * 3, 10, H("#7A4420"))
            if "tran_chau" in ids:
                for x in range(7, 17, 2):
                    c.rect(x, 19, x, 20, H("#2A1A14"))
                    c.p(x + 1, 20, H("#2A1A14"))
            if "chanh" in ids:
                c.ell(6, top + 1, 2.6, 2.6, H("#B8E050")); c.p(6, top + 1, H("#F0F8C0"))
            if "tac" in ids:
                c.ell(9, 14, 1.6, 1.6, H("#F07818")); c.ell(14, 16, 1.6, 1.6, H("#F07818"))
            if "dao_ngam" in ids:
                c.ell(9, 15, 2.2, 1.5, H("#F8C070")); c.ell(14, 17, 2.2, 1.5, H("#F8C070"))
            if "vai_ngam" in ids:
                c.ell(9, 15, 1.7, 1.7, WHITE); c.ell(14, 17, 1.7, 1.7, WHITE)
            if "soda" in ids:
                for x, y in ((9, 12), (13, 15), (11, 18), (15, 11)):
                    c.p(x, y, (230, 210, 255, 255))
            if not short:
                c.line(14, 0, 15, top + 4, H("#F05050"), 2)
    c.outline()
    return c

# char
SKIN = {"light": H("#F8D0A8"), "medium": H("#E8B088"), "tan": H("#C88A60"), "dark": H("#A06A44")}


def character(skin="medium", hair="#1E1A1A", style="short", shirt="#F28C28", pants="#3A4A6A",
              shoes="#2A2020", hat=None, hat_color=None, extras=(), apron=None, female=False):
    c = Canvas(22, 32)
    s = SKIN[skin]
    hc = H(hair)
    sh = H(shirt)
    pt = H(pants)

    # Chân & giày
    if "skirt" in extras:
        c.rect(6, 25, 15, 27, pt)
        c.rect(7, 28, 8, 29, s); c.rect(13, 28, 14, 29, s)
    else:
        c.rect(7, 25, 14, 29, pt)
        c.rect(10, 27, 11, 29, CLEAR)
        c.p(10, 26, shade(pt, 0.75)); c.p(11, 26, shade(pt, 0.75))
    c.rect(6, 30, 9, 30, H(shoes)); c.rect(12, 30, 15, 30, H(shoes))

    # Thân
    c.rect(6, 17, 15, 24, sh)
    c.rect(4, 18, 5, 23, sh); c.rect(16, 18, 17, 23, sh)
    c.rect(4, 24, 5, 25, s); c.rect(16, 24, 17, 25, s)
    c.rect(15, 18, 15, 24, shade(sh, 0.82))
    c.rect(17, 18, 17, 23, shade(sh, 0.82))
    c.p(9, 17, shade(sh, 0.8)); c.p(12, 17, shade(sh, 0.8))
    if "aobaba" in extras:
        for y in range(18, 25, 2):
            c.p(10, y, shade(sh, 0.7))
    if "singlet" in extras:
        c.rect(4, 18, 5, 23, s); c.rect(16, 18, 17, 23, s)
        c.rect(6, 17, 7, 18, s); c.rect(14, 17, 15, 18, s)
    if "vest" in extras:
        c.rect(6, 17, 15, 24, H("#F07820"))
        c.rect(6, 21, 15, 21, H("#F8E040"))
        c.rect(10, 17, 11, 24, sh)
    if "collar" in extras:
        c.p(8, 17, WHITE); c.p(9, 17, WHITE); c.p(12, 17, WHITE); c.p(13, 17, WHITE)
    if "tie" in extras:
        c.rect(10, 17, 11, 22, H("#2A50A0")); c.p(10, 23, H("#2A50A0"))
    if apron:
        ac = H(apron)
        c.rect(7, 19, 14, 26 if "skirt" not in extras else 25, ac)
        c.p(8, 17, ac); c.p(8, 18, ac); c.p(13, 17, ac); c.p(13, 18, ac)
        c.rect(9, 21, 12, 22, shade(ac, 0.85))
    if "chain" in extras:
        for x in range(8, 14):
            c.p(x, 17 + (1 if 9 <= x <= 12 else 0), H("#F0C030"))
    if "scarf" in extras:
        c.rect(8, 16, 13, 17, H("#D82020")); c.rect(10, 18, 11, 19, H("#D82020"))
    if "badge" in extras:
        c.p(13, 19, H("#F0C030")); c.p(14, 19, H("#F0C030"))
    if "phone" in extras:
        c.rect(17, 21, 18, 24, H("#4A4A55")); c.p(17, 22, H("#80C8F0"))

    # Cổ & đầu
    c.rect(9, 16, 12, 16, shade(s, 0.9))
    c.rect(6, 6, 15, 15, s)
    for x, y in ((6, 6), (15, 6), (6, 15), (15, 15)):
        c.p(x, y, CLEAR)
    c.p(5, 10, s); c.p(16, 10, s); c.p(5, 11, s); c.p(16, 11, s)

    # Mặt
    c.rect(8, 10, 8, 11, INK); c.rect(13, 10, 13, 11, INK)
    c.p(10, 13, H("#A83A30")); c.p(11, 13, H("#A83A30"))
    if female:
        c.p(7, 12, H("#F09080")); c.p(14, 12, H("#F09080"))
    if "glasses" in extras:
        fr = H("#6A5040")
        for x0 in (7, 12):
            c.rect(x0, 9, x0 + 2, 9, fr)
            c.p(x0, 10, fr); c.p(x0 + 2, 10, fr)
            c.p(x0, 11, H("#D8ECF8")); c.p(x0 + 2, 11, H("#D8ECF8"))
        c.p(10, 10, fr); c.p(11, 10, fr)
    if "sunglasses" in extras:
        c.rect(7, 10, 9, 11, INK); c.rect(12, 10, 14, 11, INK); c.rect(10, 10, 11, 10, INK)
        c.p(7, 10, H("#506080")); c.p(12, 10, H("#506080"))
    if "mustache" in extras:
        c.rect(8, 12, 13, 12, shade(hc, 0.9) if hair != "#D8D8D8" else H("#B0B0B0"))
        c.p(10, 13, CLEAR); c.p(11, 13, CLEAR)
        c.p(10, 13, H("#A83A30"))

    # Tóc
    if style in ("short", "long", "bun", "ponytail", "pigtails", "spiky", "slick"):
        c.rect(6, 4, 15, 7, hc)
        c.p(6, 4, CLEAR); c.p(15, 4, CLEAR)
        c.rect(6, 8, 6, 10, hc); c.rect(15, 8, 15, 10, hc)
        c.rect(7, 8, 9, 8, hc)
        if style != "slick":
            c.p(12, 8, hc)
        c.rect(8, 4, 12, 4, mix(hc, WHITE, 0.25))
    if style == "long":
        c.rect(5, 7, 6, 17, hc); c.rect(15, 7, 16, 17, hc)
    if style == "bun":
        c.ell(11, 3, 2.8, 2.2, hc)
    if style == "ponytail":
        c.rect(16, 7, 17, 15, hc); c.p(18, 9, hc)
    if style == "pigtails":
        c.rect(3, 9, 5, 14, hc); c.rect(16, 9, 18, 14, hc)
        c.p(5, 9, H("#D82020")); c.p(16, 9, H("#D82020"))
    if style == "spiky":
        for x in (7, 9, 11, 13):
            c.p(x, 3, hc)
        c.p(8, 2, hc); c.p(12, 2, hc)
    if style == "slick":
        c.rect(6, 4, 15, 5, hc); c.p(14, 8, hc); c.p(13, 8, hc)

    # Mũ
    if hat == "nonla":
        col = H("#E8D49A")
        for i, y in enumerate(range(0, 8)):
            half = int(1 + i * 1.35)
            c.rect(10 - half, y, 11 + half, y, col)
        c.rect(1, 7, 20, 7, shade(col, 0.82))
        for x in range(4, 18, 3):
            c.p(x, 6, shade(col, 0.85))
        c.p(9, 2, mix(col, WHITE, 0.5))
    elif hat == "helmet":
        col = H(hat_color or "#F0C020")
        c.ell(11, 6, 5.8, 4, col)
        c.rect(5, 6, 16, 7, col)
        c.rect(4, 8, 17, 8, shade(col, 0.8))
        c.p(8, 4, mix(col, WHITE, 0.5)); c.p(9, 3, mix(col, WHITE, 0.5))
    elif hat == "mucoi":
        col = H(hat_color or "#4A8A3A")
        c.ell(11, 5.5, 5.8, 4.2, col)
        c.rect(5, 6, 16, 7, col)
        c.rect(3, 8, 18, 8, shade(col, 0.8))
        c.rect(10, 4, 11, 5, H("#E02020")); c.p(10, 3, H("#F0C030"))
    elif hat == "police":
        col = H("#22305A")
        c.rect(5, 3, 16, 7, col)
        c.rect(6, 2, 15, 2, col)
        c.rect(10, 4, 11, 5, H("#F0C030"))
        c.rect(6, 8, 16, 8, INK)
    elif hat == "cap":
        col = H(hat_color or "#2A60C0")
        c.rect(6, 4, 15, 7, col)
        c.p(6, 4, CLEAR); c.p(15, 4, CLEAR)
        c.rect(2, 8, 9, 8, shade(col, 0.8))
    elif hat == "headband":
        c.rect(6, 7, 15, 7, H(hat_color or "#F4F4F4"))
        c.p(16, 7, H(hat_color or "#F4F4F4")); c.p(17, 8, H(hat_color or "#F4F4F4"))

    c.outline()
    return c


CHARACTERS = {
    "anh":  dict(skin="light", hair="#2A1A14", style="long", shirt="#F28C28", pants="#3A4A6A", apron="#C8301E", hat="headband", female=True),
    "vy":   dict(skin="light", style="ponytail", shirt="#2AA8A0", pants="#2A2A3A", apron="#F4F0E6", female=True),
    "me":   dict(skin="medium", hair="#3A3030", style="bun", shirt="#D86A9A", pants="#2A2A2A", extras=("aobaba",), female=True),
    "ba":   dict(skin="tan", hair="#9A9A9A", style="short", shirt="#8A6A4A", pants="#4A4A4A", extras=("mustache",)),
    "nam":  dict(skin="light", hair="#D8D8D8", style="bun", shirt="#8A5AB8", pants="#3A2A4A", extras=("aobaba", "glasses"), female=True),
    "lan":  dict(skin="light", style="bun", shirt="#4A7AC8", pants="#2A3050", extras=("collar", "badge", "glasses"), female=True),
    "tu":   dict(skin="tan", hat="mucoi", shirt="#4A8A3A", pants="#5A4A3A", extras=("mustache",)),
    "khai": dict(skin="medium", style="slick", shirt="#2A2A2A", pants="#1A1A1A", extras=("sunglasses", "chain")),
    "tuan": dict(skin="medium", hair="#4A3020", style="spiky", shirt="#F4F4F4", pants="#3A3A3A", apron="#2A2A2A"),
    "na":   dict(skin="light", style="pigtails", shirt="#FAFAFA", pants="#2A3A7A", extras=("skirt", "scarf"), female=True),
    "sau":  dict(skin="dark", hat="helmet", hat_color="#F0C020", shirt="#3A6AB0", pants="#4A4A4A"),
    "linh": dict(skin="light", hair="#7A4A2A", style="long", shirt="#F07AA8", pants="#F0F0F0", extras=("phone",), female=True),
}

CUSTOMERS = [
    dict(skin="medium", style="long", hat="nonla", shirt="#D84A3A", pants="#2A2A2A", female=True),
    dict(skin="light", style="short", shirt="#F8F8F8", pants="#6A6A72", extras=("tie", "collar")),
    dict(skin="tan", hat="helmet", hat_color="#F8D020", shirt="#3A5AA0", pants="#2A3A6A", extras=("vest",)),
    dict(skin="light", hat="cap", hat_color="#2A60C0", shirt="#FAFAFA", pants="#2A3A7A"),
    dict(skin="medium", hair="#F0F0F0", style="bun", shirt="#8A5A3A", pants="#3A2A2A", extras=("aobaba", "glasses"), female=True),
    dict(skin="medium", hat="police", shirt="#8AB0D8", pants="#22305A", extras=("badge", "mustache")),
    dict(skin="light", hair="#5A3A20", style="long", shirt="#F8D040", pants="#3A5A9A", extras=("skirt",), female=True),
    dict(skin="tan", hair="#2A1A10", style="spiky", shirt="#5A9A4A", pants="#A8956A"),
    dict(skin="medium", hat="helmet", hat_color="#20A050", shirt="#20A050", pants="#2A2A2A"),
    dict(skin="light", hair="#2A1A1A", style="bun", shirt="#9A5AC8", pants="#1A1A1A", female=True),
    dict(skin="dark", hair="#6A6A6A", style="short", shirt="#FAFAFA", pants="#3A3A4A", extras=("singlet", "mustache")),
    dict(skin="light", hair="#1A1A1A", style="short", shirt="#2A2A2E", pants="#4A6A9A", hat="cap", hat_color="#D83A2A"),
]


def portrait(char_canvas):
    p = Canvas(22, 20)
    for y in range(20):
        for x in range(22):
            p.px[x, y] = char_canvas.get(x, y)
    return p

def city():
    c = Canvas(192, 72)
    for y in range(72):
        c.rect(0, y, 191, y, mix(H("#93B8D8"), H("#D8E4EC"), y / 71))
    rnd = random.Random(7)
    palette = ["#6E86B0", "#8A9CC0", "#B9A88E", "#9AA6B8", "#C8B8A0", "#7890A8", "#A89880"]
    x = -4
    while x < 192:
        w = rnd.randint(18, 34)
        h = rnd.randint(30, 64)
        col = H(rnd.choice(palette))
        top = 72 - h
        c.rect(x, top, x + w - 1, 71, col)
        c.rect(x, top, x + w - 1, top, shade(col, 1.15))
        c.rect(x + w - 1, top, x + w - 1, 71, shade(col, 0.8))
        for wy in range(top + 3, 70, 5):
            for wx in range(x + 3, x + w - 3, 4):
                lit = rnd.random() < 0.18
                c.rect(wx, wy, wx + 1, wy + 2, H("#F8E080") if lit else H("#C8E0F8"))
        if rnd.random() < 0.4:
            c.rect(x + 3, top - 3, x + 6, top - 1, H("#8A8A90"))
        x += w + rnd.randint(-2, 2)
    # Cây xanh
    for cx in (12, 180):
        c.rect(cx, 40, cx + 2, 71, H("#6A4A2A"))
        c.ell(cx + 1, 38, 10, 8, H("#3A8A3A"))
        c.ell(cx - 2, 35, 5, 4, H("#5AAA4A"))
    return c


def facade():
    c = Canvas(160, 96)
    wall = H("#E8D8B8")
    c.rect(0, 0, 159, 36, wall)
    for y in (0, 17):
        c.rect(0, y, 159, y, shade(wall, 0.85))
    for wx in (6, 46, 98, 138):
        c.rect(wx, 3, wx + 15, 13, WHITE)
        c.rect(wx + 1, 4, wx + 14, 12, H("#6A9AD0"))
        c.rect(wx + 8, 4, wx + 8, 12, WHITE)
        c.p(wx + 3, 5, H("#B8D8F8")); c.p(wx + 4, 5, H("#B8D8F8"))
        c.rect(wx, 14, wx + 15, 15, H("#8A5A3A"))
        for k in range(wx + 1, wx + 15, 3):
            c.p(k, 13, H("#4CA040")); c.p(k + 1, 12, H("#6AC050"))
    # Bảng hiệu (chữ được viết đè bằng font trong game)
    c.rect(16, 18, 143, 35, H("#E8B830"))
    c.rect(18, 20, 141, 33, H("#A8201C"))
    c.rect(18, 20, 141, 20, H("#C8302A"))
    c.rect(0, 36, 159, 38, H("#5A3A28"))
    # Gian hàng
    c.rect(0, 39, 159, 95, shade(wall, 0.92))
    c.rect(8, 40, 151, 95, H("#3A2620"))
    for x in range(8, 152, 6):
        c.rect(x, 40, x, 95, H("#4E3428"))
    for y in range(40, 96, 6):
        c.rect(8, y, 151, y, H("#4E3428"))
    # Kệ & bát
    c.rect(14, 64, 145, 65, H("#8A5A30"))
    c.rect(14, 66, 145, 66, H("#5A3A20"))
    cols = ["#E84A2A", "#F0C030", "#60C060", "#F4F4F4", "#E87020", "#F4F4F4", "#60C060", "#E84A2A", "#F0C030"]
    for i, x in enumerate(range(20, 140, 14)):
        col = H(cols[i % len(cols)])
        c.ell(x + 3, 62, 4, 2.2, col)
        c.rect(x, 62, x + 6, 63, shade(col, 0.85))
    # Đèn lồng
    for lx in (22, 137):
        c.rect(lx, 41, lx, 43, H("#F0C030"))
        c.ell(lx + 0.5, 49, 4.5, 5.5, H("#D82820"))
        for y in range(45, 54, 2):
            c.rect(lx - 3, y, lx + 4, y, H("#B81810"))
        c.rect(lx - 2, 43, lx + 3, 44, H("#F0C030"))
        c.rect(lx - 2, 54, lx + 3, 55, H("#F0C030"))
        c.rect(lx, 56, lx + 1, 59, H("#F0C030"))
    # Bảng menu nhỏ
    c.rect(60, 72, 99, 90, H("#2A4A2A"))
    c.rect(60, 72, 99, 72, H("#8A5A30")); c.rect(60, 90, 99, 90, H("#8A5A30"))
    for y in range(75, 89, 3):
        c.rect(63, y, 63 + (y * 7) % 20 + 8, y, H("#E8E8D8"))
        c.rect(90, y, 96, y, H("#F0C030"))
    c.rect(0, 39, 7, 95, shade(wall, 0.85)); c.rect(152, 39, 159, 95, shade(wall, 0.85))
    return c


def annex():
    """Mặt tiền khu mở rộng (160x96): biển xanh, đèn dây, tủ nước, chậu cây."""
    c = Canvas(160, 96)
    wall = H("#D8E0C8")
    c.rect(0, 0, 159, 36, wall)
    c.rect(0, 0, 159, 0, shade(wall, 0.85))
    # Ban công & cửa sổ tầng trên
    for wx in (10, 62, 114):
        c.rect(wx, 3, wx + 35, 14, WHITE)
        c.rect(wx + 1, 4, wx + 34, 13, H("#7AAAD8"))
        for k in range(wx + 9, wx + 34, 9):
            c.rect(k, 4, k, 13, WHITE)
        c.rect(wx - 2, 15, wx + 37, 16, H("#5A5A62"))
        for k in range(wx - 2, wx + 38, 3):
            c.p(k, 14, H("#5A5A62"))
    # Bảng hiệu xanh (chữ viết đè trong game)
    c.rect(16, 18, 143, 35, WHITE)
    c.rect(18, 20, 141, 33, H("#1E7A4A"))
    c.rect(18, 20, 141, 20, H("#2E9A5E"))
    c.rect(0, 36, 159, 38, H("#3A4A3A"))
    # Mái hiên sọc
    for x in range(4, 156):
        col = H("#E84A2A") if (x // 8) % 2 == 0 else H("#F8F0E0")
        c.rect(x, 39, x, 43, col)
    for x in range(4, 156, 8):
        c.rect(x, 44, x + 3, 44, H("#E84A2A"))
    # Gian trong
    c.rect(0, 45, 159, 95, shade(wall, 0.9))
    c.rect(8, 45, 151, 95, H("#33281F"))
    for x in range(8, 152, 8):
        c.rect(x, 45, x, 95, H("#43342A"))
    # Đèn dây nhiều màu
    bulbs = ["#F8D040", "#F05050", "#60C0F0", "#70D060"]
    for i, x in enumerate(range(12, 150, 6)):
        y = 48 + int(abs(((x - 12) % 36) - 18) / 6)
        c.p(x, y - 1, H("#2A2A2A"))
        c.rect(x, y, x + 1, y + 1, H(bulbs[i % 4]))
    # Tủ nước ngọt
    c.rect(14, 56, 38, 95, H("#C8CCD4"))
    c.rect(16, 58, 36, 92, H("#A8D8F0"))
    for row, col in enumerate(("#D82020", "#40A040", "#F0A020", "#3060D0", "#D82020")):
        y = 61 + row * 6
        for x in range(18, 35, 4):
            c.rect(x, y, x + 1, y + 3, H(col))
        c.rect(16, y + 4, 36, y + 4, H("#E8F4FA"))
    c.rect(14, 56, 38, 57, H("#2E9A5E"))
    # Áp phích món ăn
    c.rect(66, 56, 94, 80, H("#F8E8C0"))
    c.rect(66, 56, 94, 60, H("#D8341C"))
    c.ell(80, 69, 8, 5, H("#3C3A40"))
    c.ell(80, 68, 6.5, 3, H("#D8341C"))
    c.rect(70, 76, 90, 77, H("#8A5A30"))
    # Quạt treo tường & đồng hồ
    c.ell(120, 62, 6, 6, H("#E8E8E8"))
    c.ell(120, 62, 2, 2, H("#5A5A62"))
    c.rect(119, 68, 121, 72, H("#5A5A62"))
    # Chậu cây hai bên
    for px in (44, 136):
        c.rect(px, 86, px + 9, 95, H("#A8643A"))
        c.rect(px - 1, 86, px + 10, 87, H("#C8844A"))
        c.ell(px + 4.5, 78, 7, 8, H("#3A8A3A"))
        c.ell(px + 2, 75, 3.5, 3.5, H("#5AAA4A"))
        c.ell(px + 7, 80, 3, 3, H("#5AAA4A"))
    c.rect(0, 45, 7, 95, shade(wall, 0.82)); c.rect(152, 45, 159, 95, shade(wall, 0.82))
    return c


def counter():
    """Quầy 128x44. Chừa khoảng trống cột 30–80 cho Minh & Vy đứng phía sau."""
    c = Canvas(128, 44)
    wood = H("#A8643A")
    c.rect(0, 18, 127, 21, H("#C8CCD4"))
    c.rect(0, 18, 127, 18, H("#ECEEF2"))
    c.rect(0, 21, 127, 21, H("#9A9EA6"))
    c.rect(1, 22, 126, 43, wood)
    for x in range(10, 127, 12):
        c.rect(x, 22, x, 43, shade(wood, 0.8))
    c.rect(1, 40, 126, 43, shade(wood, 0.7))
    # Nồi nước dùng lớn (trái)
    c.rect(5, 7, 26, 17, H("#8A8A92"))
    c.rect(4, 6, 27, 7, H("#B8B8C0"))
    c.rect(6, 6, 25, 6, H("#C89A60"))
    c.rect(2, 10, 3, 12, H("#8A8A92")); c.rect(28, 10, 29, 12, H("#8A8A92"))
    c.rect(6, 8, 6, 16, H("#B0B0B8"))
    # Chồng bát
    for i in range(3):
        c.ell(88, 15 - i * 3, 6, 2, WHITE)
        c.rect(83, 15 - i * 3, 93, 16 - i * 3, H("#E8E8E8"))
        c.rect(83, 16 - i * 3, 93, 16 - i * 3, H("#5A8AC8"))
    # Hũ ớt
    c.rect(97, 11, 102, 17, H("#D8341C")); c.rect(97, 9, 102, 10, H("#8A5A3A"))
    # Bình trà
    for i, col in enumerate(("#E8A030", "#8A4A20")):
        x = 106 + i * 11
        c.rect(x, 4, x + 8, 17, (230, 240, 248, 255))
        c.rect(x + 1, 8, x + 7, 16, H(col))
        c.rect(x, 3, x + 8, 3, H("#5A5A62"))
    c.outline()
    return c


def floor_tile():
    c = Canvas(16, 16)
    base = H("#CFCAC0")
    c.rect(0, 0, 15, 15, base)
    c.rect(0, 15, 15, 15, H("#ABA59A")); c.rect(15, 0, 15, 15, H("#ABA59A"))
    c.rect(0, 0, 14, 0, H("#DEDAD2"))
    for x, y in ((3, 4), (9, 7), (5, 11), (12, 3), (11, 12)):
        c.p(x, y, H("#B8B2A8"))
    return c


def table(col):
    c = Canvas(36, 18)
    col = H(col)
    c.rect(0, 0, 35, 4, col)
    c.rect(0, 0, 35, 0, mix(col, WHITE, 0.35))
    c.rect(0, 4, 35, 4, shade(col, 0.75))
    for x in (3, 30):
        c.rect(x, 5, x + 2, 17, shade(col, 0.85))
    c.rect(5, 12, 30, 12, shade(col, 0.7))
    c.outline()
    return c


def stove(drink=False):
    c = Canvas(32, 24)
    body = H("#5A5E66")
    c.rect(1, 10, 30, 23, body)
    c.rect(1, 10, 30, 11, H("#8A8E96"))
    c.rect(3, 14, 28, 21, H("#6A6E76"))
    for x in (8, 23):
        c.ell(x, 17.5, 2, 2, INK); c.p(x, 16, H("#D8D8D8"))
    if drink:
        for i, col in enumerate(("#E8A030", "#F4EEE0")):
            x = 5 + i * 12
            c.rect(x, 0, x + 8, 9, (230, 240, 248, 255))
            c.rect(x + 1, 3, x + 7, 9, H(col))
            c.rect(x, 0, x + 8, 0, H("#4A4A52"))
    else:
        c.rect(8, 2, 23, 8, H("#3C3A40"))
        c.rect(7, 1, 24, 2, H("#5A5860"))
        c.rect(8, 1, 23, 1, H("#D8341C"))
        c.rect(5, 4, 6, 5, H("#3C3A40")); c.rect(25, 4, 26, 5, H("#3C3A40"))
    c.outline()
    return c


def stove_tier(tier):
    c = Canvas(32, 24)
    if tier == 1:
        c.rect(1, 10, 30, 23, H("#AEB6BE"))
        c.rect(1, 10, 30, 11, H("#E6ECF2"))
        c.rect(3, 14, 28, 21, H("#C4CCD4"))
        for x in (8, 23):
            c.ell(x, 17.5, 2, 2, INK); c.p(x, 16, H("#3A8AE0"))
        c.rect(14, 15, 17, 16, H("#3A8AE0"))
        c.rect(7, 2, 24, 8, H("#C8CED6"))
        c.rect(6, 1, 25, 2, H("#E8EEF4"))
        c.rect(7, 3, 24, 3, H("#9AA2AA"))
        c.rect(4, 4, 5, 5, H("#2A2A30")); c.rect(26, 4, 27, 5, H("#2A2A30"))
        c.rect(9, 9, 22, 9, H("#3A8AE0"))
    elif tier == 2:
        c.rect(0, 10, 31, 23, H("#2E2E34"))
        c.rect(0, 10, 31, 12, H("#C8301E"))
        c.rect(2, 15, 29, 21, H("#44444C"))
        for x in (6, 16, 26):
            c.ell(x, 18, 2, 2, H("#D8A830")); c.p(x, 17, H("#FFF0A0"))
        c.ell(15.5, 6, 11, 4, H("#1E1E22"))
        c.ell(15.5, 5, 9, 2.5, H("#3A3A42"))
        c.rect(26, 3, 31, 4, H("#5A4030"))
        c.rect(5, 9, 26, 9, H("#F08A20"))
    else:
        c.rect(1, 12, 30, 23, H("#B8BEC6"))
        c.rect(1, 10, 30, 13, H("#16161E"))
        c.rect(1, 10, 30, 10, H("#4A4A58"))
        c.rect(4, 16, 27, 21, H("#D8DEE6"))
        c.rect(6, 17, 13, 19, H("#0E2A1A"))
        c.rect(7, 18, 8, 18, H("#40F080")); c.rect(10, 18, 12, 18, H("#40F080"))
        for x in (19, 23):
            c.ell(x, 18.5, 1.5, 1.5, H("#2A2A30"))
        c.ell(15.5, 11.5, 8, 1.6, H("#F05A20"))
        c.ell(15.5, 11.5, 5.5, 1.0, H("#16161E"))
        c.rect(8, 3, 23, 9, H("#E8ECF2"))
        c.rect(8, 3, 23, 4, H("#FFFFFF"))
        c.rect(5, 5, 7, 6, H("#16161E")); c.rect(24, 5, 26, 6, H("#16161E"))
    c.outline()
    return c


def drink_machine(tier=1):
    body, top, tank1, tank2, band, screen, led = {
        1: ("#C8CED6", "#E8EEF4", "#E8A030", "#F4EEE0", "#9AA2AA", "#0E2A1A", "#40F080"),
        2: ("#3A3A42", "#5A5A64", "#D88A20", "#EADCC0", "#C8301E", "#1A0E0E", "#FF6040"),
        3: ("#F2F4F8", "#FFFFFF", "#F0B040", "#FFF8EC", "#2A78C8", "#0E1A2E", "#40C8FF"),
    }[tier]
    c = Canvas(32, 24)
    c.rect(1, 15, 30, 23, H("#5A5E66"))
    c.rect(1, 15, 30, 16, H("#8A8E96"))
    c.rect(3, 18, 28, 21, H("#6A6E76"))
    c.rect(4, 1, 27, 10, H(body))
    c.rect(4, 1, 27, 2, H(top))
    if tier >= 2:
        c.rect(4, 9, 27, 10, H(band))
    c.rect(6, 3, 12, 6, H(tank1))
    c.rect(6, 3, 12, 3, mix(H(tank1), WHITE, 0.4))
    c.rect(19, 3, 25, 6, H(tank2))
    c.rect(19, 3, 25, 3, WHITE)
    c.rect(14, 4, 17, 6, H(screen))
    c.p(15, 5, H(led)); c.p(16, 5, H(led))
    if tier == 3:
        c.p(14, 7, H(led)); c.p(17, 7, H(led))
    c.rect(8, 8, 10, 10, H("#9AA2AA") if tier != 2 else H("#1E1E22"))
    c.rect(21, 8, 23, 10, H("#9AA2AA") if tier != 2 else H("#1E1E22"))
    c.rect(9, 11, 9, 11, H(tank1)); c.rect(22, 11, 22, 11, H("#D8C8A8"))
    for x0, col in ((6, tank1), (19, "#C89060")):
        c.rect(x0, 12, x0 + 6, 16, (230, 240, 248, 255))
        c.rect(x0 + 1, 14, x0 + 5, 16, H(col))
    c.outline()
    return c


STAFF = [
    dict(skin="medium", hair="#1A1A1A", style="spiky", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A"),
    dict(skin="light", hair="#3A2418", style="ponytail", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", female=True),
    dict(skin="tan", hair="#2A1A10", style="short", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", hat="cap", hat_color="#C8301E"),
    dict(skin="light", hair="#1A1A1A", style="bun", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", female=True),
    dict(skin="dark", hair="#1A1A1A", style="short", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A"),
    dict(skin="medium", hair="#5A3A20", style="long", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", female=True),
    dict(skin="tan", hair="#2A1A10", style="spiky", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A"),
    dict(skin="medium", hair="#1A1A1A", style="ponytail", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", female=True),
    dict(skin="light", hair="#4A3020", style="short", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", hat="cap", hat_color="#C8301E"),
    dict(skin="tan", hair="#2A1A10", style="pigtails", shirt="#C8301E", pants="#2A2A2A", apron="#2A2A2A", female=True),
]


def fire(frame):
    c = Canvas(18, 8)
    rnd = random.Random(frame * 13 + 1)
    for x in range(1, 17, 3):
        h = rnd.randint(3, 6)
        for y in range(8 - h, 8):
            c.p(x, y, H("#F05020")); c.p(x + 1, y, H("#F05020"))
        c.p(x, 7 - h + 2, H("#F8D040")); c.p(x + 1, 6, H("#F8D040"))
    return c


def steam(frame):
    c = Canvas(12, 14)
    for i in range(5):
        y = 12 - i * 3
        x = 5 + int(math.sin(i + frame * 1.7) * 2)
        c.ell(x, y, 2.2, 1.6, (255, 255, 255, 200 - i * 30))
    return c


def coin():
    c = Canvas(14, 14)
    c.ell(7, 7, 6.5, 6.5, H("#F0C030"))
    c.ell(7, 7, 4.5, 4.5, H("#D8A010"))
    c.ell(7, 7, 3.5, 3.5, H("#F0C030"))
    c.rect(6, 6, 7, 7, H("#8A5A10"))
    c.p(4, 3, WHITE); c.p(3, 4, WHITE)
    c.outline()
    return c


def clock():
    c = Canvas(14, 14)
    c.ell(7, 7, 6.5, 6.5, WHITE)
    c.rect(6, 3, 6, 7, INK); c.rect(7, 7, 9, 7, INK)
    c.p(7, 1, INK); c.p(12, 7, INK); c.p(7, 12, INK); c.p(1, 7, INK)
    c.outline()
    return c


def star(full):
    c = Canvas(11, 11)
    col = H("#F8C828") if full else H("#5A5048")
    pts = [(5, 0), (6, 1), (4, 1), (6, 2), (4, 2), (5, 1), (5, 2)]
    for x, y in pts:
        c.p(x, y, col)
    c.rect(0, 3, 10, 4, col)
    c.rect(2, 5, 8, 6, col)
    c.rect(1, 7, 3, 8, col); c.rect(7, 7, 9, 8, col)
    c.rect(4, 3, 6, 7, col)
    c.p(1, 9, col); c.p(9, 9, col)
    if full:
        c.p(4, 3, H("#FFF0A0"))
    c.outline()
    return c

def frame(w, h, fill, border_col=None, bevel=False, round_=True, shadow=None):
    c = Canvas(w, h)
    c.rect(1, 1, w - 2, h - 2, fill)
    if bevel:
        c.rect(1, 1, w - 2, 1, mix(fill, WHITE, 0.6))
        c.rect(1, 1, 1, h - 2, mix(fill, WHITE, 0.6))
        c.rect(1, h - 3, w - 2, h - 2, shade(fill, 0.74))
        c.rect(w - 2, 2, w - 2, h - 2, shade(fill, 0.85))
    if shadow:
        c.rect(1, h - 2, w - 2, h - 2, shadow)
    if border_col:
        c.rect(1, 1, w - 2, 2, border_col); c.rect(1, h - 3, w - 2, h - 2, border_col)
        c.rect(1, 1, 2, h - 2, border_col); c.rect(w - 3, 1, w - 2, h - 2, border_col)
    c.rect(1, 0, w - 2, 0, INK); c.rect(1, h - 1, w - 2, h - 1, INK)
    c.rect(0, 1, 0, h - 2, INK); c.rect(w - 1, 1, w - 1, h - 2, INK)
    if not round_:
        for x, y in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
            c.p(x, y, INK)
    return c


def wood_panel():
    c = Canvas(24, 24)
    c.rect(0, 0, 23, 23, INK)
    c.rect(1, 1, 22, 22, H("#8A5430"))
    c.rect(1, 1, 22, 2, H("#B07040")); c.rect(1, 1, 2, 22, H("#B07040"))
    c.rect(1, 21, 22, 22, H("#5A3418")); c.rect(21, 1, 22, 22, H("#5A3418"))
    c.rect(5, 5, 18, 18, H("#4A2A14"))
    c.rect(6, 6, 17, 17, H("#D8A868"))
    return c


def wood_tile():
    c = Canvas(8, 8)
    for y in range(8):
        for x in range(8):
            c.p(x, y, H("#C8945A") if (x + y) % 4 < 2 else H("#D8A868"))
    return c


def speaker(on):
    c = Canvas(16, 14)
    body = H("#F4F4F4")
    c.rect(2, 5, 5, 9, body)
    for i in range(5):
        c.rect(6 + i, 5 - i, 6 + i, 9 + i, body)
    if on:
        for y in (5, 6, 7, 8, 9):
            c.p(12, y, body)
        c.p(13, 3, body); c.p(14, 4, body); c.p(14, 5, body); c.p(15, 6, body); c.p(15, 7, body); c.p(15, 8, body)
        c.p(14, 9, body); c.p(14, 10, body); c.p(13, 11, body)
    else:
        for k in range(5):
            c.p(11 + k, 5 + k, H("#F05050")); c.p(15 - k, 5 + k, H("#F05050"))
    c.outline()
    return c


def bubble_tail():
    c = Canvas(9, 6)
    for y in range(5):
        c.rect(2 + y // 2, y, 6 - y // 2, y, WHITE)
    c.outline()
    return c


def main():
    with open(os.path.join(DATA, "ingredients.json"), encoding="utf-8") as f:
        ings = json.load(f)["ingredients"]
    with open(os.path.join(DATA, "recipes.json"), encoding="utf-8") as f:
        recs = json.load(f)["recipes"]
    ing_by_id = {i["id"]: i for i in ings}

    for ing in ings:
        ing_icon(ing).save(f"ing/{ing['id']}.png")
    for rec in recs:
        food_icon(rec, ing_by_id).save(f"food/{rec['id']}.png")

    for cid, args in CHARACTERS.items():
        ch = character(**args)
        ch.save(f"chars/{cid}.png")
        portrait(ch).save(f"portraits/{cid}.png")
    for i, args in enumerate(STAFF):
        ch = character(**args)
        ch.save(f"chars/nv{i + 1}.png")
        portrait(ch).save(f"portraits/nv{i + 1}.png")
    for i, args in enumerate(CUSTOMERS):
        ch = character(**args)
        ch.save(f"chars/khach_{i + 1:02d}.png")
        portrait(ch).save(f"portraits/khach_{i + 1:02d}.png")

    city().save("scene/city.png")
    facade().save("scene/facade.png")
    annex().save("scene/annex.png")
    counter().save("scene/counter.png")
    floor_tile().save("scene/floor.png")
    table("#D8402C").save("scene/table_red.png")
    table("#2A78C8").save("scene/table_blue.png")
    stove().save("scene/stove.png")
    stove(drink=True).save("scene/drinkbar.png")
    drink_machine(1).save("scene/drinkmachine.png")
    drink_machine(2).save("scene/drinkmachine_t2.png")
    drink_machine(3).save("scene/drinkmachine_t3.png")
    for t in (1, 2, 3):
        stove_tier(t).save(f"scene/stove_t{t}.png")
    for k in range(2):
        fire(k).save(f"scene/fire{k}.png")
        steam(k).save(f"scene/steam{k}.png")

    coin().save("ui/coin.png")
    clock().save("ui/clock.png")
    star(True).save("ui/star_full.png")
    star(False).save("ui/star_empty.png")
    frame(12, 12, WHITE, bevel=True).save("ui/btn.png")
    frame(12, 12, WHITE, shadow=H("#E2E2E2")).save("ui/box.png")
    frame(14, 14, H("#FFF6E2"), border_col=H("#C08850")).save("ui/card.png")
    frame(12, 12, WHITE).save("ui/bubble.png")
    frame(16, 16, H("#FFF4DC"), border_col=H("#8A5430")).save("ui/paper.png")
    frame(10, 10, (40, 28, 22, 240), round_=False).save("ui/dark.png")
    bubble_tail().save("ui/bubble_tail.png")
    speaker(True).save("ui/sound_on.png")
    speaker(False).save("ui/sound_off.png")
    wood_panel().save("ui/wood_panel.png")
    wood_tile().save("ui/wood_tile.png")

    total = sum(len(fs) for _, _, fs in os.walk(ART) for f in fs if True)
    print(f"Đã vẽ xong {len(ings)} nguyên liệu, {len(recs)} món, {len(CHARACTERS)} nhân vật, {len(CUSTOMERS)} khách -> {ART}")


if __name__ == "__main__":
    main()
