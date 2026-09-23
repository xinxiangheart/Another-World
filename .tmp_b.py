import json,collections,re
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
print("traitsMigrated:", collections.Counter(c.get("traitsMigrated") for c in cards))
print("traitEntries empty:", sum(1 for c in cards if c.get("traitEntries",[])=="[]"))
print("has traits text:", sum(1 for c in cards if c.get("traits")))
print("has effect text:", sum(1 for c in cards if c.get("effect")))
print("has counterEffect:", sum(1 for c in cards if c.get("counterEffect")))
print("has revengeEffect:", sum(1 for c in cards if c.get("revengeEffect")))
# tier vs cost check
print("\n=== tier!=expected (0->0,1->1,3->2,5->3) among summons ===")
exp={0:0,1:1,3:2,5:3}
bad=[(c["templateID"],c["cardName"],c["baseCost"],c["baseTier"]) for c in cards if c["cardType"]==0 and exp.get(c["baseCost"])!=c["baseTier"]]
print(len(bad), bad[:15])
print("\n=== spells tier vs cost ===")
sb=[(c["templateID"],c["baseCost"],c["baseTier"]) for c in cards if c["cardType"]==1 and c["baseCost"]!=c["baseTier"]]
print(len(sb), sb[:10])
# distinct effect strings
eff=[c["effect"] for c in cards if c.get("effect")]
print("\ndistinct effect strings:", len(set(eff)), "of", len(eff))
cnt=collections.Counter(eff)
print("effect strings used by >1 card:", sum(1 for k,v in cnt.items() if v>1))
# cards grouped by dir
print("\n=== dirs ===")
d=collections.Counter("/".join(c["_path"].split("/")[5:-1]) for c in cards)
for k,v in sorted(d.items()): print(f"  {k}: {v}")
