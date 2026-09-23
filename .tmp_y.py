import json,collections,statistics
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
s=[c for c in main if c["cardType"]==0]
print("=== 主库里 0 费召唤物 ===")
for c in s:
    if c["baseCost"]==0: print(f"   {c['templateID']} {c['cardName']:8s} {c['baseAttack']}/{c['baseHealth']} 阶{c['baseTier']} x{c['copyCount']}")
costs=[]
for c in s: costs+=[c["baseCost"]]*c["copyCount"]
print("\n主库召唤物的费用分布(按份数):", sorted(collections.Counter(costs).items()))
print("召唤物总份数:",len(costs)," 平均费用:",round(statistics.mean(costs),2))
print("\n=== 能量预算模型：攒 T 回合后单回合能铺几个单位 ===")
for T in (2,3,4,5):
    for d in (0,1,2,3):
        e=0; hand=3
        for t in range(T):
            e=min(15,e+6); take=min(d,5); e-=take; hand+=take
        print(f"  攒{T}回合、每回合抽{d}张 -> 回合开始能量={min(15,e+6)}, 手牌={hand}  (可铺单位数上限 = min(6,手牌))")
    print()
