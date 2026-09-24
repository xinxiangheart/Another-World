# -*- coding: utf-8 -*-
"""把 Game.unity 里所有用 NotoSansSC SDF 的 TMP 文本整体换成 NotoSerifCJKsc-Bold SDF
（含 sharedMaterial 一并指到 Bold 字库自带的默认材质，与场景里既有的 SerifBold 文本完全一致）。
"""
import re, io, sys

P = r"Assets/_Game/Scenes/Game.unity"
SANS = "3bd21f304e77db54eb98a1f35f6a2680"
BOLD = "33c204896c0cca64e8e60641e9e7c64c"
SANS_MAT = "-3509209986637506031"
BOLD_MAT = "-2553778151399652787"

src = io.open(P, encoding="utf-8", newline="").read()
blocks = list(re.finditer(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- |\Z)", src, re.M | re.S))

changed = []
badmat = []
out = []
pos = 0
for m in blocks:
    out.append(src[pos:m.start()])
    cls, fid, body = m.group(1), m.group(2), m.group(3)
    if cls == "114" and ("guid: " + SANS) in body:
        fnt = re.search(r"m_fontAsset: \{fileID: 11400000, guid: " + SANS + r", type: 2\}", body)
        mat = re.search(r"m_sharedMaterial: \{fileID: (-\d+), guid: " + SANS + r", type: 2\}", body)
        if not fnt:
            print("  !! fontAsset 形态不符，跳过", fid); out.append(m.group(0)); pos = m.end(); continue
        if not mat or mat.group(1) != SANS_MAT:
            badmat.append((fid, mat.group(1) if mat else None))
        body2 = body.replace("m_fontAsset: {fileID: 11400000, guid: " + SANS + ", type: 2}",
                             "m_fontAsset: {fileID: 11400000, guid: " + BOLD + ", type: 2}")
        if mat:
            body2 = body2.replace("m_sharedMaterial: {fileID: %s, guid: %s, type: 2}" % (mat.group(1), SANS),
                                  "m_sharedMaterial: {fileID: %s, guid: %s, type: 2}" % (BOLD_MAT, BOLD))
        changed.append(fid)
        out.append("--- !u!%s &%s\n%s" % (cls, fid, body2))
    else:
        out.append(m.group(0))
    pos = m.end()
out.append(src[pos:])
dst = "".join(out)

print("替换的文本组件数:", len(changed), changed)
if badmat: print("!! 材质 fileID 与预期不同的:", badmat)
print("残留 Sans 引用:", dst.count(SANS))
io.open(P, "w", encoding="utf-8", newline="").write(dst)