using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 更新管理器：GitHub API 检测版本 + 自定义下载源（123云盘等）。
/// </summary>
public class UpdateManager : MonoBehaviour
{
    [Header("版本配置")]
    public string currentVersion = "0.1.0";

    [Header("GitHub 仓库（版本检测用）")]
    public string repoOwner = "xinxiangheart";
    public string repoName = "Another-World";

    [Header("下载源")]
    [Tooltip("123云盘/蓝奏云直链，留空则从 GitHub Release 下载")]
    public string customDownloadUrl = "";

    [Header("UI")]
    public TMP_Text versionText;
    public Button updateButton;
    public TMP_Text updateButtonText;
    public TMP_Text downloadStatusText;
    [Tooltip("\"手动下载\"按钮，点击跳转浏览器到 GitHub Releases")]
    public Button manualDownloadButton;
    public TMP_Text manualDownloadButtonText;

    private string _latestTag;
    private string _downloadUrl;

    string GetDownloadUrl()
    {
        if (!string.IsNullOrEmpty(customDownloadUrl))
            return customDownloadUrl;
        return _downloadUrl;
    }

    private void Awake()
    {
        var versionAsset = Resources.Load<TextAsset>("version");
        if (versionAsset != null && !string.IsNullOrWhiteSpace(versionAsset.text))
            currentVersion = versionAsset.text.Trim();
    }

    private void Start()
    {
        if (versionText != null) versionText.text = $"当前版本：{currentVersion}";
        if (updateButton != null) updateButton.gameObject.SetActive(false);
        if (manualDownloadButton != null) manualDownloadButton.gameObject.SetActive(false);
        if (downloadStatusText != null) downloadStatusText.gameObject.SetActive(false);
        if (updateButton != null) updateButton.onClick.AddListener(OnUpdateClicked);
        if (manualDownloadButton != null) manualDownloadButton.onClick.AddListener(OpenManualDownload);
        StartCoroutine(CheckForUpdates());
    }

    void OpenManualDownload()
    {
        string url = $"https://github.com/{repoOwner}/{repoName}/releases/tag/{_latestTag}";
        Application.OpenURL(url);
        Debug.Log($"[UpdateManager] 打开浏览器: {url}");
    }

    // ==================== 版本检测 ====================

    private IEnumerator CheckForUpdates()
    {
        if (Application.isEditor) { yield break; }

        SetStatus("");
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
            _downloadUrl = ExtractDownloadUrl(json);

            if (string.IsNullOrEmpty(_latestTag) || string.IsNullOrEmpty(_downloadUrl))
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
                yield break;
            }

            Debug.Log($"[UpdateManager] 发现新版本 {_latestTag}");
            SetStatus($"最新版本：{_latestTag}");

            if (updateButton != null) updateButton.gameObject.SetActive(true);
            if (updateButtonText != null) updateButtonText.text = "自动更新";
            if (manualDownloadButton != null) manualDownloadButton.gameObject.SetActive(true);
            if (manualDownloadButtonText != null) manualDownloadButtonText.text = "手动下载";
        }
    }

    // ==================== 下载 ====================

    private void OnUpdateClicked()
    {
        if (string.IsNullOrEmpty(_downloadUrl)) return;
        if (downloadStatusText != null) downloadStatusText.gameObject.SetActive(true);
        StartCoroutine(DownloadAndInstall());
    }

    private IEnumerator DownloadAndInstall()
    {
        if (updateButton != null) updateButton.interactable = false;

        var tempZip = Path.Combine(Application.temporaryCachePath, $"update-{_latestTag}.zip");
        string dlUrl = GetDownloadUrl();

        if (!string.IsNullOrEmpty(customDownloadUrl))
            SetStatus("下载中...");
        else
            SetStatus("下载中（GitHub可能较慢）...");

        using (var req = UnityWebRequest.Get(dlUrl))
        {
            req.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            var handler = new DownloadHandlerFile(tempZip);
            handler.removeFileOnAbort = true;
            req.downloadHandler = handler;
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                SetStatus($"下载中... {req.downloadProgress * 100f:F0}%");
                yield return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"下载失败: {req.error}");
                if (updateButton != null) updateButton.interactable = true;
                StartCoroutine(HideStatusAfterDelay(3f));
                yield break;
            }
        }

        SetStatus("准备安装，游戏即将关闭...");
        WriteAndLaunchUpdater(tempZip);
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    // ==================== 安装 ====================

    private void WriteAndLaunchUpdater(string zipPath)
    {
#if UNITY_STANDALONE_WIN
        var gameDir = Path.GetDirectoryName(Application.dataPath);
        var exeName = "Another-World.exe";
#else
        var gameDir = Path.GetDirectoryName(Application.dataPath);
        var exeName = "Another-World";
#endif

        var ps1Path = Path.Combine(Application.temporaryCachePath, "update.ps1");

        var script = $@"
Start-Sleep -Seconds 2
do {{ Start-Sleep -Milliseconds 500 }} while (Get-Process -Name '{Path.GetFileNameWithoutExtension(exeName)}' -ErrorAction SilentlyContinue)
Expand-Archive -Path '{zipPath.Replace("'", "''")}' -DestinationPath '{gameDir.Replace("'", "''")}' -Force
Remove-Item '{zipPath.Replace("'", "''")}' -Force -ErrorAction SilentlyContinue
Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
Start-Process '{Path.Combine(gameDir, exeName).Replace("'", "''")}'
";

        File.WriteAllText(ps1Path, script, System.Text.Encoding.UTF8);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1Path}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        });
    }

    // ==================== 工具 ====================

    private void SetStatus(string msg)
    {
        if (downloadStatusText != null) downloadStatusText.text = msg;
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (downloadStatusText != null) downloadStatusText.gameObject.SetActive(false);
    }

    // ==================== JSON ====================

    private static string ExtractTagName(string json) { return ExtractStringField(json, "tag_name"); }

    private static string ExtractDownloadUrl(string json)
    {
        var marker = "\"assets\":[";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var depth = 1; var end = start;
        for (; end < json.Length && depth > 0; end++)
        {
            if (json[end] == '[') depth++;
            else if (json[end] == ']') depth--;
        }
        var assetsBlock = json.Substring(start, end - start - 1);
        var urlMarker = "\"browser_download_url\":\"";
        var urlIdx = assetsBlock.IndexOf(urlMarker, StringComparison.Ordinal);
        if (urlIdx < 0) return null;
        urlIdx += urlMarker.Length;
        var urlEnd = assetsBlock.IndexOf('"', urlIdx);
        if (urlEnd < 0) return null;
        return assetsBlock.Substring(urlIdx, urlEnd - urlIdx).Replace("\\", "");
    }

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
