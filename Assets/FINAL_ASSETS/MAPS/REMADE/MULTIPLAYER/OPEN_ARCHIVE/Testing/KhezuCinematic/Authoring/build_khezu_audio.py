"""Offline Khezu sound design. Requires NumPy; never runs in the player build.

Reads the project's existing Small Sound Kit without modifying its files. Adds
original synthesized vocal/noise layers, filters, envelopes and short room tails.
All rendered effects are mono 44.1 kHz PCM16 with bounded peaks and faded edges.
"""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir())
SOURCE = ROOT / "Assets/Assets/Sounds Effects/Small Sound Kit"
OUTPUT = ROOT / "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/Audio"
REPORTS = Path(__file__).resolve().parents[1] / "Reports"
RATE = 44100
RNG = np.random.default_rng(761209)
PROVENANCE = []


def read(name):
    with wave.open(str(SOURCE / name), "rb") as stream:
        assert stream.getsampwidth() == 2
        data = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2").astype(np.float64) / 32768
        data = data.reshape(-1, stream.getnchannels()).mean(axis=1)
        source_rate = stream.getframerate()
    if source_rate != RATE:
        data = np.interp(np.arange(round(len(data) * RATE / source_rate)) * source_rate / RATE, np.arange(len(data)), data)
    PROVENANCE.append(name)
    # Pick an energetic texture window without incorporating a long silent lead-in.
    window = min(len(data), RATE)
    starts = np.arange(0, max(1, len(data) - window), max(1, RATE // 8))
    start = max(starts, key=lambda n: np.mean(data[n:n + window] ** 2))
    data = data[start:]
    rms = np.sqrt(np.mean(data[:window] ** 2))
    return data * (.2 / max(.01, rms))


def band(data, low, high):
    freq = np.fft.rfftfreq(len(data), 1 / RATE)
    response = 1 / np.sqrt(1 + (freq / high) ** 8)
    if low:
        response *= 1 / np.sqrt(1 + (low / np.maximum(freq, .01)) ** 8)
    return np.fft.irfft(np.fft.rfft(data) * response, n=len(data))


def noise(length, low, high):
    result = band(RNG.normal(size=length), low, high)
    return result / max(.001, np.std(result))


def resample(data, length, pitch=1):
    return np.interp(np.arange(length) * pitch, np.arange(len(data)), data, left=0, right=0)


def edge(data, attack=.003, release=.04):
    t = np.arange(len(data)) / RATE
    return data * np.minimum(1, t / attack) * np.minimum(1, (len(data) / RATE - t) / release)


def room(data):
    result = data.copy()
    for delay, gain in ((.021, .12), (.039, .075), (.063, .04)):
        offset = round(delay * RATE)
        result[offset:] += data[:-offset] * gain
    return result


def vocal(length, start_pitch, end_pitch):
    t = np.arange(length) / RATE
    duration = length / RATE
    pitch = end_pitch + (start_pitch - end_pitch) * np.exp(-t * 7)
    pitch *= 1 + .028 * np.sin(2 * np.pi * 31 * t) + .012 * np.sin(2 * np.pi * 53 * t)
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    harmonics = sum(np.sin(phase * k) / k ** .9 for k in range(1, 26))
    throat = band(np.tanh(harmonics * 1.8), 75, 3600)
    breath = noise(length, 280, 3600)
    rasp = noise(length, 700, 4200) * (.35 + .65 * np.sin(phase * .5) ** 2)
    return .58 * throat + .13 * breath + .18 * rasp


def render(name, data, peak, sources):
    data = edge(band(room(np.tanh(data * 1.45)), 35, 6500))
    data -= np.mean(data)
    data = edge(data, .002, .025)
    data *= peak / max(.001, np.max(np.abs(data)))
    assert np.isfinite(data).all() and np.max(np.abs(data)) < .94
    OUTPUT.mkdir(parents=True, exist_ok=True)
    pcm = np.round(data * 32767).astype("<i2")
    with wave.open(str(OUTPUT / (name + ".wav")), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    return {"name": name, "duration": len(data) / RATE, "peakDbFS": float(20 * np.log10(np.max(np.abs(data)))),
            "rmsDbFS": float(20 * np.log10(np.sqrt(np.mean(data ** 2)))),
            "clippedSamples": int(np.count_nonzero(np.abs(pcm.astype(float)) >= 32767)),
            "sources": sources, "synthesis": "Original deterministic vocal, filtered-noise and sub-impact layers; short room reflections"}


def main():
    impact = read("Dark/Impact_Guts_Gore_01.wav")
    smash = read("Dark/Blood_Bone_Smash_01.wav")
    alien = read("Dark/Drn_Alien_01.wav")
    electric = read("Magic/Mgc_Electric_Impact_01.wav")
    magic = read("Magic/Mgc_Ice_Ball_Cast_01.wav")
    entries = []

    n = round(.34 * RATE)
    t = np.arange(n) / RATE
    sub = np.sin(2 * np.pi * (43 * t + 36 * (1 - np.exp(-t * 20)) / 20)) * np.exp(-t * 16)
    hit = .9 * resample(smash, n, .76) + .65 * resample(impact, n, .82) + .65 * sub
    entries.append(render("Khezu_Capture_Impact", hit, .82, PROVENANCE[:2]))

    n = round(.31 * RATE)
    t = np.arange(n) / RATE
    aspirate = noise(n, 180, 1900) * np.sin(np.pi * t / .31) ** 1.6
    entries.append(render("Khezu_Low_Breathing", .19 * aspirate + .22 * resample(alien, n, .68), .55, [PROVENANCE[2]]))

    n = round(.25 * RATE)
    t = np.arange(n) / RATE
    growl = vocal(n, 78, 108) * (.2 + .8 * np.minimum(1, t / .18)) * (1 + .2 * np.sin(2 * np.pi * 23 * t))
    entries.append(render("Khezu_Tense_Growl", growl + .25 * resample(alien, n, .55), .68, [PROVENANCE[2]]))

    n = round(.72 * RATE)
    t = np.arange(n) / RATE
    throat = vocal(n, 173, 64) * (.7 + .3 * np.sin(2 * np.pi * 19 * t) ** 2)
    # An inharmonic, quickly falling stinger gives the open-mouth moment a sharp edge.
    sting = (np.sin(2 * np.pi * (970 * t - 140 * t * t)) + .5 * np.sin(2 * np.pi * (1421 * t - 220 * t * t))) * np.exp(-t * 19)
    env = np.minimum(1, t / .005) * np.exp(-t * 2.1)
    roar = (throat + .28 * resample(alien, n, .48)) * env + .30 * sting + .32 * resample(electric, n, .64) * np.exp(-t * 10)
    entries.append(render("Khezu_Maw_Roar_Stinger", roar, .89, [PROVENANCE[2], PROVENANCE[3]]))

    n = round(.19 * RATE)
    t = np.arange(n) / RATE
    accent = .8 * resample(impact, n, 1.12) + .20 * noise(n, 450, 3000) * np.exp(-t * 28)
    entries.append(render("Khezu_Impact_Accent", accent, .65, [PROVENANCE[0]]))

    n = round(.35 * RATE)
    entries.append(render("Khezu_Barrier_Pulse", resample(magic, n, 1.15), .65, [PROVENANCE[4]]))

    REPORTS.mkdir(parents=True, exist_ok=True)
    # Audition reference at the same two-second marker times and master gain budget.
    count = RATE * 2
    tracks, gains = [], []
    cues = [(0, .65), (.025, .18), (.045, .3), (.16, .95), (.5, .25), (1.6, .4)]
    time = np.arange(count) / RATE
    for index, (info, (at, level)) in enumerate(zip(entries, cues)):
        with wave.open(str(OUTPUT / (info["name"] + ".wav")), "rb") as stream:
            data = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2").astype(float) / 32768
        track = np.zeros(count)
        gain = np.zeros(count)
        start = round(at * RATE)
        length = min(len(data), count - start)
        track[start:start + length] = data[:length]
        gain[start:start + length] = level * .65
        gain *= np.clip((time - at) / .008, 0, 1) * np.clip((2 - time) / .08, 0, 1)
        if index < 3:
            gain *= np.clip((.185 - time) / .025, 0, 1)
        tracks.append(track)
        gains.append(gain)
    budget = np.maximum(1, sum(gains) / .65)
    mix = sum(track * gain / budget for track, gain in zip(tracks, gains))
    preview = REPORTS / "Khezu_TwoSecond_AudioPreview.wav"
    with wave.open(str(preview), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(np.round(mix * 32767).astype("<i2").tobytes())
    (REPORTS / "AudioAssetValidation.json").write_text(json.dumps({"sampleRate": RATE, "channels": 1, "pcmBits": 16,
        "method": "Offline sound design from existing project assets and original synthesis; no external generator",
        "clips": entries, "preview": {"duration": 2, "peakDbFS": float(20 * np.log10(np.max(np.abs(mix)))),
        "clippedSamples": int(np.count_nonzero(np.abs(mix) >= 1))}}, indent=2), encoding="utf-8")
    print(json.dumps(entries, indent=2))


if __name__ == "__main__":
    main()
