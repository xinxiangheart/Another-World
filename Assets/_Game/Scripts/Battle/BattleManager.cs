using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BoardSlot;
using Mirror;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("调试")]
    public bool skipBattle = false;

    private BoardSlot[] allSlots;
    private int pendingDamageToMe = 0;
    private int pendingDamageToEnemy = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartBattle()
    {
        if (skipBattle)
        {
            Debug.Log("战斗回合 → 跳过（测试模式）");
            return;
        }

        allSlots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (allSlots == null) return;

        pendingDamageToMe = 0;
        pendingDamageToEnemy = 0;

        StartCoroutine(BattleCoroutine());
    }

    public IEnumerator BattleCoroutine()
    {
        allSlots = FindObjectOfType<BoardManager>()?.GetAllSlots();
        if (allSlots == null) yield break;

        yield return StartCoroutine(PhaseStartCoroutine());
        yield return StartCoroutine(FirstStrikeCoroutine());
        yield return StartCoroutine(MinionAttacksCoroutine());
        CompareSurvivors();
        FinalDamage();
        if (NetworkServer.active)
            FindObjectOfType<TurnManager>().StartNewPhase();
    }
    IEnumerator PhaseStartCoroutine()
    {
        if (allSlots == null) yield break;
        foreach (BoardSlot slot in allSlots)
        {
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.hasShield && ci.shieldEndAtBattleStart)
            {
                ci.RemoveShield();
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }

        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01308")
            {
                if (!ci.CanTriggerTrait("战斗回合开始")) continue;
                yield return StartCoroutine(TroubleMakerEffect(ci));
                break;
            }
        }
        // 检查对方是否有合法目标
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01535" && ci.consumedSpellCost > 0)
            {
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                    continue;

                bool hasEnemy = false;
                for (int j = 0; j <= 5; j++)
                    if (allSlots[j]?.currentCard3D != null) { hasEnemy = true; break; }

                if (hasEnemy)
                {
                    int dmg = ci.consumedSpellCost;
                    yield return StartCoroutine(ExecutionSwordDamage(ci, dmg, slot));
                }
                ci.consumedSpellCost = 0;
                break;
            }
        }
        // 疫病：攻击回合开始扣血+攻击力-1
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot == null || !slot.hasPlague || slot.currentCard3D == null) continue;

            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                ci.currentHealth -= slot.plagueRoundCount;
                ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                ci.baseAttack = Mathf.Max(0, ci.baseAttack - 1);
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            slot.plagueRoundCount++;
        }
        BoardSlot.CheckAndHandleDeaths();
        Debug.Log("[战斗] 阶段1：战斗回合开始特性");
    }
    IEnumerator FirstStrikeCoroutine()
    {
        Debug.Log("[战斗] 阶段2：先手特性");
        Debug.Log($"FirstStrikeCoroutine 开始，allSlots[6]={allSlots[6]?.currentCard3D?.name}, allSlots[7]={allSlots[7]?.currentCard3D?.name}");
        // ===== 阶段2.1：先手换位 =====
        Debug.Log("=== 阶段2.1：开始全面换位 ===");
       

        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.hasFirstStrike) continue;
         
        // 检查对方是否有合法目标
            if (ci.templateID == "01124")
            {
                int mySlotIndex = i;
                int col = mySlotIndex % 3;
                int targetSlotIndex = mySlotIndex < 9 ? 9 + col : 6 + col;

                BoardSlot mySlot = allSlots[mySlotIndex];
                BoardSlot targetSlot = allSlots[targetSlotIndex];

                GameObject myCard = mySlot.currentCard3D;
                GameObject targetCard = targetSlot?.currentCard3D;

                Vector3 myPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(mySlotIndex);
                Vector3 targetPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(targetSlotIndex);

                mySlot.SetCard(null);
                targetSlot.SetCard(null);

                if (targetCard != null)
                {
                    if (!mySlot.CanPlaceCard(targetCard.GetComponent<Card3DInstance>()?.cardInstance)) continue;
                    targetCard.transform.position = myPos;
                    mySlot.SetCard(targetCard);
                }
                if (myCard != null)
                {
                    if (!targetSlot.CanPlaceCard(myCard.GetComponent<Card3DInstance>()?.cardInstance)) continue;
                    myCard.transform.position = targetPos;
                    targetSlot.SetCard(myCard);
                }

        // 检查对方是否有合法目标
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        CardInstance attCI = obj.GetComponent<Card3DInstance>()?.cardInstance;
                        if (attCI != null && attCI.isAttached)
                        {
                            if (attCI.hostSlotID == mySlotIndex)
                                attCI.hostSlotID = targetSlotIndex;
                            else if (attCI.hostSlotID == targetSlotIndex)
                                attCI.hostSlotID = mySlotIndex;
                        }
                    }
                }

        // 检查对方是否有合法目标
                ci.hasFirstStrike = false;

                if (mySlot.currentCard3D != null)
                    BoardManager.SyncAttachedModels(mySlot);
                if (targetSlot.currentCard3D != null)
                    BoardManager.SyncAttachedModels(targetSlot);

                Debug.Log($"舞者换位完成：{mySlotIndex}->{targetSlotIndex}");
            }
        // 检查对方是否有合法目标
            if (ci.templateID == "01312")
            {
                int mySlot = i;
                int row = mySlot < 9 ? 0 : 3;
                int col = mySlot % 3;
                int rowStart = row == 0 ? 6 : 9;
                int otherRowStart = row == 0 ? 9 : 6;

                List<int> adjacentSlots = new List<int>();
                if (col > 0) adjacentSlots.Add(rowStart + col - 1);
                if (col < 2) adjacentSlots.Add(rowStart + col + 1);
                adjacentSlots.Add(otherRowStart + col);
                adjacentSlots.RemoveAll(s => allSlots[s].isBlocked);

                if (adjacentSlots.Count == 0) continue;

                bool confirmed = false;
                bool choseYes = false;
                ConfirmPanel.Instance.Show("是否与相邻格子互换位置？",
                    () => { choseYes = true; confirmed = true; },
                    () => { confirmed = true; }
                );
                yield return new WaitUntil(() => confirmed);
                if (!choseYes) continue;

                bool done = false;
                string layerId = SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                BoardSlot.extraTargetFilter = (slot) => adjacentSlots.Contains(slot.slotID);
                BoardSlot.isStrengtheningSlot = true;

                BoardSlot targetSlot = null;
                BoardSlot.onTargetSelected = (t) =>
                {
                    if (t != null && adjacentSlots.Contains(t.slotID))
                    {
                        targetSlot = t;
                        SelectionManager.Instance.EndSelection(layerId);
                        BoardSlot.isStrengtheningSlot = false;
                        BoardSlot.extraTargetFilter = null;
                        done = true;
                    }
                };

                yield return new WaitUntil(() => done);

                if (targetSlot != null)
                {
                    GameObject myCard = allSlots[mySlot].currentCard3D;
                    GameObject targetCard = targetSlot.currentCard3D;

                    Vector3 myPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(mySlot);
                    Vector3 targetPos = FindObjectOfType<HandManager>().GetSlotWorldPosition(targetSlot.slotID);

                    allSlots[mySlot].SetCard(null);
                    targetSlot.SetCard(null);

                    if (targetCard != null)
                    {
                        if (!allSlots[mySlot].CanPlaceCard(targetCard.GetComponent<Card3DInstance>()?.cardInstance)) { done = true; continue; }
                        targetCard.transform.position = myPos;
                        allSlots[mySlot].SetCard(targetCard);
                    }
                    if (myCard != null)
                    {
                        if (!targetSlot.CanPlaceCard(myCard.GetComponent<Card3DInstance>()?.cardInstance)) { done = true; continue; }
                        myCard.transform.position = targetPos;
                        targetSlot.SetCard(myCard);
                    }

                    BoardManager bmAtt = FindObjectOfType<BoardManager>();
                    if (bmAtt != null)
                    {
                        foreach (GameObject obj in bmAtt.attachedModels)
                        {
                            CardInstance attCI = obj.GetComponent<Card3DInstance>()?.cardInstance;
                            if (attCI != null && attCI.isAttached)
                            {
                                if (attCI.hostSlotID == mySlot)
                                    attCI.hostSlotID = targetSlot.slotID;
                                else if (attCI.hostSlotID == targetSlot.slotID)
                                    attCI.hostSlotID = mySlot;
                            }
                        }
                    }
                    BoardManager.SyncAttachedModels(allSlots[mySlot]);
                    BoardManager.SyncAttachedModels(targetSlot);

        // 检查对方是否有合法目标
                    ci.hasFirstStrike = false;
                }

                continue;
            }
            if (ci.templateID == "01513")
            {
                yield return StartCoroutine(MechRearrangementEffect());
                continue;
            }
            if (ci.templateID == "01516")
            {
                yield return StartCoroutine(QuickShadowRearrangeEffect(ci));
                continue;
            }
        }

        // 阶段2.2：有利Buff和护盾附加
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.hasFirstStrike) continue;

        // 检查对方是否有合法目标
            if (ci.templateID == "03012")
            {
                bool yinYangDone = false;
                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                {
                    if (targetSlot != null && targetSlot != slot && targetSlot.currentCard3D != null)
                    {
                        Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        CardInstance tci = t3d?.cardInstance;
                        if (tci != null)
                        {
                            int atk = tci.Attack;
                            int hp = tci.currentHealth - tci.tempHealthBoost;
                            if (atk > hp) tci.AddTempHealth(atk - hp);
                            else if (hp > atk) tci.AddTempAttack(hp - atk);
                            t3d.UpdateValues();
                        }
                    }
                    yinYangDone = true;
                });
                while (!yinYangDone) yield return null;
            }
            if (ci.templateID == "01512")
            {
                ci.GrantShield(false, false, true);
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        // 检查对方是否有合法目标
            if (ci.templateID == "01115")
            {
                ci.isActiveExit = false;
                ci.hasRevenge = false;
                bool shieldDone = false;
                bool hasAlly = false;
                for (int j = 6; j <= 11; j++) if (allSlots[j]?.currentCard3D != null && allSlots[j] != slot) { hasAlly = true; break; }
                if (hasAlly)
                {
                    SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                    {
                        if (targetSlot != null && targetSlot.currentCard3D != null && targetSlot != slot)
                        {
                            Card3DInstance t3d = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                            if (t3d?.cardInstance != null)
                            { t3d.cardInstance.GrantShield(false, false, true); t3d.UpdateValues(); }
                        }
                        shieldDone = true;
                    });
                    while (!shieldDone) yield return null;
                    yield return null;
                }
                slot.HandleDeath(slot.currentCard3D);
                continue;
            }
        // 检查对方是否有合法目标
            if (ci.templateID == "01324")
            {
                int highestTier = 0;
                for (int j = 0; j <= 5; j++)
                {
                    BoardSlot enemySlot = allSlots[j];
                    if (enemySlot?.currentCard3D != null)
                    {
                        CardInstance enemyCI = enemySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (enemyCI != null && enemyCI.currentTier > highestTier)
                            highestTier = enemyCI.currentTier;
                    }
                }
                if (highestTier > 0)
                {
                    ci.AddTempAttack(highestTier);
                    Debug.Log($"猎杀者攻击力临时+{highestTier}");
                }
            }
        // 检查对方是否有合法目标
            if (ci.templateID == "01519")
            {
                List<BoardSlot> candidates = new List<BoardSlot>();
                for (int j = 6; j <= 11; j++)
                {
                    BoardSlot s = allSlots[j];
                    if (s?.currentCard3D != null)
                    {
                        CardInstance c = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (c != null && !c.hasShield && !c.isAttached)
                            candidates.Add(s);
                    }
                }

                Debug.Log($"守护骑士 candidates.Count={candidates.Count}");
                foreach (var cs in candidates) Debug.Log($"候选: 槽位{cs.slotID}");

                if (candidates.Count == 0) continue;

                if (candidates.Count <= 3)
                {
                    foreach (BoardSlot s in candidates)
                    {
                        CardInstance c = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (c != null)
                        {
                            c.GrantShield(false, false, true);
                            s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                Debug.Log($"守护骑士选择给槽位{s.slotID}附加护盾");
                        }
                    }
                    continue;
                }

                string layerId = SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);
                BoardSlot.isStrengtheningSlot = true;

                List<BoardSlot> selected = new List<BoardSlot>();
                int maxSelect = 3;

                BoardSlot.onTargetSelected = (t) =>
                {
                    if (t == null || !candidates.Contains(t)) return;

                    if (selected.Contains(t))
                    {
                        selected.Remove(t);
                        t.SetHighlightColor(t.GetNormalColor());
                    }
                    else if (selected.Count < maxSelect)
                    {
                        selected.Add(t);
                        t.SetHighlightColor(Color.yellow);
                    }

                    if (selected.Count == maxSelect)
                    {
                        foreach (BoardSlot s in selected)
                        {
                            CardInstance c = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (c != null)
                            {
                                c.GrantShield(false, false, true);
                                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                s.SetHighlightColor(s.GetNormalColor());
                                Debug.Log($"守护骑士选择给槽位{s.slotID}附加护盾");
                            }
                        }
                        SelectionManager.Instance.EndSelection(layerId);
                    }
                };

                yield return new WaitUntil(() => !SelectionManager.Instance.IsSelecting);
                BoardSlot.isStrengtheningSlot = false;
            }
            if (ci.templateID == "01531")
            {
                if (!ci.hasShield)
                {
                    ci.currentHealth -= 2;
                    ci.GrantShield(true, false, false);
                    slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            }
        }

        // ===== 阶段2.3：Debuff判定 =====
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.hasFirstStrike) continue;

            // 毒巫：清除护盾+中毒
            if (ci.templateID == "03502")
            {
                bool hasEnemy = false;
                for (int j = 0; j <= 5; j++) if (allSlots[j]?.currentCard3D != null) { hasEnemy = true; break; }
                if (!hasEnemy) continue;
                bool poisonDone = false;
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                {
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance ti = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (ti?.cardInstance != null)
                        {
                            ti.cardInstance.RemoveShield();
                            ti.cardInstance.poisoned = true;
                            if (ti.cardInstance.summonType == SummonType.ChosenOne)
                            { NetworkPlayer.Remote.currentEnergy -= 1; NetworkPlayer.Remote.UpdateUI(); }
                        }
                    }
                    poisonDone = true;
                });
                while (!poisonDone) yield return null;
            }
        // 万象镜面：单次伤害最高为1
            if (ci.templateID == "01318")
            {
                bool anyTarget = false;
                for (int j = 0; j < 12; j++)
                {
                    if (allSlots[j]?.currentCard3D != null) { anyTarget = true; break; }
                }
                if (!anyTarget) continue;

                bool done = false;
                SelectionManager.Instance.BeginSelection(TargetType.AllMinions, (targetSlot) =>
                {
                    if (targetSlot?.currentCard3D != null)
                    {
                        CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (targetCI != null)
                        {
                            targetCI.originalAttackBeforeDebuff = targetCI.currentAttack;
                            targetCI.currentAttack = 1;
                            targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        }
                    }
                    done = true;
                });
                yield return new WaitUntil(() => done);
            }
            // 万人迷：对手+1能量
            if (ci.templateID == "01314")
            {
                NetworkPlayer.Remote.AddEnergy(1);
            }
        }

        // ===== 阶段2.4：伤害处理 =====
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.hasFirstStrike) continue;

        // 检查对方是否有合法目标
            if (ci.templateID == "03506")
            {
                int[] targets = { 2, 0, 4 };
                foreach (int id in targets)
                {
                    BoardSlot targetSlot = allSlots[id];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (targetInst?.cardInstance != null)
                        {
                            ApplyDamageToMinion(targetInst.cardInstance, 2, slot.currentCard3D);
                            targetInst.UpdateValues();
                        }
                    }
                }
            }

        // 检查对方是否有合法目标
            if (ci.templateID == "03513")
            {
                int[] targets = { 1, 5, 3 };
                foreach (int id in targets)
                {
                    BoardSlot targetSlot = allSlots[id];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (targetInst?.cardInstance != null)
                        {
                            ApplyDamageToMinion(targetInst.cardInstance, 2, slot.currentCard3D);
                            targetInst.UpdateValues();
                        }
                    }
                }
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.templateID == "01310")
            {
                for (int j = 0; j <= 5; j++)
                {
                    BoardSlot targetSlot = allSlots[j];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (targetInst?.cardInstance != null)
                        {
                            ApplyDamageToMinion(targetInst.cardInstance, 1, slot.currentCard3D);
                            targetInst.UpdateValues();
                        }
                    }
                }
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.templateID == "03005")
            {
                int[] frontRow = { 0, 1, 2 };
                foreach (int id in frontRow)
                {
                    BoardSlot targetSlot = allSlots[id];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (targetInst?.cardInstance != null)
                        {
                            ApplyDamageToMinion(targetInst.cardInstance, 1, slot.currentCard3D);
                            targetInst.UpdateValues();
                        }
                    }
                }
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.templateID == "03003")
            {
                int[] backRow = { 3, 4, 5 };
                foreach (int id in backRow)
                {
                    BoardSlot targetSlot = allSlots[id];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance targetInst = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (targetInst?.cardInstance != null)
                        {
                            ApplyDamageToMinion(targetInst.cardInstance, 1, slot.currentCard3D);
                            targetInst.UpdateValues();
                        }
                    }
                }
            }
            if (ci.templateID == "03020")
            {
                NetworkPlayer.Remote.TakeDamage(1);
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.grantedTraitTexts.Exists(t => t.Contains("先手：对对方前排召唤物造成1伤害")))
            {
                int[] frontRow = { 0, 1, 2 };
                foreach (int id in frontRow)
                {
                    BoardSlot targetSlot = allSlots[id];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance ti = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (ti?.cardInstance != null)
                        {
                            ApplyDamageToMinion(ti.cardInstance, 1, slot.currentCard3D);
                            ti.UpdateValues();
                        }
                    }
                }
            }
            // 修正者赋予的先手（灵能版）：对前排2伤害，后排1伤害
            if (ci.grantedTraitTexts.Exists(t => t.Contains("先手：对对方前排召唤物造成2伤害，对后排造成1伤害")))
            {
                for (int j = 0; j <= 5; j++)
                {
                    BoardSlot targetSlot = allSlots[j];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance ti = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (ti?.cardInstance != null)
                        {
                            int dmg = j < 3 ? 2 : 1;
                            ApplyDamageToMinion(ti.cardInstance, dmg, slot.currentCard3D);
                            ti.UpdateValues();
                        }
                    }
                }
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.HasFirstStrike && ci.grantedTraitTexts.Contains("先手：扣己方玩家1生命值"))
            {
                if (slot.slotID >= 6)
                    NetworkPlayer.Local.TakeDamage(1);
                else
                    NetworkPlayer.Remote.TakeDamage(1);
            }
        }

        // ===== 阶段2.5：死亡递归退出 =====
        bool anyDied;
        do
        {
            anyDied = false;
            List<GameObject> died = new List<GameObject>();
            foreach (BoardSlot slot in allSlots)
            {
                if (slot?.currentCard3D == null) continue;
                Card3DInstance inst = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (inst?.cardInstance != null && inst.cardInstance.currentHealth <= 0)
                    died.Add(slot.currentCard3D);
            }
            foreach (GameObject dead in died)
            {
                CardInstance deadInst = dead.GetComponent<Card3DInstance>()?.cardInstance;

               

                foreach (BoardSlot slot in allSlots)
                {
                    if (slot.currentCard3D == dead)
                    {
                        slot.HandleDeath(dead);
                        anyDied = true;
                        break;
                    }
                }
            }
        } while (anyDied);
    }

    void FirstStrike()
    {
        StartCoroutine(FirstStrikeCoroutine());
    }
    IEnumerator MinionAttacksCoroutine()
    {
        Debug.Log("[战斗] 阶段3：召唤物攻击");

        List<(GameObject target, int damage, GameObject source)> damageList = new List<(GameObject, int, GameObject)>();

        for (int col = 0; col < 3; col++)
        {
            ProcessPair(col, col, damageList);
            ProcessPair(col + 3, col + 3, damageList);
        }


        yield return StartCoroutine(ApplyDamageLoop(damageList, "攻击"));

        // 检查对方是否有合法目标
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot mySlot = allSlots[i];
            if (mySlot?.currentCard3D == null) continue;
            CardInstance myInst = mySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (myInst == null) continue;
            if (myInst.templateID != "01345") continue;
            if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(myInst)) continue;

            int myCol = i % 3;
            int myRow = i < 9 ? 0 : 3;
            int enemyRowStart = myRow == 0 ? 3 : 0;
            int enemySlotIndex = enemyRowStart + myCol;

            BoardSlot enemySlot = allSlots[enemySlotIndex];

            int targetEnemyRow = enemyRowStart == 0 ? 3 : 0;
            int targetEnemySlotIndex = targetEnemyRow + myCol;
            BoardSlot targetEnemySlot = allSlots[targetEnemySlotIndex];
            if (targetEnemySlot.isBlocked || targetEnemySlot.prisonBlocked) continue;

            GameObject enemyCard = enemySlot?.currentCard3D;
            GameObject targetCard = targetEnemySlot?.currentCard3D;

            Vector3 pos1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(enemySlotIndex);
            Vector3 pos2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(targetEnemySlotIndex);

            enemySlot.SetCard(null);
            targetEnemySlot.SetCard(null);

            if (targetCard != null)
            {
                targetCard.transform.position = pos1;
                enemySlot.SetCard(targetCard);
            }
            if (enemyCard != null)
            {
                enemyCard.transform.position = pos2;
                targetEnemySlot.SetCard(enemyCard);
            }

            BoardManager.SyncAttachedModels(enemySlot);
            BoardManager.SyncAttachedModels(targetEnemySlot);
        }
        BoardSlot.CheckAndHandleDeaths();
        // 检查对方是否有合法目标
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.originalAttackBeforeDebuff > 0)
            {
                ci.currentAttack = ci.originalAttackBeforeDebuff;
                ci.originalAttackBeforeDebuff = 0;
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
    }
    void ProcessPair(int mySlotIndex, int enemySlotIndex, List<(GameObject, int, GameObject)> damageList)
    {
        BoardSlot mySlot = allSlots[mySlotIndex + 6];
        BoardSlot enemySlot = allSlots[enemySlotIndex];
        GameObject myCard = mySlot?.currentCard3D;
        GameObject enemyCard = enemySlot?.currentCard3D;

        CardInstance myInst = myCard?.GetComponent<Card3DInstance>()?.cardInstance;
        CardInstance enemyInst = enemyCard?.GetComponent<Card3DInstance>()?.cardInstance;

        bool mySilenced = myInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(myInst);

        // 检查对方是否有合法目标
        if (myCard != null && myInst != null && !myInst.silencedThisPhase)
        {
            int targetEnemySlotIndex;

            if (myInst.templateID == "01305")
            {
                targetEnemySlotIndex = enemySlotIndex % 3;
                for (int i = 0; i < 3; i++)
                {
                    if (i == targetEnemySlotIndex) continue;
                    BoardSlot otherSlot = allSlots[i];
                    GameObject otherCard = otherSlot?.currentCard3D;
                    CardInstance otherInst = otherCard?.GetComponent<Card3DInstance>()?.cardInstance;
                    bool otherSilenced = otherInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(otherInst);
                    if (otherCard != null && otherInst != null && !otherInst.silencedThisPhase && !otherSilenced)
                    {
                        damageList.Add((otherCard, 2, myCard));
                    }
                }
            }
            else if (myInst.templateID == "01530")
            {
                targetEnemySlotIndex = enemySlotIndex;
                int rowStart = (targetEnemySlotIndex < 3) ? 0 : 3;
                int rowEnd = rowStart + 3;
                for (int i = rowStart; i < rowEnd; i++)
                {
                    if (i == targetEnemySlotIndex) continue;
                    BoardSlot otherSlot = allSlots[i];
                    GameObject otherCard = otherSlot?.currentCard3D;
                    CardInstance otherInst = otherCard?.GetComponent<Card3DInstance>()?.cardInstance;
                    bool otherSilenced = otherInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(otherInst);
                    if (otherCard != null && otherInst != null && !otherInst.silencedThisPhase && !otherSilenced)
                    {
                        damageList.Add((otherCard, myInst.Attack, myCard));
                    }
                }
            }
            else if (myInst.templateID == "01336" && !myInst.isAttached)
            {
                targetEnemySlotIndex = enemySlotIndex % 3; // 攻击前排对位

        // 检查对方是否有合法目标
                int rowStart = (targetEnemySlotIndex < 3) ? 0 : 3;
                for (int j = rowStart; j < rowStart + 3; j++)
                {
                    if (j == targetEnemySlotIndex) continue;
                    BoardSlot otherSlot = allSlots[j];
                    GameObject otherCard = otherSlot?.currentCard3D;
                    if (otherCard != null)
                    {
                        damageList.Add((otherCard, 1, myCard));
                    }
                }
        // 检查对方是否有合法目标
            }
            else if (myInst.attacksBackRow)
            {
                targetEnemySlotIndex = enemySlotIndex % 3 + 3;
            }
            else if (myInst.attacksFrontRow)
            {
                targetEnemySlotIndex = enemySlotIndex % 3;
            }
            else
            {
                targetEnemySlotIndex = enemySlotIndex;
            }

            BoardSlot targetEnemySlot = allSlots[targetEnemySlotIndex];
            GameObject targetEnemyCard = targetEnemySlot?.currentCard3D;
            CardInstance targetEnemyInst = targetEnemyCard?.GetComponent<Card3DInstance>()?.cardInstance;
            bool targetSilenced = targetEnemyInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(targetEnemyInst);

            if (targetEnemyCard != null && targetEnemyInst != null && !targetEnemyInst.silencedThisPhase)
            {
                int myAtk = myInst.Attack;
                if (!myInst.isXValue)
                {
                    myAtk += mySlot.slotTempAttackBoost;
                }
            // 亡命之徒：攻击对方召唤物扣对方玩家2血
                if (myInst.templateID == "01114" && targetEnemyInst.hasShield)
                {
                    targetEnemyInst.currentHealth -= 2;
                    targetEnemyInst.GetComponent<Card3DInstance>()?.UpdateValues();
                }
            // 亡命之徒：攻击对方召唤物扣对方玩家2血
                if (targetEnemyInst != null && targetEnemyInst.hasShield)
                {
                    bool breakerOnField = false;
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    for (int i = 6; i <= 11; i++)
                    {
                        BoardSlot s = bm?.GetSlot(i);
                        if (s?.currentCard3D != null)
                        {
                            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null && ci.templateID == "01328" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ci)))
                            {
                                breakerOnField = true;
                                break;
                            }
                        }
                    }
                    if (breakerOnField)
                    {
                        targetEnemyInst.currentHealth -= 2;
                        targetEnemyInst.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
                // 投机者：攻击有先手或进场特性的召唤物伤害+2
                if (myInst.templateID == "01118")
                {
                    if (targetEnemyInst.HasOnDeath || targetEnemyInst.HasActiveExit)
                    {
                        myAtk += 2;
                    }
                }
                // 投机者：攻击有先手或进场特性的召唤物伤害+2
                if (myInst.templateID == "01125")
                {
                    if (targetEnemyInst.HasFirstStrike || targetEnemyInst.HasOnEnter)
                    {
                        myAtk += 2;
                    }
                }
                damageList.Add((targetEnemyCard, myAtk, myCard));
                if (IsShadowHost(myInst) && targetEnemyInst != null)
                {
                    myInst.currentHealth -= myAtk;
                    if (myInst.currentHealth < 0) myInst.currentHealth = 0;
                    mySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
        // 检查对方是否有合法目标
                if (myInst.templateID == "01508" && !myInst._conquerorTriggered
                    && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(myInst)))
                {
                    myInst._conquerorPendingCheck = true;
                    myInst._conquerorTargetEnemyCard = targetEnemyCard;
                }
            }
            // 亡命之徒：攻击对方召唤物扣对方玩家2血
            if (myInst.templateID == "01531" && targetEnemyInst != null && !myInst._outlawPlayerDamageThisTurn
                && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(myInst)))
            {
                NetworkPlayer.Remote.TakeDamage(2);
                myInst._outlawPlayerDamageThisTurn = true;
            }
            if (targetEnemyCard == null)
            {
                if (enemySlot.prisonBlocked || enemySlot.isBlocked)
                {
        // 检查对方是否有合法目标
                }
                else if (IsShadowHost(myInst))
                {
                    int myTier = myInst.currentTier;
                    if (HasSuppressorOnField() && myInst.summonType == SummonType.Hero)
                        myTier += 1;
                    pendingDamageToEnemy += myTier;

                    myInst.currentHealth -= myInst.currentAttack;
                    if (myInst.currentHealth < 0) myInst.currentHealth = 0;
                    mySlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                }
                else if (myInst.templateID == "01531" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(myInst)))
                {
                    pendingDamageToEnemy += myInst.currentTier + 2;
                }
                else if (myInst.templateID == "03014")
                {
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    if (bm != null)
                    {
                        for (int j = 0; j <= 5; j++)
                        {
                            BoardSlot es = bm.GetSlot(j);
                            if (es?.currentCard3D != null && !es.prisonBlocked && !es.isBlocked)
                            {
                                Card3DInstance e3d = es.currentCard3D.GetComponent<Card3DInstance>();
                                if (e3d?.cardInstance != null)
                                {
                                    ApplyDamageToMinionPublic(e3d.cardInstance, 2, myCard);
                                    e3d.UpdateValues();
                                }
                            }
                        }
                    }
                    BoardSlot.CheckAndHandleDeaths();
                }
                else
                {
                    int myTier = myInst.currentTier;
                    if (HasSuppressorOnField() && myInst.summonType == SummonType.Hero)
                        myTier += 1;
                    pendingDamageToEnemy += myTier;
                }
            }
        }

        // 检查对方是否有合法目标
        bool enemySilenced = enemyInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(enemyInst);
        if (enemyCard != null && enemyInst != null && !enemyInst.silencedThisPhase)
        {
            if (myCard != null)
            {
                int enemyAtk = enemyInst.Attack;
                damageList.Add((myCard, enemyAtk, enemyCard));
            }
            else
            {
                if (!mySlot.prisonBlocked && !mySlot.isBlocked)
                {
                    pendingDamageToMe += enemyInst.currentTier;
                }
            }
        }
    }
    IEnumerator ApplyDamageLoop(List<(GameObject target, int damage, GameObject source)> initialDamage, string phase)
    {
        List<(GameObject, int, GameObject)> pending = new List<(GameObject, int, GameObject)>(initialDamage);

        while (pending.Count > 0)
        {
            foreach (var dmg in pending)
            {
                if (dmg.Item1 == null) continue;
                Card3DInstance inst = dmg.Item1.GetComponent<Card3DInstance>();
                if (inst?.cardInstance != null)
                {
                    if (inst.cardInstance.isAttached) continue;
                    if (IsShadowHost(inst.cardInstance)) continue;
                    if (inst.cardInstance.hasShield)
                    {
                        inst.cardInstance.RemoveShield();
                        continue;
                    }

                    int actualDamage = dmg.Item2;

                    if (inst.cardInstance.isYinYang && !IsTargetSilenced(inst.cardInstance))
                        actualDamage = Mathf.Max(0, actualDamage - 1);

                    if (inst.cardInstance.poisoned) actualDamage *= 2;

                    if (inst.cardInstance.tempHealthBoost > 0)
                    {
                        if (actualDamage <= inst.cardInstance.tempHealthBoost)
                        {
                            inst.cardInstance.tempHealthBoost -= actualDamage;
                            inst.cardInstance.currentHealth -= actualDamage;
                            actualDamage = 0;
                        }
                        else
                        {
                            actualDamage -= inst.cardInstance.tempHealthBoost;
                            inst.cardInstance.currentHealth -= inst.cardInstance.tempHealthBoost;
                            inst.cardInstance.tempHealthBoost = 0;
                        }
                    }

        // 万象镜面：单次伤害最高为1
                    if (inst.cardInstance.templateID == "01512" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(inst.cardInstance)))
                        actualDamage = Mathf.Min(actualDamage, 1);

        // 检查对方是否有合法目标
                    CardInstance lord = FindLordOnField();
                    if (lord != null && inst.cardInstance != lord && IsAllyUnit(inst.cardInstance) && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(lord)))
                    {
                        lord.currentHealth -= actualDamage;
                        UpdateLordDisplay(lord);
                        continue;
                    }

        // 检查对方是否有合法目标
                    if (inst.cardInstance.braveTemplateID == "01514" && inst.cardInstance.currentHealth - actualDamage <= 0)
                    {
                        if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(inst.cardInstance))
                        {
                            BoardManager bm = FindObjectOfType<BoardManager>();
                            GameObject lastFollower = null;
                            int lastOrder = -1;
                            int hostSlotID = -1;
                            for (int i = 0; i < 12; i++)
                            {
                                BoardSlot s = bm?.GetSlot(i);
                                if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == inst.cardInstance)
                                { hostSlotID = i; break; }
                            }
                            foreach (GameObject obj in bm.attachedModels)
                            {
                                CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                                if (ci != null && ci.isAttached && ci.hostSlotID == hostSlotID && ci.templateID == "03001")
                                {
                                    if (ci.attachOrder > lastOrder) { lastOrder = ci.attachOrder; lastFollower = obj; }
                                }
                            }
                            if (lastFollower != null)
                            {
                                bm.attachedModels.Remove(lastFollower);
                                Destroy(lastFollower);
                                inst.cardInstance.currentHealth = 2;
                                int newOrder = 0;
                                foreach (GameObject obj2 in bm.attachedModels)
                                {
                                    CardInstance ci2 = obj2?.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ci2 != null && ci2.isAttached && ci2.hostSlotID == hostSlotID)
                                    { ci2.attachOrder = newOrder++; }
                                }
                                BoardManager.SyncAttachedModels(bm.GetSlot(hostSlotID));
                                continue;
                            }
                        }
                    }

        // 检查对方是否有合法目标
                    if (inst.cardInstance.hasLifePriestBlessing && inst.cardInstance.currentHealth - actualDamage <= 0)
                    {
                        CardInstance priest = inst.cardInstance.lifePriestBlessingSource;
                        if (priest == null || GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(priest))
                        {
                            inst.cardInstance.hasLifePriestBlessing = false;
                            inst.cardInstance.lifePriestBlessingSource = null;
                            inst.cardInstance.currentHealth = inst.cardInstance.currentMaxHealth;
                            inst.cardInstance.currentHealth += 2;
                            inst.cardInstance.currentMaxHealth += 2;
                            inst.cardInstance.currentAttack += 1;
                            UpdateLordDisplay(inst.cardInstance);
                            CardData td = CardDatabase.Instance?.GetTemplate(inst.cardInstance.templateID);
                            if (td != null && td.hasOnEnter)
                            {
                                BoardSlot targetSlot = FindSlotOf(inst.cardInstance);
                                if (targetSlot != null)
                                    targetSlot.StartOnEnterEffect(td, inst.cardInstance);
                            }
                            continue;
                        }
                    }

                    if (inst.cardInstance.isXValue) inst.cardInstance.xAccumulatedDamage += actualDamage;
                    if (inst.cardInstance.templateID == "01534")
                        inst.cardInstance.totalDamageTaken += Mathf.Min(actualDamage, inst.cardInstance.currentHealth);
                    inst.cardInstance.currentHealth -= actualDamage;

        // 检查对方是否有合法目标
                    if (inst.cardInstance.templateID == "01508" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(inst.cardInstance)))
                        inst.cardInstance._conquerorTotalDamageThisBattle += actualDamage;

                    dmg.Item1.GetComponent<DamageSourceMarker>()?.RegisterDamage(dmg.Item3, actualDamage);
                }
            }
            pending.Clear();

        // 检查对方是否有合法目标
            CheckConquerorTrigger();

            List<GameObject> died = new List<GameObject>();
            foreach (BoardSlot slot in allSlots)
            {
                GameObject card = slot?.currentCard3D;
                if (card == null) continue;
                Card3DInstance inst = card.GetComponent<Card3DInstance>();
                if (inst?.cardInstance != null && inst.cardInstance.currentHealth <= 0)
                    died.Add(card);
            }

            foreach (GameObject dead in died)
            {
                CardInstance deadInst = dead.GetComponent<Card3DInstance>()?.cardInstance;
                DamageSourceMarker marker = dead.GetComponent<DamageSourceMarker>();

                if (deadInst != null && deadInst.damageSourceInstanceIDs.Count > 0)
                {
                    BoardManager bm = FindObjectOfType<BoardManager>();
                    if (bm != null)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            BoardSlot dragonSlot = bm.GetSlot(i);
                            if (dragonSlot?.currentCard3D == null) continue;
                            Card3DInstance c3d = dragonSlot.currentCard3D.GetComponent<Card3DInstance>();
                            if (c3d?.cardInstance?.templateID == "01530")
                            {
                                string dragonInstanceID = c3d.cardInstance.instanceID;
                                if (deadInst.damageSourceInstanceIDs.Contains(dragonInstanceID))
                                {
                                    if (NetworkPlayer.Remote != null && NetworkPlayer.Remote.handCards.Count > 0)
                                    {
                                        int randomIndex = UnityEngine.Random.Range(0, NetworkPlayer.Remote.handCards.Count);
                                        GameObject card = NetworkPlayer.Remote.handCards[randomIndex];
                                        NetworkPlayer.Remote.handCards.RemoveAt(randomIndex);
                                        Destroy(card);
                                    }
                                }
                            }
                        }
                    }
                }

        // 检查对方是否有合法目标
                if (deadInst != null && deadInst.revengeEffect != null && deadInst.revengeEffect.Contains("对方摸两张牌"))
                {
                    for (int j = 0; j < 2; j++)
                    {
                        CardData data = DeckManager.Instance?.DrawFromMain();
                        if (data != null)
                            NetworkPlayer.Remote.AddCardToHand(data);
                    }
                }
                else if (deadInst != null && deadInst.revengeEffect != null && deadInst.revengeEffect.Contains("+1能量"))
                {
                    NetworkPlayer.Local.AddEnergy(1);
                }
                else if (deadInst != null && deadInst.revengeEffect != null && deadInst.revengeEffect.Contains("选定一个格子，该格子上的召唤物临时+0-1（最少为0）并且每阶段开始扣一生命值"))
                {
                    yield return StartCoroutine(WaitForSelection((onDone) =>
                    {
                        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                        {
                            if (targetSlot != null && !targetSlot.isBlocked)
                            {
                                targetSlot.deepSeaAttackDebuff++;
                                targetSlot.deepSeaHealthDebuff = true;
                                if (targetSlot.currentCard3D != null)
                                {
                                    CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ci != null)
                                    {
                                        ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                                        targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                            }
                            onDone();
                        });
                        BoardSlot.isStrengtheningSlot = true;
                    }));
                }
                else if (deadInst != null && deadInst.revengeEffect != null && deadInst.revengeEffect.Contains("为己方一召唤物+2+1"))
                {
                    yield return StartCoroutine(WaitForSelection((onDone) =>
                    {
                        BoardManager bm = FindObjectOfType<BoardManager>();
                        bool hasAlly = false;
                        for (int j = 6; j <= 11; j++)
                            if (bm?.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
                        if (hasAlly)
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                            {
                                if (targetSlot?.currentCard3D != null)
                                {
                                    CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                    if (ci != null)
                                    {
                                        ci.currentHealth += 2;
                                        ci.currentMaxHealth += 2;
                                        ci.currentAttack += 1;
                                        targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                    }
                                }
                                onDone();
                            });
                        }
                        else onDone();
                    }));
                }
        // 检查对方是否有合法目标
                else if (deadInst != null && !string.IsNullOrEmpty(deadInst.revengeEffect))
                {
                    List<GameObject> revengeTargets = marker?.GetMinionDamageSources();
                    if (revengeTargets != null && revengeTargets.Count > 0)
                    {
                        yield return StartCoroutine(ResolveRevengeEffect(deadInst.revengeEffect, dead, revengeTargets));
                    }
                }

                BoardSlot deadSlot = null;
                foreach (BoardSlot slot in allSlots)
                {
                    if (slot.currentCard3D == dead) { deadSlot = slot; break; }
                }
                int slotID = deadSlot != null ? deadSlot.slotID : -1;

                if (deadSlot != null)
                {
                    deadSlot.HandleDeath(dead);
                    yield return null;
                    yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
                }
            }
        }
    }
    IEnumerator ResolveRevengeEffect(string effect, GameObject deadCard, List<GameObject> targets)
    {
        Debug.Log($"ResolveRevengeEffect: effect={effect}");

        if (effect.Contains("对击杀它的召唤物造成3伤害"))
        {
            foreach (GameObject target in targets)
            {
                Card3DInstance tInst = target.GetComponent<Card3DInstance>();
                if (tInst != null)
                {
                    tInst.cardInstance.currentHealth -= 3;
                    tInst.UpdateValues();
                    target.GetComponent<DamageSourceMarker>()?.RegisterDamage(deadCard, 3);
                }
            }
        }
        else if (effect.Contains("对击杀它的召唤物造成999伤害"))
        {
            foreach (GameObject target in targets)
            {
                Card3DInstance tInst = target.GetComponent<Card3DInstance>();
                if (tInst != null)
                {
                    tInst.cardInstance.currentHealth -= 999;
                    tInst.UpdateValues();
                    target.GetComponent<DamageSourceMarker>()?.RegisterDamage(deadCard, 999);
                }
            }
        }
        else if (effect.Contains("对方摸两张牌"))
        {
            foreach (GameObject target in targets)
            {
                Card3DInstance tInst = target.GetComponent<Card3DInstance>();
                if (tInst != null)
                {
                    tInst.cardInstance.baseAttack -= 1;
                    tInst.cardInstance.currentAttack = tInst.cardInstance.baseAttack;
                    tInst.UpdateValues();
                }
            }
        }
        else if (effect.Contains("为己方一召唤物+2+1"))
        {
            yield return StartCoroutine(WaitForSelection((onDone) =>
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                bool hasAlly = false;
                for (int j = 6; j <= 11; j++)
                {
                    if (bm?.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
                }
                if (hasAlly)
                {
                    SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                    {
                        if (targetSlot?.currentCard3D == null) { onDone(); return; }
                        if (targetSlot.currentCard3D == deadCard) return;
                        CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null)
                        {
                            if (!ci.cannotHealOrGainMaxHP)
                            {
                                ci.currentHealth += 2;
                                ci.currentMaxHealth += 2;
                            }
                            ci.currentAttack += 1;
                            targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                        }
                        onDone();
                    });
                }
                else
                {
                    onDone();
                }
            }));
        }
        else if (effect.Contains("对方摸两张牌"))
        {
            for (int j = 0; j < 2; j++)
            {
                CardData data = DeckManager.Instance?.DrawFromMain();
                if (data != null)
                    NetworkPlayer.Remote.AddCardToHand(data);
            }
        }
        else if (effect.Contains("选定一个格子，该格子上的召唤物临时+0-1（最少为0）并且每阶段开始扣一生命值"))
        {
            yield return StartCoroutine(WaitForSelection((onDone) =>
            {
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                {
                    if (targetSlot != null && !targetSlot.isBlocked)
                    {
                        targetSlot.deepSeaAttackDebuff++;
                        targetSlot.deepSeaHealthDebuff = true;
                        if (targetSlot.currentCard3D != null)
                        {
                            CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                            if (ci != null)
                            {
                                ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                                targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                            }
                        }
                    }
                    onDone();
                });
                BoardSlot.isStrengtheningSlot = true;
            }));
        }
        else if (effect.Contains("+1能量"))
        {
            NetworkPlayer.Local.AddEnergy(1);
        }
        else
        {
            Debug.Log($"未实现的反击效果：{effect}");
        }
    }

    void CompareSurvivors()
    {
        int my = 0, enemy = 0;
        for (int i = 0; i < 6; i++)
        {
            if (allSlots[i + 6]?.currentCard3D != null && !allSlots[i + 6].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance?.isAttached == true)
                my++;
            if (allSlots[i]?.currentCard3D != null && !allSlots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance?.isAttached == true)
                enemy++;
        }

        // 超数故障：己方存活人数+1
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot slot = bm.GetSlot(i);
                if (slot?.currentCard3D == null) continue;
                CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01128")
                {
                    if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                        continue;
                    my++;
                }
            }
            bm.attachedModels.RemoveAll(a => a == null || a.transform == null);
        // 检查对方是否有合法目标
            foreach (GameObject obj in bm.attachedModels)
            {
                CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01128")
                {
                    if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                        continue;
                    my++;
                }
            }
        }

        int diff = my - enemy;
        if (diff > 0)
            pendingDamageToEnemy += diff;
        else if (diff < 0)
            pendingDamageToMe += -diff;

        Debug.Log($"[战斗] 存活对比 己{my} vs 敌{enemy} 差{Mathf.Abs(diff)}");
    }

    void FinalDamage()
    {
        // Player health damage: server-only (SyncVar auto-replicates to clients)
        if (NetworkServer.active)
        {
            if (pendingDamageToMe > pendingDamageToEnemy)
            {
                int finalDamage = pendingDamageToMe - pendingDamageToEnemy;
                NetworkPlayer.Local?.TakeDamage(finalDamage);
                Debug.Log($"[Battle] FinalDamage: local takes {finalDamage}");
            }
            else if (pendingDamageToEnemy > pendingDamageToMe)
            {
                int finalDamage = pendingDamageToEnemy - pendingDamageToMe;
                NetworkPlayer.Remote?.TakeDamage(finalDamage);
                Debug.Log($"[Battle] FinalDamage: remote takes {finalDamage}");
            }
        }

        pendingDamageToMe = 0;
        pendingDamageToEnemy = 0;

        foreach (BoardSlot slot in allSlots)
        {
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.hasShield && ci.shieldEndAtBattleEnd)
            {
                ci.RemoveShield();
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
        }
       
        CounterManager.Instance?.CheckOnBattleEnd();

        HandManager hmFinal = FindObjectOfType<HandManager>();
        foreach (BoardSlot slot in allSlots)
        {
            if (slot?.currentCard3D == null) continue;
            Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            CardInstance ci = c3d?.cardInstance;
            if (ci == null) continue;

            if (ci.isXValue)
            {
                if (ci.xAccumulatedDamage >= ci.xInitialHealth)
                {
                    slot.HandleDeath(slot.currentCard3D);
                }
                else
                {
                    hmFinal?.UpdateXValues(ci);
                }
                ci.xAccumulatedDamage = 0;
            }

        // 检查对方是否有合法目标
            if (ci.tempHealthBoost > 0)
            {
                ci.currentHealth -= ci.tempHealthBoost;
            }
            ci._conquerorTriggered = false;
            ci._conquerorTotalDamageThisBattle = 0;
            ci._conquerorPendingCheck = false;
            ci.currentAttack -= ci.tempAttackBoost;
            ci.tempAttackBoost = 0;
            ci.tempHealthBoost = 0;
        }
        // 检查对方是否有合法目标
        bool hasShadow = false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = allSlots[i];
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.isShadow) { hasShadow = true; break; }
            }
        }
        if (hasShadow)
        {
            CardInstance.shadowTierBonus += 1;
            CardInstance.shadowAtkBonus += 2;
        // 检查对方是否有合法目标
            for (int i = 6; i <= 11; i++)
            {
                BoardSlot s = allSlots[i];
                if (s?.currentCard3D != null)
                {
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.isShadow)
                    {
                        ci.currentTier += 1;
                        ci.currentAttack += 2;
                        s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
        }
    }
    public bool HasSuppressorOnField()
    {
        if (allSlots == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03501")
            {
        // 检查对方是否有合法目标
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                    continue;
                return true;
            }
        }
        return false;
    }
    void ApplyDamageToMinion(CardInstance target, int damage, GameObject source)
    {
        if (target == null) return;
        if (target.isAttached) return;
        if (IsShadowHost(target)) return;
        if (target.hasShield)
        {
            target.RemoveShield();
            return;
        }

        int actualDamage = damage;

        if (target.isYinYang && !IsTargetSilenced(target))
            actualDamage = Mathf.Max(0, actualDamage - 1);

        if (source != null)
        {
            CardInstance sourceCI = source.GetComponent<Card3DInstance>()?.cardInstance;
            if (sourceCI != null && sourceCI.overclocked) actualDamage *= 2;
        }

        if (target.poisoned) actualDamage *= 2;

        if (target.tempHealthBoost > 0)
        {
            if (actualDamage <= target.tempHealthBoost)
            {
                target.tempHealthBoost -= actualDamage;
                target.currentHealth -= actualDamage;
                actualDamage = 0;
            }
            else
            {
                actualDamage -= target.tempHealthBoost;
                target.currentHealth -= target.tempHealthBoost;
                target.tempHealthBoost = 0;
            }
        }

        // 万象镜面：单次伤害最高为1
        if (target.templateID == "01512" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(target)))
            actualDamage = Mathf.Min(actualDamage, 1);

        // 检查对方是否有合法目标
        CardInstance lord = FindLordOnField();
        if (lord != null && target != lord && IsAllyUnit(target) && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(lord)))
        {
            lord.currentHealth -= actualDamage;
            UpdateLordDisplay(lord);
            return;
        }

        // 检查对方是否有合法目标
        if (target.braveTemplateID == "01514" && target.currentHealth - actualDamage <= 0)
        {
            if (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(target))
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                GameObject lastFollower = null;
                int lastOrder = -1;
                int hostSlotID = -1;
                for (int i = 0; i < 12; i++)
                {
                    BoardSlot s = bm?.GetSlot(i);
                    if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == target)
                    { hostSlotID = i; break; }
                }
                foreach (GameObject obj in bm.attachedModels)
                {
                    CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.isAttached && ci.hostSlotID == hostSlotID && ci.templateID == "03001")
                    {
                        if (ci.attachOrder > lastOrder) { lastOrder = ci.attachOrder; lastFollower = obj; }
                    }
                }
                if (lastFollower != null)
                {
                    bm.attachedModels.Remove(lastFollower);
                    Destroy(lastFollower);
                    target.currentHealth = 2;
                    int newOrder = 0;
                    foreach (GameObject obj2 in bm.attachedModels)
                    {
                        CardInstance ci2 = obj2?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci2 != null && ci2.isAttached && ci2.hostSlotID == hostSlotID)
                        { ci2.attachOrder = newOrder++; }
                    }
                    BoardManager.SyncAttachedModels(bm.GetSlot(hostSlotID));
                    return;
                }
            }
        }

        // 检查对方是否有合法目标
        if (target.hasLifePriestBlessing && target.currentHealth - actualDamage <= 0)
        {
            CardInstance priest = target.lifePriestBlessingSource;
            if (priest == null || GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(priest))
            {
                target.hasLifePriestBlessing = false;
                target.lifePriestBlessingSource = null;
                target.currentHealth = target.currentMaxHealth;
                target.currentHealth += 2;
                target.currentMaxHealth += 2;
                target.currentAttack += 1;
                UpdateLordDisplay(target);
                CardData td = CardDatabase.Instance?.GetTemplate(target.templateID);
                if (td != null && td.hasOnEnter)
                {
                    BoardSlot targetSlot = FindSlotOf(target);
                    if (targetSlot != null)
                        targetSlot.StartOnEnterEffect(td, target);
                }
                return;
            }
        }

        if (target.isXValue) target.xAccumulatedDamage += actualDamage;
        if (target.templateID == "01534")
            target.totalDamageTaken += Mathf.Min(actualDamage, target.currentHealth);
        target.currentHealth -= actualDamage;

    
        // 检查对方是否有合法目标
        if (target.templateID == "01508" && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(target)))
            target._conquerorTotalDamageThisBattle += actualDamage;

        if (source != null)
        {
            DamageSourceMarker marker = FindCard3DByInstance(target)?.GetComponent<DamageSourceMarker>();
            if (marker != null) marker.RegisterDamage(source, actualDamage);
        }
    }
    IEnumerator TroubleMakerEffect(CardInstance giver)
    {
        // 检查对方是否有合法目标
        bool alreadyHas = false;
        for (int i = 0; i <= 5; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.grantedTraitTexts.Contains("先手：扣己方玩家1生命值"))
            {
                alreadyHas = true;
                break;
            }
        }
        if (alreadyHas)
        {
            Debug.Log("麻烦制造者：对方场上无召唤物，跳过");
            yield break;
        }

        // 检查对方是否有合法目标
        bool hasEnemy = false;
        for (int i = 0; i <= 5; i++)
        {
            if (allSlots[i]?.currentCard3D != null) { hasEnemy = true; break; }
        }
        if (!hasEnemy)
        {
            Debug.Log("麻烦制造者：对方场上无召唤物，跳过");
            yield break;
        }

        bool done = false;
        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                CardInstance targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetCI != null)
                {
                    targetCI.GrantTrait("先手：扣己方玩家1生命值");
                    targetCI.hasFirstStrike = true;
                    targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    Debug.Log($"麻烦制造者赋予先手特性给槽位{targetSlot.slotID}");
                }
            }
            done = true;
        });

        yield return new WaitUntil(() => done);
    }
    Card3DInstance FindCard3DByInstance(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = bm?.GetSlot(i);
            if (slot?.currentCard3D != null)
            {
                Card3DInstance c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                if (c3d?.cardInstance == ci) return c3d;
            }
        }
        return null;
    }
    public void ApplyDamageToMinionPublic(CardInstance target, int damage, GameObject source)
    {
        ApplyDamageToMinion(target, damage, source);
    }
    public IEnumerator WaitForSelection(Action<Action> selection)
    {
        bool done = false;
        selection(() => done = true);
        yield return new WaitUntil(() => done);
    }
    bool IsTargetSilenced(CardInstance ci)
    {
        return GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci);
    }
    IEnumerator QuickShadowRearrangeEffect(CardInstance ci)
    {
        BoardSlot.isStrengtheningSlot = true;
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (firstSlot == null)
            {
                firstSlot = selected;
            }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);
                firstSlot.SetCard(null); secondSlot.SetCard(null);
                if (c2 != null) { c2.transform.position = p1; firstSlot.SetCard(c2); }
                if (c1 != null) { c1.transform.position = p2; secondSlot.SetCard(c1); }

                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        CardInstance cardInst = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (cardInst != null && cardInst.isAttached)
                        {
                            if (cardInst.hostSlotID == firstSlot.slotID) cardInst.hostSlotID = secondSlot.slotID;
                            else if (cardInst.hostSlotID == secondSlot.slotID) cardInst.hostSlotID = firstSlot.slotID;
                        }
                    }
                BoardManager.SyncAttachedModels(firstSlot);
                BoardManager.SyncAttachedModels(secondSlot);
                firstSlot = null;
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        ConfirmSelectionButton.Instance.Hide();
        ci.hasFirstStrike = false;
    }
  
    CardInstance FindLordOnField()
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D != null)
            {
                CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01503" && !ci.isAttached)
                    return ci;
            }
        }
        return null;
    }

    void UpdateLordDisplay(CardInstance lord)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == lord)
            {
                s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                return;
            }
        }
    }
    bool IsAllyUnit(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = 6; i <= 11; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return true;
        }
        return false;
    }
    IEnumerator ExecutionSwordDamage(CardInstance sword, int damage, BoardSlot swordSlot)
    {
        bool done = false;
        CardInstance targetCI = null;

        SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
        {
            if (targetSlot?.currentCard3D != null)
            {
                targetCI = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            }
            done = true;
        });

        yield return new WaitUntil(() => done);

        if (targetCI != null)
        {
            BattleManager.Instance.ApplyDamageToMinionPublic(targetCI, damage, swordSlot.currentCard3D);
            BoardSlot.CheckAndHandleDeaths();

            if (targetCI.currentHealth <= 0)
            {
                NetworkPlayer.Remote.TakeDamage(2);
            }
        }
    }
    IEnumerator MechRearrangementEffect()
    {
        BoardSlot.isStrengtheningSlot = true;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            if (slot?.currentCard3D == null) return false;
            CardInstance c = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            return c != null && c.prefixes.Contains("机械");
        };
        SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        ConfirmSelectionButton.Instance.Show(() => confirmed = true);

        BoardSlot.onTargetSelected = (selected) =>
        {
            if (firstSlot == null) { firstSlot = selected; }
            else if (selected != firstSlot)
            {
                BoardSlot secondSlot = selected;
                GameObject c1 = firstSlot.currentCard3D;
                GameObject c2 = secondSlot.currentCard3D;
                Vector3 p1 = FindObjectOfType<HandManager>().GetSlotWorldPosition(firstSlot.slotID);
                Vector3 p2 = FindObjectOfType<HandManager>().GetSlotWorldPosition(secondSlot.slotID);
                firstSlot.SetCard(null); secondSlot.SetCard(null);
                if (c2 != null) { c2.transform.position = p1; firstSlot.SetCard(c2); }
                if (c1 != null) { c1.transform.position = p2; secondSlot.SetCard(c1); }
                BoardManager bm = FindObjectOfType<BoardManager>();
                if (bm != null)
                    foreach (GameObject obj in bm.attachedModels)
                    {
                        CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                        if (ci != null && ci.isAttached)
                        {
                            if (ci.hostSlotID == firstSlot.slotID) ci.hostSlotID = secondSlot.slotID;
                            else if (ci.hostSlotID == secondSlot.slotID) ci.hostSlotID = firstSlot.slotID;
                        }
                    }
                BoardManager.SyncAttachedModels(firstSlot);
                BoardManager.SyncAttachedModels(secondSlot);
                firstSlot = null;
            }
        };

        yield return new WaitUntil(() => confirmed);
        SelectionManager.Instance.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.extraTargetFilter = null;
        ConfirmSelectionButton.Instance.Hide();
    }
    BoardSlot FindSlotOf(CardInstance ci)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return null;
        for (int i = 0; i < 12; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
                return s;
        }
        return null;
    }
    void CheckConquerorTrigger()
    {
        for (int i = 6; i <= 11; i++)
        {
            BoardSlot s = allSlots[i];
            if (s?.currentCard3D == null) continue;
            CardInstance conquerorCI = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (conquerorCI == null || conquerorCI.templateID != "01508") continue;
            if (conquerorCI._conquerorTriggered) continue;
            if (!conquerorCI._conquerorPendingCheck) continue;
            if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(conquerorCI)) continue;

            GameObject targetCard = conquerorCI._conquerorTargetEnemyCard;
            if (targetCard == null) continue;
            CardInstance targetInst = targetCard.GetComponent<Card3DInstance>()?.cardInstance;
            if (targetInst == null || targetInst.currentHealth > 0) continue;
            if (!targetInst.damageSourceInstanceIDs.Contains(conquerorCI.instanceID)) continue;

            conquerorCI._conquerorTriggered = true;
            if (conquerorCI._conquerorTotalDamageThisBattle > 1)
            {
                int excessDamage = conquerorCI._conquerorTotalDamageThisBattle - 1;
                conquerorCI.currentHealth += excessDamage;
            }
            s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            NetworkPlayer.Local.Heal(1);
            conquerorCI._conquerorPendingCheck = false;
        }
    }
    bool IsShadowHost(CardInstance ci)
    {
        if (ci == null) return false;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;

        int hostSlotID = -1;
        for (int i = 0; i < 12; i++)
        {
            if (bm.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci)
            { hostSlotID = i; break; }
        }
        if (hostSlotID < 0) return false;

        foreach (GameObject obj in bm.attachedModels)
        {
            Card3DInstance c3d = obj?.GetComponent<Card3DInstance>();
            if (c3d?.cardInstance?.templateID == "01327"
                && c3d.cardInstance.hostSlotID == hostSlotID
                && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(c3d.cardInstance)))
                return true;
        }
        return false;
    }
}