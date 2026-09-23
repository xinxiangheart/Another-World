import json
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
idx={c["templateID"]:c for c in cards}
enablers={
 "01119 认同指令(机械payoff)":None,
 "02004 皇帝的认可(加渊)":None,
 "02212 意念并同(加灵能+中枢)":None,
 "01501 深渊皇帝(渊payoff+加渊)":None,
 "01506 增幅结构(机械payoff+自造)":None,
 "03027 中枢(全体灵能)":None,
 "01524 画卷之核(神灵画卷payoff)":None,
 "01127 改造者(加灵能)":None,
 "01126 凝聚体(灵能附着)":None,
 "01334 集群意识(灵能payoff)":None,
 "01335 能量骇客":None,
 "01336 修正者":None,
 "01527 消逝之影(加灵能)":None,
 "01528 能量收割者":None,
 "01112 脆弱精灵":None,
}
for k in enablers:
    tid=k.split()[0]
    c=idx.get(tid)
    if c is None: print(k,"-> 无数据"); continue
    print(f"{tid} {c['cardName']:8s} {c['baseCost']}费 复制数={c['copyCount']} 入主库={c['addToMainDeck']}")
# total supply of prefix-adding effects in deck
print("\n=== 主库中所有“附加前缀”来源及其总份数 ===")
tot=0
for c in cards:
    t=(c.get("traits") or "")+(c.get("effect") or "")
    if "附加" in t and "前缀" in t and c.get("addToMainDeck")==1:
        print(f"  {c['templateID']} {c['cardName']} {c['baseCost']}费 x{c['copyCount']}")
        tot+=c["copyCount"]
print("  合计份数:",tot,"/ 328")
