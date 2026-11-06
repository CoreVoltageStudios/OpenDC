"""Prototype cues continuing tools/prepare_core_voltage_intro_audio.py: same 55/110 Hz hum, feed crackle,
whine and sub hit. Deterministic, ODC-authored, no samples. Build-time generator for the engine bootstrap resources; NumPy is not a player dependency."""
from pathlib import Path
import wave
import numpy as np
RATE = 44100
OUT = Path(__file__).resolve().parents[1] / 'viewer/DarkColony.Viewer/Branding/Landfall'
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.RandomState(1997)

def smooth(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)

def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / RATE); y = np.empty_like(x); acc = 0.0
    for k, v in enumerate(x):
        acc = (1 - a) * v + a * acc; y[k] = acc
    return y

def save(name, left, right, peak_db=-6.0):
    peak = max(np.abs(left).max(), np.abs(right).max(), 1e-9)
    g = 10 ** (peak_db / 20) / peak
    data = (np.stack([left, right], 1) * g * 32767).round().astype('<i2')
    with wave.open(str(OUT / name), 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
    print(name, round(len(left) / RATE, 2))

def crackle(n, decay, amp):
    k = np.arange(n) / RATE
    noise = rng.uniform(-1, 1, n); noise = noise - lowpass(noise, 1600)
    return amp * noise * np.exp(-k / decay) * (1 + 0.6 * np.sin(2 * np.pi * 90 * k))

# 1. Residual hold: a 16 s seamless loop. The intro's 55/110 Hz hum, a slow breath, sparse quiet ticks.
L = 16.0; n = int(RATE * L); t = np.arange(n) / RATE
breath = 0.75 + 0.25 * np.sin(2 * np.pi * t / L - np.pi / 2)
hum = (np.sin(2 * np.pi * 55 * t) + 0.3 * np.sin(2 * np.pi * 110 * t) + 0.08 * np.sin(2 * np.pi * 165 * t)) * breath * 0.5
air = np.sin(2 * np.pi * 1760 * t) * 0.006 * (0.5 + 0.5 * np.sin(2 * np.pi * 2 * t / L))
left = hum + air; right = hum * 0.98 + np.sin(2 * np.pi * 1763.0625 * t) * 0.006 * (0.5 + 0.5 * np.cos(2 * np.pi * 2 * t / L))
for at, a, pan in ((3.1, .05, -.4), (7.7, .035, .3), (12.4, .045, .1)):
    c = crackle(int(.09 * RATE), .016, a); i = int(at * RATE)
    left[i:i + len(c)] += c * (1 - pan) / 2; right[i:i + len(c)] += c * (1 + pan) / 2
save('cue-residual-hold.wav', left, right, -14)

# 2. Charge transfer: the import begins; the held charge rises and is released toward the scene.
L = 3.2; n = int(RATE * L); t = np.arange(n) / RATE; u = np.clip(t / 2.5, 0, 1)
freq = 110 * (1400 / 110) ** (u ** 1.6); ph = 2 * np.pi * np.cumsum(freq) / RATE
lvl = smooth(t / .4) * (0.05 + .22 * u ** 2) * (1 - smooth((t - 2.5) / .5))
sig = (np.sin(ph) + .35 * np.sin(2.003 * ph)) * lvl
left = sig.copy(); right = np.sin(ph * 1.006) * lvl
for at, a in ((.9, .3), (1.5, .4), (2.0, .5), (2.42, .75)):
    c = crackle(int(.1 * RATE), .018, a); i = int(at * RATE)
    left[i:i + len(c)] += c * .7; right[i:i + len(c)] += c * .7
save('cue-charge-transfer.wav', left, right, -8)

# 3. First contact: one arc down the tower, grounded with a short lock and thump.
L = 2.0; n = int(RATE * L); left = np.zeros(n); right = np.zeros(n)
for i, at in enumerate((0, .06, .13, .21, .3, .41)):
    c = crackle(int(.09 * RATE), .016, .5 - i * .05); s = int(at * RATE)
    left[s:s + len(c)] += c * (.6 + .05 * i); right[s:s + len(c)] += c * (.6 - .05 * i)
k = np.arange(int(.5 * RATE)) / RATE
lock = .22 * (np.sin(2 * np.pi * 330 * k) + .5 * np.sin(2 * np.pi * 330 * 2.71 * k) * np.exp(-k / .03)) * np.exp(-k / .09) + .4 * np.sin(2 * np.pi * (90 * k - 40 * k * k)) * np.exp(-k / .08)
s = int(.48 * RATE); left[s:s + len(k)] += lock; right[s:s + len(k)] += lock
save('cue-first-contact.wav', left, right, -9)

# 4. Milestone tick: a real importer stage finished. Dry and quiet.
L = .5; n = int(RATE * L); k = np.arange(n) / RATE
tick = .4 * np.sin(2 * np.pi * 520 * k) * np.exp(-k / .05) + .3 * np.sin(2 * np.pi * (90 * k - 40 * k * k)) * np.exp(-k / .05)
save('cue-milestone.wav', tick, tick, -16)

# 5. Awakening: charge is released through the colony. Reversed in energy to the intro: the sub hit comes first,
# then the whine falls and the hum opens out into a low bed that ends under the menu.
L = 12.0; n = int(RATE * L); t = np.arange(n) / RATE; left = np.zeros(n); right = np.zeros(n)
for i, at in enumerate((0, .07, .15, .24, .34, .46, .6)):
    c = crackle(int(.1 * RATE), .018, .55 - i * .05); s = int(at * RATE)
    left[s:s + len(c)] += c * .7; right[s:s + len(c)] += c * .7
k = np.arange(int(2.6 * RATE)) / RATE
f = 38 + 60 * np.exp(-k / .06)
bass = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-k / .8) * .9
s = int(.72 * RATE); left[s:s + len(k)] += bass; right[s:s + len(k)] += bass
u = np.clip((t - .72) / 3.2, 0, 1)
freq = 2200 * (140 / 2200) ** (u ** .7); ph = 2 * np.pi * np.cumsum(freq) / RATE
lvl = smooth((t - .72) / .25) * (1 - smooth((t - 3.2) / 1.1)) * .1
left += np.sin(ph) * lvl; right += np.sin(ph * 1.004) * lvl
bed = (np.sin(2 * np.pi * 55 * t) + .45 * np.sin(2 * np.pi * 82.5 * t) + .3 * np.sin(2 * np.pi * 110 * t) + .12 * np.sin(2 * np.pi * 165 * t))
bedl = smooth((t - 1.6) / 2.5) * (1 - smooth((t - 9.5) / 2.4)) * .16
left += bed * bedl; right += bed * bedl * .96
save('cue-awakening.wav', left, right, -5)
