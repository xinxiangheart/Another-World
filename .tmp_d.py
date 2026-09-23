import os,re,collections
root=r"Assets/_Game/Scripts"
ids=collections.Counter()
files=collections.Counter()
for dp,dn,fn in os.walk(root):
    for f in fn:
        if not f.endswith(".cs"): continue
        p=os.path.join(dp,f)
        try: t=open(p,encoding="utf-8",errors="replace").read()
        except: continue
        found=set(re.findall(r'"(\d{5})"', t))
        for x in found:
            ids[x]+=1; files[p.replace("\\","/")]+=1
print("distinct 5-digit card IDs hardcoded in scripts:", len(ids))
print("total hardcoded ID mentions:", sum(ids.values()))
print("files referencing>=1 card ID:", len(files))
print("\ntop files by distinct card IDs:")
for p,c in files.most_common(12): print(f"  {c:3d}  {p}")
print("\ntop card IDs by distinct-file mentions:")
for i,c in ids.most_common(15): print(f"  {i}: {c} files")
