using UnityEngine;
using Mirror;
using TMPro;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer LocalPlayer { get; private set; }

    [Header("基础属性")]
    public int maxHealth = 20;
    public int maxEnergy = 15;
    public int maxHandSize = 20;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar(hook = nameof(OnEnergyChanged))]
    public int currentEnergy;

    [SyncVar]
    public int currentHandCount;

    [Header("手牌")]
    public Transform handArea;
    public HandManager handManager;
    public GameObject cardPrefab2D;
    public GameObject spellCardPrefab2D;
    public SyncList<CardSyncData> syncedHandCards = new SyncList<CardSyncData>();

    [Header("UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI energyText;

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;
        currentHealth = maxHealth;
        currentEnergy = 0;

        // 只启用本地玩家的交互
        EnableLocalInteraction(true);
    }
    // 快捷访问：本地玩家（等同于原来的 Player.Instance）
    public static NetworkPlayer Local { get; private set; }

    // 快捷访问：远程玩家（等同于原来的 EnemyPlayer.Instance）
    public static NetworkPlayer Remote { get; private set; }

    
    // 在 NetworkManager 中，当另一个玩家连接时设置 Remote
    public static void SetRemote(NetworkPlayer remotePlayer)
    {
        Remote = remotePlayer;
    }
    public override void OnStartClient()
    {
        UpdateUI();
    }

    void EnableLocalInteraction(bool enabled)
    {
        // 手牌拖拽、按钮等由本地控制
        if (handManager != null)
            handManager.SetHandAreaRaycast(enabled);
    }

    void OnHealthChanged(int oldValue, int newValue)
    {
        UpdateUI();
        if (newValue <= 0)
            Debug.Log("玩家死亡");
    }

    void OnEnergyChanged(int oldValue, int newValue)
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (healthText != null)
            healthText.text = isLocalPlayer ? $" {currentHealth}" : currentHealth.ToString();
        if (energyText != null)
            energyText.text = isLocalPlayer ? $" {currentEnergy}/{maxEnergy}" : $"{currentEnergy}/{maxEnergy}";
    }

    [Command]
    public void CmdTakeDamage(int amount)
    {
        currentHealth -= amount;
    }

    [Command]
    public void CmdHeal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    [Command]
    public void CmdAddEnergy(int amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
    }

    [Command]
    public void CmdUseEnergy(int amount)
    {
        currentEnergy -= amount;
    }

    [Command]
    public void CmdDrawCard(string templateID, int copyIndex)
    {
        // 服务器验证抽牌合法性
        currentHandCount++;
        RpcOnCardDrawn(templateID, copyIndex);
    }

    [ClientRpc]
    void RpcOnCardDrawn(string templateID, int copyIndex)
    {
        if (isLocalPlayer) return;
        // 远程玩家看到对方抽牌动画（不暴露手牌内容）
    }

    [Command]
    public void CmdPlayCard(string cardInstanceID, int fromSlotID, int targetSlotID)
    {
        // 服务器验证并广播
        RpcOnCardPlayed(cardInstanceID, fromSlotID, targetSlotID);
    }

    [ClientRpc]
    void RpcOnCardPlayed(string cardInstanceID, int fromSlotID, int targetSlotID)
    {
        // 客户端执行卡牌效果
    }

    [Command]
    public void CmdEndTurn()
    {
        TurnManager.Instance.ServerEndTurn(this);
    }
}

// 手牌同步数据结构
[System.Serializable]
public struct CardSyncData
{
    public string templateID;
    public string instanceID;
}