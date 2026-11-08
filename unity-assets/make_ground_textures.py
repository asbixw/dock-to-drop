"""Generate seamless, higher-resolution ground textures for Dock to Drop.

The original 512 px library textures looked like coloured noise once they were
tiled across the apron and the yard. These are 1024 px, seamless, and deliberately
low-contrast: broad mottling for scale, fine grain for close-ups, plus a handful of
cracks and stains so the ground stops reading as a single flat plane.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "Textures")
RNG = np.random.default_rng(20261104)


def periodic_noise(size, max_freq, octaves=4, gain=0.55):
    """Tileable smooth noise built from integer-frequency sine waves."""
    x = np.arange(size, dtype=np.float32) / size
    y = np.arange(size, dtype=np.float32) / size
    xx, yy = np.meshgrid(x, y)
    out = np.zeros((size, size), np.float32)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        lo = 1 + o * max(1, max_freq // (2 ** (octaves - 1)))
        hi = max(lo + 1, max_freq // (2 ** (octaves - 1 - o)))
        for _ in range(5):
            fx = int(RNG.integers(lo, hi + 1))
            fy = int(RNG.integers(lo, hi + 1))
            ph = float(RNG.uniform(0, np.pi * 2))
            out += amp * np.sin(2 * np.pi * (fx * xx + fy * yy) + ph)
            total += amp
        amp *= gain
    out /= max(total, 1e-6)
    out -= out.min()
    out /= max(out.max(), 1e-6)
    return out


def grain(size, sigma=0.035):
    return RNG.normal(0.0, sigma, (size, size)).astype(np.float32)


def normal_from_height(h, strength=2.6):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    nz = np.ones_like(h)
    ln = np.sqrt(dx * dx + dy * dy + nz * nz)
    return np.stack([-dx / ln, -dy / ln, nz / ln], axis=-1)


def save_rgb(arr, name):
    arr = np.clip(arr, 0.0, 1.0)
    Image.fromarray((arr * 255.0 + 0.5).astype(np.uint8), "RGB").save(
        os.path.join(OUT, name), quality=94, subsampling=1)


def save_normal(h, name, strength):
    n = normal_from_height(h, strength)
    save_rgb(n * 0.5 + 0.5, name)


def draw_cracks(img, count=5, width=(1, 2), color=(28, 28, 30), alpha=110):
    """Draw wrapping hairline cracks on a 3x3 tiled copy, then crop the middle."""
    size = img.width
    big = Image.new("RGBA", (size * 3, size * 3), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    for _ in range(count):
        x = RNG.uniform(0, size * 3)
        y = RNG.uniform(0, size * 3)
        pts = [(x, y)]
        ang = RNG.uniform(0, np.pi * 2)
        for _step in range(int(RNG.integers(25, 70))):
            ang += RNG.normal(0, 0.35)
            step = RNG.uniform(3, 9)
            x += np.cos(ang) * step
            y += np.sin(ang) * step
            pts.append((x, y))
        d.line(pts, fill=(*color, int(alpha)), width=int(RNG.integers(width[0], width[1] + 1)))
        for _ in range(3):
            sx, sy = pts[int(RNG.integers(2, len(pts) - 2))]
            d.line([(sx, sy), (sx + RNG.uniform(-22, 22), sy + RNG.uniform(-22, 22))],
                   fill=(*color, int(alpha * 0.7)), width=1)
    big = big.crop((size, size, size * 2, size * 2))
    img.alpha_composite(big)


def asphalt():
    size = 1024
    coarse = periodic_noise(size, 5, 4, 0.58)
    mid = periodic_noise(size, 22, 3, 0.55)
    fine = periodic_noise(size, 90, 2, 0.5)
    g = grain(size, 0.022)
    base = 0.205 + coarse * 0.075 + mid * 0.045 + fine * 0.018 + g
    # aggregate chips; sparse light and dark stones
    chips = np.zeros((size, size), np.float32)
    for _ in range(9000):
        x = int(RNG.integers(0, size)); y = int(RNG.integers(0, size))
        r = int(RNG.integers(1, 3))
        v = RNG.uniform(-0.055, 0.075)
        yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
        m = (xx * xx + yy * yy) <= r * r
        ys = (y + yy[m]) % size
        xs = (x + xx[m]) % size
        chips[ys, xs] += v
    base += chips
    rgb = np.stack([base * 0.96, base * 0.965, base * 1.0], axis=-1)
    # slight warm patches where the sun has bleached the surface
    warm = periodic_noise(size, 3, 2, 0.5)
    rgb += (warm[..., None] - 0.5) * np.array([0.018, 0.015, 0.009], np.float32)
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
    draw_cracks(img, count=7, width=(1, 2), color=(24, 24, 25), alpha=115)
    img.convert("RGB").save(os.path.join(OUT, "asphalt_hq.jpg"), quality=94, subsampling=1)
    h = coarse * 0.5 + mid * 0.33 + fine * 0.22 + grain(size, 0.05) * 0.35
    save_normal(h, "asphalt_hq_n.jpg", 1.9)
    print("asphalt_hq.jpg + normal")


def concrete():
    size = 1024
    coarse = periodic_noise(size, 4, 4, 0.60)
    mid = periodic_noise(size, 16, 3, 0.55)
    fine = periodic_noise(size, 70, 2, 0.5)
    base = 0.56 + coarse * 0.055 + mid * 0.035 + fine * 0.014 + grain(size, 0.020)
    # larger damp / oil shadows, softly blurred
    stains = np.zeros((size, size), np.float32)
    stain_img = Image.new("L", (size * 3, size * 3), 0)
    d = ImageDraw.Draw(stain_img)
    for _ in range(20):
        cx = RNG.uniform(0, size * 3); cy = RNG.uniform(0, size * 3)
        rx = RNG.uniform(15, 95); ry = RNG.uniform(10, 70)
        d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=int(RNG.uniform(30, 85)))
    stain_img = stain_img.filter(ImageFilter.GaussianBlur(16))
    stains = np.asarray(stain_img.crop((size, size, size * 2, size * 2))).astype(np.float32) / 255.0
    base -= stains * 0.055
    rgb = np.stack([base * 1.00, base * 1.005, base * 0.97], axis=-1)
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
    draw_cracks(img, count=4, width=(1, 1), color=(45, 45, 44), alpha=95)
    img.convert("RGB").save(os.path.join(OUT, "concrete_hq.jpg"), quality=94, subsampling=1)
    h = coarse * 0.35 + mid * 0.3 + fine * 0.25 + grain(size, 0.03) * 0.2
    save_normal(h, "concrete_hq_n.jpg", 1.2)
    print("concrete_hq.jpg + normal")


def dirt():
    size = 1024
    coarse = periodic_noise(size, 6, 4, 0.6)
    mid = periodic_noise(size, 30, 3, 0.55)
    fine = periodic_noise(size, 110, 2, 0.5)
    base = 0.40 + coarse * 0.09 + mid * 0.06 + fine * 0.025 + grain(size, 0.028)
    # dry gravel: pebbles with a light top and dark lower edge
    pebble = np.zeros((size, size), np.float32)
    for _ in range(26000):
        x = int(RNG.integers(0, size)); y = int(RNG.integers(0, size))
        rx = int(RNG.integers(1, 4)); ry = int(RNG.integers(1, 3))
        v = RNG.uniform(-0.09, 0.11)
        yy, xx = np.mgrid[-ry:ry + 1, -rx:rx + 1]
        m = (xx * xx) / max(rx * rx, 1) + (yy * yy) / max(ry * ry, 1) <= 1
        ys = (y + yy[m]) % size
        xs = (x + xx[m]) % size
        pebble[ys, xs] += v
    base += pebble
    rgb = np.stack([base * 1.03, base * 0.97, base * 0.88], axis=-1)
    warm = periodic_noise(size, 3, 2, 0.5)
    rgb += (warm[..., None] - 0.5) * np.array([0.03, 0.02, 0.005], np.float32)
    save_rgb(rgb, "dirt_hq.jpg")
    print("dirt_hq.jpg")


def grass():
    size = 1024
    coarse = periodic_noise(size, 7, 4, 0.6)
    mid = periodic_noise(size, 34, 3, 0.55)
    fine = periodic_noise(size, 130, 2, 0.5)
    base = 0.22 + coarse * 0.09 + mid * 0.055 + fine * 0.03 + grain(size, 0.03)
    # straw-coloured dry patches in a Texas autumn field
    dry = periodic_noise(size, 4, 2, 0.5)
    r = base * 1.25 + (dry - 0.5) * 0.10
    g = base * 1.28
    b = base * 0.78
    rgb = np.stack([r, g, b], axis=-1)
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
    # fine vertical blade strokes
    d = ImageDraw.Draw(img)
    for _ in range(9000):
        x = RNG.uniform(0, size); y = RNG.uniform(0, size)
        ln = RNG.uniform(2, 7)
        col = (int(RNG.uniform(45, 95)), int(RNG.uniform(65, 120)), int(RNG.uniform(30, 65)), int(RNG.uniform(30, 90)))
        d.line([(x, y), (x + RNG.uniform(-1.2, 1.2), y - ln)], fill=col, width=1)
    img.convert("RGB").save(os.path.join(OUT, "grass_hq.jpg"), quality=93, subsampling=1)
    print("grass_hq.jpg")


def stains():
    size = 512
    # Oil / diesel stain: irregular overlapping blobs with a soft edge.
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    cx, cy = size // 2, size // 2
    for _ in range(9):
        rx = RNG.uniform(35, 135); ry = RNG.uniform(25, 90)
        ox = RNG.normal(0, 28); oy = RNG.normal(0, 22)
        ang = RNG.uniform(0, 180)
        box = (cx + ox - rx, cy + oy - ry, cx + ox + rx, cy + oy + ry)
        d.ellipse(box, fill=int(RNG.uniform(85, 170)))
    mask = mask.filter(ImageFilter.GaussianBlur(14))
    mask = mask.point(lambda v: int(min(255, v * 1.25)))
    arr = np.zeros((size, size, 4), np.uint8)
    arr[..., 0] = 24; arr[..., 1] = 22; arr[..., 2] = 18
    arr[..., 3] = np.asarray(mask)
    Image.fromarray(arr, "RGBA").save(os.path.join(OUT, "oil_stain.png"))

    # Two tyre streaks for a lane, with a little tread breakup.
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for x in (128, 384):
        for y in range(0, size, 5):
            a = int(RNG.uniform(45, 105))
            wob = int(RNG.normal(0, 1.2))
            d.rectangle((x - 30 + wob, y, x + 30 + wob, y + 4), fill=(18, 18, 18, a))
        for _ in range(45):
            yy = RNG.uniform(0, size)
            d.rectangle((x - 31, yy, x + 31, yy + RNG.uniform(1, 3)), fill=(12, 12, 12, int(RNG.uniform(50, 120))))
    im = im.filter(ImageFilter.GaussianBlur(1.4))
    im.save(os.path.join(OUT, "tire_marks.png"))
    print("oil_stain.png, tire_marks.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    asphalt()
    concrete()
    dirt()
    grass()
    stains()
