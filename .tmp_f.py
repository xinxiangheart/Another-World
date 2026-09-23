import json,collections
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
tot=sum(c.get("copyCount",0) for c in cards)
print("templates:",len(cards),"total copies in deck:",tot)
print("copyCount distribution:",collections.Counter(c.get("copyCount") for c in cards))
print("addToMainDeck=0 count:",sum(1 for c in cards if c.get("addToMainDeck")==0))
print("addToMainDeck=0 ids:",[ (c['templateID'],c['cardName'],c.get('summonType'),c['cardType']) for c in cards if c.get('addToMainDeck')==0][:40])
