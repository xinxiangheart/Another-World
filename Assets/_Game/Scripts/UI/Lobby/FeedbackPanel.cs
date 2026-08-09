using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// 反馈面板——玩家输入文字发给你。
/// 支持两种方式：
///   1. Discord 网页钩子（填 webhookUrl，消息秒到 Discord 频道）
///   2. 本地保存（留空 webhookUrl，存到 persistentDataPath/feedback.txt）
/// </summary>
public class FeedbackPanel : MonoBehaviour
{
    public static FeedbackPanel Instance { get; private set; }

    [Header("面板")]
    public GameObject panelRoot;

    [Header("输入")]
    public TMP_InputField inputField;
    public TMP_Text charCountText;

    [Header("按钮")]
    public Button sendButton;
    public Button cancelButton;

    [Header("提示")]
    public TMP_Text statusText;

    [Header("配置")]
    [Tooltip("Discord 网页钩子 URL（留空则本地保存）")]
    public string webhookUrl = "";

    void Awake()
    {
        Instance = this;
        if (panelRoot) panelRoot.SetActive(false);
        if (sendButton) sendButton.onClick.AddListener(Send);
        if (cancelButton) cancelButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (inputField) { inputField.text = ""; inputField.Select(); inputField.ActivateInputField(); }
        if (statusText) statusText.text = "";
        UpdateCharCount();
    }

    void Close()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (inputField && inputField.isFocused)
            UpdateCharCount();
    }

    void UpdateCharCount()
    {
        if (charCountText && inputField)
            charCountText.text = $"{inputField.text.Length}/500";
    }

    void Send()
    {
        string msg = inputField?.text?.Trim();
        if (string.IsNullOrEmpty(msg)) return;

        if (sendButton) sendButton.interactable = false;
        if (statusText) statusText.text = "发送中...";

        if (!string.IsNullOrEmpty(webhookUrl))
            StartCoroutine(SendToDiscord(msg));
        else
            SaveLocal(msg);
    }

    IEnumerator SendToDiscord(string msg)
    {
        string steamName = "未知";
        try { steamName = Steamworks.SteamFriends.GetPersonaName(); } catch { }
        string payload = $"{{\"content\":\"💬 **玩家反馈**\\n👤 {steamName}\\n📝 {msg}\"}}";

        using (var req = new UnityWebRequest(webhookUrl, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(payload);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (statusText) statusText.text = "发送成功，谢谢反馈！";
                StartCoroutine(AutoClose());
            }
            else
            {
                if (statusText) statusText.text = $"发送失败，已保存本地";
                SaveLocal(msg);
            }
        }
    }

    void SaveLocal(string msg)
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "feedback.txt");
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        System.IO.File.AppendAllText(path, $"[{timestamp}] {msg}\n");
        if (statusText) statusText.text = "反馈已保存，谢谢！";
        StartCoroutine(AutoClose());
    }

    IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(2f);
        if (sendButton) sendButton.interactable = true;
        Close();
    }
}
