import json,re
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
impl=[c for c in cards if True]
def txt(c): return (c.get("traits") or "")+"\n"+(c.get("effect") or "")+"\n"+(c.get("traitProperties") or "")
pats={
 "要求同前缀密度(payoff)": r"(每另有|每有一个|每有一个己方|前缀数|血歌前缀|机械前缀|灵能前缀|渊前缀|前缀召唤物数|带有\w*前缀)",
 "要求特定张数/条件": r"(3机械|三名|两张|两画卷|集齐|每3张|同场)",
 "附着(需要宿主)": r"附着",
}
for k,p in pats.items():
    hits=[(c["templateID"],c["cardName"],c["baseCost"],txt(c).replace("\n"," / ")[:70]) for c in impl if re.search(p,txt(c))]
    print(f"=== {k}: {len(hits)} 张 ===")
    for h in hits: print("   ",h[0],h[1],f"({h[2]}费)","|",h[3])
    print()
