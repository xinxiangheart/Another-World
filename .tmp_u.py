import json, random, statistics
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
# 粗模型：只用召唤物的“攻+血”当价值代理，逐一复制成池子
pool0=[]
for c in main:
    if c["cardType"]!=0: continue
    for _ in range(c["copyCount"]): pool0.append(c["baseAttack"]+c["baseHealth"])
N0=len(pool0); print(f"召唤物池（价值代理=攻+血）: {N0} 份, 初始均值 {statistics.mean(pool0):.2f}")

def run(mode, turns=30, trials=400):
    got=[]; pooldrift=[]
    for t in range(trials):
        pool=pool0[:]; random.shuffle(pool)
        g=[]
        for turn in range(turns):
            k=3 if mode=="3pick" else 1
            hand=pool[:k]; del pool[:k]
            take=max(hand)
            if mode=="3pick":
                # 其余两张永久离池（弃掉）
                pass
            g.append(take)
        got.append(statistics.mean(g))
        pooldrift.append(statistics.mean(pool))
    return statistics.mean(got), statistics.mean(pooldrift)

for m,label in [("blind","盲抽（现状）"),("3pick","每回合 3选1")]:
    g,d=run(m)
    print(f"\n{label}:")
    print(f"   玩家每回合实际拿到的牌 均值 = {g:.2f}")
    print(f"   30 回合后池子剩余均值     = {d:.2f}  (初始 {statistics.mean(pool0):.2f}, 漂移 {d-statistics.mean(pool0):+.2f})")
