import json, statistics, collections, math
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
summons=[c for c in main if c["cardType"]==0]
print("=== 主库同费内部：攻+血 的离散度（只看召唤物）===")
for cost in (0,1,2,3,4,5):
    g=[c for c in summons if c["baseCost"]==cost]
    if len(g)<2: continue
    s=[c["baseAttack"]+c["baseHealth"] for c in g]
    print(f"{cost}费 n={len(g):3d}  和: min={min(s):2d} 中位={statistics.median(s):4.1f} max={max(s):2d}  σ={statistics.pstdev(s):4.2f}   最弱={min(g,key=lambda c:c['baseAttack']+c['baseHealth'])['cardName']} ={min(s)}  最强={max(g,key=lambda c:c['baseAttack']+c['baseHealth'])['cardName']} ={max(s)}")

# 按份数加权的全体分布（这才是“抽一张”的真实分布）
den=[]
for c in main:
    if c["cardType"]!=0: continue
    for _ in range(c["copyCount"]): den.append(c["baseAttack"]+c["baseHealth"])
print(f"\n=== 抽一张召唤物时 攻+血 的分布（按份数加权, n={len(den)}）===")
print(f"均值={statistics.mean(den):.2f} σ={statistics.pstdev(den):.2f} 中位={statistics.median(den)}")
print("分布:", sorted(collections.Counter(den).items()))
sig=statistics.pstdev(den)
print(f"\n→ 2选1 的理论增益 ≈ 0.564σ = {0.564*sig:.2f} 点身材（3选1 ≈ 0.846σ = {0.846*sig:.2f}）")
