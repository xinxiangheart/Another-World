using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 大厅玩家信息面板——显示 Steam 头像和昵称。
/// </summary>
public class PlayerProfilePanel : MonoBehaviour
{
    [Header("头像")]
    public RawImage avatarImage;
    public AspectRatioFitter avatarFitter;

    [Header("文字")]
    public TMP_Text nameText;

    void Start()
    {
        Refresh();
    }

    private int _loadFrame;

    void Update()
    {
        var sd = SteamDataManager.Instance;
        if (sd == null) return;

        // 名字后补（Steam 初始化比 Start 晚）
        if (nameText != null && (string.IsNullOrEmpty(nameText.text) || nameText.text == "未知玩家"))
        {
            if (!string.IsNullOrEmpty(sd.localPlayerName) && sd.localPlayerName != "未知玩家")
                nameText.text = sd.localPlayerName;
        }

        // 头像后补（Steam 异步加载）
        if (avatarImage != null && avatarImage.texture == null && sd.localAvatar != null)
        {
            avatarImage.texture = sd.localAvatar;
            if (avatarFitter != null)
                avatarFitter.aspectRatio = (float)sd.localAvatar.width / sd.localAvatar.height;
        }
    }

    public void Refresh()
    {
        var sd = SteamDataManager.Instance;
        if (sd == null) return;

        if (nameText != null)
            nameText.text = sd.localPlayerName;

        if (avatarImage != null && sd.localAvatar != null)
        {
            avatarImage.texture = sd.localAvatar;
            if (avatarFitter != null)
                avatarFitter.aspectRatio = (float)sd.localAvatar.width / sd.localAvatar.height;
        }
    }
}
