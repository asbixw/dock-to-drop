"""Make the silver Rig_Logistics tractor read as silver paint instead of near-black metal.

usage: python3 -P fix_rig_logistics_pbr.py <original SR_Log_Material_diff.jpg> <original SR_Log_Material_ms.png>

The Sketchfab texture marks ~64% of the body fully metallic over a dark (sRGB ~84) albedo,
so under the dawn sky the cab reflects almost nothing and looks black. This lifts the albedo
where the source was metallic (silver paint, chrome) and caps metallic at 0.45. Always run it
on the originals (kept in backups/2026-11-06-ground-rigs/), never on its own output.
"""
import os
import sys
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures", "Props")
diff = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.float32)
ms = np.asarray(Image.open(sys.argv[2]).convert("RGBA")).astype(np.float32)
m = ms[..., 0:1] / 255.0
if m.shape[:2] != diff.shape[:2]:
    m = np.asarray(Image.fromarray((m[..., 0] * 255).astype(np.uint8)).resize(diff.shape[1::-1])).astype(np.float32)[..., None] / 255.0
diff = diff * (1.0 + 1.05 * m)
ms[..., 0] *= 0.45
Image.fromarray(np.clip(diff, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "SR_Log_Material_diff.jpg"), quality=93)
Image.fromarray(np.clip(ms, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "SR_Log_Material_ms.png"))
print("diff mean", diff.reshape(-1, 3).mean(0).round(1), "metal mean", round(float(ms[..., 0].mean()), 1))
