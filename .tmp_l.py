import os,struct,collections,re
root=r"Assets/_Game/Resources/Cards"
dims=collections.Counter(); sizes=[]
for dp,dn,fn in os.walk(root):
    for f in fn:
        if not f.endswith(".png"): continue
        p=os.path.join(dp,f)
        with open(p,"rb") as fh:
            d=fh.read(33)
        if d[:8]!=b"\x89PNG\r\n\x1a\n": continue
        w,h=struct.unpack(">II", d[16:24])
        dims[(w,h)]+=1
        sizes.append(os.path.getsize(p))
for k,v in dims.most_common(): print(k,v)
print("total png", sum(dims.values()))
print("total bytes MB", round(sum(sizes)/1048576,1))
# old dir
root2=r"Assets/_Game/Art"
