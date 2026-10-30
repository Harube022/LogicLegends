"""Render one original dry pneumatic dart-gun report; never touches other assets.

Requires NumPy from the workspace Python runtime. No downloaded samples or kit
recordings are used. The deterministic noise, short air release, damped body
and static mechanical resonances avoid liquid sounds and sci-fi pitch chirps.
"""
from pathlib import Path
import hashlib
import json
import wave

import numpy as np


ROOT = next(p for p in Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir())
OUTPUT = ROOT / "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Rewards/Audio/Archive_Tranquilizer_Dry_Pneumatic_Shot.wav"
RATE = 44100
DURATION = 0.34
RNG = np.random.default_rng(291026)


def band_noise(length, low, high):
    """Smooth spectral limits, no periodic modulation, no musical pitch sweep."""
    frequencies = np.fft.rfftfreq(length, 1 / RATE)
    response = 1 / np.sqrt(1 + (frequencies / high) ** 8)
    response *= 1 / np.sqrt(1 + (low / np.maximum(frequencies, 0.01)) ** 8)
    noise = np.fft.irfft(np.fft.rfft(RNG.normal(size=length)) * response, n=length)
    return noise / max(0.001, np.std(noise))


def click(t, at, gain, decay):
    local = np.maximum(0, t - at)
    envelope = (t >= at) * (1 - np.exp(-local / 0.00022)) * np.exp(-local / decay)
    # Short valve/bolt contacts: mostly broadband, weak rapidly damped metal.
    texture = band_noise(len(t), 1100, 8200)
    metal = sum(np.sin(2 * np.pi * hz * local + phase) * weight for hz, phase, weight in
                ((1463, 0.2, 0.30), (2381, 0.9, 0.22), (3857, 0.4, 0.13)))
    return gain * (texture * 0.86 + metal * 0.14) * envelope


def render():
    t = np.arange(round(RATE * DURATION)) / RATE
    # Initial pressure slap has a 1 ms onset and short, dry broadband decay.
    blast_envelope = (1 - np.exp(-t / 0.0008)) * np.exp(-t / 0.018)
    pressure = band_noise(len(t), 140, 5400) * blast_envelope * 0.64
    # Audible compressed gas follows, with no watery/pitched tail or long hiss.
    gas_envelope = (1 - np.exp(-t / 0.0025)) * np.exp(-t / 0.043)
    gas = band_noise(len(t), 750, 6800) * gas_envelope * 0.15
    body_envelope = (1 - np.exp(-t / 0.0009)) * np.exp(-t / 0.026)
    body = (band_noise(len(t), 90, 590) * 0.85 + np.sin(2 * np.pi * 162 * t) * 0.15) * body_envelope * 0.22
    mechanics = click(t, 0.0007, 0.30, 0.0032) + click(t, 0.030, 0.16, 0.0045) + click(t, 0.079, 0.055, 0.003)
    signal = pressure + gas + body + mechanics
    # Very short chassis coupling; this is not an outdoor echo/reverb tail.
    offset = round(RATE * 0.0041)
    signal[offset:] += signal[:-offset] * 0.045
    signal -= signal.mean()
    signal *= np.minimum(1, t / 0.0005) * np.minimum(1, (DURATION - t) / 0.018)
    signal *= 10 ** (-3 / 20) / np.max(np.abs(signal))
    pcm = np.round(signal * 32767).astype("<i2")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    # Metadata is included explicitly so Android imports keep this tiny transient
    # as PCM and do not normalize away the intentionally bounded mix headroom.
    meta = Path(str(OUTPUT) + ".meta")
    if not meta.exists():
        meta.write_text("""fileFormatVersion: 2
guid: 1abc85c18b9347458521537accdc417e
AudioImporter:
  externalObjects: {}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: Original deterministic pneumatic report; Tools/Audio/build_archive_tranquilizer_audio.py
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")
    restored = pcm.astype(np.float64) / 32768
    rms = lambda samples: float(20 * np.log10(max(1e-12, np.sqrt(np.mean(samples ** 2)))))
    result = {
        "asset": str(OUTPUT.relative_to(ROOT)).replace("\\", "/"),
        "durationSeconds": len(pcm) / RATE,
        "sampleRate": RATE,
        "channels": 1,
        "bitsPerSample": 16,
        "peakDbFS": float(20 * np.log10(np.max(np.abs(restored)))),
        "wholeClipRmsDbFS": rms(restored),
        "activeFirst100msRmsDbFS": rms(restored[:round(RATE * 0.1)]),
        "clippedSamples": int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32767)),
        "dcOffset": float(restored.mean()),
        "tailLast20msPeakDbFS": float(20 * np.log10(max(1e-12, np.max(np.abs(restored[-round(RATE * 0.02):]))))),
        "sizeBytes": OUTPUT.stat().st_size,
        "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
        "provenance": "Entirely original deterministic filtered noise and damped mechanical synthesis; no source recordings or external service.",
        "design": "Dry compressed-gas transient, short low body and valve/bolt clicks; no liquid, pitch chirp, long hiss or rifle blast.",
    }
    report = ROOT / "Temp/ArchiveTranquilizerAudio/AudioAnalysis.json"
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    render()
