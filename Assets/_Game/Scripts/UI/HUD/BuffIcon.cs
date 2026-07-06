using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [PLACEHOLDER] Small icon+number sprite displayed next to a board minion.
/// Shows buffs like shield, poison, attack boost, silenced, etc.
/// </summary>
public class BuffIcon : MonoBehaviour
{
    public string buffID;
    public int amount;
    public Image icon;
    public Text amountText;

    public void Setup(string id, int amt, Sprite sprite) { }
    public void UpdateAmount(int newAmt) { }
}
