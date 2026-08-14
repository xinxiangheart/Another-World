/// <summary>
/// 音效类型枚举——所有音效通过 AudioManager.Play(SoundEffectType) 统一播放。
/// 新增音效：先在下面加一项，再到 AudioManager 的 Inspector 映射里拖入对应 AudioClip。
/// </summary>
public enum SoundEffectType
{
    DrawCard,          // 抽牌
    PlayCard,          // 打出卡牌
    Attack,            // 攻击（打随从）
    AttackHero,        // 攻击英雄（打空位）
    Death,             // 死亡
    TurnStart,         // 回合开始
    TurnEnd,           // 回合结束
    ButtonClick,       // UI按钮
    Victory,           // 胜利
    Defeat,            // 失败
    // 后续按需扩展
}
