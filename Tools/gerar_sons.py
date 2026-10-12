"""
Gera os sons provisórios do Apagão por síntese (spec 21).
Saída: Assets/_Apagao/Resources/Audio/*.wav (mono, 16 bits, 22050 Hz).
Uso: python3 Tools/gerar_sons.py [pasta_de_saida]
Trocar um som por um real = salvar um .wav com o mesmo nome por cima.
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 22050
rng = np.random.default_rng(7)
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "_Apagao", "Resources", "Audio")


# ---------- utilidades ----------

def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def noise(dur):
    return rng.uniform(-1, 1, int(dur * SR))


def lp(x, hz, order=2):
    b, a = signal.butter(order, min(hz, SR / 2 - 100) / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def hp(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bp(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def env(n, attack=0.005, decay=0.2, curve=4.0):
    """Envelope ataque linear + queda exponencial (em segundos)."""
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-np.maximum(t - attack, 0) * curve / max(decay, 1e-4))
    return a * d


def sweep_sine(f0, f1, dur, kind="exp"):
    t = t_axis(dur)
    if kind == "exp":
        f = f0 * (f1 / f0) ** (t / dur)
    else:
        f = f0 + (f1 - f0) * t / dur
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def saw(freq, dur):
    t = t_axis(dur)
    return signal.sawtooth(2 * np.pi * freq * t)


def metal(dur, base, decay, partials=(1.0, 2.76, 5.4, 8.93, 13.3)):
    t = t_axis(dur)
    x = np.zeros_like(t)
    for i, p in enumerate(partials):
        x += np.sin(2 * np.pi * base * p * t + rng.uniform(0, 6)) * np.exp(-t * (3 + i * 2.5) / decay) / (i + 1)
    return x


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def pad(x, dur):
    n = int(dur * SR)
    return np.pad(x, (0, max(0, n - len(x))))[:n] if len(x) < n else x


def norm(x, peak=0.9):
    m = np.max(np.abs(x))
    return x / m * peak if m > 0 else x


def fade(x, fin=0.003, fout=0.02):
    n = len(x)
    a, b = int(fin * SR), int(fout * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def make_loop(x, cross=0.5):
    """Funde o fim no começo para o loop não estalar."""
    c = int(cross * SR)
    head, body, tail = x[:c], x[c:-c], x[-c:]
    w = np.linspace(0, 1, c)
    blended = tail * (1 - w) + head * w
    return np.concatenate([body, blended])


def save(name, x, peak=0.9, loop=False):
    x = norm(np.asarray(x, dtype=np.float64), peak)
    if not loop:
        x = fade(x)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ---------- Caio ----------

def footstep(i):
    n = int(0.16 * SR)
    thump = np.sin(2 * np.pi * (70 + 15 * i) * t_axis(0.16)) * env(n, 0.002, 0.05, 5)
    grit = bp(noise(0.16), 400 + 120 * i, 2600) * env(n, 0.001, 0.06, 5)
    return mix(thump * 0.7, grit)


def build_player():
    for i in range(4):
        save(f"passo_{i + 1}", footstep(i), 0.8)
    n = int(0.4 * SR)
    save("queda", mix(np.sin(2 * np.pi * 55 * t_axis(0.4)) * env(n, 0.003, 0.15, 4),
                      bp(noise(0.4), 200, 1800) * env(n, 0.002, 0.1, 5)))
    n = int(0.4 * SR)
    wh = bp(noise(0.4), 300, 3000) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2
    save("esquiva", wh, 0.6)
    n = int(0.5 * SR)
    save("escalada", mix(bp(noise(0.5), 800, 4000) * env(n, 0.05, 0.3, 3) * 0.6,
                         np.sin(2 * np.pi * 90 * t_axis(0.5)) * env(n, 0.003, 0.08, 4)), 0.6)
    n = int(0.35 * SR)
    save("dano_caio", mix(np.sin(2 * np.pi * 85 * t_axis(0.35)) * env(n, 0.001, 0.12, 4),
                          lp(noise(0.35), 1200) * env(n, 0.001, 0.05, 5)), 0.85)
    n = int(2.0 * SR)
    save("morte", mix(np.sin(2 * np.pi * 45 * t_axis(2.0)) * env(n, 0.01, 1.2, 3),
                      np.sin(2 * np.pi * 440 * t_axis(2.0)) * env(n, 0.3, 1.5, 3) * 0.15), 0.8)
    n = int(0.7 * SR)
    crackle = hp(noise(0.7), 1500) * (rng.random(n) < 0.08) * env(n, 0.02, 0.6, 2)
    save("cura", crackle + bp(noise(0.7), 1000, 5000) * env(n, 0.05, 0.4, 3) * 0.3, 0.5)


# ---------- armas ----------

def build_weapons():
    n = int(0.25 * SR)
    save("facao_leve", bp(noise(0.25), 900, 5000) * np.sin(np.pi * np.linspace(0, 1, n)) ** 3, 0.55)
    n = int(0.45 * SR)
    save("facao_pesado", bp(noise(0.45), 500, 3500) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2, 0.7)
    save("acerto_metal", mix(metal(0.6, 520, 0.5), hp(noise(0.6), 2000) * env(int(0.6 * SR), 0.001, 0.03, 5)), 0.8)
    n = int(1.0 * SR)
    fizz = hp(noise(1.0), 3000) * (rng.random(n) < 0.15) * env(n, 0.05, 0.8, 3)
    save("takedown", mix(metal(1.0, 300, 0.7), fizz * 0.6), 0.85)

    def gun(dur, body, crack_hz, tail):
        n = int(dur * SR)
        crack = hp(noise(dur), crack_hz) * env(n, 0.0005, 0.02, 5)
        boom = lp(noise(dur), 900) * env(n, 0.001, tail, 3)
        low = np.sin(2 * np.pi * body * t_axis(dur)) * env(n, 0.001, tail * 0.5, 4)
        return mix(crack, boom * 1.2, low)
    save("revolver", gun(0.7, 70, 2500, 0.35), 0.95)
    save("espingarda", gun(1.0, 55, 1500, 0.55), 0.95)

    def click(f, d=0.04):
        n = int(d * SR)
        return hp(noise(d), f) * env(n, 0.0005, 0.01, 5)
    x = np.zeros(int(1.2 * SR))
    for at, f in ((0.05, 2500), (0.35, 1800), (0.8, 3000), (1.0, 2200)):
        c = click(f)
        s = int(at * SR)
        x[s:s + len(c)] += c
    save("recarga", x, 0.6)
    save("vazio", pad(click(3500, 0.03), 0.1), 0.5)

    n = int(0.7 * SR)
    charge = sweep_sine(200, 2400, 0.7) * np.linspace(0, 1, n) ** 2 * 0.4
    crack = hp(noise(0.7), 2000) * (rng.random(n) < 0.3) * env(n, 0.3, 0.3, 3)
    save("pem", mix(charge, crack), 0.8)
    n = int(0.3 * SR)
    save("arremesso", bp(noise(0.3), 600, 3000) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2, 0.5)
    n = int(0.8 * SR)
    shards = sum(np.sin(2 * np.pi * f * t_axis(0.8)) * np.exp(-t_axis(0.8) * rng.uniform(6, 15))
                 for f in rng.uniform(2500, 7000, 10))
    save("vidro", mix(hp(noise(0.8), 3000) * env(n, 0.001, 0.15, 4), shards * 0.15), 0.8)

    # fogo: estalos sobre ronco grave
    d = 4.0
    n = int(d * SR)
    roar = lp(noise(d), 500) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.7 * t_axis(d)))
    pops = hp(noise(d), 2500) * (rng.random(n) < 0.004)
    pops = lp(pops, 6000) * 3
    save("fogo_loop", make_loop(roar + pops, 0.4), 0.6, loop=True)

    x = np.zeros(int(1.4 * SR))
    for k in range(4):
        b = np.sign(np.sin(2 * np.pi * (1800 if k % 2 == 0 else 1400) * t_axis(0.15))) * 0.6
        s = int(k * 0.3 * SR)
        x[s:s + len(b)] += b
    save("bomba_ruido", lp(x, 5000), 0.8)

    d = 2.0
    hum = saw(100, d) * 0.4 + np.sin(2 * np.pi * 200 * t_axis(d)) * 0.3
    buzz = hp(noise(d), 3000) * (rng.random(int(d * SR)) < 0.02)
    save("isca_loop", make_loop(lp(hum, 2000) + buzz, 0.2), 0.5, loop=True)

    n = int(0.25 * SR)
    save("coleta", mix(np.sin(2 * np.pi * 880 * t_axis(0.25)) * env(n, 0.002, 0.08, 4),
                       np.sin(2 * np.pi * 1320 * t_axis(0.25)) * env(n, 0.04, 0.1, 4) * 0.6), 0.45)
    x = np.zeros(int(0.9 * SR))
    for k in range(3):
        c = mix(metal(0.25, 800 + 100 * k, 0.15) * 0.5, lp(noise(0.25), 3000) * env(int(0.25 * SR), 0.001, 0.02, 5))
        s = int(k * 0.28 * SR)
        x[s:s + len(c)] += c
    save("craft", x, 0.6)
    n = int(1.2 * SR)
    save("checkpoint", mix(np.sin(2 * np.pi * 660 * t_axis(1.2)) * env(n, 0.01, 0.6, 3),
                           np.sin(2 * np.pi * 990 * t_axis(1.2)) * env(n, 0.15, 0.7, 3) * 0.7), 0.35)


# ---------- máquinas ----------

def build_machines():
    d = 3.0
    t = t_axis(d)
    servo = saw(55, d) * 0.5 + saw(55.7, d) * 0.5
    servo = lp(servo, 400) * (0.75 + 0.25 * np.sin(2 * np.pi * 1.3 * t))
    whine = np.sin(2 * np.pi * (900 + 60 * np.sin(2 * np.pi * 0.5 * t)) * t) * 0.05
    save("sentinela_servo_loop", make_loop(servo + whine, 0.3), 0.5, loop=True)

    save("sentinela_passo", mix(metal(0.3, 180, 0.15) * 0.6, np.sin(2 * np.pi * 60 * t_axis(0.3)) * env(int(0.3 * SR), 0.001, 0.08, 4)), 0.7)
    n = int(0.5 * SR)
    save("sentinela_mira", sweep_sine(800, 2000, 0.5) * np.linspace(0.2, 1, n) * 0.5, 0.5)
    n = int(0.25 * SR)
    zap = sweep_sine(2500, 300, 0.25) * env(n, 0.001, 0.12, 4)
    save("sentinela_tiro", mix(zap, hp(noise(0.25), 2000) * env(n, 0.0005, 0.02, 5)), 0.85)

    x = np.concatenate([np.sin(2 * np.pi * 740 * t_axis(0.15)), np.sin(2 * np.pi * 988 * t_axis(0.3))])
    x = np.sign(x) * 0.5 + x * 0.5
    save("alerta", lp(x, 4000) * env(len(x), 0.005, 0.4, 2), 0.6)
    n = int(0.3 * SR)
    save("suspeito", sweep_sine(500, 800, 0.3) * env(n, 0.01, 0.2, 3), 0.35)

    save("cao_passo", mix(metal(0.12, 900, 0.05) * 0.5, hp(noise(0.12), 2000) * env(int(0.12 * SR), 0.0005, 0.02, 5)), 0.5)
    d = 0.6
    t = t_axis(d)
    growl = signal.sawtooth(2 * np.pi * np.cumsum(70 + 12 * np.sin(2 * np.pi * 9 * t)) / SR)
    growl = lp(growl, 900) * (0.6 + 0.4 * np.sin(2 * np.pi * 23 * t)) * env(len(t), 0.05, 0.5, 2)
    save("cao_rosnado", growl, 0.7)
    save("cao_mordida", mix(metal(0.3, 700, 0.12), lp(noise(0.3), 2500) * env(int(0.3 * SR), 0.0005, 0.04, 5)), 0.8)

    d = 1.4
    n = int(d * SR)
    crunch = lp(noise(d), 2000) * env(n, 0.001, 0.4, 3)
    sparks = hp(noise(d), 3500) * (rng.random(n) < 0.05) * env(n, 0.05, 1.0, 2)
    save("robo_destruido", mix(crunch, metal(d, 240, 0.6) * 0.5, sparks * 0.7, np.sin(2 * np.pi * 50 * t_axis(d)) * env(n, 0.001, 0.3, 3)), 0.9)

    # drones
    d = 2.0
    t = t_axis(d)
    blade = sum(np.sin(2 * np.pi * 110 * k * t + rng.uniform(0, 6)) / k for k in range(1, 8))
    rotor = blade * (0.8 + 0.2 * np.sin(2 * np.pi * 37 * t)) + bp(noise(d), 300, 2500) * 0.4
    save("rotor_loop", make_loop(lp(rotor, 4000), 0.2), 0.5, loop=True)
    n = int(0.6 * SR)
    save("olho_scanner", sweep_sine(1200, 3000, 0.6, "lin") * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 20 * t_axis(0.6)))) * env(n, 0.01, 0.5, 2), 0.3)
    n = int(0.08 * SR)
    save("vespa_trava", np.sin(2 * np.pi * 2200 * t_axis(0.08)) * env(n, 0.001, 0.06, 3), 0.5)

    d = 2.5
    n = int(d * SR)
    boom = lp(noise(d), 600) * env(n, 0.002, 1.0, 3)
    sub = np.sin(2 * np.pi * 40 * t_axis(d)) * env(n, 0.002, 0.6, 3)
    crack = hp(noise(d), 1500) * env(n, 0.0005, 0.05, 5)
    debris = hp(noise(d), 2500) * (rng.random(n) < 0.01) * env(n, 0.3, 1.5, 2)
    save("explosao", mix(boom * 1.4, sub, crack, debris), 0.95)

    d = 3.0
    flame = lp(noise(d), 700) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.9 * t_axis(d)))
    turbine = np.sin(2 * np.pi * 1600 * t_axis(d)) * 0.06
    save("brasa_chama_loop", make_loop(flame + turbine, 0.3), 0.55, loop=True)
    n = int(1.0 * SR)
    save("brasa_despejo", mix(bp(noise(1.0), 300, 3000) * np.sin(np.pi * np.linspace(0, 1, n)), lp(noise(1.0), 400) * env(n, 0.5, 0.5, 3)), 0.7)

    # Brutamontes
    d = 0.6
    n = int(d * SR)
    save("bruto_passo", mix(np.sin(2 * np.pi * 38 * t_axis(d)) * env(n, 0.002, 0.3, 3) * 1.4, metal(d, 120, 0.3) * 0.4,
                            lp(noise(d), 800) * env(n, 0.001, 0.08, 5)), 0.95)
    n = int(0.6 * SR)
    save("bruto_soco", mix(bp(noise(0.6), 200, 2000) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2,
                           np.sin(2 * np.pi * 60 * t_axis(0.6)) * env(n, 0.35, 0.2, 4)), 0.85)
    d = 1.2
    t = t_axis(d)
    motor = signal.sawtooth(2 * np.pi * np.cumsum(40 + 80 * t / d) / SR)
    save("bruto_investida", lp(motor, 1200) * np.linspace(0.3, 1, len(t)) * env(len(t), 0.05, 1.0, 1), 0.8)
    d = 1.5
    n = int(d * SR)
    save("bruto_parede", mix(lp(noise(d), 1500) * env(n, 0.001, 0.5, 3) * 1.3, metal(d, 150, 0.8) * 0.6,
                             np.sin(2 * np.pi * 35 * t_axis(d)) * env(n, 0.001, 0.5, 3)), 0.95)

    # antena
    d = 2.0
    t = t_axis(d)
    hum = np.sin(2 * np.pi * 60 * t) + 0.5 * np.sin(2 * np.pi * 120 * t) + 0.25 * np.sin(2 * np.pi * 180 * t)
    whine = np.sin(2 * np.pi * 3100 * t) * 0.04
    save("antena_loop", make_loop(hum * 0.5 + whine, 0.2), 0.45, loop=True)
    d = 1.5
    n = int(d * SR)
    save("antena_sabotada", mix(hp(noise(d), 2000) * (rng.random(n) < 0.2) * env(n, 0.001, 0.4, 3),
                                sweep_sine(400, 40, d) * env(n, 0.01, 1.2, 2) * 0.6), 0.85)


# ---------- rádio, ambiente e música ----------

def build_world():
    d = 3.0
    save("radio_estatica_loop", make_loop(bp(noise(d), 500, 3500) * (0.8 + 0.2 * (rng.random(int(d * SR)) < 0.1)), 0.2), 0.4, loop=True)
    n = int(0.25 * SR)
    save("radio_bip", np.sin(2 * np.pi * 1000 * t_axis(0.25)) * env(n, 0.005, 0.2, 2), 0.4)

    # mar: ondas lentas
    d = 14.0
    t = t_axis(d)
    swell = np.clip(np.sin(2 * np.pi * t / 7.0) * 0.5 + 0.5, 0, 1) ** 2
    sea = lp(noise(d), 700) * (0.25 + swell) + hp(lp(noise(d), 4000), 1500) * swell ** 3 * 0.4
    save("mar_loop", make_loop(sea, 1.5), 0.5, loop=True)

    # vento: ruído filtrado com rajadas
    d = 12.0
    t = t_axis(d)
    gust = 0.5 + 0.5 * np.sin(2 * np.pi * t / 6.0 + 1) * np.sin(2 * np.pi * t / 2.3)
    wind = bp(noise(d), 250, 1200) * (0.4 + 0.6 * gust ** 2)
    save("vento_loop", make_loop(wind, 1.5), 0.4, loop=True)

    # cigarras: chiado agudo pulsando
    d = 8.0
    t = t_axis(d)
    chirp = bp(noise(d), 4200, 6500, 4) * (0.5 + 0.5 * np.sin(2 * np.pi * 45 * t)) ** 2
    chirp *= 0.6 + 0.4 * np.sin(2 * np.pi * t / 4.0) ** 2
    save("cigarras_loop", make_loop(chirp, 1.0), 0.35, loop=True)

    # música de tensão: pad grave em ré menor, lento
    d = 16.0
    t = t_axis(d)
    pad_ = np.zeros_like(t)
    for f, amp in ((73.4, 1.0), (110.0, 0.6), (87.3, 0.5), (146.8, 0.25)):
        pad_ += (signal.sawtooth(2 * np.pi * f * t) + signal.sawtooth(2 * np.pi * f * 1.004 * t)) * amp
    pad_ = lp(pad_, 600) * (0.7 + 0.3 * np.sin(2 * np.pi * t / 8.0))
    heart = np.zeros_like(t)
    for k in range(int(d / 1.0)):
        s = int(k * SR)
        b = np.sin(2 * np.pi * 50 * t_axis(0.25)) * env(int(0.25 * SR), 0.005, 0.1, 4)
        heart[s:s + len(b)] += b[: len(heart) - s] * 0.8
    save("musica_tensao_loop", make_loop(pad_ * 0.35 + heart, 1.0), 0.5, loop=True)

    # música de combate: 120 bpm, bumbo, baixo em semicolcheias e estalo
    bpm, bars = 120, 4
    d = bars * 4 * 60 / bpm
    n = int(d * SR)
    x = np.zeros(n)
    beat = 60 / bpm
    notes = [36.7, 36.7, 43.65, 41.2]  # ré, ré, fá, mi (grave)
    for b in range(bars * 4):
        s = int(b * beat * SR)
        kick = np.sin(2 * np.pi * np.cumsum(np.linspace(120, 45, int(0.25 * SR))) / SR) * env(int(0.25 * SR), 0.001, 0.12, 4)
        x[s:s + len(kick)] += kick[: n - s]
        if b % 2 == 1:
            sn = hp(noise(0.15), 1500) * env(int(0.15 * SR), 0.001, 0.05, 5) * 0.5
            x[s:s + len(sn)] += sn[: n - s]
        f = notes[(b // 4) % 4] * 2
        for k in range(4):
            s2 = int((b * beat + k * beat / 4) * SR)
            note = lp(signal.sawtooth(2 * np.pi * f * t_axis(beat / 4)), 700) * env(int(beat / 4 * SR), 0.002, 0.08, 3) * 0.35
            x[s2:s2 + len(note)] += note[: n - s2]
    save("musica_combate_loop", x, 0.6, loop=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    build_player()
    build_weapons()
    build_machines()
    build_world()
    print("sons gerados em", OUT)
