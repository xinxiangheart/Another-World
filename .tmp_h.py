import re
t=open(r"Assets/_Game/Scripts/Localization/CardTextTable.cs",encoding="utf-8",errors="replace").read()
rows=re.findall(r'Add\("(\d{5})","([^"]*)","([^"]*)"\)', t)
print("entries:",len(rows))
for i,(i2,n,d) in enumerate(rows):
    print(f"{i2} {n} | {d}")
