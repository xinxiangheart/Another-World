import json,collections,os
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
tot=sum(c.get("copyCount",0) for c in cards if c.get("addToMainDeck")==1)
print("main-deck cards total (addToMainDeck=1):",tot)
print("templates in main deck:",sum(1 for c in cards if c.get("addToMainDeck")==1))
# prefix lists
for p in ["渊","机械","灵能","神灵画卷","血歌"]:
    g=[(c['templateID'],c['cardName'],c['baseCost']) for c in cards if p in c.get('prefix','')]
    print(p, len(g), g)
