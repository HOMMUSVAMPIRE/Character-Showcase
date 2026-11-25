#!/usr/bin/env python3
"""Procedural movement sound effects for a game character.

Pure Python standard library: no installs needed. Every sound is synthesized
from noise and sine waves, so there is nothing to license.

USAGE
    python3 make_sfx.py                      make every sound that doesn't exist yet
    python3 make_sfx.py --list               list all sound names
    python3 make_sfx.py land splash -f       remake these sounds, replacing their files
    python3 make_sfx.py jump --surface metal -f
    Options: --out DIR (default sfx), --seed N (default 1; a new seed gives new variations)

    Existing files are never replaced unless you pass -f/--overwrite, so sounds
    you have approved (or edited by hand) stay as they are.

OUTPUT  (44.1 kHz, 16-bit, mono WAV)
    sfx/surfaces/<surface>/<name>_<surface>_NN.wav   ground moves, one set per surface
    sfx/<group>/<name>_NN.wav                        movement, traversal and water
    sfx/manifest.js                                  file list used by preview.html

IN THE GAME
    * Pick a random variation each time (not the one just played) and vary
      pitch by about +/-4% and volume by about +/-10%.
    * Levels are pre-balanced against the footsteps (peak -1 dBFS): quiet moves
      such as crouching are quiet on purpose, so use one shared volume.
    * Files ending in _loop loop seamlessly. For a long move, play <move>_start
      and <move>_loop together, then on release fade the loop out over ~0.15 s
      and play <move>_end.

ADDING OR CHANGING A SOUND
    Each sound is a recipe function decorated with @sound(group, peak_db,
    variations). Recipes layer building blocks with add(out, layer, at=seconds,
    gain=...). Surface recipes take (rng, surface, plate); the others take (rng).
"""
import argparse
import json
import math
import os
import random
import struct
import wave
from dataclasses import dataclass
from typing import Callable

SR = 44100
SURFACES = ("concrete", "metal", "grass")


# ================================================================ signal basics
def silence(seconds):
    return [0.0] * int(seconds * SR)


def noise(n, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def add(dst, src, at=0.0, gain=1.0):
    """Mix src into dst starting `at` seconds in, growing dst if needed."""
    off = int(at * SR)
    if off + len(src) > len(dst):
        dst.extend([0.0] * (off + len(src) - len(dst)))
    for i, v in enumerate(src):
        dst[off + i] += v * gain
    return dst


def gain(x, g):
    return [v * g for v in x]


def unit(x):
    """Scale to a peak of 1, so layers can be mixed by their gains alone."""
    p = max((abs(v) for v in x), default=0.0) or 1.0
    return [v / p for v in x]


def curve(n, pts):
    """Piecewise-linear curve over n samples; pts are (time 0..1, value)."""
    out, j = [], 0
    for i in range(n):
        t = i / max(1, n - 1)
        while j < len(pts) - 2 and t > pts[j + 1][0]:
            j += 1
        (t0, v0), (t1, v1) = pts[j], pts[j + 1]
        a = 0.0 if t1 == t0 else min(1.0, max(0.0, (t - t0) / (t1 - t0)))
        out.append(v0 + (v1 - v0) * a)
    return out


# ================================================================ filters
def lowpass(x, cutoff):
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
    y, s = [], 0.0
    for v in x:
        s += a * (v - s)
        y.append(s)
    return y


def highpass(x, cutoff):
    return [a - b for a, b in zip(x, lowpass(x, cutoff))]


def bandpass(x, freq, q):
    """RBJ biquad band-pass (constant 0 dB peak gain)."""
    w0 = 2 * math.pi * freq / SR
    alpha = math.sin(w0) / (2 * q)
    a0 = 1 + alpha
    b0, b2, a1, a2 = alpha / a0, -alpha / a0, -2 * math.cos(w0) / a0, (1 - alpha) / a0
    y = []
    x1 = x2 = y1 = y2 = 0.0
    for v in x:
        out = b0 * v + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, v
        y2, y1 = y1, out
        y.append(out)
    return y


def sweep_bandpass(x, freqs, q):
    """Band-pass whose centre frequency changes every sample (state-variable filter)."""
    low = band = 0.0
    damp = 1.0 / q
    out = []
    for v, fc in zip(x, freqs):
        f = 2 * math.sin(math.pi * min(fc, 7000) / SR)
        low += f * band
        high = v - low - damp * band
        band += f * high
        out.append(band * damp)
    return out


# ================================================================ envelopes and finishing
def env_exp(n, attack_s, decay_s):
    """Linear attack, then exponential decay."""
    a = max(1, int(attack_s * SR))
    return [i / a if i < a else math.exp(-(i - a) / (decay_s * SR)) for i in range(n)]


def apply_env(x, e):
    return [a * b for a, b in zip(x, e)]


def periodic(n, hz, depth):
    """Slow loudness wobble. Loop-safe when hz x loop length is a whole number."""
    return [1 - depth + depth * math.sin(2 * math.pi * hz * i / SR) for i in range(n)]


def make_loop(x, seconds, xfade):
    """Fold x's tail over its head with an equal-power crossfade so it loops.
    x must be at least seconds + xfade long."""
    nl, nx = int(seconds * SR), int(xfade * SR)
    out = x[:nl]
    for i in range(nx):
        t = i / nx
        out[i] = x[i] * math.sin(t * math.pi / 2) + x[nl + i] * math.cos(t * math.pi / 2)
    return out


def trim(x, thresh_db=-60):
    """Cut trailing near-silence, keeping 10 ms after the sound dies away."""
    t = 10 ** (thresh_db / 20) * max(abs(v) for v in x)
    end = len(x)
    while end > 0 and abs(x[end - 1]) < t:
        end -= 1
    return x[: min(len(x), end + int(0.01 * SR))]


def fade_edges(x, ms=3):
    n = int(ms / 1000 * SR)
    for i in range(min(n, len(x))):
        g = i / n
        x[i] *= g
        x[-1 - i] *= g
    return x


def normalize(x, peak_db):
    peak = max(abs(v) for v in x) or 1.0
    target = 10 ** (peak_db / 20)
    return [v * target / peak for v in x]


# ================================================================ sound sources
def burst(rng, seconds, attack_s, decay_s):
    """A noise hit with a sharp envelope."""
    n = int(seconds * SR)
    return apply_env(noise(n, rng), env_exp(n, attack_s, decay_s))


def thump(seconds, freq, decay_s, rng, drop=0.5):
    """Low thud: a decaying sine with a slight downward pitch glide."""
    n = int(seconds * SR)
    out, phase = [], rng.random() * 0.2
    for i in range(n):
        t = i / SR
        f = freq * (1 - drop * (1 - math.exp(-t / 0.03)))
        phase += 2 * math.pi * f / SR
        out.append(math.sin(phase) * math.exp(-t / decay_s))
    return out


def crackle(seconds, density, rng, grain_ms=(0.3, 1.5)):
    """Sparse random micro-clicks (stems snapping), `density` per second."""
    n = int(seconds * SR)
    out = [0.0] * n
    for _ in range(int(density * seconds)):
        pos = int(rng.random() * n)
        ln = int(rng.uniform(*grain_ms) / 1000 * SR)
        amp = rng.uniform(0.2, 1.0) * rng.choice((-1, 1))
        for j in range(ln):
            if pos + j >= n:
                break
            out[pos + j] += amp * rng.uniform(-1, 1) * (1 - j / ln)
    return out


def crinkle(rng, seconds, rate_hz):
    """Noise whose loudness flickers randomly: blades, fabric or water brushing."""
    n = int(seconds * SR)
    mod = lowpass([abs(v) ** 2 for v in noise(n, rng)], rate_hz)
    peak = max(mod) or 1.0
    return [rng.uniform(-1, 1) * (m / peak) for m in mod]


def resonate(rng, seconds, modes, g):
    """Ring a set of (freq, amp, decay_s) modes, each slightly detuned."""
    n = int(seconds * SR)
    out = [0.0] * n
    for f, amp, dec in modes:
        f *= rng.uniform(0.98, 1.02)
        k = 2 * math.pi * f / SR
        ph = rng.random() * 2 * math.pi
        for i in range(n):
            out[i] += amp * math.sin(ph + k * i) * math.exp(-i / (dec * SR))
    att = int(0.0008 * SR)
    for i in range(min(att, n)):
        out[i] *= i / att
    return [v * g for v in out]


def ring_modes(rng, base_hz, ratios, decay_s, amp_falloff, decay_falloff):
    """Modes for a resonant object: base frequency times each ratio, upper modes
    quieter and shorter. base_hz and decay_s are (low, high) ranges."""
    base = rng.uniform(*base_hz)
    return [(base * r, 1 / (1 + i * amp_falloff), rng.uniform(*decay_s) / (1 + i * decay_falloff))
            for i, r in enumerate(ratios)]


def whoosh(rng, dur, freqs, peak=0.4, q=1.2):
    """Air rushing past: noise through a band-pass sweeping through `freqs`,
    loudest at `peak` (0..1 of the duration)."""
    n = int(dur * SR)
    fc = curve(n, [(i / (len(freqs) - 1), f * rng.uniform(0.9, 1.1)) for i, f in enumerate(freqs)])
    x = sweep_bandpass(noise(n, rng), fc, q)
    amp = [a * a for a in curve(n, [(0, 0), (peak, 1), (1, 0)])]
    return unit(apply_env(x, amp))


def cloth(rng, dur, center=2400, sharp=False):
    """Clothing rustle with slow, uneven swells. sharp=True for a quick flap."""
    n = int(dur * SR)
    x = bandpass(crinkle(rng, dur, rng.uniform(150, 300)), center * rng.uniform(0.85, 1.15), 0.6)
    x = lowpass(x, 6000)
    mod = unit(lowpass(noise(n, rng), 12))
    shape = curve(n, [(0, 0), (0.06 if sharp else 0.25, 1), (1, 0)])
    return unit([v * s * (0.55 + 0.45 * abs(m)) for v, s, m in zip(x, shape, mod)])


def creak(rng, dur, start_ms=4, end_ms=14, center=700):
    """Stick-slip creak of rope fibres tightening: clicks that slow down."""
    n = int(dur * SR)
    x = [0.0] * n
    t = 0.0
    while True:
        frac = t / dur
        t += (start_ms + (end_ms - start_ms) * frac) / 1000 * rng.uniform(0.8, 1.2)
        if t >= dur:
            break
        p = int(t * SR)
        a = (1 - frac) ** 0.5 * rng.uniform(0.6, 1.0)
        for j in range(int(0.0012 * SR)):
            if p + j < n:
                x[p + j] += a * rng.uniform(-1, 1)
    y1 = bandpass(x, center, 4)
    y2 = bandpass(x, center * 2.1, 4)
    return unit([a + 0.5 * b for a, b in zip(y1, y2)])


def bubbles(rng, dur, count, fmin, fmax, t0=0.0, t1=None, front_load=True):
    """Bubbles or drops as rising-pitch decaying sines (van den Doel's model),
    starting between t0 and t1 seconds; front_load bunches them early."""
    n = int(dur * SR)
    t1 = dur * 0.8 if t1 is None else t1
    x = [0.0] * n
    for _ in range(count):
        f0 = math.exp(rng.uniform(math.log(fmin), math.log(fmax)))
        r = rng.random() ** 2 if front_load else rng.random()
        start = int((t0 + (t1 - t0) * r) * SR)
        d = 0.13 * f0 + 0.0072 * f0 ** 1.5      # damping, 1/s
        sigma = 0.1 * d                          # pitch rise rate
        life = min(0.25, 6.9 / d)
        a = rng.uniform(0.3, 1.0) * (300 / f0) ** 0.3
        ph = 0.0
        for j in range(int(life * SR)):
            if start + j >= n:
                break
            t = j / SR
            ph += 2 * math.pi * f0 * (1 + sigma * t) / SR
            x[start + j] += a * math.sin(ph) * math.exp(-d * t)
    return unit(x)


def whine(rng, dur, f0, wobble_hz, depth=0.01):
    """Wheel-on-cable tone with harmonics and a pitch wobble. Loop-safe when
    f0 and wobble_hz each make whole cycles over the loop length."""
    n = int(dur * SR)
    ph = 0.0
    out = []
    for i in range(n):
        f = f0 * (1 + depth * math.sin(2 * math.pi * wobble_hz * i / SR))
        ph += 2 * math.pi * f / SR
        out.append(math.sin(ph) + 0.4 * math.sin(2 * ph) + 0.2 * math.sin(3 * ph))
    return unit(out)


# ================================================================ body contacts
# A step is ONE perceived event: the heel contact, then the forefoot rolling
# down as a quiet, smeared tail 20-40 ms later (never a second distinct hit).

def sneaker_heel(rng):
    """Rubber athletic sole meeting a hard floor: a muted 'thup'. The rubber rounds
    off the attack (~1.5 ms) and soaks up the 1-3 kHz splat that reads as a wet slap."""
    n = int(0.06 * SR)
    body = apply_env(noise(n, rng), env_exp(n, rng.uniform(0.0012, 0.0018), rng.uniform(0.008, 0.011)))
    body = lowpass(bandpass(body, rng.uniform(420, 560), 0.8), 1400)
    low = thump(0.06, rng.uniform(105, 125), 0.011, rng, drop=0.2)
    grain = highpass(burst(rng, 0.03, 0.0008, 0.003), 4500)   # faint grain of the hard surface
    return [b * 1.0 + l * 0.7 + g * 0.06 for b, l, g in zip(body, low, grain)]


def sole_roll(rng, seconds, center, g):
    """Quiet, soft-attack scuff of the forefoot landing: smeared, not a click."""
    n = int(seconds * SR)
    x = bandpass(noise(n, rng), center, 0.8)
    return [v * g for v in apply_env(x, env_exp(n, 0.008, 0.018))]


def scuff(rng, dur, center, strength=1.0):
    """Short friction: a sole or palm dragging against a surface."""
    n = int(dur * SR)
    x = unit(bandpass(crinkle(rng, dur, 500), center, 0.9))
    return apply_env(gain(x, strength), env_exp(n, 0.003, dur * 0.35))


def body_thud(rng, strength=1.0, freq=None, decay=0.03):
    """The mass of the body meeting something: low, soft, no surface character."""
    f = freq or rng.uniform(65, 85)
    low = thump(0.25, f, decay, rng, drop=0.3)
    n = int(0.25 * SR)
    mass = unit(lowpass(apply_env(noise(n, rng), env_exp(n, 0.002, decay * 0.6)), 700))
    return [(l * 0.8 + m * 0.5) * strength for l, m in zip(low, mass)]


def palm_slap(rng, strength=1.0):
    """Bare hand landing flat on a hard edge."""
    n = int(0.06 * SR)
    x = apply_env(noise(n, rng), env_exp(n, 0.0004, rng.uniform(0.004, 0.006)))
    x = unit(bandpass(x, rng.uniform(1100, 1600), 0.6))
    low = thump(0.06, rng.uniform(180, 230), 0.008, rng, drop=0.2)
    return [(a + 0.5 * b) * strength for a, b in zip(x, low)]


# ================================================================ surfaces
def plate_modes(rng):
    """Resonances of a thick steel plate laid on a solid floor. A thick, supported
    plate has no low 'cavity' tone: only dense, high modes that die almost at once."""
    modes = []
    f = rng.uniform(2300, 2700)
    while f < 9500:
        modes.append((f, rng.uniform(0.5, 1.0) * (2600 / f) ** 0.5, rng.uniform(0.004, 0.009)))
        f *= rng.uniform(1.12, 1.3)
    return modes


def step_concrete(rng):
    """Athletic shoe on smooth concrete: a soft, solid 'thup' and a quiet roll."""
    out = silence(0.2)
    add(out, sneaker_heel(rng), at=0.002)
    add(out, sole_roll(rng, 0.06, rng.uniform(900, 1300), rng.uniform(0.1, 0.14)), at=rng.uniform(0.025, 0.035))
    return lowpass(highpass(out, 70), 5000)


def step_metal(rng, plate):
    """Athletic shoe on a steel plate fixed to the ground: the concrete 'thup'
    with a quick, dense metallic sheen on top. No hollow tone."""
    out = silence(0.2)
    add(out, sneaker_heel(rng), at=0.002)
    add(out, resonate(rng, 0.06, plate, rng.uniform(0.13, 0.17)), at=0.0025)   # high 'tsk', gone in ~15 ms
    add(out, bandpass(burst(rng, 0.03, 0.0006, 0.003), rng.uniform(3000, 3800), 1.0), at=0.002, gain=0.25)
    t2 = rng.uniform(0.025, 0.035)
    add(out, sole_roll(rng, 0.06, rng.uniform(1000, 1400), 0.12), at=t2)
    add(out, resonate(rng, 0.04, plate, 0.03), at=t2 + 0.006)
    return lowpass(highpass(out, 70), 11000)


def step_grass(rng):
    """Foot pressing into grass: a soft, muffled swish with a few blade snaps
    and a gentle thud of earth. No sharp transient: grass cushions the foot."""
    out = silence(0.4)
    dur = rng.uniform(0.18, 0.24)
    n = int(dur * SR)
    # swish: crinkly mid-band noise, quick rise, smooth fall
    sw = lowpass(bandpass(crinkle(rng, dur, rng.uniform(250, 400)), rng.uniform(1600, 2200), 0.5), 5000)
    rise = int(0.012 * SR)
    sw = apply_env(sw, [i / rise if i < rise else math.exp(-(i - rise) / (0.055 * SR)) for i in range(n)])
    # second, smaller swell as the forefoot presses the grass down
    sw2 = bandpass(crinkle(rng, dur * 0.7, 300), rng.uniform(1800, 2400), 0.5)
    sw2 = apply_env(sw2, env_exp(len(sw2), 0.01, 0.04))
    # a handful of blade/stem snaps
    snaps = [0.0] * n
    for _ in range(rng.randint(6, 12)):
        pos = int(rng.uniform(0.0, 0.09) * SR)
        ln = int(rng.uniform(0.0015, 0.003) * SR)
        a = rng.uniform(0.3, 1.0)
        for j in range(ln):
            if pos + j < n:
                snaps[pos + j] += a * rng.uniform(-1, 1) * (1 - j / ln) ** 2
    snaps = bandpass(snaps, rng.uniform(2500, 3500), 0.9)
    low = lowpass(thump(0.12, rng.uniform(70, 90), 0.02, rng, drop=0.3), 250)   # soft earth thud
    add(out, sw)
    add(out, sw2, at=rng.uniform(0.045, 0.06), gain=0.45)
    add(out, snaps, at=0.004, gain=0.5)
    add(out, low, at=0.006, gain=0.9)
    return lowpass(highpass(out, 60), 7000)


def impact(rng, surface, strength, plate):
    """A foot (or feet) coming down on the surface; strength > 1 is heavier."""
    out = silence(0.3)
    if surface == "grass":
        add(out, step_grass(rng), gain=0.7 + 0.3 * min(strength, 2))
        add(out, lowpass(thump(0.25, rng.uniform(55, 75), 0.02 + 0.02 * strength, rng, drop=0.3), 220),
            at=0.006, gain=0.4 * strength)
        return gain(out, strength)
    add(out, sneaker_heel(rng))
    if strength > 1:
        add(out, lowpass(thump(0.25, rng.uniform(70, 90), 0.015 * strength, rng, drop=0.3), 300),
            at=0.001, gain=0.4 * (strength - 1))
    if surface == "metal":
        add(out, resonate(rng, 0.08, plate, 0.15 * strength ** 0.5), at=0.0005)
        add(out, bandpass(burst(rng, 0.03, 0.0006, 0.003), rng.uniform(3000, 3800), 1.0), gain=0.25)
    return gain(out, strength)


def friction(rng, surface, dur, plate):
    """Continuous sliding contact (shoes, clothing) against the surface."""
    n = int(dur * SR)
    if surface == "grass":
        x = unit(lowpass(bandpass(crinkle(rng, dur, rng.uniform(300, 450)), rng.uniform(1700, 2300), 0.5), 5000))
        snaps = unit(bandpass(crackle(dur, 25, rng, (1.5, 3.0)), 3000, 0.9))
        low = unit(lowpass(noise(n, rng), 120))
        return unit([a + 0.35 * b + 0.5 * c for a, b, c in zip(x, snaps, low)])
    x = unit(bandpass(crinkle(rng, dur, rng.uniform(700, 900)), rng.uniform(1500, 2000), 0.6))
    grit = unit(highpass(crinkle(rng, dur, 1500), 3500))
    low = unit(lowpass(noise(n, rng), 150))
    mix = [a + 0.2 * b + 0.6 * c for a, b, c in zip(x, grit, low)]
    if surface == "metal":   # the plate 'sings' faintly under the scrape
        sing = [0.0] * n
        for f, amp, _ in plate[:5]:
            sing = [p + q * amp for p, q in zip(sing, bandpass(noise(n, rng), f, 40))]
        mix = [a * 0.8 + 0.6 * b for a, b in zip(unit(mix), unit(sing))]
    return unit(mix)


def push_off(rng, surface, plate, dur, band, scuff_gain, heel_gain, sheen):
    """The foot driving off the ground at the start of a jump or dash."""
    out = []
    if surface == "grass":
        return add(out, highpass(step_grass(rng), 300), gain=0.8)
    add(out, scuff(rng, dur, rng.uniform(*band)), gain=scuff_gain)
    add(out, sneaker_heel(rng), gain=heel_gain)
    if surface == "metal":
        add(out, resonate(rng, 0.06, plate, sheen), at=0.001)
    return out


def metal_clank(rng, modes):
    """A small metal part (zipline trolley) striking something."""
    out = silence(0.3)
    add(out, resonate(rng, 0.3, modes, 0.5))
    add(out, unit(bandpass(burst(rng, 0.04, 0.0003, 0.004), rng.uniform(2200, 2800), 0.9)), gain=0.8)
    add(out, thump(0.08, rng.uniform(140, 170), 0.012, rng, drop=0.2), gain=0.5)
    return out


def trolley_modes(rng):
    return ring_modes(rng, (900, 1100), [1.0, 1.58, 2.37, 3.41, 4.9], (0.06, 0.12), 0.6, 0.3)


def crate_modes(rng):
    return ring_modes(rng, (180, 260), [1.0, 2.3, 3.9, 5.6], (0.02, 0.04), 0.5, 0.5)


# ================================================================ catalogue
@dataclass
class Sound:
    name: str
    group: str            # "surfaces" means one set per surface
    recipe: Callable
    variations: int
    peak_db: float
    pad: bool = True      # add 3 ms lead-in so the edge fade can't blunt an attack at 0

    @property
    def loop(self):
        return self.name.endswith("_loop")


SOUNDS = {}


def sound(group, peak_db, variations=3, pad=True):
    """Register a recipe. peak_db sets its level in the mix (footsteps are -1)."""
    def register(fn):
        SOUNDS[fn.__name__] = Sound(fn.__name__, group, fn, 1 if fn.__name__.endswith("_loop") else variations,
                                    peak_db, pad)
        return fn
    return register


# ================================================================ surface recipes: (rng, surface, plate)
# Footsteps already start 2 ms in, so they skip the lead-in pad (this also keeps
# the approved footstep files byte-identical when regenerated).
@sound("surfaces", -1, variations=6, pad=False)
def footstep(rng, surface, plate):
    if surface == "concrete":
        return step_concrete(rng)
    if surface == "metal":
        return step_metal(rng, plate)
    return step_grass(rng)


@sound("surfaces", -9, variations=6)
def crouch_step(rng, surface, plate):
    out = silence(0.4)
    if surface == "grass":
        add(out, lowpass(step_grass(rng), 3500))
    else:   # slower foot placement: softer heel, longer roll
        add(out, lowpass(sneaker_heel(rng), 900), gain=0.6)
        add(out, sole_roll(rng, 0.1, rng.uniform(800, 1100), 0.18), at=rng.uniform(0.04, 0.06))
        if surface == "metal":
            add(out, resonate(rng, 0.05, plate, 0.05), at=0.001)
    add(out, cloth(rng, 0.25, 2000), gain=0.08)
    return out


@sound("surfaces", -5)
def jump(rng, surface, plate):
    out = silence(0.45)
    add(out, push_off(rng, surface, plate, 0.09, (1200, 1700), scuff_gain=0.9, heel_gain=0.45, sheen=0.08))
    add(out, cloth(rng, 0.22, 2600, sharp=True), at=0.01, gain=0.25)
    add(out, whoosh(rng, 0.3, [500, 1400, 900], peak=0.3), at=0.02, gain=0.35)
    return out


@sound("surfaces", -3)
def dash(rng, surface, plate):
    out = silence(0.55)
    add(out, push_off(rng, surface, plate, 0.12, (1100, 1500), scuff_gain=1.0, heel_gain=0.6, sheen=0.1))
    add(out, whoosh(rng, 0.42, [350, 2200, 700], peak=0.25, q=1.0), at=0.01, gain=0.9)
    add(out, cloth(rng, 0.3, 2800, sharp=True), gain=0.35)
    return out


@sound("surfaces", -2)
def land(rng, surface, plate):
    out = silence(0.5)
    add(out, impact(rng, surface, 1.2, plate))
    add(out, impact(rng, surface, 1.0, plate), at=rng.uniform(0.012, 0.025))   # second foot
    add(out, body_thud(rng, 0.35), at=0.004)
    add(out, cloth(rng, 0.2, 2400, sharp=True), gain=0.18)
    return out


@sound("surfaces", -1)
def land_hard(rng, surface, plate):
    out = silence(0.8)
    add(out, impact(rng, surface, 2.0, plate))
    add(out, impact(rng, surface, 1.8, plate), at=rng.uniform(0.004, 0.01))
    add(out, body_thud(rng, 1.0, freq=rng.uniform(55, 65), decay=0.06), at=0.003)
    add(out, body_thud(rng, 0.35, decay=0.04), at=rng.uniform(0.09, 0.12))    # knees take the weight
    add(out, cloth(rng, 0.4, 2200, sharp=True), gain=0.35)
    if surface == "grass":
        add(out, step_grass(rng), at=0.02, gain=0.6)
    return out


@sound("surfaces", -3, variations=2)
def slide_start(rng, surface, plate):
    out = silence(0.8)
    add(out, body_thud(rng, 0.6))
    add(out, impact(rng, surface, 0.8, plate))
    fr = friction(rng, surface, 0.6, plate)
    add(out, apply_env(fr, curve(len(fr), [(0, 0), (0.12, 1), (0.45, 0.8), (1, 0)])), at=0.005, gain=0.9)
    add(out, cloth(rng, 0.3, 2200, sharp=True), gain=0.3)
    return out


@sound("surfaces", -8)
def slide_loop(rng, surface, plate):
    x = friction(rng, surface, 2.2, plate)
    x = [a * w for a, w in zip(x, periodic(len(x), 1.5, 0.12))]
    return make_loop(x, 2.0, 0.2)


@sound("surfaces", -5, variations=2)
def slide_end(rng, surface, plate):
    out = silence(0.6)
    fr = friction(rng, surface, 0.4, plate)
    add(out, apply_env(fr, curve(len(fr), [(0, 1), (0.7, 0.4), (1, 0)])), gain=0.9)
    add(out, impact(rng, surface, 0.8, plate), at=rng.uniform(0.3, 0.35))      # foot catches, body stops
    add(out, cloth(rng, 0.3, 2200), at=0.25, gain=0.3)
    return out


@sound("surfaces", -3)
def roll(rng, surface, plate):
    out = silence(0.8)
    add(out, body_thud(rng, 0.6))                                              # shoulder down
    add(out, impact(rng, surface, 0.5, plate), gain=0.6)
    add(out, body_thud(rng, 0.45, decay=0.04), at=rng.uniform(0.14, 0.18))     # across the back
    fr = friction(rng, surface, 0.4, plate)
    add(out, apply_env(fr, curve(len(fr), [(0, 0), (0.3, 1), (1, 0)])), at=0.03, gain=0.4)
    add(out, cloth(rng, 0.45, 2200), gain=0.5)
    t = rng.uniform(0.36, 0.42)                                                # back on the feet
    add(out, impact(rng, surface, 0.9, plate), at=t)
    add(out, impact(rng, surface, 0.7, plate), at=t + rng.uniform(0.03, 0.05))
    return out


@sound("surfaces", -4)
def step_up(rng, surface, plate):
    out = silence(0.4)
    if surface == "grass":
        add(out, unit(bandpass(crinkle(rng, 0.06, 300), 2200, 0.6)), gain=0.2)   # toe brushes blades
    else:
        add(out, scuff(rng, 0.05, rng.uniform(1800, 2400)), gain=0.25)          # toe brushes the edge
    add(out, impact(rng, surface, 1.1, plate), at=rng.uniform(0.06, 0.08))
    add(out, cloth(rng, 0.2, 2200), gain=0.12)
    return out


# ================================================================ movement recipes: (rng)
@sound("movement", -6)
def air_jump(rng):
    out = silence(0.4)
    add(out, cloth(rng, 0.18, 2600, sharp=True), gain=0.7)
    add(out, cloth(rng, 0.15, 2200, sharp=True), at=rng.uniform(0.05, 0.07), gain=0.4)
    add(out, whoosh(rng, 0.3, [700, 2000, 1100], peak=0.2, q=1.4), gain=0.8)
    return out


@sound("movement", -4)
def dash_air(rng):
    out = silence(0.5)
    add(out, whoosh(rng, 0.42, [350, 2200, 700], peak=0.25, q=1.0))
    add(out, cloth(rng, 0.3, 2800, sharp=True), gain=0.35)
    return out


@sound("movement", -3)
def wall_jump(rng):
    out = silence(0.5)
    add(out, sneaker_heel(rng), gain=0.9)                                      # foot planted on the wall
    add(out, palm_slap(rng, 0.5), at=rng.uniform(0.005, 0.015))
    add(out, scuff(rng, 0.08, rng.uniform(1300, 1700)), at=0.02, gain=0.6)
    add(out, whoosh(rng, 0.3, [500, 1600, 900], peak=0.3), at=0.04, gain=0.6)
    add(out, cloth(rng, 0.25, 2500, sharp=True), at=0.01, gain=0.3)
    return out


@sound("movement", -1)
def wall_hit(rng):
    out = silence(0.5)
    add(out, body_thud(rng, 1.0, freq=rng.uniform(70, 80), decay=0.045))
    add(out, palm_slap(rng, 0.6), at=rng.uniform(0.008, 0.016))
    add(out, sneaker_heel(rng), at=rng.uniform(0.015, 0.025), gain=0.4)
    add(out, cloth(rng, 0.3, 2300, sharp=True), gain=0.35)
    return out


@sound("movement", -3)
def ceiling_hit(rng):
    out = silence(0.4)
    add(out, body_thud(rng, 0.7, freq=rng.uniform(90, 100), decay=0.025))
    add(out, palm_slap(rng, 0.4), at=rng.uniform(0.004, 0.01))
    add(out, cloth(rng, 0.2, 2300, sharp=True), gain=0.25)
    return lowpass(out, 3000)


@sound("movement", -10)
def crouch(rng):
    out = silence(0.5)
    add(out, cloth(rng, 0.32, 1800))
    add(out, body_thud(rng, 0.25, decay=0.03), at=rng.uniform(0.13, 0.17))    # weight settles
    add(out, sole_roll(rng, 0.06, 1000, 0.4), at=rng.uniform(0.1, 0.14))
    return out


@sound("movement", -10)
def stand(rng):
    out = silence(0.5)
    add(out, cloth(rng, 0.35, 2000))
    add(out, scuff(rng, 0.06, rng.uniform(1200, 1600)), at=rng.uniform(0.15, 0.22), gain=0.3)
    return out


# ================================================================ traversal recipes: (rng)
@sound("traversal", -3)
def ledge_grab(rng):
    out = silence(0.5)
    add(out, palm_slap(rng, 1.0))
    add(out, palm_slap(rng, 0.85), at=rng.uniform(0.018, 0.035))
    add(out, body_thud(rng, 0.4, decay=0.04), at=rng.uniform(0.1, 0.14))      # body swings into the wall
    add(out, cloth(rng, 0.35, 2300, sharp=True), at=0.02, gain=0.35)
    return out


@sound("traversal", -5)
def climb_up(rng):
    out = silence(1.0)
    add(out, scuff(rng, 0.1, 1500), gain=0.4)                                 # palms shift
    add(out, cloth(rng, 0.7, 2100), gain=0.5)
    add(out, body_thud(rng, 0.35), at=rng.uniform(0.18, 0.22))                 # knee onto the ledge
    add(out, scuff(rng, 0.08, 1700), at=rng.uniform(0.28, 0.32), gain=0.3)     # shoe scrapes the wall
    add(out, palm_slap(rng, 0.5), at=rng.uniform(0.33, 0.37))                  # push up
    add(out, sneaker_heel(rng), at=rng.uniform(0.52, 0.58), gain=0.7)          # foot on top
    add(out, sole_roll(rng, 0.06, 1100, 0.15), at=0.6)
    return out


@sound("traversal", -7)
def ledge_drop(rng):
    out = silence(0.4)
    add(out, scuff(rng, 0.05, 1800), gain=0.3)
    add(out, cloth(rng, 0.25, 2500, sharp=True), at=0.01, gain=0.6)
    add(out, whoosh(rng, 0.3, [1200, 700, 400], peak=0.4), gain=0.4)
    return out


@sound("traversal", -4)
def push_grab(rng):
    """Both hands on a wooden crate."""
    out = silence(0.4)
    crate = crate_modes(rng)
    add(out, palm_slap(rng, 0.8))
    add(out, resonate(rng, 0.12, crate, 0.4), at=0.001)
    t = rng.uniform(0.02, 0.04)
    add(out, palm_slap(rng, 0.7), at=t)
    add(out, resonate(rng, 0.12, crate, 0.3), at=t + 0.001)
    add(out, cloth(rng, 0.25, 2200), gain=0.3)
    return out


@sound("traversal", -4)
def vault(rng):
    out = silence(0.6)
    add(out, palm_slap(rng, 1.0))
    add(out, body_thud(rng, 0.3), at=0.005)
    add(out, whoosh(rng, 0.4, [400, 1500, 600], peak=0.4), at=0.03, gain=0.7)
    add(out, cloth(rng, 0.4, 2300), at=0.02, gain=0.4)
    return out


@sound("traversal", -3)
def wall_run_start(rng):
    out = silence(0.5)
    add(out, sneaker_heel(rng))
    add(out, scuff(rng, 0.08, rng.uniform(1300, 1700)), at=0.01, gain=0.6)
    add(out, cloth(rng, 0.25, 2500, sharp=True), gain=0.3)
    add(out, whoosh(rng, 0.35, [400, 1600, 1000], peak=0.3), at=0.01, gain=0.5)
    return out


@sound("traversal", -6)
def wall_run_loop(rng):
    loop_s, xfade = 2.0, 0.15
    steps = 14                                   # 7 steps per second
    period = loop_s / steps
    jitter = [rng.uniform(-0.008, 0.008) for _ in range(steps)]   # repeats every loop
    x = silence(loop_s + xfade + 0.2)
    k = 0
    while k * period < loop_s + xfade:
        t = max(0.0, k * period + jitter[k % steps])
        g = 1.0 if k % 2 == 0 else 0.8           # alternate feet
        add(x, sneaker_heel(rng), at=t, gain=g)
        add(x, scuff(rng, 0.05, rng.uniform(1400, 1900)), at=t + 0.005, gain=0.25 * g)
        k += 1
    n = len(x)
    wind = unit(lowpass(bandpass(noise(n, rng), 600, 0.7), 2000))
    flutter = unit(bandpass(crinkle(rng, n / SR, 200), 2200, 0.6))
    x = [a + 0.15 * w + 0.08 * f for a, w, f in zip(x, wind, flutter)]
    return make_loop(x, loop_s, xfade)


@sound("traversal", -4)
def wall_run_end(rng):
    out = silence(0.5)
    add(out, sneaker_heel(rng))
    add(out, scuff(rng, 0.1, rng.uniform(1300, 1700)), at=0.01, gain=0.7)
    add(out, whoosh(rng, 0.35, [1200, 700, 400], peak=0.3), at=0.02, gain=0.5)
    add(out, cloth(rng, 0.25, 2300, sharp=True), gain=0.3)
    return out


@sound("traversal", -4)
def wall_climb_start(rng):
    out = silence(0.5)
    add(out, palm_slap(rng, 1.0))
    add(out, palm_slap(rng, 0.8), at=rng.uniform(0.025, 0.04))
    add(out, sneaker_heel(rng), at=rng.uniform(0.09, 0.11), gain=0.5)
    for t, g in ((0.08, 0.5), (0.16, 0.4), (0.22, 0.35)):                     # feet scrabble for grip
        add(out, scuff(rng, 0.06, rng.uniform(1300, 1800)), at=t + rng.uniform(-0.01, 0.01), gain=g)
    add(out, cloth(rng, 0.4, 2200), gain=0.4)
    return out


@sound("traversal", -4)
def rope_grab(rng):
    out = silence(0.6)
    add(out, lowpass(palm_slap(rng, 0.8), 1500))
    add(out, lowpass(palm_slap(rng, 0.7), 1500), at=rng.uniform(0.015, 0.03))
    add(out, creak(rng, 0.25, center=rng.uniform(600, 900)), at=0.03, gain=0.5)
    add(out, whoosh(rng, 0.4, [300, 500, 250], peak=0.3), at=0.02, gain=0.35)
    add(out, cloth(rng, 0.3, 2200), gain=0.3)
    return out


@sound("traversal", -6)
def rope_release(rng):
    out = silence(0.6)
    add(out, scuff(rng, 0.08, 1500), gain=0.5)
    add(out, creak(rng, 0.12, center=rng.uniform(600, 900)), gain=0.3)
    add(out, whoosh(rng, 0.45, [250, 450, 200], peak=0.25), at=0.02, gain=0.6)   # rope swings away
    add(out, cloth(rng, 0.25, 2300, sharp=True), gain=0.25)
    return out


ZIP_WHINE_HZ = 700.0   # whole cycles over the 2 s loop, so the loop is phase-continuous


@sound("traversal", -2)
def zipline_on(rng):
    out = silence(1.0)
    add(out, palm_slap(rng, 0.5))
    add(out, metal_clank(rng, trolley_modes(rng)), at=0.01)
    # cable twang: a low string with harmonics, sagging slightly in pitch
    n = int(0.6 * SR)
    f0 = rng.uniform(85, 100)
    ph, twang = 0.0, []
    for i in range(n):
        t = i / SR
        ph += 2 * math.pi * f0 * (1 - 0.03 * (1 - math.exp(-t / 0.1))) / SR
        twang.append(sum(math.sin(h * ph) / h for h in range(1, 7)) * math.exp(-t / 0.18))
    add(out, unit(twang), at=0.012, gain=0.5)
    wh = whine(rng, 0.6, ZIP_WHINE_HZ, 1)
    add(out, apply_env(wh, curve(len(wh), [(0, 0), (1, 1)])), at=0.25, gain=0.25)   # wheels spin up
    add(out, cloth(rng, 0.3, 2300, sharp=True), gain=0.3)
    return out


@sound("traversal", -6)
def zipline_loop(rng):
    loop_s, xfade = 2.0, 0.2
    n = int((loop_s + xfade) * SR)
    wh = whine(rng, n / SR, ZIP_WHINE_HZ, 3, depth=0.012)      # 3 Hz x 2 s = 6 whole wobbles
    wh = [a * w for a, w in zip(wh, periodic(n, 2.5, 0.15))]
    rumble = unit(lowpass(noise(n, rng), 200))
    hiss = unit(bandpass(noise(n, rng), 3500, 1.0))
    wind = unit(bandpass(noise(n, rng), 500, 0.7))
    x = [0.35 * a + 0.6 * b + 0.15 * c + 0.3 * d for a, b, c, d in zip(wh, rumble, hiss, wind)]
    return make_loop(x, loop_s, xfade)


@sound("traversal", -2)
def zipline_off(rng):
    out = silence(0.9)
    wh = whine(rng, 0.35, ZIP_WHINE_HZ * 0.9, 1)
    add(out, apply_env(wh, curve(len(wh), [(0, 1), (1, 0)])), gain=0.25)
    add(out, metal_clank(rng, trolley_modes(rng)), at=rng.uniform(0.04, 0.06))   # hits the end stop
    add(out, scuff(rng, 0.06, 1500), at=0.12, gain=0.3)                         # hands let go
    add(out, cloth(rng, 0.3, 2300, sharp=True), at=0.1, gain=0.35)
    add(out, whoosh(rng, 0.3, [900, 600, 400], peak=0.3), at=0.12, gain=0.3)
    return out


@sound("traversal", -2)
def bounce_pad(rng):
    out = silence(0.9)
    add(out, sneaker_heel(rng), gain=0.6)
    # membrane: low 'whomp' rising in pitch as the pad snaps back
    n = int(0.35 * SR)
    f0 = rng.uniform(65, 80)
    ph, whomp = 0.0, []
    for i in range(n):
        t = i / SR
        ph += 2 * math.pi * f0 * (1 + 1.2 * (1 - math.exp(-t / 0.08))) / SR
        whomp.append(math.sin(ph) * math.exp(-t / 0.09) * min(1.0, t / 0.004))
    add(out, whomp, at=0.002)
    # springs: metallic twang with a fast, fading vibrato
    n = int(0.6 * SR)
    f1 = rng.uniform(260, 320)
    ph, spring = 0.0, []
    for i in range(n):
        t = i / SR
        ph += 2 * math.pi * f1 * (1 + 0.06 * math.sin(2 * math.pi * 14 * t) * math.exp(-t / 0.2)) / SR
        spring.append((math.sin(ph) + 0.5 * math.sin(2.76 * ph) + 0.25 * math.sin(5.4 * ph)) * math.exp(-t / 0.12))
    add(out, unit(spring), at=0.004, gain=0.35)
    add(out, whoosh(rng, 0.4, [400, 1500, 2200], peak=0.35), at=0.05, gain=0.6)
    add(out, cloth(rng, 0.25, 2500, sharp=True), at=0.03, gain=0.3)
    return out


# ================================================================ water recipes: (rng)
@sound("water", -1)
def splash(rng):
    out = silence(1.4)
    n = int(0.08 * SR)
    add(out, unit(lowpass(apply_env(noise(n, rng), env_exp(n, 0.0005, 0.015)), 4000)))   # body hits the surface
    add(out, body_thud(rng, 0.5))
    n = int(0.9 * SR)
    spray = unit(bandpass(crinkle(rng, 0.9, 400), 2500, 0.5))
    add(out, apply_env(spray, env_exp(n, 0.01, 0.25)), at=0.005, gain=0.6)
    n = int(0.6 * SR)
    swoosh = unit(lowpass(noise(n, rng), 500))
    add(out, apply_env(swoosh, env_exp(n, 0.005, 0.25)), gain=0.5)
    add(out, bubbles(rng, 1.0, 120, 200, 1500, 0.0, 0.6), at=0.02, gain=0.5)                       # trapped air
    add(out, bubbles(rng, 1.1, 40, 1000, 4000, 0.3, 1.0, front_load=False), gain=0.25)              # spray falls back
    return out


@sound("water", -4)
def dive_under(rng):
    out = silence(1.3)
    n = int(1.0 * SR)
    rush = unit(lowpass(noise(n, rng), 600))
    add(out, apply_env(rush, env_exp(n, 0.05, 0.4)), gain=0.8)
    add(out, lowpass(bubbles(rng, 1.1, 80, 120, 600, 0.0, 0.9), 1500), at=0.02, gain=0.6)
    add(out, body_thud(rng, 0.4, decay=0.05))
    return lowpass(out, 900)                                                   # heard from under water


@sound("water", -3)
def resurface(rng):
    out = silence(1.5)
    n = int(0.06 * SR)
    add(out, unit(bandpass(apply_env(noise(n, rng), env_exp(n, 0.0008, 0.01)), 1500, 0.7)), gain=0.4)
    n = int(0.9 * SR)
    pour = unit(bandpass(crinkle(rng, 0.9, 350), 1800, 0.5))                    # water runs off
    add(out, apply_env(pour, env_exp(n, 0.02, 0.35)), gain=0.8)
    low = unit(lowpass(noise(n, rng), 300))
    add(out, apply_env(low, env_exp(n, 0.02, 0.2)), gain=0.3)
    add(out, bubbles(rng, 1.4, 25, 1200, 3500, 0.2, 1.2, front_load=False), gain=0.3)   # drips
    return out


# ================================================================ output
def write_wav(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in x))


def finish(x, snd):
    """Trim, de-click and set the level of a rendered sound."""
    if snd.loop:
        return normalize(x, snd.peak_db)
    if snd.pad:
        x = silence(0.003) + x
    return normalize(fade_edges(trim(x)), snd.peak_db)


def jobs(snd, out_dir, seed, surfaces):
    """(rng, render, paths) for each output set of a sound. Every variation is
    drawn from one rng in order, so a sound's files are always the same for a seed."""
    if snd.group != "surfaces":
        rng = random.Random(f"{seed}-{snd.name}")
        yield rng, lambda: snd.recipe(rng), filenames(snd, os.path.join(out_dir, snd.group))
        return
    plate = plate_modes(random.Random(f"{seed}-modes"))   # one steel plate shared by every metal sound
    for s in surfaces:
        # footsteps keep their original seed names so they regenerate unchanged
        rng = random.Random(f"{seed}-{s}" if snd.name == "footstep" else f"{seed}-{snd.name}-{s}")
        yield rng, (lambda r=rng, s=s: snd.recipe(r, s, plate)), filenames(snd, os.path.join(out_dir, "surfaces", s), s)


def filenames(snd, folder, surface=None):
    base = snd.name[:-5] if snd.loop else snd.name
    base += f"_{surface}" if surface else ""
    if snd.loop:
        return [os.path.join(folder, f"{base}_loop.wav")]
    return [os.path.join(folder, f"{base}_{i:02d}.wav") for i in range(1, snd.variations + 1)]


def write_manifest(out_dir):
    """manifest.js lists every WAV, grouped by sound, so preview.html works from disk."""
    manifest = {}
    for root, _, files in sorted(os.walk(out_dir)):
        wavs = sorted(f for f in files if f.endswith(".wav"))
        if not wavs:
            continue
        group = os.path.relpath(root, out_dir).replace(os.sep, "/")
        for f in wavs:
            stem = f[:-4]
            key = stem if stem.endswith("_loop") else stem.rsplit("_", 1)[0]
            manifest.setdefault(group, {}).setdefault(key, []).append(f"{out_dir}/{group}/{f}")
    with open(os.path.join(out_dir, "manifest.js"), "w") as fh:
        fh.write("window.SFX = " + json.dumps(manifest, indent=1) + ";\n")


def main():
    p = argparse.ArgumentParser(description="Generate the game's movement sound effects.",
                                formatter_class=argparse.RawDescriptionHelpFormatter,
                                epilog="examples:\n  python3 make_sfx.py\n  python3 make_sfx.py land splash -f\n"
                                       "  python3 make_sfx.py jump --surface metal -f")
    p.add_argument("names", nargs="*", metavar="NAME", help="sounds to make (default: all); see --list")
    p.add_argument("-f", "--overwrite", action="store_true", help="replace files that already exist")
    p.add_argument("--surface", nargs="+", choices=SURFACES, default=list(SURFACES),
                   help="limit surface sounds to these surfaces")
    p.add_argument("--list", action="store_true", help="list sound names and exit")
    p.add_argument("--out", default="sfx", help="output folder (default: sfx)")
    p.add_argument("--seed", type=int, default=1, help="change for a different set of variations")
    args = p.parse_args()

    if args.list:
        for group in dict.fromkeys(s.group for s in SOUNDS.values()):
            names = [s.name for s in SOUNDS.values() if s.group == group]
            print(f"{group + (' (per surface)' if group == 'surfaces' else ''):24s} {' '.join(names)}")
        return
    unknown = [n for n in args.names if n not in SOUNDS]
    if unknown:
        p.error(f"unknown sound(s): {', '.join(unknown)}. Run with --list to see names.")

    written = kept = 0
    for snd in (SOUNDS[n] for n in (args.names or SOUNDS)):
        for rng, render, paths in jobs(snd, args.out, args.seed, args.surface):
            if not args.overwrite and all(os.path.exists(pth) for pth in paths):
                kept += len(paths)
                continue
            os.makedirs(os.path.dirname(paths[0]), exist_ok=True)
            for pth in paths:   # render every variation in order, even ones we keep
                x = finish(render(), snd)
                if args.overwrite or not os.path.exists(pth):
                    write_wav(pth, x)
                    written += 1
                    print(f"{pth}  {len(x) / SR:.2f}s")
                else:
                    kept += 1
    write_manifest(args.out)
    print(f"{written} written, {kept} kept" + ("" if args.overwrite or not kept else " (use -f to replace)"))


if __name__ == "__main__":
    main()
