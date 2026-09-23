import os
root=r"Assets/_Game/Scripts"
agg={}
for dp,dn,fn in os.walk(root):
    for f in fn:
        if not f.endswith(".cs"): continue
        p=os.path.join(dp,f)
        rel=os.path.relpath(p,root).replace("\\","/")
        cat=rel.split("/")[0] if "/" in rel else "root"
        agg[cat]=agg.get(cat,0)+sum(1 for _ in open(p,encoding="utf-8",errors="replace"))
for k,v in sorted(agg.items(),key=lambda x:-x[1]): print(f"  {v:6d}  {k}")
print("  TOTAL", sum(agg.values()))
