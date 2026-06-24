using UnityEngine;

public class ButtonOrderFix : MonoBehaviour
{
    public GameObject endTurnButton;
    public GameObject drawCardButton;

    void Start()
    {
        endTurnButton.transform.SetAsFirstSibling();
        drawCardButton.transform.SetAsFirstSibling();
    }
}