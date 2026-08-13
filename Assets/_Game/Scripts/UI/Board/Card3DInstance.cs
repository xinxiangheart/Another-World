using UnityEngine;

public class Card3DInstance : MonoBehaviour
{
    public CardInstance cardInstance;

    void Awake()
    {
        // 每个 3D 卡牌实例自动挂漂浮/呼吸动画组件（无需逐个在实例化点手动添加）
        if (GetComponent<Card3DAnimator>() == null)
            gameObject.AddComponent<Card3DAnimator>();
    }

    public void UpdateValues()
    {
        CardDisplay3D display = GetComponent<CardDisplay3D>();
        if (display != null) display.Refresh();
    }
}