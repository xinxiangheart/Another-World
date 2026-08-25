public enum CardType { Summon, Spell }
public enum SummonType { Hero, ChosenOne, Special }

[System.Flags]
public enum SpellType
{
    Normal = 0,
    Evil = 1,
    Counter = 2
}

public enum TargetType
{
    None,
    SingleEnemy,
    SingleAlly,
    EnemyFrontRow,
    EnemyBackRow,
    AllyFrontRow,
    AllyBackRow,
    EnemyAnyRow,    // 敌方任意一排
    AllyAnyRow,     // 己方任意一排
    AllEnemies,
    AllAllies,
    AllMinions,
    SingleAny       // 任意目标：敌方或己方的单个召唤物（追加在末尾，保持既有序列化值不变）
}

public enum CounterTriggerTiming
{
    OnCardPlayed,
    OnPhaseStart,
    OnPhaseEnd,
    OnBattleEnd,
    OnEnemyTurnEnd,
    OnPlayerDying  // 新增：玩家生命值<=0时
}