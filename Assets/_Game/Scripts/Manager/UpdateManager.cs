using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 更新管理器：GitHub API 检测版本（2026-09-19 起不再自带下载流程，
/// 版本过期就把版本号变红、「开始游戏」置灰，并让右上角的
/// MenuUpdateNotice 提示窗弹出来；下载走浏览器打开 Release 页）。
/// </summary>
public class UpdateManager : MonoBehaviour
{
    [Header("版本配置")]
    public string currentVersion = "0.1.0";

    [Header("GitHub 仓库（版本检测用）")]
    public string repoOwner = "xinxiangheart";
    public string repoName = "Another-World";

    [Header("UI")]
    public TMP_Text versionText;
    [Tooltip("非最新版本时版本号的颜色")]
    public Color outdatedVersionColor = new Color32(0xE8, 0x4B, 0x4B, 0xFF);

    [Header("版本门槛")]
    [Tooltip("非最新版本时置灰 + 不可点击的「开始游戏」")]
    public Button startButton;
    [Tooltip("右上角的「有新版本」提示窗")]
    public MenuUpdateNotice notice;

    [Header("调试")]
    [Tooltip("在编辑器里也真的去查一次（默认关，避免每次 Play 都走网络）")]
    public bool checkInEditor = false;

    private string _latestTag;
    private Color _versionColor = Color.white;

    private void Awake()
    {
        var versionAsset = Resources.Load<TextAsset>("version");
        if (versionAsset != null && !string.IsNullOrWhiteSpace(versionAsset.text))
            currentVersion = versionAsset.text.Trim();
    }

    private void Start()
    {
        if (versionText != null)
        {
            _versionColor = versionText.color;
            versionText.text = currentVersion;      // 只显示版本号：如 0.2.0
        }
        StartCoroutine(CheckForUpdates());
    }

    // ==================== 版本检测 ====================

    private IEnumerator CheckForUpdates()
    {
        if (Application.isEditor && !checkInEditor) { yield break; }

        var url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            req.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[UpdateManager] 检查更新失败: {req.error}");
                yield break;
            }

            var json = req.downloadHandler.text;
            _latestTag = ExtractTagName(json);

            if (string.IsNullOrEmpty(_latestTag))
            {
                Debug.LogWarning("[UpdateManager] 解析 Release 信息失败");
                yield break;
            }

            var latestVerStr = _latestTag.TrimStart('v').TrimStart('V');

            if (!Version.TryParse(latestVerStr, out var latestVer) ||
                !Version.TryParse(currentVersion, out var curVer))
            {
                Debug.LogWarning($"[UpdateManager] 版本号解析失败: current={currentVersion}, latest={latestVerStr}");
                yield break;
            }

            if (latestVer <= curVer)
            {
                Debug.Log($"[UpdateManager] 已是最新版本 ({currentVersion})");
                SetOutdated(false);
                yield break;
            }

            Debug.Log($"[UpdateManager] 发现新版本 {_latestTag}（当前 {currentVersion}）");
            SetOutdated(true);
        }
    }

    /// <summary>
    /// 版本过期：版本号变红、「开始游戏」置灰且不可点击
    /// （置灰后 TextMenuButton 不再变色、不再放大、也不出指示标），
    /// 并让右上角提示窗弹出来。
    /// </summary>
    private void SetOutdated(bool outdated)
    {
        if (versionText != null)
            versionText.color = outdated ? outdatedVersionColor : _versionColor;
        if (startButton != null)
            startButton.interactable = !outdated;
        if (notice != null)
            notice.SetOutdated(outdated);
    }

    // ==================== JSON ====================

    private static string ExtractTagName(string json) { return ExtractStringField(json, "tag_name"); }

    private static string ExtractStringField(string json, string fieldName)
    {
        var search = $"\"{fieldName}\":\"";
        var idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx += search.Length;
        var end = json.IndexOf('"', idx);
        if (end < 0) return null;
        return json.Substring(idx, end - idx);
    }
}
