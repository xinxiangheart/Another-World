# -*- coding: utf-8 -*-
"""把 cardframe-v7 装进 Resources/（原地覆盖，先全量备份）

用法：仓库根目录下  python Tools/cardframe/InstallCardFrameV7.py
回滚：用打印出来的备份目录整目录覆盖回去即可。
"""
import os, sys, shutil, datetime, importlib.util
from PIL import Image, ImageDraw

ROOT = os.getcwd()
assert os.path.isdir(os.path.join(ROOT, "Assets", "_Game")), "请在仓库根目录运行"

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("cf", os.path.join(HERE, "CardFrameV7.py"))
cf = importlib.util.module_from_spec(spec); spec.loader.exec_module(cf)

SRC = os.path.join(ROOT, cf.OUT)
R   = os.path.join(ROOT, "Assets", "_Game", "Resources")

STAMP = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
BK = os.path.join(os.environ.get("TEMP", "."), "cardframe-v7-backup-" + STAMP)


def bake(layers, out):
    L = Image.new("RGBA", (cf.W, cf.H), (0, 0, 0, 0))
    for n in layers:
        L.alpha_composite(Image.open(os.path.join(SRC, n)).convert("RGBA"))
    L.save(out)


# ── 先烘六档整框（预制体现在还是「一张 CostFrameBase」，拆层是下一步）──
tmp = os.path.join(os.environ.get("TEMP", "."), "cardframe-v7-baked")
os.makedirs(tmp, exist_ok=True)
for i in range(6):
    bake(["Card_Body.png", "Card_Edge_%d.png" % i, "Card_NamePlate.png",
          "Card_ArtWindow.png", "Card_StatPlate.png"], os.path.join(tmp, "SummonCard_%d.png" % i))
    bake(["Card_Body_Spell.png", "Card_Edge_%d.png" % i, "Card_NamePlate.png",
          "Card_ArtWindow_Spell.png", "Card_StatPlate_Spell.png"], os.path.join(tmp, "SpellCard_%d.png" % i))

ICON = [
    ("Badge_Cost.png",           "UI/Cost.png"),
    ("Badge_Attack.png",         "UI/Attack.png"),
    ("Badge_Health.png",         "UI/Health.png"),
    ("Badge_Type_Hero.png",      "UI/Hero.png"),
    ("Badge_Type_Chosen.png",    "UI/Chosen.png"),
    ("Badge_Type_Special.png",   "UI/Special.png"),
    ("Icon_Trait_First.png",     "UI/First.png"),
    ("Icon_Trait_Enter.png",     "UI/Enter.png"),
    ("Icon_Trait_Leave.png",     "UI/Leave.png"),
    ("Icon_Trait_Exit.png",      "UI/Exit.png"),
    ("Icon_Trait_Revenge.png",   "UI/Reverge.png"),
    ("Icon_Trait_Discard.png",   "UI/Discard.png"),
    ("Icon_Trait_Attach.png",    "UI/Attach.png"),
    ("Icon_Prefix_Abyss.png",    "Icons/Prefixes/Abyss.png"),
    ("Icon_Prefix_Blood.png",    "Icons/Prefixes/Blood.png"),
    ("Icon_Prefix_Mech.png",     "Icons/Prefixes/Mech.png"),
    ("Icon_Prefix_Psychic.png",  "Icons/Prefixes/Psychic.png"),
    ("Icon_Prefix_Scroll.png",   "Icons/Prefixes/Scroll.png"),
    ("Icon_Status_Shield.png",   "Icons/Buffs/Shield.png"),
    ("Icon_Status_Buff.png",     "Icons/Buffs/Buff.png"),
    ("Icon_Status_DeBuff.png",   "Icons/Buffs/DeBuff.png"),
    ("Card_Back.png",            "Cards/Back And Front/Back.png"),
]
for i in range(6):
    ICON.append((os.path.join(tmp, "SummonCard_%d.png" % i), "Cards/Back And Front/Summon/SummonCard_%d.png" % i))
    ICON.append((os.path.join(tmp, "SpellCard_%d.png" % i),  "Cards/Back And Front/Spell/SpellCard_%d.png" % i))

print("备份 ->", BK)
rows = []
for src, rel in ICON:
    s = src if os.path.isabs(src) else os.path.join(SRC, src)
    dst = os.path.join(R, rel.replace("/", os.sep))
    assert os.path.isfile(s), "缺源文件 " + s
    assert os.path.isfile(dst), "目标不存在（路径写错？）" + dst
    b = os.path.join(BK, rel.replace("/", os.sep))
    os.makedirs(os.path.dirname(b), exist_ok=True)
    shutil.copy2(dst, b)
    shutil.copy2(s, dst)
    old = Image.open(b).size; new = Image.open(dst).size
    rows.append((rel, "%dx%d" % old, "%dx%d" % new))
    print("   %-52s %s -> %s" % (rel, "%dx%d" % old, "%dx%d" % new))

with open(os.path.join(BK, "MANIFEST.txt"), "w", encoding="utf-8", newline="\n") as f:
    f.write("cardframe-v7 安装备份 %s\n回滚：把本目录整棵覆盖回 Assets/_Game/Resources/\n\n" % STAMP)
    for rel, o, n in rows:
        f.write("%-52s %s -> %s\n" % (rel, o, n))
print("\n共覆盖 %d 个文件。回滚目录：%s" % (len(rows), BK))
