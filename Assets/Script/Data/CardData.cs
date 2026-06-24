using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    public string templateID;
    public string cardName;
    public CardType cardType;
    public int copyCount = 3;

    [Header("召唤物属性")]
    public bool addToMainDeck = true; // 是否加入主牌库
    public SummonType summonType;
    public int baseCost;
    public int baseTier = 1;
    public string prefix = "无";
    public int baseHealth;
    public int baseAttack;
    [Header("特性标记")]
    public bool hasFirstStrike;      // 先手
    public bool hasOnEnter;          // 进场
    public bool hasOnDeath;          // 退场
    public bool hasActiveExit;       // 主动退场
    public bool hasRevenge;          // 反击
    public bool hasDiscard;          // 抛置
    public bool canAttach; // 是否拥有附着特性
    public CounterTriggerTiming counterTiming;
    public string counterTriggerCondition;
    public string counterEffect;
    public int counterDuration;
    public bool isXValue;
    public bool xHealthReadsHighest;
    public bool xAttackReadsHighest;
    public bool attacksFrontRow;
    public bool attacksBackRow;
    [TextArea] public string revengeEffect; // 反击效果文本
    [TextArea] public string traits;

    [Header("法术属性")]
    public SpellType spellType;
    [TextArea] public string effect;

    [Header("表现层")]
    public Sprite cardSprite2D;
    public GameObject prefab3D;
    public GameObject spellPrefab3D;
    
    [Header("目标选择")]
    public TargetType targetType = TargetType.None;
}