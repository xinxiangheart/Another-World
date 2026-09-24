# -*- coding: utf-8 -*-
"""卡牌预制体文字统一（2026-09-24）
把 Assets/_Game/Prefabs/Cards/**/*.prefab（召唤 4 + 法术 4）上的 TMP 文字统一到
  字体 = NotoSerifCJKsc-Black SDF (guid d233f3062e8579e43aace0d4bdcf90fb)
  材质 = 该字库内唯一的 Material 子资产 (fileID -3788505136060853876)
并修掉指向「字库里不存在的材质」的坏引用 -7300241376912848282（TMP 侧 + 3D 卡 MeshRenderer 侧）。
本次实际改动：SpellCard00_2D 3 处、SpellCard00_3D 3 处、Card00_2D 5 处、Card00_3D 5 处文本；
外加 3D 卡文字网格材质 8 处。Spell/Card 的 *_New_* 预制体本来就合规，0 改动。
原件备份：%TEMP%\cardtext-backup-20260924\
"""
import re, io, os, glob, shutil

BLACK_FONT = "d233f3062e8579e43aace0d4bdcf90fb"
BLACK_MAT = "-3788505136060853876"
FONT_GUIDS = {BLACK_FONT, "33c204896c0cca64e8e60641e9e7c64c", "7829167474b371a408a38c121ec905cf",
              "7e64c36f04c21774cadd6472a1e56772", "375f3ee9f90fde246bb887d5d20bb231",
              "0c3da36f6ef92ff42baf3734a74c719d", "77e3791087d98504ea63516ab5f01197"}
PAIRS = [("-7300241376912848282", BLACK_FONT, BLACK_MAT, BLACK_FONT),
         ("-4995267273640441941", "33c204896c0cca64e8e60641e9e7c64c", BLACK_MAT, BLACK_FONT),
         ("3340492365137237637", "7829167474b371a408a38c121ec905cf", BLACK_MAT, BLACK_FONT)]
BACKUP = r"C:\Users\22589\AppData\Local\Temp\cardtext-backup-20260924"

def main():
    os.makedirs(BACKUP, exist_ok=True)
    files = sorted(glob.glob("Assets/_Game/Prefabs/Cards/**/*.prefab", recursive=True))
    for p in files:
        dst = os.path.join(BACKUP, os.path.basename(p))
        if not os.path.exists(dst): shutil.copy2(p, dst)
    for p in files:
        s = io.open(p, encoding="utf-8", newline="").read()
        out, pos, n = [], 0, 0
        for m in re.finditer(r"^ *--- !u!114 &(-?\d+)(?: stripped)?\r?\n(.*?)(?=^--- |\Z)", s, re.M | re.S):
            out.append(s[pos:m.start()])
            head = "--- !u!114 &%s" % m.group(1) + ("\r\n" if "\r\n" in s else "\n")
            body = m.group(2)
            if "m_fontAsset:" in body:
                b2 = re.sub(r"(m_fontAsset: \{fileID: )\d+, guid: \w+(, type: 2\})",
                            r"\g<1>11400000, guid: " + BLACK_FONT + r"\g<2>", body)
                b2 = re.sub(r"(m_sharedMaterial: \{fileID: )-?\d+, guid: (" + "|".join(FONT_GUIDS) + r")(, type: 2\})",
                            r"\g<1>" + BLACK_MAT + r", guid: " + BLACK_FONT + r"\g<3>", b2)
                b2 = re.sub(r"(^ *- \{fileID: )-?\d+, guid: (" + "|".join(FONT_GUIDS) + r")(, type: 2\})",
                            r"\g<1>" + BLACK_MAT + r", guid: " + BLACK_FONT + r"\g<3>", b2, flags=re.M)
                if b2 != body: n += 1
                body = b2
            out.append(head + body)
            pos = m.end()
        out.append(s[pos:])
        s = "".join(out)
        for oldID, oldGuid, newID, newGuid in PAIRS:
            s = s.replace("{fileID: %s, guid: %s, type: 2}" % (oldID, oldGuid),
                          "{fileID: %s, guid: %s, type: 2}" % (newID, newGuid))
        io.open(p, "w", encoding="utf-8", newline="").write(s)
        print("%-46s 文本组件改 %d 处" % (os.path.basename(p), n))

if __name__ == "__main__":
    main()