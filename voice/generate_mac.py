# LEGACY: macOS `say` voices produced the robotic dispatcher.
# Use generate_natural.py (Edge neural TTS) instead.
import subprocess, pathlib, json, shutil

LINES = [
    ("dispatch_01", "Reed (English (US))", 172, "radio", "Morning, driver. It's Mike. You awake? I've got a loaded fifty-three footer out of the Amazon fulfillment center in Dallas. Good paying run."),
    ("dispatch_02", "Reed (English (US))", 172, "radio", "Pickup is the Amazon fulfillment center, four eight zero zero Industrial Boulevard, Dallas. Appointment is seven thirty. They charge for no-shows, so don't be late."),
    ("dispatch_03", "Reed (English (US))", 172, "radio", "Delivery is the Amazon delivery station in Houston. Appointment is fourteen hundred. It's about two hundred forty miles."),
    ("dispatch_04", "Reed (English (US))", 172, "radio", "Fifty-three foot dry container, thirty-eight thousand five hundred pounds, palletized retail. Rate is twenty-four fifty all in. Seal number is four eight two nine one three."),
    ("dispatch_05", "Reed (English (US))", 172, "radio", "I'm sending the load screenshot to your phone now. Open Relay, verify the appointment and the seal, then call me if anything doesn't match. Wake up and get rolling."),
    ("guard_01", "Daniel (English (UK))", 178, "clean", "Morning. I need your appointment time, the shipper name, and the seal number before I raise this arm."),
    ("guard_02", "Daniel (English (UK))", 178, "clean", "Door fourteen. Safety vest on. Pull to the yellow line and wait for the dock light."),
    ("receiver_01", "Samantha (English (US))", 182, "clean", "Back into dock fourteen. Chock your wheels. We'll unload from there."),
    ("receiver_02", "Samantha (English (US))", 182, "clean", "Six pallets. Seal intact. Sign here, and I'll get you a copy of the BOL."),
    ("coach_01", "Samantha (English (US))", 176, "clean", "Before you move, check your mirrors, your fifth wheel, and your air lines. A proper walk-around takes two minutes. A breakdown takes two days."),
    ("alert_01", "Samantha (English (US))", 180, "clean", "New load posted. Dallas to Houston. Fifty-three foot container. Tap to review.")
]

base = pathlib.Path(__file__).parent
raw = base / "raw"
raw.mkdir(exist_ok=True)
manifest = []
for name, voice, rate, style, text in LINES:
    aiff = raw / f"{name}.aiff"
    wav = raw / f"{name}.wav"
    mp3 = base / f"{name}.mp3"
    subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", str(aiff), text], check=True)
    subprocess.run(["afconvert", "-f", "WAVE", "-d", "LEI16@44100", str(aiff), str(wav)], check=True)
    if style == "radio":
        af = "highpass=f=260,lowpass=f=3400,acompressor=threshold=-18dB:ratio=5:attack=4:release=70,volume=1.25"
    else:
        af = "highpass=f=90,lowpass=f=10000,acompressor=threshold=-20dB:ratio=2.5:attack=8:release=120,volume=1.1"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-af", af, "-ar", "44100", "-ac", "1", "-b:a", "128k", str(mp3)], check=True)
    # Estimate duration
    out = subprocess.check_output(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(mp3)], text=True).strip()
    manifest.append({"id": name, "voice": voice, "style": style, "text": text, "file": mp3.name, "duration": round(float(out), 2)})
    print(name, mp3.stat().st_size, out)

(base / "manifest.json").write_text(json.dumps(manifest, indent=2))
