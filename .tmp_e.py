import os,re
root=r"Assets/_Game/Scripts"
tot=0; files=[]
pat=re.compile(r'(?:static\s+)?(?:IEnumerator|void|bool|int)\s+(Handle\d{5})\s*\(')
pat2=re.compile(r'Register\("(\d{5})"')
for dp,dn,fn in os.walk(root):
    for f in fn:
        if not f.endswith(".cs"): continue
        p=os.path.join(dp,f); t=open(p,encoding="utf-8",errors="replace").read()
        h=set(pat.findall(t)); r=set(pat2.findall(t))
        if h or r: files.append((p.replace("\\","/"),len(h),len(r)))
        tot+=len(h)
print("bespoke per-card handler methods:", tot)
print("Register(templateID,...) calls:", sum(r for _,_,r in files))
for p,h,r in sorted(files,key=lambda x:-x[1])[:10]: print(f"  handlers={h:3d} reg={r:3d}  {p}")
