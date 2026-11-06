"""Synthesise the Core Voltage Studios intro sound (refined Concept C, 7.0 s), deterministically.

Cue list from the design (cv-intro-refined.jsx CUE_LIST), matched by
StudioIntro/CoreVoltageIntro.cs:
 0.30 ignition tick (pre-flickers 0.08/0.18); 0.94 whine starts, feed crackles on every
 bolt strike; 2.04 ring 1 lock with the arc jump; 3.05 ring 2 lock and gap crackles;
 3.72 bass hit with the overload flash; 6.00 tail, fading with the picture to 6.90.
ODC-authored; no samples or third-party audio. Usage: python tools/prepare_core_voltage_intro_audio.py
"""
from pathlib import Path
import wave
import numpy as np

RATE = 44100
LENGTH = 7.0
# OM_SCENES starts; keep in sync with CoreVoltageIntro.cs.
TRANSFER, FORMATION, STABILISE, REVEAL, HANDOFF = 0.9, 2.05, 3.0, 3.72, 6.0
IGNITION = 0.3
I_START, O_START = TRANSFER + 0.04, FORMATION + 0.02
STRIKES = ([(I_START + o, a) for o, a in zip([0, .16, .31, .47, .62, .78, .92], [1, .9, .95, .85, .78, .66, .5])]
           + [(FORMATION - 0.01, 1.0)]
           + [(O_START + o, a) for o, a in zip([.14, .3, .46, .62, .76], [.9, .82, .72, .6, .48])]
           + [(STABILISE + o, a) for o, a in ((.05, .62), (.2, .46), (.36, .3), (.5, .18))])
LOCKS = [(FORMATION - 0.01, 330.0), (STABILISE + 0.05, 520.0)]

out = Path(__file__).resolve().parents[1] / 'viewer/DarkColony.Viewer/Branding/CoreVoltage/core-voltage-intro.wav'
rng = np.random.RandomState(1997)
t = np.arange(int(RATE * LENGTH)) / RATE
left = np.zeros_like(t); right = np.zeros_like(t)

def smooth(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)

def add(signal, start=0.0, pan=0.0):
    i = int(round(start * RATE)); n = min(len(signal), len(t) - i)
    left[i:i + n] += signal[:n] * np.sqrt((1 - pan) / 2)
    right[i:i + n] += signal[:n] * np.sqrt((1 + pan) / 2)

def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / RATE); y = np.empty_like(x); acc = 0.0
    for k, v in enumerate(x):
        acc = (1 - a) * v + a * acc; y[k] = acc
    return y

def env(n, decay):
    k = np.arange(n) / RATE; return k, np.exp(-k / decay)

# Pre-flickers and the ignition tick; the pulse ring swells softly after it.
for at, level in ((0.08, 0.05), (0.18, 0.06)):
    k, e = env(int(0.03 * RATE), 0.004); add(level * rng.uniform(-1, 1, len(k)) * e, at)
k, e = env(int(0.08 * RATE), 0.01)
add(0.22 * rng.uniform(-1, 1, len(k)) * e + 0.16 * np.sin(2 * np.pi * 2900 * k) * np.exp(-k / 0.02), IGNITION)
k = np.arange(int((TRANSFER - IGNITION + 0.3) * RATE)) / RATE
add(0.09 * np.sin(2 * np.pi * (70 + 30 * k) * k) * smooth(k / 0.08) * np.exp(-k / 0.3), IGNITION)

# Feed crackles: one band-limited burst per bolt strike, scaled by its amplitude.
for j, (at, amp) in enumerate(STRIKES):
    n = int(0.1 * RATE); k, e = env(n, 0.018)
    noise = rng.uniform(-1, 1, n); noise = noise - lowpass(noise, 1600)
    add(0.24 * amp * noise * e * (1 + 0.6 * np.sin(2 * np.pi * 90 * k)), at, pan=((j * 0.41) % 1.4) - 0.7)

# Ring locks: a short metallic clunk with a low thump.
for at, pitch in LOCKS:
    k, e = env(int(0.35 * RATE), 0.09)
    tone = np.sin(2 * np.pi * pitch * k) + 0.5 * np.sin(2 * np.pi * pitch * 2.71 * k) * np.exp(-k / 0.03)
    thump = np.sin(2 * np.pi * (90 * k - 40 * k * k)) * np.exp(-k / 0.06)
    add(0.2 * tone * e + 0.28 * thump, at)

# Containment whine: rises from 0.94 with the rings and cuts at the flash.
seg = (t >= I_START) & (t < REVEAL + 0.02)
u = np.clip((t - I_START) / (REVEAL - I_START), 0, 1)
freq = 170 * (2700 / 170) ** (u ** 1.5)
phase = 2 * np.pi * np.cumsum(freq) / RATE
spin = 2 * np.pi * np.cumsum(3 + 20 * u ** 2) / RATE
level = smooth((t - I_START) / 0.5) * (0.05 + 0.28 * u ** 2) * (1 - smooth((t - REVEAL + 0.015) / 0.02))
whine = (np.sin(phase) + 0.35 * np.sin(2.003 * phase) + 0.18 * np.sin(phase * 0.5)) * (0.78 + 0.22 * np.sin(spin))
add(np.where(seg, whine * level, 0.0) * 0.9, 0.0, pan=-0.15)
add(np.where(seg, np.sin(phase * 1.006) * level * 0.5, 0.0), 0.0, pan=0.25)
rumble = lowpass(rng.uniform(-1, 1, len(t)), 120) * 6
add(np.where(t < REVEAL, rumble * smooth((t - 0.8) / (REVEAL - 0.8)) * 0.22, 0.0))

# Overload flash: sub bass hit with pitch drop, transient, air burst and shimmer.
n = int(2.6 * RATE); k = np.arange(n) / RATE
f = 38 + 60 * np.exp(-k / 0.06)
bass = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-k / 0.8) * 0.95
click = rng.uniform(-1, 1, n) * np.exp(-k / 0.004) * 0.5
air = lowpass(rng.uniform(-1, 1, n), 5000) * np.exp(-k / 0.28) * 0.55
shimmer = (np.sin(2 * np.pi * 1760 * k) + 0.6 * np.sin(2 * np.pi * 2637 * k)) * np.exp(-k / 0.9) * 0.05
add(bass + click, REVEAL)
add(air + shimmer, REVEAL, pan=-0.3)
add(air * 0.9 + shimmer, REVEAL + 0.012, pan=0.3)

# Hold: quiet hum under the mark and title, then the tail into the main menu.
hum = (np.sin(2 * np.pi * 55 * t) + 0.3 * np.sin(2 * np.pi * 110 * t)) * smooth((t - (REVEAL + 0.5)) / 0.8) * 0.07
add(np.where(t >= REVEAL + 0.5, hum, 0.0))

def in_out_cubic(x):
    x = np.clip(x, 0, 1); return np.where(x < .5, 4 * x ** 3, (x - 1) * (2 * x - 2) ** 2 + 1)
fade = 1 - in_out_cubic((t - HANDOFF) / 0.9)   # the picture's handoff fade
left *= fade; right *= fade
peak = max(np.abs(left).max(), np.abs(right).max())
gain = 10 ** (-1 / 20) / peak
data = (np.stack([left, right], 1) * gain * 32767).round().astype('<i2')
out.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(out), 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
print('Wrote', out, len(data), 'frames')
