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
        // StartNewPhase 移至 SafeBattle——确保 BoardSyncManager.MarkDirty()+SyncNow
        // 先于 BroadcastTurnPhase 执行，防止远端在收到恢复后的板面前就上报旧 currentAttack
    }
    IEnumerator PhaseStartCoroutine()
    {
        if (allSlots == null) yield break;
        foreach (BoardSlot slot in allSlots)
        {
            if (slot?.currentCard3D == null) continue;
            var c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            CardInstance ci = c3d?.cardInstance;
            if (ci != null && ci.hasShield && ci.shieldEndAtBattleStart)
            {
                ci.RemoveShield();
                c3d?.UpdateValues();
            }
        }

        // 麻烦制造者(01308)：赋予对方召唤物先手扣血。遍历全部12槽，两边都能触发。
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01308")
            {
                if (!ci.CanTriggerTrait("战斗回合开始")) continue;
                yield return StartCoroutine(TroubleMakerEffect(ci, i));
                break;
            }
        }
        // 处刑剑(01535)：消耗法术费用造成伤害。遍历全部12槽。
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01535" && ci.consumedSpellCost > 0)
            {
                if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                    continue;

                if (BoardManager.HasEnemyMinion(i))
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

            var c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
            CardInstance ci = c3d?.cardInstance;
            if (ci != null)
            {
                ci.currentHealth -= slot.plagueRoundCount;
                ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                ci.baseAttack = Mathf.Max(0, ci.baseAttack - 1);
                DamagePipeline.ShowFloaterAt(ci, slot.plagueRoundCount, FloaterType.Debuff);
                DamagePipeline.ShowFloaterAt(ci, 1, FloaterType.Debuff);
                c3d?.UpdateValues();
            }
            slot.plagueRoundCount++;
        }
        BoardSlot.CheckAndHandleDeaths();
        yield return StartCoroutine(WaitForSimultaneousWindow());
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
            if (ci == null || !ci.HasFirstStrike) continue;
         
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
                // 刷新换位卡牌放置时间（替代全局12槽全量刷新，避免
                // _placedAtTime 过新导致后续死亡同步时 EnsureEmpty 被 0.5s 守卫误保护）
                var mySwapInst = mySlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (mySwapInst != null) mySwapInst._placedAtTime = Time.time;
                var targetSwapInst = targetSlot.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance;
                if (targetSwapInst != null) targetSwapInst._placedAtTime = Time.time;
            }
        // 检查对方是否有合法目标
            if (ci.templateID == "01312")
            {
                // 先手交互选择只能在拥有者客户端上展示；对方半场(0-5)暂时跳过
                if (i < 6) continue;

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
                    int slotA = mySlot;
                    int slotB = targetSlot.slotID;
                    BoardManager.SwapCards(slotA, slotB);
                    ci.hasFirstStrike = false;

                    // 通知远端客户端同步交换结果（服务端 6-11 → 远端视角 0-5）
                    if (Mirror.NetworkServer.active && NetworkPlayer.Remote != null)
                        NetworkPlayer.Remote.TargetSwapCards(NetworkPlayer.Remote.connectionToClient, slotA - 6, slotB - 6);
                }

                continue;
            }
            if (ci.templateID == "01513")
            {
                if (i < 6) continue; // 交互先手：UI 只能展示给卡牌拥有者
                yield return StartCoroutine(MechRearrangementEffect());
                continue;
            }
            if (ci.templateID == "01516")
            {
                if (i < 6) continue;
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
            if (ci == null || !ci.HasFirstStrike) continue;

        // 检查对方是否有合法目标
            if (ci.templateID == "03012")
            {
                if (i < 6) continue; // 交互先手：UI 只能展示给卡牌拥有者
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
                int fsSideStart = (i >= 6) ? 6 : 0;
                int fsSideEnd = fsSideStart + 5;
                for (int j = fsSideStart; j <= fsSideEnd; j++) if (allSlots[j]?.currentCard3D != null && allSlots[j] != slot) { hasAlly = true; break; }
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
                int enemyStart = i >= 6 ? 0 : 6;
                for (int j = enemyStart; j < enemyStart + 6; j++)
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
                if (i < 6) continue; // 交互先手：UI 只能展示给卡牌拥有者
                // 判断 GK 所在半场，只给自己的友方上盾
                bool isOnHostSide = (i >= 6);
                int sideStart = isOnHostSide ? 6 : 0;
                int sideEnd   = isOnHostSide ? 11 : 5;

                List<BoardSlot> candidates = new List<BoardSlot>();
                for (int j = sideStart; j <= sideEnd; j++)
                {
                    BoardSlot s = allSlots[j];
                    if (s?.currentCard3D != null)
                    {
                        // 排除 GK 自身
                        if (j == i) continue;
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
                    DamagePipeline.ShowFloaterAt(ci, 2, FloaterType.Debuff);
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
            if (ci == null || !ci.HasFirstStrike) continue;

            // 毒巫：清除护盾+中毒
            if (ci.templateID == "03502")
            {
                if (i < 6) continue; // 交互先手：UI 只能展示给卡牌拥有者
                int myStart = i >= 6 ? 0 : 6;
                bool hasEnemy = false;
                for (int j = myStart; j < myStart + 6; j++) if (allSlots[j]?.currentCard3D != null) { hasEnemy = true; break; }
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
                            {
                                if (targetSlot.slotID >= 6)
                                    NetworkPlayer.Local.currentEnergy -= 1;
                                else
                                    NetworkPlayer.Remote.currentEnergy -= 1;
                                NetworkPlayer.Local?.UpdateUI();
                                NetworkPlayer.Remote?.UpdateUI();
                            }
                        }
                    }
                    poisonDone = true;
                });
                while (!poisonDone) yield return null;
            }
        // 万象镜面：单次伤害最高为1
            if (ci.templateID == "01318")
            {
                if (i < 6) continue; // 交互先手：UI 只能展示给卡牌拥有者
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
                if (i >= 6)
                    NetworkPlayer.Remote.AddEnergy(1);
                else
                    NetworkPlayer.Local.AddEnergy(1);
            }
        }

        // ===== 阶段2.4：伤害处理 =====
        for (int i = 0; i < 12; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci == null || !ci.HasFirstStrike) continue;

        // 检查对方是否有合法目标
            if (ci.templateID == "03506")
            {
                int offset = i >= 6 ? 0 : 6;
                int[] targets = { 2 + offset, 0 + offset, 4 + offset };
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
                int offset = i >= 6 ? 0 : 6;
                int[] targets = { 1 + offset, 5 + offset, 3 + offset };
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
                int myStart = i >= 6 ? 0 : 6;
                for (int j = myStart; j < myStart + 6; j++)
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
                int offset = i >= 6 ? 0 : 6;
                int[] frontRow = { offset, 1 + offset, 2 + offset };
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
                int offset = i >= 6 ? 0 : 6;
                int[] backRow = { 3 + offset, 4 + offset, 5 + offset };
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
                if (i >= 6)
                    NetworkPlayer.Remote.TakeDamage(1);
                else
                    NetworkPlayer.Local.TakeDamage(1);
            }
            // 麻烦制造者赋予的先手：扣对方玩家1生命值
            if (ci.grantedTraitTexts.Exists(t => t.Contains("先手：对对方前排召唤物造成1伤害")))
            {
                int myStart = i >= 6 ? 0 : 6;
                int[] frontRow = { myStart, myStart + 1, myStart + 2 };
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
                int myStart = i >= 6 ? 0 : 6;
                for (int j = myStart; j < myStart + 6; j++)
                {
                    BoardSlot targetSlot = allSlots[j];
                    if (targetSlot?.currentCard3D != null)
                    {
                        Card3DInstance ti = targetSlot.currentCard3D.GetComponent<Card3DInstance>();
                        if (ti?.cardInstance != null)
                        {
                            int dmg = (j - myStart) < 3 ? 2 : 1;
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

        // ── 先手同时窗口结束 → 等待远端先手完成 → 处理死亡 → 反击 ──
        // 触发远端客户端运行己方的交互式先手（Remote 的 6-11 = 服务器的 0-5）
        if (Mirror.NetworkServer.active && NetworkPlayer.Remote != null)
        {
            BoardSlot._remoteFirstStrikeDone = false;
            NetworkPlayer.Remote.TargetRunRemoteFirstStrikes(NetworkPlayer.Remote.connectionToClient);
            yield return new WaitWhile(() => !BoardSlot._remoteFirstStrikeDone);
            // 广播完整板面给远端客户端，确保交换后的正确状态同步出去
            BoardSyncManager.MarkDirty();
            yield return null; // 让 LateUpdate 中的 SyncNow 执行
        }
        BoardSlot.CheckAndHandleDeaths();
        yield return StartCoroutine(WaitForSimultaneousWindow());
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

        // 01345 操控者: 交换对方前后排同列两个随从。检查全部12槽。
        for (int i = 0; i < 12; i++)
        {
            BoardSlot mySlot = allSlots[i];
            if (mySlot?.currentCard3D == null) continue;
            CardInstance myInst = mySlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (myInst == null) continue;
            if (myInst.templateID != "01345") continue;
            if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(myInst)) continue;

            int col = i % 3;
            BoardManager.GetEnemySideRange(i, out int enemyHalfStart, out int _);
            // Row within the card's own half: 0=front, 3=back
            int ownHalfStart = i >= 6 ? 6 : 0;
            int ownRow = (i - ownHalfStart) < 3 ? 0 : 3;
            // Opposite row in enemy half
            int enemyRowOffset = ownRow == 0 ? 3 : 0;
            int enemySlotIndex = enemyHalfStart + enemyRowOffset + col;
            // The other enemy row
            int otherEnemyRow = enemyRowOffset == 0 ? 3 : 0;
            int targetEnemySlotIndex = enemyHalfStart + otherEnemyRow + col;

            BoardSlot enemySlot = allSlots[enemySlotIndex];
            BoardSlot targetEnemySlot = allSlots[targetEnemySlotIndex];
            if (targetEnemySlot.isBlocked || targetEnemySlot.prisonBlocked) continue;

            BoardManager.SwapCards(enemySlotIndex, targetEnemySlotIndex);
        }
        BoardSlot.CheckAndHandleDeaths();
        yield return StartCoroutine(WaitForSimultaneousWindow());
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

        // ── 攻击方A（6-11, 主机侧）攻击防守方B（0-5）──
        ProcessAttackerVsDefender(
            attackerCard: myCard, attackerInst: myInst, attackerSlot: mySlot,
            attackerSlotID: mySlotIndex + 6,
            defenderSlotID: enemySlotIndex,
            defenderHalfStart: 0, defenderHalfEnd: 5,
            damageList: damageList,
            pendingDamageToOpponent: ref pendingDamageToEnemy,
            attackerOwner: NetworkPlayer.Local,
            defenderOwner: NetworkPlayer.Remote
        );

        // ── 攻击方B（0-5, 客户端侧）攻击防守方A（6-11）──
        ProcessAttackerVsDefender(
            attackerCard: enemyCard, attackerInst: enemyInst, attackerSlot: enemySlot,
            attackerSlotID: enemySlotIndex,
            defenderSlotID: mySlotIndex + 6,
            defenderHalfStart: 6, defenderHalfEnd: 11,
            damageList: damageList,
            pendingDamageToOpponent: ref pendingDamageToMe,
            attackerOwner: NetworkPlayer.Remote,
            defenderOwner: NetworkPlayer.Local
        );
    }

    void ProcessAttackerVsDefender(
        GameObject attackerCard, CardInstance attackerInst, BoardSlot attackerSlot,
        int attackerSlotID, int defenderSlotID,
        int defenderHalfStart, int defenderHalfEnd,
        List<(GameObject, int, GameObject)> damageList,
        ref int pendingDamageToOpponent,
        NetworkPlayer attackerOwner, NetworkPlayer defenderOwner)
    {
        if (attackerCard == null || attackerInst == null || attackerInst.silencedThisPhase)
            return;

        bool attackerSilenced = GlobalEventManager.Instance != null
            && GlobalEventManager.Instance.IsFullySilenced(attackerInst);

        int col = attackerSlotID % 3;
        int targetDefenderSlotIndex;

        // ── 特殊目标选择 ──
        if (attackerInst.templateID == "01305")
        {
            // 喷溅：攻击同排另外两列
            targetDefenderSlotIndex = defenderSlotID;
            int rowStart = (defenderSlotID - defenderHalfStart) < 3 ? defenderHalfStart : defenderHalfStart + 3;
            for (int i = rowStart; i < rowStart + 3; i++)
            {
                if (i == defenderSlotID) continue;
                BoardSlot otherSlot = allSlots[i];
                GameObject otherCard = otherSlot?.currentCard3D;
                CardInstance otherInst = otherCard?.GetComponent<Card3DInstance>()?.cardInstance;
                bool otherSilenced = otherInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(otherInst);
                if (otherCard != null && otherInst != null && !otherInst.silencedThisPhase && !otherSilenced)
                {
                    damageList.Add((otherCard, 2, attackerCard));
                }
            }
        }
        else if (attackerInst.templateID == "01530")
        {
            // 恐惧之龙：攻击同排另外两列
            targetDefenderSlotIndex = defenderSlotID;
            int rowStart = (defenderSlotID - defenderHalfStart) < 3 ? defenderHalfStart : defenderHalfStart + 3;
            for (int i = rowStart; i < rowStart + 3; i++)
            {
                if (i == defenderSlotID) continue;
                BoardSlot otherSlot = allSlots[i];
                GameObject otherCard = otherSlot?.currentCard3D;
                CardInstance otherInst = otherCard?.GetComponent<Card3DInstance>()?.cardInstance;
                bool otherSilenced = otherInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(otherInst);
                if (otherCard != null && otherInst != null && !otherInst.silencedThisPhase && !otherSilenced)
                {
                    damageList.Add((otherCard, attackerInst.Attack, attackerCard));
                }
            }
        }
        else if (attackerInst.templateID == "01336" && !attackerInst.isAttached)
        {
            // 碎阵先锋：攻击前排对位 + 同排另外两列 1 伤害
            targetDefenderSlotIndex = defenderHalfStart + col;
            int rowStart2 = (targetDefenderSlotIndex - defenderHalfStart) < 3 ? defenderHalfStart : defenderHalfStart + 3;
            for (int j = rowStart2; j < rowStart2 + 3; j++)
            {
                if (j == targetDefenderSlotIndex) continue;
                BoardSlot otherSlot = allSlots[j];
                GameObject otherCard = otherSlot?.currentCard3D;
                if (otherCard != null)
                {
                    damageList.Add((otherCard, 1, attackerCard));
                }
            }
        }
        else if (attackerInst.attacksBackRow)
        {
            targetDefenderSlotIndex = defenderHalfStart + 3 + col;
        }
        else if (attackerInst.attacksFrontRow)
        {
            targetDefenderSlotIndex = defenderHalfStart + col;
        }
        else
        {
            targetDefenderSlotIndex = defenderSlotID;
        }

        BoardSlot targetSlot = allSlots[targetDefenderSlotIndex];
        GameObject targetCard = targetSlot?.currentCard3D;
        CardInstance targetInst = targetCard?.GetComponent<Card3DInstance>()?.cardInstance;
        bool targetSilenced = targetInst != null && GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(targetInst);

        if (targetCard != null && targetInst != null && !targetInst.silencedThisPhase)
        {
            int atk = attackerInst.Attack;
            if (!attackerInst.isXValue)
            {
                atk += attackerSlot.slotTempAttackBoost;
            }
            // 暴徒(01114)：攻击护盾目标额外扣2HP
            if (!attackerSilenced && attackerInst.templateID == "01114" && targetInst.hasShield)
            {
                targetInst.currentHealth -= 2;
                targetInst.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            // 破防者(01328)光环：护盾目标额外扣2HP
            if (targetInst.hasShield && HasBreakerOnSide(attackerSlotID))
            {
                targetInst.currentHealth -= 2;
                targetInst.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            // 01118/01125 攻击修正已在 DamagePipeline.Stage1_Give 统一处理，此处不重复
            damageList.Add((targetCard, atk, attackerCard));
            // 01327 阴影聚合体：宿主攻击时自伤宿主自己的 HP
            if (IsShadowHost(attackerInst) && targetInst != null)
            {
                attackerInst.currentHealth -= atk;
                if (attackerInst.currentHealth < 0) attackerInst.currentHealth = 0;
                attackerSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            // 征服者(01508)：标记目标
            if (!attackerSilenced && attackerInst.templateID == "01508" && !attackerInst._conquerorTriggered)
            {
                attackerInst._conquerorPendingCheck = true;
                attackerInst._conquerorTargetEnemyCard = targetCard;
            }
        }

        // ── 亡命之徒(01531) 攻击时扣对方玩家2血 ──
        if (!attackerSilenced && attackerInst.templateID == "01531" && targetInst != null
            && !attackerInst._outlawPlayerDamageThisTurn)
        {
            defenderOwner?.TakeDamage(2);
            attackerInst._outlawPlayerDamageThisTurn = true;
        }

        // ── 攻击空位 → 对玩家伤害 ──
        if (targetCard == null)
        {
            if (targetSlot.prisonBlocked || targetSlot.isBlocked)
            {
                // blocked slot, no damage
            }
            else if (IsShadowHost(attackerInst))
            {
                int myTier = attackerInst.currentTier;
                if (HasSuppressorOnField(attackerSlotID) && attackerInst.summonType == SummonType.Hero)
                    myTier += 1;
                pendingDamageToOpponent += myTier;
                // 01327：空位攻击也自伤宿主自己的 HP
                attackerInst.currentHealth -= attackerInst.currentAttack;
                if (attackerInst.currentHealth < 0) attackerInst.currentHealth = 0;
                attackerSlot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
            else if (!attackerSilenced && attackerInst.templateID == "01531")
            {
                pendingDamageToOpponent += attackerInst.currentTier + 2;
            }
            else if (attackerInst.templateID == "03014")
            {
                // 死光：对方全场 2 伤害
                for (int j = defenderHalfStart; j <= defenderHalfEnd; j++)
                {
                    BoardSlot es = FindObjectOfType<BoardManager>()?.GetSlot(j);
                    if (es?.currentCard3D != null && !es.prisonBlocked && !es.isBlocked)
                    {
                        Card3DInstance e3d = es.currentCard3D.GetComponent<Card3DInstance>();
                        if (e3d?.cardInstance != null)
                        {
                            ApplyDamageToMinionPublic(e3d.cardInstance, 2, attackerCard);
                            e3d.UpdateValues();
                        }
                    }
                }
                BoardSlot.CheckAndHandleDeaths();
            }
            else
            {
                int myTier = attackerInst.currentTier;
                if (HasSuppressorOnField(attackerSlotID) && attackerInst.summonType == SummonType.Hero)
                    myTier += 1;
                pendingDamageToOpponent += myTier;
            }
        }
    }

    bool HasBreakerOnSide(int slotID)
    {
        BoardManager.GetSideRange(slotID, out int brS, out int brE);
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return false;
        for (int i = brS; i <= brE; i++)
        {
            BoardSlot s = bm.GetSlot(i);
            if (s?.currentCard3D == null) continue;
            CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "01328"
                && (GlobalEventManager.Instance == null || !GlobalEventManager.Instance.IsFullySilenced(ci)))
                return true;
        }
        return false;
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
                CardInstance sourceCI = dmg.Item3?.GetComponent<Card3DInstance>()?.cardInstance;
                if (inst?.cardInstance != null)
                {
                        DamagePipeline.Process(new DamageInput(sourceCI, inst.cardInstance, dmg.Item2, dmg.Item3, DamagePhase.Battle));
                }
            }
            pending.Clear();

            CheckConquerorTrigger();

            // ── Step 6: 反击/恐惧之龙等预死亡反应 ──
            List<GameObject> died = new List<GameObject>();
            foreach (BoardSlot slot in allSlots)
            {
                GameObject card = slot?.currentCard3D;
                if (card == null) continue;
                Card3DInstance inst = card.GetComponent<Card3DInstance>();
                if (inst?.cardInstance != null && inst.cardInstance.currentHealth <= 0)
                    died.Add(card);
            }

            // ── 同时窗口：处理死亡 → 退场 → 反击（反伤在新同时窗口执行）──
            if (died.Count > 0)
            {
                BoardSlot.CheckAndHandleDeaths();
                yield return StartCoroutine(WaitForSimultaneousWindow());
            }
        }
    }
    IEnumerator ResolveRevengeEffect(string effect, GameObject deadCard, List<GameObject> targets)
    {
        Debug.Log($"ResolveRevengeEffect: effect={effect}");

        // 通用：对击杀它的召唤物造成{X}伤害（含 1/2/3/999 等任意数值）
        int revengeDmg = ParseRevengeDamage(effect);
        if (revengeDmg > 0)
        {
            foreach (GameObject target in targets)
            {
                Card3DInstance tInst = target.GetComponent<Card3DInstance>();
                if (tInst != null)
                {
                    tInst.cardInstance.currentHealth -= revengeDmg;
                    tInst.UpdateValues();
                    target.GetComponent<DamageSourceMarker>()?.RegisterDamage(deadCard, revengeDmg);
                    // 浮动数字
                    DamagePipeline.ShowFloaterAt(tInst.cardInstance, revengeDmg, FloaterType.Damage);
                }
            }
        }
        // 通用：攻击力永久减{N}（如 01109 尖啸者）
        int permAtkDebuff = ParseRevengeAtkDebuff(effect);
        if (permAtkDebuff > 0)
        {
            foreach (GameObject target in targets)
            {
                Card3DInstance tInst = target.GetComponent<Card3DInstance>();
                if (tInst != null)
                {
                    tInst.cardInstance.baseAttack = Mathf.Max(0, tInst.cardInstance.baseAttack - permAtkDebuff);
                    tInst.cardInstance.currentAttack = Mathf.Max(0, tInst.cardInstance.currentAttack - permAtkDebuff);
                    tInst.UpdateValues();
                    Refresh2DDisplayOf(tInst.cardInstance);
                    DamagePipeline.ShowFloaterAt(tInst.cardInstance, permAtkDebuff, FloaterType.Debuff);
                }
            }
        }
        else if (effect.Contains("为己方一召唤物+2+1"))
        {
            int deadSlot = FindSlotOfGameObject(deadCard);
            BoardManager.GetSideRange(deadSlot, out int allyS, out int allyE);
            yield return StartCoroutine(WaitForSelection((onDone) =>
            {
                BoardManager bm = FindObjectOfType<BoardManager>();
                bool hasAlly = false;
                for (int j = allyS; j <= allyE; j++)
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
        else if (effect.Contains("选定一个格子，该格子上的召唤物临时+0-1（最少为0）并且每阶段开始扣一生命值"))
        {
            yield return StartCoroutine(WaitForSelection((onDone) =>
            {
                SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (targetSlot) =>
                {
                    if (targetSlot != null && !targetSlot.isBlocked)
                    {
                        ApplyDeepSeaDebuff(targetSlot);
                        targetSlot.deepSeaMarked = true;
                        targetSlot.SyncVisual();
                    }
                    onDone();
                });
                BoardSlot.isStrengtheningSlot = true;
            }));
            if (Mirror.NetworkServer.active) BoardSyncManager.MarkDirty();
        }
        else if (effect.Contains("+1能量"))
        {
            int deadSlot3 = FindSlotOfGameObject(deadCard);
            BoardManager.GetOwnerPlayer(deadSlot3)?.AddEnergy(1);
        }
        else
        {
            Debug.Log($"未实现的反击效果：{effect}");
        }
    }

    /// <summary>
    /// 反击窗口（新的同时窗口）— 在退场/模型销毁后执行。
    /// 读取 BoardSlot.pendingRevenges 快照，处理伤害型反伤和非伤害型反伤。
    /// </summary>
    public static IEnumerator ResolveRevengesFromSnapshot()
    {
        var bm = FindObjectOfType<BoardManager>();
        var bmInstance = BattleManager.Instance;
        int safety = 0;
        var batch = new List<(int deadSlotID, string effect, List<string> sourceIDs)>();
        while (BoardSlot.pendingRevenges.Count > 0 && safety++ < 20)
        {
            batch.Clear();
            batch.AddRange(BoardSlot.pendingRevenges);
            BoardSlot.pendingRevenges.Clear();

            foreach (var (deadSlotID, effect, sourceIDs) in batch)
            {
                // 对方摸两张牌——始终用 deadSlotID 的对手（与 sourceIDs 是否为空无关）
                if (effect.Contains("对方摸两张牌"))
                {
                    NetworkPlayer opponent = BoardManager.GetOpponentPlayer(deadSlotID);
                    for (int j = 0; j < 2; j++)
                    {
                        CardData data = DeckManager.Instance?.DrawFromMain();
                        if (data != null && opponent != null)
                        {
                            opponent.TargetReceiveCard(opponent.connectionToClient, data.templateID, data._instanceID ?? "");
                            opponent.AddServerSideCard(data, data._instanceID);
                        }
                    }
                    continue;
                }

                // ── 非伤害型反击（与 sourceIDs 无关——即使被随从打死的卡也可能有选择型反击）──
                // +1能量
                if (effect.Contains("+1能量"))
                {
                    BoardManager.GetOwnerPlayer(deadSlotID)?.AddEnergy(1);
                    continue;
                }
                // 选定一个格子 → debuff（01338 深海恶物反击）
                if (effect.Contains("选定一个格子"))
                {
                    NetworkPlayer revOwner = BoardManager.GetOwnerPlayer(deadSlotID);
                    if (revOwner == NetworkPlayer.Remote && Mirror.NetworkServer.active)
                    {
                        // 远端玩家的卡 → 委托远端选择目标
                        BoardSlot._deepSeaRevengeTargetSlot = -1;
                        BoardSlot._deepSeaRevengeWaiting = true;
                        NetworkPlayer.Remote.TargetDeepSeaRevengeSelect(
                            NetworkPlayer.Remote.connectionToClient, deadSlotID);
                        float t0 = Time.time;
                        while (BoardSlot._deepSeaRevengeWaiting && Time.time - t0 < 30f)
                            yield return null;
                        BoardSlot._deepSeaRevengeWaiting = false;
                        int chosen = BoardSlot._deepSeaRevengeTargetSlot;
                        if (chosen >= 0 && chosen < 12)
                        {
                            BoardSlot ts = bm.GetSlot(chosen);
                            if (ts != null) { ApplyDeepSeaDebuff(ts); ts.deepSeaMarked = true; ts.SyncVisual(); }
                        }
                    }
                    else
                    {
                        // 主机玩家 → 直接本地选择
                        yield return bmInstance.StartCoroutine(bmInstance.WaitForSelection((onDone) =>
                        {
                            SelectionManager.Instance.BeginSelection(TargetType.SingleEnemy, (ts) =>
                            {
                                if (ts != null && !ts.isBlocked)
                                { ApplyDeepSeaDebuff(ts); ts.deepSeaMarked = true; ts.SyncVisual(); }
                                onDone();
                            });
                            BoardSlot.isStrengtheningSlot = true;
                        }));
                    }
                    BoardSyncManager.MarkDirty();
                    continue;
                }
                // 为己方一召唤物+2+1（01527）
                if (effect.Contains("为己方一召唤物+2+1"))
                {
                    NetworkPlayer revOwner = BoardManager.GetOwnerPlayer(deadSlotID);
                    if (revOwner == NetworkPlayer.Remote && Mirror.NetworkServer.active)
                    {
                        // 远端玩家的卡 → 委托远端选择目标
                        BoardSlot._allyBuffRevengeTargetSlot = -1;
                        BoardSlot._allyBuffRevengeWaiting = true;
                        NetworkPlayer.Remote.TargetAllyBuffRevengeSelect(
                            NetworkPlayer.Remote.connectionToClient, deadSlotID);
                        float t0 = Time.time;
                        while (BoardSlot._allyBuffRevengeWaiting && Time.time - t0 < 30f)
                            yield return null;
                        BoardSlot._allyBuffRevengeWaiting = false;
                        int chosen = BoardSlot._allyBuffRevengeTargetSlot;
                        if (chosen >= 0 && chosen < 12)
                        {
                            BoardSlot ts = bm.GetSlot(chosen);
                            if (ts?.currentCard3D != null)
                            {
                                CardInstance ci = ts.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                if (ci != null)
                                {
                                    if (!ci.cannotHealOrGainMaxHP)
                                    { ci.currentHealth += 2; ci.currentMaxHealth += 2; }
                                    ci.currentAttack += 1;
                                    ts.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                                }
                            }
                        }
                    }
                    else
                    {
                        // 主机玩家 → 直接本地选择
                        BoardManager.GetSideRange(deadSlotID, out int aStart, out int aEnd);
                        yield return bmInstance.StartCoroutine(bmInstance.WaitForSelection((onDone) =>
                        {
                            BoardManager bm2 = FindObjectOfType<BoardManager>();
                            bool hasAlly = false;
                            for (int j = aStart; j <= aEnd; j++)
                                if (bm2?.GetSlot(j)?.currentCard3D != null) { hasAlly = true; break; }
                            if (hasAlly)
                            {
                                SelectionManager.Instance.BeginSelection(TargetType.SingleAlly, (targetSlot) =>
                                {
                                    if (targetSlot?.currentCard3D != null)
                                    {
                                        CardInstance ci = targetSlot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                                        if (ci != null)
                                        {
                                            if (!ci.cannotHealOrGainMaxHP)
                                            { ci.currentHealth += 2; ci.currentMaxHealth += 2; }
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
                    if (Mirror.NetworkServer.active) BoardSyncManager.MarkDirty();
                    continue;
                }

                // ── 伤害型反击（必须有来源目标）──
                if (sourceIDs == null || sourceIDs.Count == 0) continue;

                var targets = new List<GameObject>();
                for (int i = 0; i < 12; i++)
                {
                    var go = bm.GetSlot(i)?.currentCard3D;
                    if (go == null) continue;
                    var ci = go.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && sourceIDs.Contains(ci.instanceID))
                        targets.Add(go);
                }
                if (targets.Count == 0) continue;

                yield return bmInstance.StartCoroutine(
                    bmInstance.ResolveRevengeEffect(effect, null, targets));
            }

            // 反伤造成新死亡 → 递归
            BoardSlot.CheckAndHandleDeaths();
            yield return ActionQueueManager.WaitForDrain();
            // 广播反伤修改的属性（攻击力减益等）到客户端
            if (Mirror.NetworkServer.active)
                BoardSyncManager.MarkDirty();
        }
    }

    /// <summary>从反击文本中解析伤害数值："造成{X}伤害" → X</summary>
    static int ParseRevengeDamage(string effect)
    {
        if (string.IsNullOrEmpty(effect)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(effect, @"造成(\d+)伤害");
        if (m.Success && int.TryParse(m.Groups[1].Value, out int dmg))
            return dmg;
        return 0;
    }

    /// <summary>从反击文本中解析攻击力永久减值："攻击力永久减{N}" → N（默认为 1）</summary>
    static int ParseRevengeAtkDebuff(string effect)
    {
        if (string.IsNullOrEmpty(effect)) return 0;
        if (effect.Contains("攻击力永久减"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(effect, @"攻击力永久减(\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int d))
                return d;
            return 1; // "攻击力永久减一" 中的 "一" 不是数字 → 默认 1
        }
        return 0;
    }

    void CompareSurvivors()
    {
        int my = 0, enemy = 0;
        BoardManager bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 6; i++)
        {
            if (allSlots[i + 6]?.currentCard3D != null && !allSlots[i + 6].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance?.isAttached == true)
                my++;
            if (allSlots[i]?.currentCard3D != null && !allSlots[i].currentCard3D.GetComponent<Card3DInstance>()?.cardInstance?.isAttached == true)
                enemy++;
        }

        // 超数故障(01128)：所在半场存活人数+1。检查两个半场。
        if (bm != null)
        {
            for (int half = 0; half < 2; half++)
            {
                int start = half * 6, end = start + 5;
                bool isHostHalf = (half == 1);
                for (int i = start; i <= end; i++)
                {
                    BoardSlot slot = bm.GetSlot(i);
                    if (slot?.currentCard3D == null) continue;
                    CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.templateID == "01128")
                    {
                        if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                            continue;
                        if (isHostHalf) my++; else enemy++;
                    }
                }
            }
            bm.attachedModels.RemoveAll(a => a == null || a.transform == null);
            foreach (GameObject obj in bm.attachedModels)
            {
                CardInstance ci = obj?.GetComponent<Card3DInstance>()?.cardInstance;
                if (ci != null && ci.templateID == "01128")
                {
                    if (GlobalEventManager.Instance != null && GlobalEventManager.Instance.IsFullySilenced(ci))
                        continue;
                    if (ci.hostSlotID >= 6) my++; else enemy++;
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
            // 兜底恢复 originalAttackBeforeDebuff（弱化棱晶等，远程先手路径可能未在
            // MinionAttacksCoroutine 中恢复——服务器端未追踪该字段）
            if (ci.originalAttackBeforeDebuff > 0)
            {
                ci.currentAttack = ci.originalAttackBeforeDebuff;
                ci.originalAttackBeforeDebuff = 0;
            }
            c3d?.UpdateValues();
        }
        // 影之终幕：任意半场有影子存活 → 全局加成生效，影子自身 +1阶 +2攻
        bool hasShadow = false;
        for (int i = 0; i < 12; i++)
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
            for (int i = 0; i < 12; i++)
            {
                BoardSlot s = allSlots[i];
                if (s?.currentCard3D != null)
                {
                    CardInstance ci = s.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
                    if (ci != null && ci.isShadow)
                    {
                        ci.currentTier += 1;
                        ci.baseTier += 1;
                        ci.currentAttack += 2;
                        ci.baseAttack += 2;
                        s.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
                    }
                }
            }
        }
    }
    /// <summary>检查指定槽位所在半场是否有激活的压制者(03501)。</summary>
    public bool HasSuppressorOnField(int mySlotID)
    {
        if (allSlots == null) return false;
        BoardManager.GetSideRange(mySlotID, out int start, out int end);
        for (int i = start; i <= end; i++)
        {
            BoardSlot slot = allSlots[i];
            if (slot?.currentCard3D == null) continue;
            CardInstance ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null && ci.templateID == "03501")
            {
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

        // ── Step D6: 统一走 DamagePipeline 五阶段 ───────────────────
        CardInstance sourceCI = source?.GetComponent<Card3DInstance>()?.cardInstance;
        DamagePipeline.Process(new DamageInput(
            attacker: sourceCI,
            defender: target,
            baseDamage: damage,
            sourceObject: source,
            phase: DamagePhase.Battle
        ));
        // 护盾吸收/领主重定向/追随者挡死/祭司复活 → DamagePipeline 内全处理。
        // 调用方后续读 target.currentHealth 即可判断生死。
    }
    IEnumerator TroubleMakerEffect(CardInstance giver, int mySlotID)
    {
        // Check enemy half for existing trouble-maker trait
        BoardManager.GetEnemySideRange(mySlotID, out int enemyStart, out int enemyEnd);
        bool alreadyHas = false;
        for (int i = enemyStart; i <= enemyEnd; i++)
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
            Debug.Log("麻烦制造者：对方场上已有此特性，跳过");
            yield break;
        }

        // Check enemy half has any minion
        bool hasEnemy = false;
        for (int i = enemyStart; i <= enemyEnd; i++)
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
            yield return ActionQueueManager.WaitForDrain();

            if (targetCI.currentHealth <= 0)
            {
                BoardManager.GetOpponentPlayer(swordSlot.slotID)?.TakeDamage(2);
            }
        }
    }
    IEnumerator MechRearrangementEffect()
    {
        var selMgr = SelectionManager.Instance;
        var confirmBtn = ConfirmSelectionButton.Instance;
        if (selMgr == null || confirmBtn == null)
        {
            Debug.LogWarning("[MechRearrangementEffect] SelectionManager or ConfirmSelectionButton not available");
            yield break;
        }

        BoardSlot.isStrengtheningSlot = true;
        BoardSlot.extraTargetFilter = (slot) =>
        {
            if (slot?.currentCard3D == null) return false;
            CardInstance c = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            return c != null && c.prefixes.Contains("机械");
        };
        selMgr.BeginSelection(TargetType.SingleAlly, null);

        BoardSlot firstSlot = null;
        bool confirmed = false;
        confirmBtn.Show(() => confirmed = true);

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
        selMgr.ForceEndAll();
        BoardSlot.isStrengtheningSlot = false;
        BoardSlot.extraTargetFilter = null;
        confirmBtn.Hide();
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

    int FindSlotOfGameObject(GameObject go)
    {
        if (go == null) return -1;
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return -1;
        for (int i = 0; i < 12; i++)
            if (bm.GetSlot(i)?.currentCard3D == go) return i;
        return -1;
    }
    void CheckConquerorTrigger()
    {
        // 征服者(01508) 可在任意半场
        for (int i = 0; i < 12; i++)
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
            BoardManager.GetOwnerPlayer(i)?.Heal(1);
            conquerorCI._conquerorPendingCheck = false;
        }
    }
    /// <summary>刷新与指定 CardInstance 同 instanceID 的 2D 手牌显示。</summary>
    static void Refresh2DDisplayOf(CardInstance ci)
    {
        if (ci == null) return;
        var player = BoardManager.GetOwnerPlayer(GetSlotOfComp(ci));
        if (player == null) return;
        // 清理已销毁的 GameObject 残留
        player.handCards.RemoveAll(c => c == null);
        foreach (GameObject card in player.handCards)
        {
            var inst = card?.GetComponent<CardInstance>();
            if (inst != null && inst.instanceID == ci.instanceID)
            {
                card.GetComponent<CardDisplay2D>()?.RefreshWithInstance(inst);
                break;
            }
        }
    }

    static int GetSlotOfComp(CardInstance ci)
    {
        var bm = FindObjectOfType<BoardManager>();
        for (int i = 0; i < 12; i++)
            if (bm?.GetSlot(i)?.currentCard3D?.GetComponent<Card3DInstance>()?.cardInstance == ci) return i;
        return -1;
    }

    /// <summary>深海恶物 debuff：格子攻击力-1 + 每阶段扣血 + 当前卡攻击力-1。</summary>
    static void ApplyDeepSeaDebuff(BoardSlot slot)
    {
        if (slot == null) return;
        bool alreadyDebuffed = slot.deepSeaAttackDebuff >= 1;
        slot.deepSeaAttackDebuff = 1;   // 不可叠加，始终 -1
        slot.deepSeaHealthDebuff = true;
        if (slot.currentCard3D != null && !alreadyDebuffed)
        {
            var ci = slot.currentCard3D.GetComponent<Card3DInstance>()?.cardInstance;
            if (ci != null)
            {
                ci.currentAttack = Mathf.Max(0, ci.currentAttack - 1);
                slot.currentCard3D.GetComponent<Card3DInstance>()?.UpdateValues();
            }
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

    /// <summary>
    /// 等待当前"同时窗口"完全处理完毕：
    /// ActionQueue 排空 → 嵌套上下文结束 → UI 选择完成 → 反击结算。
    /// 用于替换 BattleManager 中多处重复的 ad-hoc 等待链。
    /// </summary>
    public static IEnumerator WaitForSimultaneousWindow()
    {
        yield return ActionQueueManager.WaitForDrain();
        yield return new WaitWhile(() => NestingContext.IsNested);
        yield return new WaitWhile(() => BoardSlot.isPlacingCard);
        yield return new WaitWhile(() => SelectionManager.Instance.IsSelecting);
        var cqm = ConfirmQueueManager.Instance;
        if (cqm != null) yield return new WaitWhile(() => cqm.IsBusy());
        if (BoardSlot.pendingRevenges.Count > 0)
            yield return Instance.StartCoroutine(ResolveRevengesFromSnapshot());
    }
}