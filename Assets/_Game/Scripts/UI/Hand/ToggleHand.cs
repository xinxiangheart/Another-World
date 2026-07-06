using UnityEngine;
using UnityEngine.UI;

public class ToggleHandButton : MonoBehaviour
{
    private bool isHidden = false;
    private HandManager handManager;

    void Start()
    {
        handManager = FindObjectOfType<HandManager>();
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        isHidden = !isHidden;
        if (isHidden)
            handManager?.HideAllCards();
        else
            handManager?.ShowAllCards();
    }
}