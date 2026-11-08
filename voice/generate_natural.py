#!/usr/bin/env python3
"""Generate the game's dialogue with Microsoft Edge neural voices.

The shipped voices were made with macOS `say` (Reed/Daniel/Samantha), which is
why the dispatcher sounded like a 1990s screen reader.  Edge's neural voices are
real neural TTS; a light telephone band and gentle compression make the dispatch
call sit in the phone UI without turning it into a robot.

Run with the project venv:
    ./.tts-venv/bin/python voice/generate_natural.py
"""

import asyncio
import json
import pathlib
import subprocess

import edge_tts
import numpy as np

HERE = pathlib.Path(__file__).parent
SR = 44100

# id, voice, rate, pitch, style, spoken text
LINES = [
    ("dispatch_01", "en-US-AndrewNeural", "-4%", "-2Hz", "radio",
     "Morning, driver. It's Mike. You awake? I've got a loaded fifty-three footer out of the Amazon fulfillment center in Dallas. Good paying run."),
    ("dispatch_02", "en-US-AndrewNeural", "-4%", "-2Hz", "radio",
     "Pickup is the Amazon fulfillment center, four eight zero zero Industrial Boulevard, Dallas. Appointment is seven thirty. They charge for no-shows, so don't be late."),
    ("dispatch_03", "en-US-AndrewNeural", "-4%", "-2Hz", "radio",
     "Delivery is the Amazon delivery station in Houston. Appointment is fourteen hundred. It's about two hundred forty miles."),
    ("dispatch_04", "en-US-AndrewNeural", "-4%", "-2Hz", "radio",
     "Fifty-three foot dry container, thirty-eight thousand five hundred pounds, palletized retail. Rate is twenty-four fifty all in. Seal number is four eight two nine one three."),
    ("dispatch_05", "en-US-AndrewNeural", "-4%", "-2Hz", "radio",
     "I'm sending the load screenshot to your phone now. Open Relay, verify the appointment and the seal, then call me if anything doesn't match. Wake up and get rolling."),
    ("guard_01", "en-US-ChristopherNeural", "-3%", "-3Hz", "clean",
     "Morning. I need your appointment time, the shipper name, and the seal number before I raise this arm."),
    ("guard_02", "en-US-ChristopherNeural", "-3%", "-3Hz", "clean",
     "Door fourteen. Safety vest on. Pull to the yellow line and wait for the dock light."),
    ("receiver_01", "en-US-JennyNeural", "-2%", "+0Hz", "clean",
     "Back into dock fourteen. Chock your wheels. We'll unload from there."),
    ("receiver_02", "en-US-JennyNeural", "-2%", "+0Hz", "clean",
     "Six pallets. Seal intact. Sign here, and I'll get you a copy of the B O L."),
    ("coach_01", "en-US-AvaNeural", "-2%", "+0Hz", "clean",
     "Before you move, check your mirrors, your fifth wheel, and your air lines. A proper walk-around takes two minutes. A breakdown takes two days."),
    ("alert_01", "en-US-AvaNeural", "+0%", "+0Hz", "clean",
     "New load posted. Dallas to Houston. Fifty-three foot container. Tap to review."),
]

FILTERS = {
    # Wide enough to keep the voice human; narrow enough to read as a phone call.
    "radio": "highpass=f=95,lowpass=f=8000,acompressor=threshold=-18dB:ratio=2.5:attack=8:release=120",
    "clean": "highpass=f=75,lowpass=f=11000,acompressor=threshold=-20dB:ratio=2:attack=8:release=140",
}


def decode(path):
    p = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
        capture_output=True,
        check=True,
    )
    return np.frombuffer(p.stdout, dtype="<f4").astype(np.float64)


def encode(path, x):
    x = x - float(np.mean(x))
    peak = float(np.max(np.abs(x)))
    if peak > 1e-9:
        x = x * (0.82 / peak)
    raw = np.clip(x, -1.0, 1.0)
    tmp = path.with_suffix(".f32")
    tmp.write_bytes(raw.astype("<f4").tobytes())
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", str(tmp),
         "-codec:a", "libmp3lame", "-b:a", "128k", str(path)],
        check=True,
    )
    tmp.unlink()
    return float(np.sqrt(np.mean(raw * raw))), float(np.max(np.abs(raw)))


def process(src, dst, style):
    p = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(src), "-af", FILTERS[style], "-ar", str(SR), "-ac", "1", "-f", "f32le", "-"],
        capture_output=True,
        check=True,
    )
    x = np.frombuffer(p.stdout, dtype="<f4").astype(np.float64)
    return encode(dst, x)


async def main():
    manifest = []
    for name, voice, rate, pitch, style, text in LINES:
        raw = HERE / f".{name}.edge.mp3"
        dst = HERE / f"{name}.mp3"
        tts = edge_tts.Communicate(text, voice, rate=rate, pitch=pitch)
        await tts.save(str(raw))
        rms, peak = process(raw, dst, style)
        raw.unlink()
        out = subprocess.check_output(
            ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(dst)],
            text=True,
        ).strip()
        manifest.append({
            "id": name,
            "engine": "edge-tts",
            "voice": voice,
            "rate": rate,
            "pitch": pitch,
            "style": style,
            "text": text,
            "file": dst.name,
            "duration": round(float(out), 2),
            "rms": round(rms, 3),
            "peak": round(peak, 3),
        })
        print(f"{name:12s} {out:>6}s  {voice}  rms={rms:.3f} peak={peak:.3f}")

    (HERE / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    asyncio.run(main())
