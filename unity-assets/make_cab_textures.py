"""Cab interior textures for WorldBuilder.UpgradeCabInterior (PIL + numpy).

Writes into unity-assets/Textures/:
  cab_cluster.png   instrument cluster: tach + speedo faces (needles are meshes),
                    six small gauges, a centre display and telltales
  cab_screen.png    centre-stack infotainment screen (route map + load card)
  cab_hvac.png      HVAC panel under the screen
  cab_switch.png    rocker-switch bank
  cab_overhead.png  overhead storage console front
  cab_grain_nor.png fine plastic grain normal map (tileable)
Run: python3 unity-assets/make_cab_textures.py
"""
import math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures")
FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
FONT_N = "/System/Library/Fonts/Supplemental/Arial Narrow Bold.ttf"


def font(size, narrow=False):
    try:
        return ImageFont.truetype(FONT_N if narrow else FONT, size)
    except OSError:
        return ImageFont.load_default()


def ctext(d, xy, s, f, fill):
    d.text(xy, s, font=f, fill=fill, anchor="mm")


def polar(cx, cy, r, deg):
    # deg measured clockwise from straight up
    a = math.radians(deg)
    return cx + r * math.sin(a), cy - r * math.cos(a)


def dial(d, cx, cy, r, vmax, major, minor, labels, red_from=None, green=None, unit="", sub="", inner=None):
    """Dial face, 240 degree sweep from -120 to +120."""
    d.ellipse([cx - r - 9, cy - r - 9, cx + r + 9, cy + r + 9], fill=(92, 96, 102))
    d.ellipse([cx - r - 5, cy - r - 5, cx + r + 5, cy + r + 5], fill=(28, 30, 33))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(10, 11, 13))
    ang = lambda v: -120 + 240 * v / vmax
    if green:
        d.arc([cx - r + 6, cy - r + 6, cx + r - 6, cy + r - 6], ang(green[0]) - 90, ang(green[1]) - 90, fill=(40, 170, 70), width=7)
    if red_from is not None:
        d.arc([cx - r + 4, cy - r + 4, cx + r - 4, cy + r - 4], ang(red_from) - 90, ang(vmax) - 90, fill=(210, 30, 20), width=10)
    v = 0.0
    while v <= vmax + 1e-6:
        is_major = abs(v / major - round(v / major)) < 1e-6
        r0 = r - (24 if is_major else 13)
        d.line([polar(cx, cy, r0, ang(v)), polar(cx, cy, r - 3, ang(v))], fill=(236, 238, 240), width=4 if is_major else 2)
        v += minor
    fl = font(int(r * 0.20))
    for val, s in labels:
        ctext(d, polar(cx, cy, r - 46, ang(val)), s, fl, (240, 242, 244))
    if inner:
        vmax2, step = inner
        fi = font(int(r * 0.095))
        for k in range(0, int(vmax2) + 1, step):
            a = ang(k * vmax / vmax2)
            d.line([polar(cx, cy, r - 70, a), polar(cx, cy, r - 62, a)], fill=(150, 156, 162), width=2)
            ctext(d, polar(cx, cy, r - 84, a), str(k), fi, (150, 156, 162))
    ctext(d, (cx, cy + r * 0.42), unit, font(int(r * 0.12)), (200, 204, 208))
    if sub:
        ctext(d, (cx, cy + r * 0.60), sub, font(int(r * 0.085)), (140, 146, 152))


def small_gauge(d, cx, cy, r, title, lo, hi, frac, warn_low=False, warn_high=False):
    d.ellipse([cx - r - 5, cy - r - 5, cx + r + 5, cy + r + 5], fill=(82, 86, 92))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(10, 11, 13))
    # 120 degree arc on the upper half
    for k in range(9):
        a = -60 + 15 * k
        big = k % 4 == 0
        d.line([polar(cx, cy, r - (11 if big else 6), a), polar(cx, cy, r - 2, a)], fill=(230, 232, 234), width=3 if big else 2)
    if warn_low:
        d.arc([cx - r + 3, cy - r + 3, cx + r - 3, cy + r - 3], -150, -135, fill=(210, 30, 20), width=5)
    if warn_high:
        d.arc([cx - r + 3, cy - r + 3, cx + r - 3, cy + r - 3], -45, -30, fill=(210, 30, 20), width=5)
    f = font(int(r * 0.30))
    ctext(d, polar(cx, cy, r - 22, -60), lo, f, (220, 222, 224))
    ctext(d, polar(cx, cy, r - 22, 60), hi, f, (220, 222, 224))
    ctext(d, (cx, cy + r * 0.45), title, font(int(r * 0.26), True), (190, 194, 198))
    tip = polar(cx, cy, r - 6, -60 + 120 * frac)
    d.line([(cx, cy), tip], fill=(255, 96, 20), width=4)
    d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=(40, 42, 46))


def cluster():
    W, H = 1024, 400
    im = Image.new("RGB", (W, H), (14, 15, 17))
    d = ImageDraw.Draw(im)
    for y in range(H):  # faint top-lit gradient on the cluster lens
        c = int(14 + 9 * (1 - y / H))
        d.line([(0, y), (W, y)], fill=(c, c + 1, c + 3))
    dial(d, 290, 200, 150, 30, 5, 1, [(v, str(v)) for v in range(0, 31, 5)], red_from=21, green=(11, 15),
         unit="x100 r/min", sub="ENGINE")
    dial(d, 734, 200, 150, 90, 10, 2.5, [(v, str(v)) for v in range(0, 91, 10)], unit="MPH", sub="km/h", inner=(140, 20))
    # centre colour display
    d.rounded_rectangle([450, 62, 574, 338], 8, fill=(4, 9, 15), outline=(54, 58, 64), width=3)
    ctext(d, (512, 92), "N", font(58), (245, 247, 250))
    ctext(d, (512, 132), "AUTO", font(15), (120, 200, 255))
    d.line([(462, 152), (562, 152)], fill=(40, 60, 80), width=2)
    fs = font(14, True)
    ctext(d, (512, 170), "ODO 412806 mi", fs, (210, 220, 230))
    ctext(d, (512, 192), "TRIP A  128.4", fs, (210, 220, 230))
    ctext(d, (512, 214), "AVG  7.1 MPG", fs, (210, 220, 230))
    for k, (lab, fr, col) in enumerate([("FUEL", 0.72, (230, 230, 230)), ("DEF", 0.64, (90, 170, 255))]):
        y = 244 + k * 30
        d.text((462, y - 9), lab, font=font(12, True), fill=(170, 180, 190))
        d.rectangle([500, y - 6, 562, y + 4], outline=(70, 80, 90))
        d.rectangle([501, y - 5, 501 + int(60 * fr), y + 3], fill=col)
    ctext(d, (512, 318), "05:40  72°F", font(13, True), (160, 175, 190))
    # six small gauges
    small_gauge(d, 64, 82, 44, "FUEL", "E", "F", 0.72, warn_low=True)
    small_gauge(d, 64, 205, 44, "DEF", "E", "F", 0.64, warn_low=True)
    small_gauge(d, 64, 328, 44, "OIL PSI", "0", "100", 0.45, warn_low=True)
    small_gauge(d, 960, 82, 44, "WATER °F", "100", "250", 0.52, warn_high=True)
    small_gauge(d, 960, 205, 44, "AIR 1 PSI", "0", "150", 0.80, warn_low=True)
    small_gauge(d, 960, 328, 44, "AIR 2 PSI", "0", "150", 0.79, warn_low=True)
    # telltales: parking brake lit red, others dim
    tt = [(380, 368, "BRAKE", (235, 40, 30)), (512, 372, "", None), (644, 368, "STOP", (70, 30, 28))]
    for x, y, s, col in tt:
        if col:
            d.rounded_rectangle([x - 34, y - 11, x + 34, y + 11], 4, outline=col, width=2)
            ctext(d, (x, y), s, font(15), col)
    for k, col in enumerate([(30, 90, 40), (90, 70, 20), (30, 60, 100), (30, 90, 40)]):
        x = 470 + k * 28
        d.ellipse([x - 7, 365, x + 7, 379], fill=col)
    d.polygon([(120, 26), (104, 34), (120, 42)], fill=(25, 70, 35))
    d.polygon([(904, 26), (920, 34), (904, 42)], fill=(25, 70, 35))
    im.save(os.path.join(OUT, "cab_cluster.png"))


def screen():
    W, H = 512, 320
    im = Image.new("RGB", (W, H), (22, 30, 34))
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(7)
    # map: blocks, roads, route
    for _ in range(26):
        x, y = rng.integers(0, 340), rng.integers(36, 320)
        d.rectangle([x, y, x + rng.integers(20, 70), y + rng.integers(14, 50)], fill=(30, 40, 44))
    for x in (40, 120, 230, 300):
        d.line([(x, 36), (x + 18, 320)], fill=(70, 80, 86), width=5)
    for y in (90, 170, 250):
        d.line([(0, y), (340, y - 12)], fill=(70, 80, 86), width=5)
    d.line([(0, 300), (340, 120)], fill=(150, 130, 70), width=9)  # highway
    route = [(170, 300), (176, 230), (196, 172), (300, 158), (330, 60)]
    d.line(route, fill=(50, 160, 255), width=8, joint="curve")
    d.polygon([(170, 286), (160, 306), (170, 300), (180, 306)], fill=(255, 255, 255))
    d.ellipse([322, 52, 338, 68], fill=(255, 150, 30))
    # load card
    d.rectangle([344, 36, 512, 320], fill=(14, 19, 24))
    ctext(d, (428, 62), "RELAY", font(24), (255, 158, 40))
    ctext(d, (428, 90), "LOAD D2D-0612", font(15, True), (220, 228, 236))
    ctext(d, (428, 112), "DFW9  ·  DOCK A14", font(13, True), (160, 172, 184))
    d.line([(356, 130), (500, 130)], fill=(40, 52, 62), width=2)
    ctext(d, (428, 160), "ETA 06:12", font(22), (245, 248, 250))
    ctext(d, (428, 188), "2.4 mi  ·  9 min", font(14, True), (160, 172, 184))
    d.rounded_rectangle([362, 220, 494, 256], 6, fill=(30, 110, 200))
    ctext(d, (428, 238), "START TRIP", font(15), (255, 255, 255))
    ctext(d, (428, 290), "HOS  10:42 left", font(13, True), (120, 210, 140))
    # status bar
    d.rectangle([0, 0, 512, 36], fill=(8, 12, 16))
    d.text((12, 9), "05:40", font=font(16), fill=(230, 235, 240))
    d.text((80, 10), "72°F", font=font(14, True), fill=(160, 172, 184))
    ctext(d, (300, 18), "NAV    MEDIA    PHONE    TRUCK", font(13, True), (150, 162, 174))
    for k in range(4):
        d.rectangle([470 + k * 8, 24 - k * 4, 475 + k * 8, 28], fill=(200, 210, 220))
    im.save(os.path.join(OUT, "cab_screen.png"))


def knob_ring(d, cx, cy, r, ticks, colors=None):
    for k in range(ticks):
        a = -135 + 270 * k / (ticks - 1)
        col = colors[k] if colors else (200, 204, 208)
        d.line([polar(cx, cy, r + 4, a), polar(cx, cy, r + 13, a)], fill=col, width=3)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(18, 19, 21), outline=(70, 74, 80), width=3)


def hvac():
    W, H = 512, 256
    im = Image.new("RGB", (W, H), (30, 32, 35))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([6, 6, W - 6, H - 6], 14, fill=(22, 23, 26), outline=(60, 63, 68), width=3)
    temps = [(int(40 + 200 * k / 10), 80, int(240 - 200 * k / 10)) for k in range(11)]
    knob_ring(d, 96, 104, 46, 11, temps)
    knob_ring(d, 256, 104, 46, 7)
    knob_ring(d, 416, 104, 46, 5)
    f = font(16, True)
    for x, s in ((96, "TEMP"), (256, "FAN"), (416, "MODE")):
        ctext(d, (x, 178), s, f, (190, 194, 198))
    for k, s in enumerate(["A/C", "RECIRC", "DEFROST", "MAX", "SLEEPER"]):
        x = 58 + k * 99
        d.rounded_rectangle([x - 42, 200, x + 42, 238], 6, fill=(40, 42, 46), outline=(80, 84, 90), width=2)
        ctext(d, (x, 214), s, font(13, True), (210, 214, 218))
        d.rectangle([x - 10, 228, x + 10, 232], fill=(255, 140, 20) if k in (0, 2) else (60, 50, 40))
    im.save(os.path.join(OUT, "cab_hvac.png"))


def switches():
    W, H = 512, 256
    im = Image.new("RGB", (W, H), (24, 25, 28))
    d = ImageDraw.Draw(im)
    labels = ["MARKER", "BEACON", "FIFTH WHL", "DIFF LOCK", "ENG BRAKE", "MIRROR HT", "WORK LT", "AUX"]
    for k, s in enumerate(labels):
        col, row = k % 4, k // 4
        x0, y0 = 22 + col * 122, 18 + row * 120
        d.rounded_rectangle([x0, y0, x0 + 100, y0 + 100], 8, fill=(12, 13, 15))
        for yy in range(0, 72):  # rocker: lit top half, shaded bottom
            t = yy / 72
            c = int(70 - 50 * t) if t < 0.5 else int(18 + 14 * (t - 0.5))
            d.line([(x0 + 16, y0 + 8 + yy), (x0 + 84, y0 + 8 + yy)], fill=(c, c + 1, c + 3))
        d.rectangle([x0 + 44, y0 + 16, x0 + 56, y0 + 22], fill=(255, 140, 20) if k in (0, 4) else (70, 54, 36))
        ctext(d, (x0 + 50, y0 + 90), s, font(14, True), (200, 204, 208))
    im.save(os.path.join(OUT, "cab_switch.png"))


def overhead():
    W, H = 1024, 224
    base = (122, 122, 120)
    im = Image.new("RGB", (W, H), base)
    d = ImageDraw.Draw(im)
    for y in range(H):  # slight top-down shading
        c = int(128 - 18 * y / H)
        d.line([(0, y), (W, y)], fill=(c, c, c - 2))
    doors = [(14, 300), (724, 1010)]
    for x0, x1 in doors:
        for k in range(2):
            a, b = x0 + k * (x1 - x0) // 2 + 4, x0 + (k + 1) * (x1 - x0) // 2 - 4
            d.rounded_rectangle([a, 18, b, 206], 10, outline=(62, 62, 62), width=4)
            d.rounded_rectangle([a + 6, 24, b - 6, 200], 8, outline=(150, 150, 148), width=2)
            m = (a + b) // 2
            d.rounded_rectangle([m - 34, 170, m + 34, 192], 6, fill=(54, 55, 57))
            d.rounded_rectangle([m - 28, 174, m + 28, 186], 4, fill=(30, 31, 33))
    # centre bay: CB radio slot and speakers
    d.rounded_rectangle([316, 14, 708, 210], 10, fill=(70, 71, 72))
    d.rounded_rectangle([420, 64, 604, 160], 6, fill=(18, 19, 21), outline=(40, 40, 42), width=3)
    d.rectangle([440, 84, 520, 110], fill=(255, 120, 20))
    ctext(d, (480, 97), "CH 19", font(18), (30, 10, 0))
    for k in range(2):
        d.ellipse([552 + k * 30 - 11, 86, 552 + k * 30 + 11, 108], fill=(55, 56, 58), outline=(90, 90, 92))
    for cx in (360, 664):
        for yy in range(30, 196, 12):
            for xx in range(cx - 30, cx + 31, 12):
                d.ellipse([xx - 3, yy - 3, xx + 3, yy + 3], fill=(36, 36, 38))
    im = im.filter(ImageFilter.GaussianBlur(0.6))
    im.save(os.path.join(OUT, "cab_overhead.png"))


def grain():
    N = 256
    rng = np.random.default_rng(3)
    f = np.fft.fft2(rng.standard_normal((N, N)))
    ky, kx = np.meshgrid(np.fft.fftfreq(N), np.fft.fftfreq(N), indexing="ij")
    k = np.sqrt(kx ** 2 + ky ** 2)
    h = np.real(np.fft.ifft2(f * np.exp(-(k / 0.09) ** 2)))  # periodic, so it tiles
    h = (h - h.mean()) / h.std()
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    s = 0.55
    n = np.dstack([-gx * s, gy * s, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    img = ((n * 0.5 + 0.5) * 255).astype(np.uint8)
    Image.fromarray(img, "RGB").save(os.path.join(OUT, "cab_grain_nor.png"))


if __name__ == "__main__":
    cluster(); screen(); hvac(); switches(); overhead(); grain()
    print("cab textures written to", OUT)
