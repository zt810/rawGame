import math
import os
import random
import struct
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 22050
rnd = random.Random(42)

# Tần số nốt nhạc
N = {"C4": 261.6, "E4": 329.6, "G4": 392.0, "A4": 440.0, "B4": 493.9, "C5": 523.3, "D5": 587.3, "E5": 659.3,
     "F5": 698.5, "G5": 784.0, "A5": 880.0, "B5": 987.8, "C6": 1046.5, "E6": 1318.5, "G6": 1568.0, "C7": 2093.0}


def silence(sec):
    return [0.0] * int(SR * sec)


def tone(freq, sec, wave_="square", vol=0.5, decay=6.0, attack=0.004, freq_end=None, duty=0.5):
    n = int(SR * sec)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = freq if freq_end is None else freq + (freq_end - freq) * (i / n)
        phase += f / SR
        ph = phase % 1.0
        if wave_ == "square":
            v = 1.0 if ph < duty else -1.0
        elif wave_ == "tri":
            v = 4 * abs(ph - 0.5) - 1
        elif wave_ == "noise":
            v = rnd.uniform(-1, 1)
        else:
            v = math.sin(2 * math.pi * ph)
        env = min(1.0, t / attack) * math.exp(-decay * t)
        out.append(v * env * vol)
    return out


def noise(sec, vol=0.4, decay=8.0, smooth=0):
    n = int(SR * sec)
    out, prev = [], 0.0
    for i in range(n):
        v = rnd.uniform(-1, 1)
        if smooth:
            prev = prev + (v - prev) / (smooth + 1)
            v = prev * 1.6
        out.append(v * vol * math.exp(-decay * i / SR))
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def seq(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def bell(freq, sec, vol=0.4):
    return mix(tone(freq, sec, "sine", vol, decay=5), tone(freq * 2.01, sec, "sine", vol * 0.35, decay=8),
               tone(freq * 3.0, sec, "sine", vol * 0.15, decay=12))


def save(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = min(1.0, 0.9 / peak)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))


BPM = 112
NOTE_IDX = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def note_freq(name):
    return 440.0 * 2 ** ((NOTE_IDX[name[:-1]] + 12 * (int(name[-1]) + 1) - 69) / 12)


def voice(buf, start, dur, freq, wave_, vol, duty=0.5, punch=3.0):
    n = int(SR * dur)
    phase = 0.0
    for i in range(n):
        if start + i >= len(buf):
            break
        t = i / SR
        phase += freq / SR
        ph = phase % 1.0
        if wave_ == "square":
            v = 1.0 if ph < duty else -1.0
        elif wave_ == "tri":
            v = 4 * abs(ph - 0.5) - 1
        else:
            v = math.sin(2 * math.pi * ph)
        env = min(1.0, t / 0.004) * min(1.0, (dur - t) / 0.012) * (0.55 + 0.45 * math.exp(-punch * t))
        buf[start + i] += v * env * vol


def drum(buf, start, kind):
    if kind == "kick":
        n = int(SR * 0.16)
        phase = 0.0
        for i in range(n):
            t = i / SR
            phase += (150 - 100 * min(1, t / 0.12)) / SR
            buf[(start + i) % len(buf)] += math.sin(2 * math.pi * phase) * 0.55 * math.exp(-18 * t)
    else:
        n = int(SR * (0.03 if kind == "hat" else 0.12))
        vol, dec = (0.07, 90) if kind == "hat" else (0.16, 22)
        for i in range(n):
            buf[(start + i) % len(buf)] += rnd.uniform(-1, 1) * vol * math.exp(-dec * i / SR)


BGM_CHORDS = ["C", "Am", "F", "G", "C", "Am", "F", "G", "F", "G", "C", "Am", "F", "G", "C", "G"]
CHORD_NOTES = {"C": ("C", "E", "G"), "Am": ("A", "C", "E"), "F": ("F", "A", "C"), "G": ("G", "B", "D")}
BGM_MELODY = [
    "E5 G5 A5 G5 E5 D5 C5 D5", "E5 . G5 . A5 C6 A5 G5", "A5 G5 E5 D5 C5 . D5 E5", "D5 . G4 . A4 C5 D5 -",
    "E5 G5 A5 G5 E5 D5 C5 D5", "E5 . G5 . A5 . C6 .", "D6 C6 A5 G5 A5 C6 A5 G5", "E5 . D5 . C5 . - -",
    "A5 . C6 . A5 G5 E5 G5", "D5 . G5 . D5 E5 G5 .", "E5 G5 C6 . G5 E5 C5 E5", "A5 . G5 E5 D5 . C5 .",
    "A5 C6 D6 . C6 A5 G5 A5", "G5 . E5 G5 D5 . E5 .", "C6 . A5 G5 E5 G5 A5 G5", "E5 D5 C5 D5 G4 . - -",
]


def bgm():
    eighth = 60.0 / BPM / 2
    bar = eighth * 8
    total = int(SR * bar * len(BGM_CHORDS))
    buf = [0.0] * total
    e = lambda k: int(SR * eighth * k)
    for b, (chord, line) in enumerate(zip(BGM_CHORDS, BGM_MELODY)):
        base = e(8 * b)
        steps = line.split()
        k = 0
        while k < 8:
            nm = steps[k]
            if nm in ("-", "."):
                k += 1
                continue
            length = 1
            while k + length < 8 and steps[k + length] == ".":
                length += 1
            voice(buf, base + e(k), eighth * length * 0.95, note_freq(nm), "square", 0.16, duty=0.25)
            k += length
        root, third, fifth = CHORD_NOTES[chord]
        for q in range(4):
            nm = (root if q % 2 == 0 else fifth) + ("2" if q % 2 == 0 else "3")
            voice(buf, base + e(2 * q), eighth * 1.9, note_freq(nm), "tri", 0.32, punch=1.5)
        for k in range(16):
            nm = (root, third, fifth, third)[k % 4] + "4"
            voice(buf, base + int(SR * eighth / 2 * k), eighth / 2 * 0.8, note_freq(nm), "square", 0.035, duty=0.5, punch=6)
        for k in range(8):
            if k in (0, 4):
                drum(buf, base + e(k), "kick")
            elif k in (2, 6):
                drum(buf, base + e(k), "snare")
            if k % 2 == 1:
                drum(buf, base + e(k), "hat")
    return buf

SOUNDS = {
    # Giao diện
    "click": lambda: tone(1200, 0.04, "square", 0.25, decay=60, duty=0.25),
    "blip": lambda: tone(900, 0.045, "square", 0.2, decay=50, duty=0.25),
    # Khách
    "door": lambda: seq(bell(N["G6"], 0.22), bell(N["E6"], 0.45)),
    "order": lambda: seq(tone(N["E5"], 0.06, "square", 0.3, decay=20), tone(N["A5"], 0.1, "square", 0.3, decay=15)),
    "cash": lambda: mix(noise(0.05, 0.5, decay=60), seq(silence(0.04), bell(N["C7"], 0.18, 0.35), bell(N["E6"] * 2, 0.4, 0.3))),
    "angry": lambda: tone(380, 0.45, "square", 0.3, decay=3, freq_end=140, duty=0.3),
    # Bếp
    "pop": lambda: tone(300, 0.08, "sine", 0.6, decay=25, freq_end=900),
    "sizzle": lambda: mix(noise(0.55, 0.35, decay=3, smooth=0), noise(0.55, 0.25, decay=4, smooth=2)),
    "pour": lambda: seq(*[tone(rnd.uniform(500, 1100), 0.04, "sine", 0.35, decay=40) for _ in range(7)]),
    "ding": lambda: bell(N["E6"], 0.7, 0.5),
    "error": lambda: seq(tone(150, 0.12, "square", 0.35, decay=5), silence(0.04), tone(130, 0.16, "square", 0.35, decay=5)),
    # Tiền, thuế
    "coin": lambda: seq(tone(N["B5"], 0.06, "square", 0.3, decay=10), tone(N["E6"], 0.18, "square", 0.3, decay=12)),
    "stamp": lambda: mix(noise(0.12, 0.6, decay=40, smooth=3), tone(110, 0.15, "sine", 0.7, decay=25)),
    "alarm": lambda: seq(*[tone(880 if i % 2 == 0 else 660, 0.1, "square", 0.3, decay=4) for i in range(6)]),
    # Ngày, chương
    "open": lambda: seq(*[tone(N[n], 0.09, "square", 0.3, decay=8) for n in ("C5", "E5", "G5")], tone(N["C6"], 0.3, "square", 0.3, decay=6)),
    "close": lambda: seq(*[tone(N[n], 0.14, "tri", 0.45, decay=5) for n in ("G5", "E5", "C5")], tone(N["G4"], 0.35, "tri", 0.45, decay=4)),
    "fanfare": lambda: seq(*[tone(N[n], d, "square", 0.3, decay=4) for n, d in
                            (("C5", 0.1), ("E5", 0.1), ("G5", 0.1), ("C6", 0.22), ("G5", 0.1), ("C6", 0.5))]),
    "fail": lambda: seq(*[tone(N[n], 0.22, "tri", 0.45, decay=3) for n in ("B4", "A4", "G4")], tone(N["E4"], 0.6, "tri", 0.45, decay=2)),
}

def main():
    for name, make in SOUNDS.items():
        save(name, make())
    save("bgm_main", bgm())
    print(f"Đã tạo {len(SOUNDS)} hiệu ứng âm thanh -> {OUT}")


if __name__ == "__main__":
    main()
