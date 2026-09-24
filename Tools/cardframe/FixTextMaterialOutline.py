# -*- coding: utf-8 -*-
"""把 Serif 字库默认材质里的「粗描边 + Underlay 黑晕」压掉，让文字清晰。
- Black 字库材质：_OutlineWidth 0.2 -> 0.08，_UnderlayDilate 0.25 -> 0
- Bold  字库材质：_OutlineWidth 0.16 -> 0.08
只动 Material 子资产（!u!21）内的两三个键，其余一字不改。
"""
import io, re

JOBS = [(r"Assets/_Game/Fonts/NotoSerifCJKsc-Black SDF.asset",
         [("- _OutlineWidth: 0.2", "- _OutlineWidth: 0.08"),
          ("- _UnderlayDilate: 0.25", "- _UnderlayDilate: 0")]),
        (r"Assets/_Game/Fonts/NotoSerifCJKsc-Bold SDF.asset",
         [("- _OutlineWidth: 0.16", "- _OutlineWidth: 0.08")])]

for path, subs in JOBS:
    s = io.open(path, encoding="utf-8", newline="").read()
    eol = "\r\n" if "\r\n" in s else "\n"
    m = re.search(r"^--- !u!21 &(-?\d+)" + eol + r"Material:" + eol + r"(.*?)(?=^--- |\Z)", s, re.M | re.S)
    if not m:
        print("!! 找不到 Material 块:", path); continue
    body = m.group(2)
    for old, new in subs:
        if old not in body:
            print("   ! 键不存在（可能已是目标值）:", old); continue
        body = body.replace(old, new, 1)
    s2 = s[:m.start(2)] + body + s[m.end(2):]
    io.open(path, "w", encoding="utf-8", newline="").write(s2)
    # 复核
    chk = io.open(path, encoding="utf-8", newline="").read()
    mm = re.search(r"^--- !u!21 &(-?\d+)" + eol + r"Material:" + eol + r"(.*?)(?=^--- |\Z)", chk, re.M | re.S)
    vals = dict(re.findall(r"- (_\w+): ([-\d.]+)", mm.group(2)))
    print("%-52s -> OutlineWidth=%s  UnderlayDilate=%s  (字节 %d -> %d)" % (
        path.split("/")[-1], vals.get("_OutlineWidth"), vals.get("_UnderlayDilate", "0(默认)"), len(s), len(chk)))