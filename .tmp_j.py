import os,re
root=r"Assets/_Game/Resources/Cards"
have=set()
for dp,dn,fn in os.walk(root):
    for f in fn:
        m=re.search(r"\{(\d+)\}",f)
        if m and f.endswith(".png"): have.add(m.group(1))
missd=['01120','01121','01122','01123','01130','01132','01133','01134','01340','01341','01342','01518','01529','01532','01533','01536','02003','02007','02008','02108','02112','02208','02216','02217','02312','02405','02406','02502','02503','02504','02505','02506','02507','03008','03013','03023','03024','03028','03505','03507','03508','03509','03510','03512']
print("text-only ids:",len(missd))
wa=[i for i in missd if i in have]
print("of which HAVE card art:",len(wa))
print(sorted(wa))
