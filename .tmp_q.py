import json, math
from math import comb
cards=json.load(open(r"C:\Users\22589\Documents\GitHub\Another-World\.tmp_cards.json",encoding="utf-8"))
idx={c["templateID"]:c for c in cards}
main=[c for c in cards if c.get("addToMainDeck")==1]
N=sum(c["copyCount"] for c in main)
print("主库总量 N =",N)

def prob_at_least(k_target, copies, n):
    # hypergeometric: draw n from N which contains `copies` successes
    tot=comb(N,n) if N>=n else 1
    p=0
    for k in range(k_target, min(copies,n)+1):
        p+=comb(copies,k)*comb(N-copies,n-k)/tot
    return p

cases={
 "神灵画卷 (01110/01322/01523)":["01110","01322","01523"],
 "灵能 (11 张)":["01126","01127","01128","01129","01333","01334","01335","01336","01527","01528","03027"],
 "机械 (12 张)":["01114","01118","01125","01308","01328","01332","01345","01505","01506","01513","03004","03005"],
}
for name,ids in cases.items():
    copies=sum(idx[i]["copyCount"] for i in ids if i in idx and idx[i].get("addToMainDeck")==1)
    tok=sum(1 for i in ids if i in idx and idx[i].get("addToMainDeck")==0)
    print(f"\n{name}: 主库份数={copies}, 非主库(只能被效果造出来)={tok}")
    for n in (10,20,40,60):
        print(f"   抽{n:3d}张时: 见到>=1 的概率={prob_at_least(1,copies,n)*100:5.1f}%   >=2 的概率={prob_at_least(2,copies,n)*100:5.1f}%")

print("\n=== 01524 画卷之核 的条件：己方已有 2 张神灵画卷 ===")
c=sum(idx[i]["copyCount"] for i in ["01110","01322","01523"])
for n in (20,40,60):
    print(f"   抽{n}张: >=2 的概率={prob_at_least(2,c,N and n)*100:.1f}%")
