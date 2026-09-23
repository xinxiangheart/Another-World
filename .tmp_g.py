import json
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
print([ (c['templateID'],c['cardName'],c['copyCount'],c['cardType'],c.get('summonType')) for c in cards if c.get('copyCount')==99])
print()
print("=== chosen ones (03501-03513) ===")
for c in cards:
    if c['templateID'].startswith('035'):
        print(" ",c['templateID'],c['cardName'],"cost",c['baseCost'],"atk",c['baseAttack'],"hp",c['baseHealth'],"addToMainDeck",c.get('addToMainDeck'),"copy",c.get('copyCount'))
