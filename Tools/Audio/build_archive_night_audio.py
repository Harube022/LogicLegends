"""Original bounded night ambience for Open Archive; requires only NumPy.

Renders exactly two mono seamless-loop assets. Filtered deterministic noise and
weak damped resonances make a low wind/rumble bed and spaced timber/stone creaks
with distant non-verbal breaths. No recordings, speech, attacks or stingers.
"""
from pathlib import Path
import hashlib
import json
import wave

import numpy as np

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir())
OUTPUT = ROOT / "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Atmosphere/Audio"
RATE = 22050
RNG = np.random.default_rng(291061)


def band_noise(count, low, high):
    frequency = np.fft.rfftfreq(count, 1 / RATE)
    response = 1 / np.sqrt(1 + (frequency / high) ** 8)
    response *= 1 / np.sqrt(1 + (low / np.maximum(frequency, 0.01)) ** 8)
    result = np.fft.irfft(np.fft.rfft(RNG.normal(size=count)) * response, n=count)
    return result / max(0.001, np.std(result))


def envelope(t, attack, release):
    duration = len(t) / RATE
    return np.sin(np.clip(t / attack, 0, 1) * np.pi / 2) ** 2 * np.sin(np.clip((duration - t) / release, 0, 1) * np.pi / 2) ** 2


def add(destination, at, sound, gain):
    start = round(at * RATE)
    destination[start:start + len(sound)] += sound * gain


def creak(duration, pitch):
    t = np.arange(round(duration * RATE)) / RATE
    # Inharmonic wood/stone friction, gradual onset rather than a false attack.
    phase = 2 * np.pi * np.cumsum(pitch * (1 - 0.16 * t / duration) * (1 + 0.012 * np.sin(t * 23))) / RATE
    friction = band_noise(len(t), 180, 1800)
    grain = 0.65 + 0.35 * np.sin(t * 19 + np.sin(t * 7)) ** 2
    resonance = np.sin(phase) * 0.25 + np.sin(phase * 1.57) * 0.12 + np.sin(phase * 2.31) * 0.08
    return (friction * 0.7 + resonance) * grain * envelope(t, 0.17, 0.33)


def distant_breath(duration):
    t = np.arange(round(duration * RATE)) / RATE
    phase = 2 * np.pi * np.cumsum(77 - 10 * t / duration + 1.4 * np.sin(t * 4)) / RATE
    throat = sum(np.sin(phase * harmonic + harmonic * 0.4) / harmonic ** 1.2 for harmonic in range(1, 6))
    # Noise dominates: distant non-word texture, never the guardian's real roar.
    return (band_noise(len(t), 85, 680) * 0.82 + throat * 0.18) * envelope(t, 0.45, 0.65)


def write(name, data, peak_db, guid, design):
    data -= data.mean()
    data *= 10 ** (peak_db / 20) / np.max(np.abs(data))
    pcm = np.round(data * 32767).astype("<i2")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    path = OUTPUT / (name + ".wav")
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 6
  defaultSettings:
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.65
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 1
  userData: Original deterministic night ambience; Tools/Audio/build_archive_night_audio.py
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")
    restored = pcm.astype(np.float64) / 32768
    return {"asset": str(path.relative_to(ROOT)).replace("\\", "/"), "durationSeconds": len(pcm) / RATE,
            "sampleRate": RATE, "channels": 1, "bitsPerSample": 16,
            "peakDbFS": float(20 * np.log10(np.max(np.abs(restored)))),
            "rmsDbFS": float(20 * np.log10(np.sqrt(np.mean(restored ** 2)))),
            "clippedSamples": int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32767)),
            "loopBoundaryJump": float(abs(restored[-1] - restored[0])), "sizeBytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "design": design}


def main():
    t = np.arange(RATE * 24) / RATE
    # All envelopes complete whole cycles; FFT noise is periodic at this length.
    gust = 0.74 + 0.16 * np.sin(2 * np.pi * t / 12 + 0.4) + 0.10 * np.sin(2 * np.pi * t / 24 + 1.1)
    wind = band_noise(len(t), 190, 2100) * gust * 0.58
    wind += band_noise(len(t), 65, 220) * (0.85 + 0.15 * np.sin(2 * np.pi * t / 8)) * 0.25
    wind += band_noise(len(t), 340, 1100) * (0.7 + 0.3 * np.sin(2 * np.pi * t / 6 + 2)) * 0.12
    # A gentle 140 ms gust lull makes the PCM endpoints exactly zero too, so
    # streaming decoder loop boundaries cannot introduce a pressure click.
    wind *= np.minimum(1, t / 0.14) * np.minimum(1, (24 - t) / 0.14)
    wind[0] = wind[-1] = 0
    results = [write("Archive_Night_Ominous_Wind_Loop", wind, -8, "c0f952946a6146638b5c41d69fe0dbbf",
                     "Seamless 24-second soft wind and low rumble; no sub-only tone, rise or attack stinger.")]
    details = np.zeros(RATE * 36)
    for at, duration, pitch, gain in ((3.2, 0.95, 238, 0.8), (13.8, 1.2, 181, 0.7), (26.1, 1.0, 284, 0.65)):
        add(details, at, creak(duration, pitch), gain)
    for at, duration, gain in ((8.5, 1.8, 0.42), (20.4, 2.1, 0.38)):
        add(details, at, distant_breath(duration), gain)
    results.append(write("Archive_Night_Distant_Creaks_Loop", details, -12, "50f58529a1834ba8a238aa3e6adbe680",
                         "Seamless 36-second sparse creaks at 3.2/13.8/26.1s and distant non-word breaths at 8.5/20.4s. Silent loop boundary; no close attack or jump-scare imitation."))
    report = ROOT / "Temp/ArchiveNightAtmosphere/AudioAnalysis.json"
    report.parent.mkdir(parents=True, exist_ok=True)
    result = {"provenance": "Entirely original deterministic DSP; no source recordings or external service.",
              "runtimeVoices": "Two existing mono looping ambience voices plus the existing entrance roar source; all SFX-mixer routed.",
              "assets": results}
    report.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
