import json, collections, statistics
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
summons=[c for c in cards if c["cardType"]==0]
spells=[c for c in cards if c["cardType"]==1]
print("=== summons by cost ===")
for cost in sorted(set(c["baseCost"] for c in summons)):
    g=[c for c in summons if c["baseCost"]==cost]
    print(f"cost {cost}: n={len(g)}  tiers={sorted(set(c['baseTier'] for c in g))}  A+H med={statistics.median([c['baseAttack']+c['baseHealth'] for c in g])}  sample={[ (c['templateID'],c['cardName'],c['baseAttack'],c['baseHealth']) for c in g[:4]]}")
print()
print("=== spells by cost ===")
for cost in sorted(set(c["baseCost"] for c in spells)):
    g=[c for c in spells if c["baseCost"]==cost]
    print(f"cost {cost}: n={len(g)}  tiers={sorted(set(c['baseTier'] for c in g))}  ids={[c['templateID'] for c in g]}")
print()
print("=== summonType dist (summons) ===", collections.Counter(c["summonType"] for c in summons))
print("=== spellType dist (spells) ===", collections.Counter(c["spellType"] for c in spells))
print()
fs=[c for c in summons if c.get("hasFirstStrike")==1 or c.get("hasOnEnter")==1 or c.get("hasOnDeath")==1 or c.get("hasActiveExit")==1 or c.get("hasRevenge")==1 or c.get("hasDiscard")==1 or c.get("canAttach")==1]
print("summons with any trait flag:", len(fs), "/", len(summons))
print("summons pure vanilla (no flags, no spells-ish text):", len(summons)-len(fs))
print()
print("=== attach ===", [c['templateID'] for c in summons if c.get('canAttach')==1])
print()
print("=== prefix per cost (summons) ===")
for cost in sorted(set(c['baseCost'] for c in summons)):
    g=[c for c in summons if c['baseCost']==cost]
    print(cost, collections.Counter(c['prefix'] for c in g))
print()
print("=== 血歌 in text? ===")
for c in cards:
    if "血歌" in (c.get("traits","")+c.get("effect","")+c.get("traitProperties","")):
        print(" ", c["templateID"], c["cardName"], "| prefix=",c["prefix"], "| cost",c["baseCost"])
