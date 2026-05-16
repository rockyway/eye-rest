#!/usr/bin/env python3
"""
BL-002: One-off WAV synthesis for the four bundled default popup sounds.
Generates 44.1kHz / 16-bit / mono PCM WAVs into EyeRest.UI/Assets/Sounds/.

Run once from the repo root:
    python3 scripts/generate-default-sounds.py

The WAVs themselves are checked into the repo; this script is only re-run if
the sound design changes. Python is used rather than dotnet-script to avoid a
global tool install dependency.
"""
import math
import os
import struct
import wave

SAMPLE_RATE = 44100
OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "EyeRest.UI", "Assets", "Sounds",
)


def write_wav(path: str, samples: list[float]) -> None:
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        frames = b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples
        )
        w.writeframes(frames)


def tone(freq: float, duration: float, attack: float = 0.01, release: float = 0.05,
         amplitude: float = 0.6) -> list[float]:
    """Sine tone with linear attack/release envelope to avoid clicks."""
    n = int(duration * SAMPLE_RATE)
    out: list[float] = [0.0] * n
    for i in range(n):
        t = i / SAMPLE_RATE
        env = 1.0
        if t < attack:
            env = t / attack
        elif t > duration - release:
            env = max(0.0, (duration - t) / release)
        out[i] = math.sin(2 * math.pi * freq * t) * env * amplitude
    return out


def concat(*parts: list[float]) -> list[float]:
    result: list[float] = []
    for p in parts:
        result.extend(p)
    return result


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)

    # Eye-rest channels intentionally use the platform-native named sounds
    # (NSSound "Glass" / "Tink" on macOS, SystemSounds.Beep on Windows) rather
    # than bundled WAVs — see BundledSoundCache.GetPath for the opt-out logic.
    # The synthesized chimes here sounded less polished than the curated platform
    # sounds for the short eye-rest cue duration. Re-enable by adding the two
    # write_wav calls back AND removing the EyeRestStart/EyeRestEnd null-return
    # in BundledSoundCache.GetPath.

    # Break start: warm single bell (E5) — firmer "stop working" cue
    write_wav(
        os.path.join(OUT_DIR, "break-start.wav"),
        tone(659.25, 1.00, attack=0.005, release=0.40),
    )

    # Break end: energetic three-note rise (E5 → G5 → B5) — back-to-work
    write_wav(
        os.path.join(OUT_DIR, "break-end.wav"),
        concat(tone(659.25, 0.20), tone(783.99, 0.20), tone(987.77, 0.40)),
    )

    print(f"Generated 4 default popup WAVs in {OUT_DIR}")


if __name__ == "__main__":
    main()
