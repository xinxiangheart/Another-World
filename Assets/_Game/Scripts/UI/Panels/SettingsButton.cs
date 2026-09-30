using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// Game 场景右上角那颗齿轮：
/// - 悬停放大（不会被回合动作挡住）
/// - 点击开关设置面板（由 SettingsLauncher 接线）
/// - 投降：面板右下角的场景动作调 Surrender()
/// </summary>
/// <remarks>2026-09-30：原来它自己打开的那块「设置 / 投降」面板 SettingPanel
/// 已从 Game 场景删掉，面板相关的字段与 TogglePanel 一并删。</remarks>
public class SettingsButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover")]
    public float hoverScale = 1.15f;

    private Button _button;
    private Vector3 _originalScale;
    private bool _surrendering;

    void Awake()
    {
        _button = GetComponent<Button>();
        _originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = _originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = _originalScale;
    }

    public void Surrender()
    {
        if (_surrendering) return;
        _surrendering = true;

        Debug.Log("[SettingsButton] Surrender");

        if (NetworkClient.isConnected)
        {
            // 服务端将投降方血量归零 → OnHealthChanged → GameEndPanel
            NetworkPlayer.Local?.CmdSurrender();
        }
        else
        {
            // 离线模式：直接触发GameEndPanel
            GameEndPanel.Instance?.OnPlayerDied(true);
        }
    }

    // 对方投降时由 GameEndPanel 统一处理，此方法删除
    public void OnOpponentSurrendered()
    {
        // No-op — 对方血量降到0时 OnHealthChanged → GameEndPanel.OnPlayerDied(false)
        Debug.Log("[SettingsButton] OnOpponentSurrendered — handled by GameEndPanel");
    }
}
