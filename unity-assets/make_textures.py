import os, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures")
os.makedirs(OUT, exist_ok=True)
BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
REG = "/System/Library/Fonts/Supplemental/Arial.ttf"


def logo(path, color, w=2048, h=768, smile=True, arrow=True):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(BOLD, 560)
    text = "amazon"
    bb = d.textbbox((0, 0), text, font=f)
    tw = bb[2] - bb[0]
    th = bb[3] - bb[1]
    x = (w - tw) // 2 - bb[0]
    y = 40 - bb[1]
    d.text((x, y), text, font=f, fill=color)
    if smile:
        # sweep under the wordmark
        pts = []
        for i in range(160):
            t = i / 159
            sx = 180 + t * (w - 360)
            sy = h - 150 - 85 * math.sin(math.pi * t) ** 1.2
            pts.append((sx, sy))
        for i in range(len(pts) - 1):
            d.line([pts[i], pts[i + 1]], fill=(255, 120, 20, 255), width=34)
        if arrow:
            ax, ay = pts[-1]
            d.polygon([(ax - 8, ay - 38), (ax + 64, ay + 5), (ax - 8, ay + 40)],
                      fill=(255, 120, 20, 255))
    img.save(os.path.join(OUT, path))


def gate_sign():
    w, h = 1600, 620
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([10, 10, w - 10, h - 10], radius=34, fill=(13, 34, 58, 255))
    d.rounded_rectangle([10, 10, w - 10, h - 10], radius=34, outline=(255, 255, 255, 60), width=4)
    f = ImageFont.truetype(BOLD, 300)
    text = "amazon"
    bb = d.textbbox((0, 0), text, font=f)
    x = (w - (bb[2] - bb[0])) // 2 - bb[0]
    d.text((x, 40 - bb[1]), text, font=f, fill=(255, 255, 255, 255))
    # smile
    pts = []
    for i in range(140):
        t = i / 139
        sx = 300 + t * (w - 600)
        sy = 380 - 45 * math.sin(math.pi * t) ** 1.2
        pts.append((sx, sy))
    for i in range(len(pts) - 1):
        d.line([pts[i], pts[i + 1]], fill=(255, 120, 20, 255), width=22)
    d.polygon([(pts[-1][0] - 4, pts[-1][1] - 26), (pts[-1][0] + 42, pts[-1][1] + 2),
               (pts[-1][0] - 4, pts[-1][1] + 28)], fill=(255, 120, 20, 255))
    f2 = ImageFont.truetype(BOLD, 74)
    d.text((80, h - 130), "DFW9  ·  FULFILLMENT CENTER", font=f2, fill=(210, 220, 230, 255))
    d.text((w - 330, h - 130), "RELAY", font=f2, fill=(255, 140, 40, 255))
    img.save(os.path.join(OUT, "amazon_gate.png"))


def siding():
    w = h = 512
    img = Image.new("RGB", (w, h), (196, 200, 203))
    px = img.load()
    random.seed(7)
    for x in range(w):
        # 8 vertical ribs
        p = (x % 64) / 64.0
        s = 0.78 + 0.32 * (0.5 + 0.5 * math.cos(2 * math.pi * p))
        s *= 0.97 + 0.03 * random.random()
        for y in range(h):
            n = 0.98 + 0.02 * random.random()
            v = int(max(0, min(255, 205 * s * n)))
            px[x, y] = (v, v + 2, v + 4)
    img.save(os.path.join(OUT, "warehouse_siding.jpg"), quality=92)

    # tangent-space normal map from the rib profile
    import struct
    nrm = Image.new("RGB", (w, h), (128, 128, 255))
    np = nrm.load()
    for x in range(w):
        p = (x % 64) / 64.0
        dx = -0.55 * math.sin(2 * math.pi * p)
        for y in range(h):
            nx = int(128 + dx * 90)
            np[x, y] = (max(0, min(255, nx)), 128, 255)
    nrm.save(os.path.join(OUT, "warehouse_siding_normal.jpg"), quality=92)


def concrete():
    w = h = 512
    img = Image.new("RGB", (w, h), (150, 151, 148))
    px = img.load()
    random.seed(11)
    for y in range(h):
        for x in range(w):
            n = random.random()
            v = 145 + int(18 * (n - 0.5)) + int(8 * math.sin(x * 0.05) * math.sin(y * 0.04))
            px[x, y] = (v, v, v - 2)
    # expansion joints
    d = ImageDraw.Draw(img)
    for i in range(0, w, 256):
        d.line([(i, 0), (i, h)], fill=(118, 118, 116), width=2)
        d.line([(0, i), (w, i)], fill=(118, 118, 116), width=2)
    img.save(os.path.join(OUT, "yard_concrete.jpg"), quality=92)


def dirt():
    w = h = 512
    img = Image.new("RGB", (w, h), (86, 78, 58))
    px = img.load()
    random.seed(5)
    for y in range(h):
        for x in range(w):
            n = random.random()
            g = 0.5 + 0.5 * math.sin(x * 0.03) * math.sin(y * 0.05)
            v = 0.7 + 0.25 * n + 0.08 * g
            px[x, y] = (int(95 * v), int(88 * v), int(58 * v))
    img.filter(ImageFilter.GaussianBlur(0.4)).save(os.path.join(OUT, "ground_dirt.jpg"), quality=90)


def asphalt():
    w = h = 512
    img = Image.new("RGB", (w, h), (62, 64, 66))
    px = img.load()
    random.seed(3)
    for y in range(h):
        for x in range(w):
            n = random.random()
            v = 0.72 + 0.32 * n
            px[x, y] = (int(64 * v), int(66 * v), int(68 * v))
    img.save(os.path.join(OUT, "road_asphalt.jpg"), quality=92)


logo("amazon_logo_white.png", (255, 255, 255, 255))
logo("amazon_logo_navy.png", (15, 40, 70, 255))
logo("amazon_logo_black.png", (20, 24, 28, 255))
gate_sign()
siding()
concrete()
dirt()
asphalt()
print("textures written")
