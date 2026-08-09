using UnityEngine;
using TMPro;

/// <summary>调试：绑定特性追踪文本。挂在 Game 场景常驻对象上，拖入 TMP_Text。</summary>
public class DebugTraitText : MonoBehaviour
{
    public TMP_Text debugText;

    void Awake()
    {
        EffectDispatcher.debugText = debugText;
        if (debugText != null) debugText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (EffectDispatcher.debugText == debugText)
            EffectDispatcher.debugText = null;
    }
}
