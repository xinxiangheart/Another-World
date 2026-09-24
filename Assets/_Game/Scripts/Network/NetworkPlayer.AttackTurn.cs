using Mirror;

/// <summary>
/// 攻击回合伤害面板的网络镜像 —— 服务端（含离线 AI）在 BattleManager 里直接驱动面板，
/// 纯客户端拿不到 pendingDamageToMe/Enemy（那对字段只在服务端算），所以这里把
/// 「最终结算 + 结算那一刻的两个累计值」一并推过去，由客户端播同一段演出。
///
/// 不做「开场」RPC：面板的显隐在两端都由当前阶段（BattlePhase）自己开关。
/// 飞行中的伤害数字也不需要这条通道 —— 客户端本来就会收到 TargetPlayAttack，
/// 由 BattleManager.PlayAttackLocally 直接调同一套 AttackTurnDamagePanel.ShowIncoming。
///
/// 侧别一律按**接收方视角**给：0 = 上方（对方），1 = 下方（己方）。
/// </summary>
public partial class NetworkPlayer
{
    [TargetRpc]
    public void TargetAttackTurnSettle(NetworkConnectionToClient target,
        int finalDamage, int loserSide, int topValue, int bottomValue)
    {
        AttackTurnDamagePanel.PlaySettledForClient(finalDamage, loserSide, topValue, bottomValue);
    }
}