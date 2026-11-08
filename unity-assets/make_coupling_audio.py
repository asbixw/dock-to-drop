# Synthesizes the trailer-coupling sounds into unity-assets/Audio (no downloads):
#   fifth_wheel_lock  heavy clunk of the jaws closing on the kingpin, then the lock bar
#   glad_hand         metal latch click and a short burst of air as a glad hand seals
#   crank_ratchet     about three seconds of landing-gear crank ratchet
#   trailer_air       valve pop and the long hiss of trailer air filling the brakes
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Audio')
rng = np.random.default_rng(7)


def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def smooth(x, n):
    k = np.ones(n) / n
    return np.convolve(x, k, mode='same')


def band_noise(dur, lo_n, hi_n):
    """White noise smoothed by hi_n samples minus a wider smooth (crude band-pass)."""
    w = rng.uniform(-1, 1, int(SR * dur))
    a = smooth(w, hi_n)
    return a - smooth(a, lo_n) if lo_n else a


def ring(freqs, dur, decay):
    t = t_axis(dur)
    out = np.zeros_like(t)
    for i, f in enumerate(freqs):
        out += np.sin(2 * np.pi * f * t + i) * np.exp(-t * decay * (1 + 0.35 * i)) / (1 + i * 0.6)
    return out


def place(buf, clip, at):
    i = int(at * SR)
    n = min(len(clip), len(buf) - i)
    buf[i:i + n] += clip[:n]


def write(name, x, peak=0.88):
    x = x / max(1e-6, np.max(np.abs(x))) * peak
    fade = min(len(x), int(0.02 * SR))
    x[-fade:] *= np.linspace(1, 0, fade)
    data = (np.clip(x, -1, 1) * 32767).astype('<i2')
    with wave.open(os.path.join(OUT, name + '.wav'), 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(name, round(len(x) / SR, 2))


def click(dur=0.012, bright=3):
    c = band_noise(dur, 0, bright) * np.exp(-t_axis(dur) * 260)
    return c


def fifth_wheel_lock():
    buf = np.zeros(int(SR * 1.15))
    t = t_axis(0.5)
    thud = np.sin(2 * np.pi * (62 - 18 * t) * t) * np.exp(-t * 16) * 1.0
    body = band_noise(0.25, 60, 8) * np.exp(-t_axis(0.25) * 30) * 0.7
    place(buf, thud, 0.0)
    place(buf, body, 0.0)
    place(buf, click(0.014, 2) * 0.9, 0.0)
    place(buf, ring([410, 1135, 2290, 3420], 1.0, 7.5) * 0.45, 0.004)
    # the lock bar snaps across a beat later
    place(buf, click(0.010, 2) * 0.55, 0.13)
    place(buf, ring([980, 2650], 0.5, 18) * 0.22, 0.132)
    return buf


def glad_hand():
    buf = np.zeros(int(SR * 0.95))
    place(buf, click(0.010, 2) * 0.8, 0.02)
    place(buf, ring([1850, 3900], 0.25, 30) * 0.25, 0.021)
    hiss = band_noise(0.55, 6, 1) * np.exp(-t_axis(0.55) * 7) * 0.55
    place(buf, hiss, 0.05)
    return buf


def crank_ratchet():
    dur = 3.25
    buf = np.zeros(int(SR * dur))
    at = 0.05
    k = 0
    while at < dur - 0.08:
        place(buf, click(0.008, 2) * (0.55 + 0.25 * rng.random()), at)
        place(buf, ring([2350 + 120 * rng.random(), 4100], 0.06, 70) * 0.18, at)
        # a heavier clack every turn of the handle
        if k % 4 == 0:
            place(buf, ring([620, 1500], 0.12, 35) * 0.25, at)
        at += 0.105 + 0.02 * rng.random()
        k += 1
    grind = band_noise(dur, 40, 6) * 0.10 * (0.6 + 0.4 * np.sin(2 * np.pi * 2.3 * t_axis(dur)))
    return buf + grind


def trailer_air():
    dur = 1.7
    buf = np.zeros(int(SR * dur))
    place(buf, click(0.012, 2) * 0.7, 0.0)
    place(buf, ring([720, 1600], 0.15, 40) * 0.25, 0.0)
    t = t_axis(dur - 0.03)
    env = np.minimum(1, t / 0.06) * np.exp(-t * 1.6)
    place(buf, band_noise(dur - 0.03, 5, 1) * env * 0.75, 0.03)
    return buf


if __name__ == '__main__':
    write('fifth_wheel_lock', fifth_wheel_lock())
    write('glad_hand', glad_hand())
    write('crank_ratchet', crank_ratchet(), 0.7)
    write('trailer_air', trailer_air(), 0.75)
