# -*- coding: utf-8 -*-
import urllib.request, ssl, json, os, random, time
OUT = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\cardref"
os.makedirs(OUT, exist_ok=True)
ctx = ssl.create_default_context(); ctx.check_hostname=False; ctx.verify_mode=ssl.CERT_NONE
H = {"User-Agent": "Mozilla/5.0 (CardArtResearch)", "Accept": "application/json"}
def get(url, timeout=60, noverify=False):
    return urllib.request.urlopen(urllib.request.Request(url, headers=H), timeout=timeout,
                                  context=ctx if noverify else None).read()
def save(game, name, data):
    d = os.path.join(OUT, game); os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name)
    with open(p, "wb") as f: f.write(data)
    return len(data)

random.seed(20260926)
report = []

# ---------- MTG (art crop + 3 full cards) ----------
try:
    d = json.loads(get("https://api.scryfall.com/cards/search?q=is%3Acreature&order=random&unique=art"))
    cards = [c for c in d["data"] if c.get("image_uris", {}).get("art_crop")]
    for c in cards[:24]:
        u = c["image_uris"]["art_crop"]
        try: save("mtg", "%s.png" % c["id"], get(u))
        except Exception as e: print("mtg img fail", c["name"], e)
    for c in cards[:3]:
        try: save("mtg", "FULL_%s.png" % c["id"], get(c["image_uris"]["normal"]))
        except Exception as e: print("mtg full fail", e)
    report.append(("mtg", len(cards[:24])))
    print("mtg ok", len(cards[:24]))
except Exception as e:
    print("mtg FAIL", repr(e)[:200])

# ---------- Hearthstone (full art, no frame) ----------
try:
    hs = json.loads(get("https://api.hearthstonejson.com/v1/latest/enUS/cards.collectible.json"))
    pool = [c for c in hs if c.get("type") in ("MINION", "HERO") and c.get("id") and not c.get("set") == "HERO_SKINS"]
    random.shuffle(pool)
    ok = 0
    for c in pool[:40]:
        if ok >= 24: break
        try:
            save("hearthstone", "%s.png" % c["id"], get("https://art.hearthstonejson.com/v1/orig/%s.png" % c["id"]))
            ok += 1
        except Exception:
            pass
    for c in pool[:3]:
        try: save("hearthstone", "FULL_%s.png" % c["id"],
                 get("https://art.hearthstonejson.com/v1/render/latest/enUS/512x/%s.png" % c["id"]))
        except Exception as e: print("hs full fail", e)
    report.append(("hearthstone", ok)); print("hearthstone ok", ok)
except Exception as e:
    print("hearthstone FAIL", repr(e)[:200])

# ---------- Yu-Gi-Oh (art crop + full) ----------
try:
    off = random.randint(0, 8000)
    d = json.loads(get("https://db.ygoprodeck.com/api/v7/cardinfo.php?num=24&offset=%d&type=Effect%%20Monster" % off))
    ok = 0
    for c in d["data"][:24]:
        img = c["card_images"][0]
        try:
            save("yugioh", "%s_crop.jpg" % img["id"], get(img["image_url_cropped"])); ok += 1
        except Exception as e: print("ygo fail", c["name"], e)
    for c in d["data"][:3]:
        img = c["card_images"][0]
        try: save("yugioh", "FULL_%s.jpg" % img["id"], get(img["image_url"]))
        except Exception: pass
    report.append(("yugioh", ok)); print("yugioh ok", ok)
except Exception as e:
    print("yugioh FAIL", repr(e)[:200])

# ---------- Legends of Runeterra (full art) ----------
try:
    meta = json.loads(get("https://dd.b.pvp.net/latest/set1/en_us/data/set1-en_us.json", noverify=True))
    champs = [c for c in meta if c.get("rarity") == "Champion" and c.get("assets")]
    random.shuffle(champs)
    pool = (champs + [c for c in meta if c.get("assets")])[:24]
    ok = 0
    for c in pool[:30]:
        if ok >= 24: break
        try:
            a = c["assets"][0]
            u = a.get("fullAbsolutePath") or a.get("gameAbsolutePath")
            save("runeterra", "%s.png" % c["cardCode"], get(u, noverify=True)); ok += 1
        except Exception:
            pass
    for c in pool[:3]:
        try:
            a = c["assets"][0]
            save("runeterra", "FULL_%s.png" % c["cardCode"], get(a["gameAbsolutePath"], noverify=True))
        except Exception: pass
    report.append(("runeterra", ok)); print("runeterra ok", ok)
except Exception as e:
    print("runeterra FAIL", repr(e)[:200])

print("SUMMARY", report)
