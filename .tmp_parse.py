import os, re, json, collections
root = r"Assets/_Game/Resources/CardData"
cards=[]
for dp,dn,fn in os.walk(root):
    for f in fn:
        if not f.endswith(".asset"): continue
        p=os.path.join(dp,f)
        txt=open(p,encoding="utf-8",errors="replace").read()
        d={}
        for line in txt.splitlines():
            m=re.match(r"^  ([A-Za-z0-9_]+): (.*)$", line)
            if m:
                k,v=m.group(1),m.group(2).strip()
                d[k]=v
        d["_path"]=p.replace("\\","/")
        cards.append(d)
def unq(s):
    if s is None: return ""
    if s.startswith('"') and s.endswith('"'):
        try: return s[1:-1].encode().decode("unicode_escape")
        except: return s[1:-1]
    return s
for c in cards:
    for k in ("cardName","prefix","effect","traits","traitProperties","counterEffect","counterTriggerCondition","revengeEffect","buffText","debuffText"):
        if k in c: c[k]=unq(c[k])
    for k in ("baseCost","baseTier","baseHealth","baseAttack","cardType","summonType","copyCount","spellType","addToMainDeck"):
        try: c[k]=int(c.get(k,0))
        except: c[k]=0
print("total cards:", len(cards))
print("cardType dist:", collections.Counter(c["cardType"] for c in cards))
print("cost dist:", sorted(collections.Counter(c["baseCost"] for c in cards).items()))
print("prefix dist:", collections.Counter(c["prefix"] for c in cards))
json.dump(cards, open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json","w",encoding="utf-8"), ensure_ascii=False, indent=0)
