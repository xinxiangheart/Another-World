import json,re
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
main=[c for c in cards if c.get("addToMainDeck")==1]
N=sum(c["copyCount"] for c in main)
def txt(c): return ((c.get("traits") or "")+"\n"+(c.get("effect") or ""))
hits=[c for c in main if re.search(r"(对方玩家|己方玩家).{0,6}(伤害|HP|生命)", txt(c))]
print(f"主库中带“直接对玩家造成伤害/扣血”的卡: {len(hits)} 张模板, {sum(c['copyCount'] for c in hits)} 份 / {N}")
for c in hits: print("   ",c["templateID"],c["cardName"],f"({c['baseCost']}费 x{c['copyCount']})","|",txt(c).replace("\n"," / ")[:60])
