import json,collections,statistics
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
s=[c for c in main if c["cardType"]==0]
tiers=[]
for c in s:
    tiers += [c["baseTier"]]*c["copyCount"]
print("抽到召唤物时期望阶位 =", round(statistics.mean(tiers),2), " 分布:", sorted(collections.Counter(tiers).items()))
T=statistics.mean(tiers)
print("\n=== 空打+存活差 的每回合税（对方 D 个单位站场、我方该列全空）===")
print("  伤害 = D×阶位(空打) + D(存活差)")
for D in range(1,7):
    print(f"   对方 {D} 个单位 -> 我方每回合掉 {D*T:4.1f} + {D} = {D*T+D:5.1f} 点")
print("\n=== 我方放 1 个阻挡者能减多少税 ===")
for D in range(2,7):
    full=D*T+D
    withblocker=(D-1)*T+(D-1)
    print(f"   对方 {D}: 全空 {full:5.1f} -> 挡 1 个 {withblocker:5.1f}  (省 {full-withblocker:4.1f})")
print("\n=== 双方都空场 ===")
print("   0 单位 vs 0 单位 -> 空打 0、存活差 0 = 每回合掉 0 点")
