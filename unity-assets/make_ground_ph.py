"""Ground textures and decals for the travel center and the yard (2026-11-06 ground pass).

usage: python3 -P make_ground_ph.py <dir with the Poly Haven 2k jpgs>

Inputs (Poly Haven, CC0): asphalt_07_{diff,nor_gl}_2k.jpg, concrete_floor_worn_001_{diff,nor_gl}_2k.jpg.
Writes into unity-assets/Textures:
  ph_asphalt.jpg / ph_asphalt_nor.jpg    neutral grey asphalt (the source is warm brown)
  ph_concrete.jpg / ph_concrete_nor.jpg  lighter, higher-contrast concrete (the source is dark and flat)
  crack_a.png, asphalt_patch.png, ground_grime.png   black RGB decals; alpha is the darkening
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC = sys.argv[1]
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures")
RNG = np.random.default_rng(20261106)


def regrade(src, dst, target, contrast):
    a = np.asarray(Image.open(os.path.join(SRC, src)).convert("RGB")).astype(np.float32)
    lum = a.mean(axis=2, keepdims=True)
    m = lum.mean()
    # keep a little of the source tint, mostly neutral grey around the target mean
    grey = (lum - m) * contrast + m
    a = 0.25 * a + 0.75 * grey
    a = a / a.reshape(-1, 3).mean(0) * np.array(target, np.float32)
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(os.path.join(OUT, dst), quality=92)
    print(dst, a.reshape(-1, 3).mean(0).round(1))


def copy_normal(src, dst):
    Image.open(os.path.join(SRC, src)).convert("RGB").save(os.path.join(OUT, dst), quality=94)


def blur_noise(size, sigma):
    n = Image.fromarray((RNG.random((size, size)) * 255).astype(np.uint8))
    n = np.asarray(n.filter(ImageFilter.GaussianBlur(sigma))).astype(np.float32)
    n -= n.min(); n /= max(n.max(), 1e-6)
    return n


def crack_decal(path, size=1024):
    """Branching hairline cracks with a soft halo, plus a couple of tar-sealed runs."""
    alpha = Image.new("L", (size, size), 0)
    halo = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(alpha); h = ImageDraw.Draw(halo)

    def walk(x, y, ang, steps, width, depth):
        pts = [(x, y)]
        for _ in range(steps):
            ang += RNG.normal(0, 0.16)
            step = RNG.uniform(6, 14)
            x += np.cos(ang) * step; y += np.sin(ang) * step
            if not (40 < x < size - 40 and 40 < y < size - 40):
                break
            pts.append((x, y))
            if depth < 3 and RNG.random() < 0.05:
                walk(x, y, ang + RNG.choice([-1, 1]) * RNG.uniform(0.5, 1.2), int(steps * 0.45), max(1, width - 1), depth + 1)
        if len(pts) > 1:
            h.line(pts, fill=90, width=width * 7)
            d.line(pts, fill=235, width=width)

    for k in range(4):
        a0 = k * np.pi / 2 + RNG.normal(0, 0.4)
        walk(size / 2 + RNG.normal(0, 60), size / 2 + RNG.normal(0, 60), a0, 160, 3, 0)
    halo = halo.filter(ImageFilter.GaussianBlur(9))
    a = np.maximum(np.asarray(alpha.filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32),
                   np.asarray(halo).astype(np.float32) * 0.55)
    # fade toward the quad edges so no square outline shows
    yy, xx = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    edge = np.clip((1 - np.maximum(abs(xx), abs(yy))) / 0.12, 0, 1)
    a *= edge
    rgba = np.zeros((size, size, 4), np.uint8); rgba[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path)


def patch_decal(path, size=512):
    """A newer, darker asphalt patch with ragged edges and a sealed seam."""
    n = blur_noise(size, 6)
    yy, xx = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    box = np.maximum(abs(xx) / 0.86, abs(yy) / 0.80) + (n - 0.5) * 0.10
    inside = np.clip((1 - box) / 0.05, 0, 1)
    seam = np.clip(1 - abs(1 - box) / 0.035, 0, 1)
    grain = blur_noise(size, 1.2)
    a = inside * (95 + grain * 40) + seam * 120
    rgba = np.zeros((size, size, 4), np.uint8); rgba[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path)


def grime_decal(path, size=1024):
    """Very broad, soft blotches that break up texture tiling across a whole lot."""
    def up(res):
        g = Image.fromarray((RNG.random((res, res)) * 255).astype(np.uint8))
        return np.asarray(g.resize((size, size), Image.BICUBIC)).astype(np.float32) / 255
    n = 0.55 * up(7) + 0.3 * up(18) + 0.15 * up(60)
    n = (n - n.min()) / (n.max() - n.min())
    a = np.clip((n - 0.5) / 0.25, 0, 1) ** 1.3 * 105
    yy, xx = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    a *= np.clip((1 - np.maximum(abs(xx), abs(yy))) / 0.06, 0, 1)
    rgba = np.zeros((size, size, 4), np.uint8); rgba[..., 3] = a.astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path)


def wet_decal(path, size=512):
    """A drying puddle: darker, slightly ragged ellipse with a wetter core."""
    def up(res):
        g = Image.fromarray((RNG.random((res, res)) * 255).astype(np.uint8))
        return np.asarray(g.resize((size, size), Image.BICUBIC)).astype(np.float32) / 255
    yy, xx = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2) + (0.6 * up(6) + 0.4 * up(14) - 0.5) * 0.35
    a = np.clip((0.82 - r) / 0.25, 0, 1) * 120 + np.clip((0.45 - r) / 0.2, 0, 1) * 55
    rgba = np.zeros((size, size, 4), np.uint8); rgba[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path)


wet_decal(os.path.join(OUT, "wet_patch.png"))
regrade("asphalt_07_diff_2k.jpg", "ph_asphalt.jpg", (92, 91, 90), 1.25)
copy_normal("asphalt_07_nor_gl_2k.jpg", "ph_asphalt_nor.jpg")
regrade("concrete_floor_worn_001_diff_2k.jpg", "ph_concrete.jpg", (150, 149, 145), 2.6)
copy_normal("concrete_floor_worn_001_nor_gl_2k.jpg", "ph_concrete_nor.jpg")
crack_decal(os.path.join(OUT, "crack_a.png"))
patch_decal(os.path.join(OUT, "asphalt_patch.png"))
grime_decal(os.path.join(OUT, "ground_grime.png"))
print("done")
