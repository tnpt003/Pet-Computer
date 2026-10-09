"""Prepare the checked-in CC0 recordings. Runtime playback needs no Python/ffmpeg."""
import array
import hashlib
import json
import math
from pathlib import Path
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / ".tools/cat-audio-sources"
FFMPEG = ROOT / ".tools/ffmpeg/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"
OUTPUT = ROOT / "src/DesktopPet/Assets/Sounds"
OUTPUT.mkdir(parents=True, exist_ok=True)
clips = [
    ("mew-soft", "cat_softmew", 0, None),
    ("mew-food", "cat_mewfood", 0, None),
    ("mew-purr", "cat_mewpurr", 0, None),
    ("mew-purr2", "cat_mewpurr2", 0, None),
    ("purr-active-a", "cat_purractive_loop", 0.3, 1.8),
    ("purr-active-b", "cat_purractive_loop", 3.0, 1.8),
    ("purr-sleep-a", "cat_purrsleepy_loop", 0.3, 1.8),
    ("purr-sleep-b", "cat_purrsleepy_loop", 2.9, 1.8),
]
manifest = []
for name, original, start, duration in clips:
    path = SOURCE / (original + ".wav")
    with wave.open(str(path), "rb") as source:
        expected = source.getnframes() * source.getsampwidth() * source.getnchannels()
        if len(source.readframes(source.getnframes())) != expected:
            raise ValueError(f"Incomplete download: {path}")
    filters = "highpass=f=35,lowpass=f=6500,afftdn=nr=6:nf=-40"
    args = [str(FFMPEG), "-hide_banner", "-loglevel", "error", "-ss", str(start), "-i", str(path)]
    if duration is not None:
        args += ["-t", str(duration)]
    args += ["-af", filters, "-ac", "1", "-ar", "22050", "-f", "s16le", "pipe:1"]
    pcm = array.array("h", subprocess.check_output(args))
    rms = math.sqrt(sum(x * x for x in pcm) / len(pcm))
    gain = min(1.2, 2000 / max(1, rms), 10000 / max(1, max(abs(x) for x in pcm)))
    for i, sample in enumerate(pcm):
        fade = min(1, i / (22050 * 0.025), (len(pcm) - 1 - i) / (22050 * 0.1))
        pcm[i] = round(sample * gain * fade)
    target = OUTPUT / (name + ".wav")
    with wave.open(str(target), "wb") as output:
        output.setparams((1, 2, 22050, 0, "NONE", "not compressed"))
        output.writeframes(pcm.tobytes())
    manifest.append(dict(file=target.name, source="https://opengameart.org/sites/default/files/" + path.name,
        author="Kerzoven", license="CC0-1.0", source_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        start_seconds=start, duration_seconds=round(len(pcm) / 22050, 4),
        output_sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    print(target.name, round(len(pcm) / 22050, 2), "seconds")
(OUTPUT / "sources.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
