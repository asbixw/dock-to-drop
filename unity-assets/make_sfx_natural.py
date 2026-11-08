#!/usr/bin/env python3
"""Build the game's sound effects from real CC0 field recordings.

The old make_audio.py synthesised every effect from square/saw waves and noise,
which is why the game sounded like a test tone.  This script cuts, filters and
levels real recordings instead.  Sources live in AudioSource/ and are documented
in Audio/CREDITS.md.

Run:  python3 unity-assets/make_sfx_natural.py
"""

import os
import subprocess
import wave

import numpy as np
from scipy.signal import butter, sosfiltfilt

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "AudioSource")
OUT = os.path.join(HERE, "Audio")


def load(name):
    path = os.path.join(SRC, name)
    p = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
        capture_output=True,
        check=True,
    )
    x = np.frombuffer(p.stdout, dtype="<f4").astype(np.float64)
    if not len(x):
        raise RuntimeError("empty audio: " + path)
    return x


def save(name, x, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    x = x - float(np.mean(x))
    m = float(np.max(np.abs(x)))
    if m > 1e-9:
        x = x * (peak / m)
    x = np.clip(x, -1.0, 1.0)
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(np.round(x * 32767.0).astype("<i2").tobytes())
    rms = float(np.sqrt(np.mean(x * x)))
    print(f"{name:22s} {len(x) / SR:6.2f}s  rms={rms:.4f}  peak={np.max(np.abs(x)):.3f}")


def hp(x, hz, order=2):
    sos = butter(order, hz / (SR / 2.0), btype="highpass", output="sos")
    return sosfiltfilt(sos, x, padlen=min(len(x) - 1, 512))


def lp(x, hz, order=2):
    sos = butter(order, hz / (SR / 2.0), btype="lowpass", output="sos")
    return sosfiltfilt(sos, x, padlen=min(len(x) - 1, 512))


def norm(x, rms=None, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    x = x - float(np.mean(x))
    if rms is not None:
        cur = float(np.sqrt(np.mean(x * x)))
        if cur > 1e-9:
            x = x * (rms / cur)
    m = float(np.max(np.abs(x)))
    if m > peak:
        x = x * (peak / m)
    return x


def fade(x, fin=0.0, fout=0.0):
    x = x.copy()
    if fin > 0:
        n = min(len(x), int(fin * SR))
        x[:n] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, n))
    if fout > 0:
        n = min(len(x), int(fout * SR))
        x[-n:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n))
    return x


def resample_len(x, n):
    """Linear resample; used to pitch a slam down without a synthetic source."""
    n = max(2, int(n))
    old = np.arange(len(x), dtype=np.float64)
    new = np.linspace(0.0, len(x) - 1.0, n)
    return np.interp(new, old, x)


def loopify(x, fade_s, equal_power=False):
    """Crossfade the tail into the head so the clip loops without a seam."""
    n = int(fade_s * SR)
    if n <= 0 or n * 2 >= len(x):
        raise ValueError("loop crossfade too long")
    length = len(x) - n
    y = x[:length].copy()
    a = np.linspace(0.0, 1.0, n)
    if equal_power:
        fi, fo = np.sin(a * np.pi / 2.0), np.cos(a * np.pi / 2.0)
    else:
        fi, fo = a, 1.0 - a
    y[:n] = x[:n] * fi + x[length:length + n] * fo
    return y


def build_engine():
    # 637916: steady close-mic semi diesel idle, 48 s, CC0.
    x = load("637916.mp3")
    seg = x[int(10.0 * SR):int(26.0 * SR)]
    seg = hp(seg, 28.0)
    seg = lp(seg, 6500.0)
    seg = loopify(seg, 2.0)
    seg = norm(seg, rms=0.18, peak=0.82)
    save("engine_idle.wav", seg, peak=0.82)


def build_ignition():
    # 478133: Dodge diesel starter crank then catch, CC0.
    x = load("478133.mp3")
    seg = x[int(0.45 * SR):int(2.65 * SR)]
    seg = hp(seg, 30.0)
    seg = lp(seg, 7500.0)
    seg = fade(seg, 0.01, 0.35)
    seg = norm(seg, rms=0.16, peak=0.82)
    save("ignition.wav", seg, peak=0.82)


def build_footsteps():
    # 465299: work boots on concrete, clean individual impacts, CC0.
    # Six different impacts so the walk does not machine-gun one sample.
    x = load("465299.mp3")
    times = [1.68, 2.53, 3.38, 5.05, 7.57, 9.21]
    for i, t in enumerate(times, 1):
        a = int((t - 0.06) * SR)
        b = int((t + 0.34) * SR)
        seg = x[a:b]
        seg = hp(seg, 45.0)
        seg = lp(seg, 9500.0)
        seg = fade(seg, 0.002, 0.12)
        seg = norm(seg, rms=0.16, peak=0.80)
        save(f"footstep_{i}.wav", seg, peak=0.80)


def build_doors():
    # 361823: truck door handle/latch. 450225: clean pickup-door slam.
    latch = load("361823.mp3")
    open_seg = latch[int(0.17 * SR):int(0.46 * SR)]
    open_seg = hp(open_seg, 60.0)
    open_seg = lp(open_seg, 9000.0)
    open_seg = fade(open_seg, 0.003, 0.10)

    # A quiet leather/vinyl creak gives the opening door a little travel.
    creak = load("611460.mp3")[int(2.0 * SR):int(2.55 * SR)]
    creak = hp(creak, 120.0)
    creak = lp(creak, 6000.0)
    creak = norm(creak, rms=0.030, peak=0.35)
    out = np.zeros(int(0.78 * SR))
    out[:len(open_seg)] += open_seg
    start = int(0.18 * SR)
    out[start:start + len(creak)] += creak * np.linspace(1.0, 0.25, len(creak))
    out = fade(out, 0.003, 0.22)
    out = norm(out, rms=0.13, peak=0.82)
    save("door_open.wav", out, peak=0.82)

    # A real slam pitched down 0.82 gives a heavy cab-door thud.
    slam = load("450225.mp3")[int(0.20 * SR):int(0.78 * SR)]
    slam = resample_len(slam, len(slam) / 0.82)
    click = latch[int(0.20 * SR):int(0.34 * SR)]
    click = hp(click, 180.0)
    click = lp(click, 9000.0)
    click = norm(click, rms=0.06, peak=0.40)
    out = slam.copy()
    out[:len(click)] += click[:len(out)]
    out = hp(out, 30.0)
    out = lp(out, 8000.0)
    out = fade(out, 0.002, 0.18)
    out = norm(out, rms=0.22, peak=0.90)
    save("door_close.wav", out, peak=0.90)


def build_air():
    # 504865: loud air-brake release that fades out naturally, CC0.
    x = load("504865.mp3")
    brake = x[int(0.0 * SR):int(2.2 * SR)]
    brake = hp(brake, 35.0)
    brake = lp(brake, 10000.0)
    brake = fade(brake, 0.005, 0.55)
    brake = norm(brake, rms=0.13, peak=0.82)
    save("air_brake.wav", brake, peak=0.82)

    trailer = x[int(0.0 * SR):int(3.8 * SR)]
    trailer = hp(trailer, 35.0)
    trailer = lp(trailer, 10000.0)
    trailer = fade(trailer, 0.005, 0.80)
    trailer = norm(trailer, rms=0.11, peak=0.82)
    save("trailer_air.wav", trailer, peak=0.82)

    # Glad hand: latch click + a short puff of air.
    click = load("361823.mp3")[int(0.18 * SR):int(0.36 * SR)]
    click = hp(click, 180.0)
    click = lp(click, 9000.0)
    click = norm(click, rms=0.10, peak=0.50)
    hiss = x[int(0.0 * SR):int(0.80 * SR)]
    hiss = hp(hiss, 500.0)
    hiss = lp(hiss, 10000.0)
    hiss = norm(hiss, rms=0.045, peak=0.30)
    out = np.zeros(int(0.85 * SR))
    out[:len(click)] += click
    out[:len(hiss)] += hiss
    out = fade(out, 0.002, 0.25)
    out = norm(out, rms=0.10, peak=0.82)
    save("glad_hand.wav", out, peak=0.82)


def build_horn():
    # 131930: single continuous industrial air-horn blast, CC0.
    x = load("131930.mp3")
    seg = x[int(0.30 * SR):int(2.15 * SR)]
    seg = hp(seg, 70.0)
    seg = lp(seg, 7000.0)
    seg = fade(seg, 0.01, 0.12)
    seg = norm(seg, rms=0.20, peak=0.82)
    save("horn.wav", seg, peak=0.82)


def build_gate():
    # 545024: electric sliding gate motor and chain, CC0.
    x = load("545024.mp3")
    seg = x[int(9.0 * SR):int(11.2 * SR)]
    seg = hp(seg, 50.0)
    seg = lp(seg, 10000.0)
    seg = fade(seg, 0.03, 0.40)
    seg = norm(seg, rms=0.09, peak=0.75)
    save("gate_motor.wav", seg, peak=0.75)


def build_shutter():
    # 376205: real DSLR shutter click-clack, CC0.
    x = load("376205.mp3")
    seg = x[int(0.94 * SR):int(1.22 * SR)]
    seg = hp(seg, 200.0)
    seg = lp(seg, 12000.0)
    seg = fade(seg, 0.002, 0.05)
    seg = norm(seg, rms=0.16, peak=0.82)
    save("camera_shutter.wav", seg, peak=0.82)


def build_seat():
    # 611460: leather seat creak, CC0.
    x = load("611460.mp3")
    seg = x[int(2.0 * SR):int(2.9 * SR)]
    seg = hp(seg, 60.0)
    seg = lp(seg, 9000.0)
    seg = fade(seg, 0.01, 0.20)
    seg = norm(seg, rms=0.09, peak=0.72)
    save("seat.wav", seg, peak=0.72)


def build_ring():
    # 138272: generic phone ring, CC0.  Two rings with a natural gap.
    x = load("138272.mp3")
    one = x[int(0.55 * SR):int(2.0 * SR)]
    one = hp(one, 200.0)
    one = lp(one, 8000.0)
    one = fade(one, 0.01, 0.08)
    gap = np.zeros(int(0.35 * SR))
    ring = np.concatenate([one, gap, one])
    ring = fade(ring, 0.005, 0.15)
    ring = norm(ring, rms=0.10, peak=0.72)
    save("ring.wav", ring, peak=0.72)


def build_music():
    # 832628: calm ambient piano loop, CC0.  A 31 s seamless bed.
    x = load("832628.mp3")
    seg = x[int(10.0 * SR):int(44.0 * SR)]
    seg = hp(seg, 30.0)
    seg = lp(seg, 9000.0)
    seg = loopify(seg, 3.0, equal_power=True)
    seg = norm(seg, rms=0.055, peak=0.65)
    save("music_loop.wav", seg, peak=0.65)


def build_fifth_wheel():
    # Heavy steel jaw closing: clean door slam pitched down + a low thump.
    slam = load("450225.mp3")[int(0.22 * SR):int(0.72 * SR)]
    slam = resample_len(slam, len(slam) / 0.80)
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    thump = 0.35 * np.sin(2.0 * np.pi * 55.0 * t) * np.exp(-t * 18.0)
    out = np.zeros(max(len(slam), n))
    out[:len(slam)] += slam
    out[:n] += thump
    out = hp(out, 30.0)
    out = lp(out, 7000.0)
    out = fade(out, 0.002, 0.15)
    out = norm(out, rms=0.20, peak=0.90)
    save("fifth_wheel_lock.wav", out, peak=0.90)


def build_ratchet():
    # 591529: real ratchet wrench clicks, CC0.  Slow it slightly for a
    # landing-gear crank rather than a race mechanic.
    x = load("591529.mp3")
    seg = x[int(0.55 * SR):int(3.30 * SR)]
    # Drop it a little: a landing-gear crank is heavier and slower than a
    # mechanic's 3/8 ratchet.
    seg = resample_len(seg, len(seg) / 0.82)[:int(3.2 * SR)]
    seg = hp(seg, 60.0)
    seg = lp(seg, 6000.0)
    seg = fade(seg, 0.005, 0.15)
    seg = norm(seg, rms=0.10, peak=0.75)
    save("crank_ratchet.wav", seg, peak=0.75)


def main():
    os.makedirs(OUT, exist_ok=True)
    for stale in ("wind_loop.wav", "footstep.wav"):
        p = os.path.join(OUT, stale)
        if os.path.exists(p):
            os.remove(p)
            print("removed stale", stale)
    build_engine()
    build_ignition()
    build_footsteps()
    build_doors()
    build_air()
    build_horn()
    build_gate()
    build_shutter()
    build_seat()
    build_ring()
    build_music()
    build_fifth_wheel()
    build_ratchet()


if __name__ == "__main__":
    main()
