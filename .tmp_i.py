import json,re,os
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
data_ids={c["templateID"] for c in cards}
chosen=set()
for f in os.listdir(r"Assets/_Game/Resources/ChosenOneData"):
    if f.endswith(".asset"): chosen.add(f[:-6])
print("data ids:",len(data_ids),"chosen:",sorted(chosen))
t=open(r"Assets/_Game/Scripts/Localization/CardTextTable.cs",encoding="utf-8",errors="replace").read()
txt_ids=[m for m in re.findall(r'Add\("(\d{5})"', t)]
print("text ids:",len(txt_ids))
allids=data_ids|chosen
print("\nin TEXT but NOT in data:",sorted(set(txt_ids)-allids))
print("\nin DATA but NOT in text:",sorted(allids-set(txt_ids)))
