import json,os,re
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
root=r"C:\Users\22589\Documents\GitHub\Another-World\Assets\_Game\Resources\Cards"
have=set()
for dp,dn,fn in os.walk(root):
    for f in fn:
        m=re.search(r"\{(\d+)\}", f)
        if m and f.endswith(".png"): have.add(m.group(1))
missing=[c for c in cards if c["templateID"] not in have]
print("cards without own png:", len(missing))
import collections
print(collections.Counter((c["cardType"],c["baseCost"]) for c in missing))
for c in missing: print("  ",c["templateID"],c["cardName"],"type",c["cardType"],"cost",c["baseCost"])
