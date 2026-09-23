import json,re
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
data={c["templateID"] for c in cards}
t=open(r"Assets/_Game/Scripts/Localization/CardTextTable.cs",encoding="utf-8",errors="replace").read()
rows={tid:(n,d) for tid,n,d in re.findall(r'Add\("(\d{5})","([^"]*)","([^"]*)"\)', t)}
print("=== 防爆发 / 改伤害 / 改能量 类，及实现状态 ===")
for tid,(name,desc) in sorted(rows.items()):
    if any(k in desc for k in ["受伤害","累积伤害","无效化","调整为","恢复玩家","扣己方玩家","拖"]):
        ok="已实现" if tid in data else "★只有文本、无数据"
        print(f"  {tid} {name:12s} {ok:16s} | {desc[:72]}")
