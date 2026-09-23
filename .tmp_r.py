import json, statistics, collections
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
s=[c for c in cards if c["cardType"]==0]
def text(c): return ((c.get("traits") or "")+(c.get("traitEntries") and "" or ""))
# 白板：traits 文本为空 且 effect 为空
vanilla=[c for c in s if not (c.get("traits") or "").strip() and not (c.get("effect") or "").strip()]
print("=== 无任何特性文本的召唤物（白板）===")
for cost in (0,1,3,5):
    g=[c for c in vanilla if c["baseCost"]==cost]
    print(f"\n{cost}费 白板 {len(g)} 张:")
    for c in sorted(g,key=lambda x:(-x['baseAttack'])): print(f"   {c['templateID']} {c['cardName']:8s} {c['baseAttack']}/{c['baseHealth']}  和={c['baseAttack']+c['baseHealth']}")

print("\n=== 各费用档：攻-血 差值的分布（全部召唤物）===")
for cost in (1,3,5):
    g=[c for c in s if c["baseCost"]==cost]
    d=[c["baseAttack"]-c["baseHealth"] for c in g]
    print(f"{cost}费 n={len(g)} ATK-HP: min={min(d)} 中位={statistics.median(d)} max={max(d)}  | 高血低攻(HP>ATK)={sum(1 for x in d if x<0)} 高攻低血={sum(1 for x in d if x>0)} 相等={sum(1 for x in d if x==0)}")
