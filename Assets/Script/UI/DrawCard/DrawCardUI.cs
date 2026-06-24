using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DrawCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TextMeshProUGUI drawCountText;
    public Image buttonImage;

    public float hoverScale = 1.2f;
    public float smoothSpeed = 10f;

    private int remainingDraws = 5;
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        UpdateDisplay();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * smoothSpeed);
    }

    public void ResetForNewPhase()
    {
        remainingDraws = 5;
        UpdateDisplay();
    }

    public int GetRemainingDraws()
    {
        return remainingDraws;
    }

    public void UpdateDisplay()
    {
        if (drawCountText != null)
            drawCountText.text = remainingDraws.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (remainingDraws > 0)
            targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"点击抽牌：remainingDraws={remainingDraws}, playerEnergy={Player.Instance.GetEnergy()}");

        if (remainingDraws <= 0)
        {
            Debug.Log("抽牌次数已用完");
            return;
        }

        Player player = Player.Instance;
        if (player != null && player.UseEnergy(1))
        {
            Debug.Log("能量足够，调用 DrawCard");
            player.DrawCard();
            remainingDraws--;
            UpdateDisplay();
        }
        else
        {
            Debug.Log("能量不足");
        }
    }
    public void UseOneDraw()
    {
        if (remainingDraws > 0)
            remainingDraws--;
    }
   
}