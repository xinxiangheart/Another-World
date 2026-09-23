import json
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
s=[c for c in cards if c.get("addToMainDeck")==1]
print("main-deck templates:",len(s),"copies:",sum(c["copyCount"] for c in s))
import collections
print("copies per template:",sorted(collections.Counter(c["copyCount"] for c in s).items()))
print()
print("cards that mention 神选者 in text:")
for c in cards:
    t=(c.get("traits") or "")+(c.get("effect") or "")
    if "神选者" in t: print("  ",c["templateID"],c["cardName"],"copy",c["copyCount"],"|",t[:60])
