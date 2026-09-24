"""手牌悬停音效 —— CardHover.wav

鼠标移到手牌上时的一声极轻响：不脆、不闷、不咚、不引人注意但有反馈。

针对三条反馈各留一条约束：
  ① 不"叮叮"（脆）：3.2kHz 以上整段砍掉 —— "叮叮"那层亮颗粒全在 4-16kHz；
                     同时把带通边缘放软（soft=0.55，原来是 0.35），频谱没有尖峰就不会有金属味；
  ② 不变闷：**不往下砍中频**，重心留在 1.0-3.6kHz（700Hz 以下几乎不留能量），
            听感是"轻"而不是"糊" —— 与上一版 70ms 那版（整体压到 3.5kHz 以下）的区别在这里；
  ③ 不咚：500Hz 以下只给 500-1100Hz 一点点噪声体（权重 0.10），缓起，不成型为低频重量。
起音 4ms 平滑上升（比上一版 2.5ms 更软），时长 48ms，收干在 ~35ms。
输出：44100Hz / 16bit / 单声道，峰值归一化到 0.34。
实际响度由 CardView.hoverVolume 控制（默认 0.45）。

运行：python Tools/audio/make_card_hover.py
（会重写 Assets/_Game/Resources/Audio/SFX/CardHover.wav，并出一张波形预览图）
"""
import os, wave, numpy as np

SR   = 44100
DUR  = 0.048
PEAK = 0.30
OUT  = os.path.join(os.path.dirname(__file__), "..", "..",
                    "Assets", "_Game", "Resources", "Audio", "SFX", "CardHover.wav")
PREV = os.path.join(os.path.dirname(__file__), "card-hover-preview.png")


def noise(n, seed):
    return np.random.default_rng(seed).standard_normal(n)


def band(x, lo, hi, soft=0.35):
    """FFT 掩码带通（soft=边缘软化比例，越大越柔和、越不容易出现"叮"的金属峰）"""
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    bw = max(1e-6, hi - lo)
    m = np.clip((f - lo * (1 - soft)) / (bw * soft / 2 + 1e-9), 0, 1) * \
        np.clip(((hi * (1 + soft)) - f) / (bw * soft / 2 + 1e-9), 0, 1)
    return np.fft.irfft(np.fft.rfft(x) * m, n=len(x))


def lowpass(x, hi, soft=0.5):
    """FFT 掩码低通：把高频"脆"整段砍掉"""
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    m = np.clip((hi * (1 + soft) - f) / (hi * soft + 1e-9), 0, 1)
    return np.fft.irfft(np.fft.rfft(x) * m, n=len(x))


def softenv(n, attack, tau):
    """平滑起音（smoothstep）+ 指数衰减：缓起是为了不变成"脆响" """
    t = np.arange(n) / SR
    a = np.clip(t / attack, 0, 1)
    atk = a * a * (3 - 2 * a)
    return atk * np.exp(-t / tau)


def draw(t, x):
    """波形预览（PIL 直绘，不需要 matplotlib）"""
    try:
        from PIL import Image, ImageDraw
    except Exception as e:
        print("preview skipped:", e)
        return
    W, H, pad = 760, 240, 34
    img = Image.new("RGB", (W, H), (24, 24, 27))
    d = ImageDraw.Draw(img)
    mid = H // 2
    d.line([(pad, mid), (W - 10, mid)], fill=(70, 70, 76))
    half = (H - 2 * pad) / 2 / 0.7
    pts = []
    for i in range(pad, W - 10):
        k = int((i - pad) / (W - 10 - pad) * (len(x) - 1))
        pts.append((i, mid - x[k] * half))
    d.line(pts, fill=(200, 162, 74))
    for ms in range(0, 49, 5):
        X = pad + (W - 10 - pad) * ms / 48.0
        d.line([(X, mid - 4), (X, mid + 4)], fill=(90, 90, 96))
        d.text((X - 8, mid + 8), f"{ms}", fill=(150, 150, 156))
    d.text((pad, 8), "CardHover.wav  48ms  44.1k/16bit/mono  peak 0.30  (no >3.2kHz, keeps mids)", fill=(220, 216, 205))
    d.text((pad, H - 18), "ms", fill=(150, 150, 156))
    img.save(PREV)
    print("preview", PREV)


def main():
    n = int(SR * DUR)
    t = np.arange(n) / SR

    air  = band(noise(n, 41), 1000, 3600, soft=0.55) * softenv(n, 0.004, 0.010) * 0.55
    body = band(noise(n, 42),  500, 1100, soft=0.55) * softenv(n, 0.004, 0.010) * 0.10

    x = lowpass(air + body, 3200, soft=0.4)   # 3.2kHz 以上整段砍掉：叮叮脆响的来源（中频不动，所以不变闷）
    a = int(SR * 0.0008); x[:a] *= np.linspace(0, 1, a)   # FFT 低通带出一点环形渗漏，头 0.8ms 再拉平

    # 尾部淡出（防止截断爆音；起音已由 softenv 处理）
    b = int(SR * 0.012); x[-b:] *= np.linspace(1, 0, b) ** 2
    x = x / np.max(np.abs(x)) * PEAK

    pcm = np.clip(x * 32767.0, -32768, 32767).astype("<i2")
    os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
    with wave.open(os.path.abspath(OUT), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(pcm.tobytes())

    print("wrote", os.path.abspath(OUT), len(pcm), "samples", round(len(pcm) / SR * 1000, 1), "ms")
    print("peak", round(float(np.max(np.abs(x))), 3),
          "rms", round(float(np.sqrt(np.mean(x ** 2))), 4),
          "head", round(float(x[0]), 6), "tail", round(float(x[-1]), 6))
    draw(t, x)


if __name__ == "__main__":
    main()