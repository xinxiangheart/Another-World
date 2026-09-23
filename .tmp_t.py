import json,re,collections
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
N=sum(c["copyCount"] for c in main)
def txt(c): return (c.get("traits") or "")+"\n"+(c.get("effect") or "")
def has(c,kw): return kw in txt(c)

print("=== 各“协同轴”的密度（主库 328 份）===")
axes={
 "退场收益 (退场：)": lambda c: has(c,"退场：") or has(c,"退场:"),
 "主动退场收益": lambda c: has(c,"主动退场：") or has(c,"主动退场:"),
 "反击收益": lambda c: has(c,"反击"),
 "先手收益": lambda c: has(c,"先手"),
 "进场收益": lambda c: has(c,"进场"),
}
for name,f in axes.items():
    g=[c for c in main if f(c)]
    print(f"  {name:16s} 模板={len(g):3d}  份数={sum(c['copyCount'] for c in g):3d}  ({sum(c['copyCount'] for c in g)/N*100:.1f}% of 328)")

print("\n=== “退场”协同轴的两半：收益方 vs 促成方 ===")
payoff=[c for c in main if (has(c,"退场：") or has(c,"主动退场：")) and "使己方" not in txt(c)]
enabler=[c for c in main if re.search(r"使己方(一|两|全体|其他)?召唤物退场|使己方全体退场|令己方.*退场", txt(c))]
print("  收益方(payoff):", [(c['templateID'],c['cardName'],c['copyCount']) for c in payoff])
print("  促成方(enabler):", [(c['templateID'],c['cardName'],c['copyCount']) for c in enabler])
print("  促成方总份数:", sum(c['copyCount'] for c in enabler))

print("\n=== “对局进程必然触发”的特性 vs “需要抽到特定牌”的特性 ===")
always=["退场：","主动退场：","反击","先手"]
for k in always:
    g=[c for c in main if k in txt(c)]
    print(f"  {k:8s}: 模板 {len(g):3d} / 份数 {sum(c['copyCount'] for c in g):3d}")
pref=[c for c in main if re.search(r"(灵能|机械|渊|神灵画卷|血歌)", c.get("prefix") or "")]
print(f"  前缀依赖: 模板 {len(pref)} / 份数 {sum(c['copyCount'] for c in pref)}")
