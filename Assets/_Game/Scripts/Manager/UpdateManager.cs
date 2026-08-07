using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 启动时检查 GitHub Releases 是否有新版本，有则下载更新。
/// 8 线程分块下载，国内速度提升明显。
/// </summary>
public class UpdateManager : MonoBehaviour
{
    [Header("版本配置")]
    public string currentVersion = "0.1.0";

    [Header("GitHub 仓库")]
    public string repoOwner = "xinxiangheart";
    public string repoName = "Another-World";

    [Header("下载加速")]
    [Tooltip("启用国内镜像加速下载（直连 GitHub 太慢时勾选）")]
    public bool useMirror = true;
    [Tooltip("镜像 URL 前缀，将 GitHub 原始链接转为镜像链接")]
    public string mirrorUrl = "https://ghproxy.net/";

    [Header("下载线程数")]
    [Range(1, 16)]
    public int downloadThreads = 8;

    [Header("UI")]
    public TMP_Text versionText;
    public Button updateButton;
    public TMP_Text updateButtonText;
    public TMP_Text downloadStatusText;

    private string _latestTag;
    private string _downloadUrl;

    /// <summary>返回下载链接（若启用镜像则走加速）</summary>
    private string GetDownloadUrl()
    {
        if (useMirror && !string.IsNullOrEmpty(mirrorUrl) && !string.IsNullOrEmpty(_downloadUrl))
            return mirrorUrl.TrimEnd('/') + "/" + _downloadUrl;
        return _downloadUrl;
    }

    /// <summary>始终返回原始直连 URL</summary>
    private string GetDownloadUrlDirect() { return _downloadUrl; }

    bool IsCoroutineRunning(Coroutine c)
    {
        if (c == null) return false;
        try { return c.ToString() != null; } catch { return false; }
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
        if (downloadStatusText != null) downloadStatusText.gameObject.SetActive(false);
        if (updateButton != null) updateButton.onClick.AddListener(OnUpdateClicked);
        StartCoroutine(CheckForUpdates());
    }

    // ==================== 版本检测（不变） ====================

    private IEnumerator CheckForUpdates()
    {
        if (Application.isEditor)
        {
            UnityEngine.Debug.Log("[UpdateManager] 编辑器模式，跳过更新检测");
            yield break;
        }

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
                UnityEngine.Debug.LogWarning($"[UpdateManager] 检查更新失败: {req.error}");
                yield break;
            }

            var json = req.downloadHandler.text;
            _latestTag = ExtractTagName(json);
            _downloadUrl = ExtractDownloadUrl(json);

            if (string.IsNullOrEmpty(_latestTag) || string.IsNullOrEmpty(_downloadUrl))
            {
                UnityEngine.Debug.LogWarning("[UpdateManager] 解析 Release 信息失败");
                yield break;
            }

            var latestVerStr = _latestTag.TrimStart('v').TrimStart('V');

            if (!Version.TryParse(latestVerStr, out var latestVer) ||
                !Version.TryParse(currentVersion, out var curVer))
            {
                UnityEngine.Debug.LogWarning($"[UpdateManager] 版本号解析失败: current={currentVersion}, latest={latestVerStr}");
                yield break;
            }

            if (latestVer <= curVer)
            {
                UnityEngine.Debug.Log($"[UpdateManager] 已是最新版本 ({currentVersion})");
                yield break;
            }

            UnityEngine.Debug.Log($"[UpdateManager] 发现新版本 {_latestTag}");
            SetStatus($"最新版本：{_latestTag}");

            if (updateButton != null) updateButton.gameObject.SetActive(true);
            if (updateButtonText != null) updateButtonText.text = "有新版本，点此更新";
        }
    }

    // ==================== 多线程分块下载 ====================

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

        // ── 先试镜像，失败自动直连 ──────────────────
        if (useMirror)
        {
            string mirrorUrl = GetDownloadUrl();
            SetStatus("连接镜像...");
            yield return StartCoroutine(DownloadSingle(tempZip, mirrorUrl));
            if (!File.Exists(tempZip)) SetStatus("镜像失败，切换直连...");
        }

        // ── 直连兜底 ──────────────────────────────
        if (!File.Exists(tempZip))
        {
            string directUrl = GetDownloadUrlDirect();
            yield return StartCoroutine(DownloadSingle(tempZip, directUrl));
        }

        if (!File.Exists(tempZip))
        {
            SetStatus("下载失败，请重试");
            if (updateButton != null) updateButton.interactable = true;
            StartCoroutine(HideStatusAfterDelay(3f));
            yield break;
        }

        SetStatus("准备安装，游戏即将关闭...");
        WriteAndLaunchUpdater(tempZip);
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    IEnumerator DownloadSingle(string tempZip, string url)
    {
        SetStatus("下载中（镜像加速）...");
        using (var req = UnityWebRequest.Get(url))
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
                yield break;
            }
        }
        SetStatus("下载完成");
    }

    IEnumerator DownloadMulti(string tempZip, string url)
    {
        long fileSize = 0;
        SetStatus("正在连接...");
        using (var headReq = UnityWebRequest.Head(url))
        {
            headReq.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            headReq.timeout = 10;
            yield return headReq.SendWebRequest();
            if (headReq.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"连接失败: {headReq.error}");
                yield break;
            }
            string cl = headReq.GetResponseHeader("Content-Length");
            if (!long.TryParse(cl, out fileSize) || fileSize <= 0)
            {
                SetStatus("获取文件大小失败");
                yield break;
            }
        }

        int threads = Mathf.Clamp(downloadThreads, 1, 16);
        long chunkSize = fileSize / threads;
        var requests = new UnityWebRequest[threads];
        var chunkFiles = new string[threads];

        for (int i = 0; i < threads; i++)
        {
            long start = i * chunkSize;
            long end = (i == threads - 1) ? fileSize - 1 : start + chunkSize - 1;
            chunkFiles[i] = tempZip + $".part{i}";
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            req.SetRequestHeader("Range", $"bytes={start}-{end}");
            req.downloadHandler = new DownloadHandlerFile(chunkFiles[i]) { removeFileOnAbort = true };
            req.SendWebRequest();
            requests[i] = req;
        }

        bool anyFailed = false;
        while (true)
        {
            int done = 0;
            for (int i = 0; i < threads; i++)
            {
                if (requests[i] == null) { done++; continue; }
                if (requests[i].isDone)
                {
                    if (requests[i].result != UnityWebRequest.Result.Success) anyFailed = true;
                    requests[i].Dispose(); requests[i] = null;
                    done++;
                }
            }
            SetStatus($"下载中... {(float)done / threads * 100f:F0}%");
            if (done >= threads) break;
            yield return null;
        }

        if (anyFailed)
        {
            for (int i = 0; i < threads; i++)
                if (File.Exists(chunkFiles[i])) File.Delete(chunkFiles[i]);
            yield break;
        }

        SetStatus("正在合并...");
        using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            for (int i = 0; i < threads; i++)
            {
                if (!File.Exists(chunkFiles[i])) continue;
                byte[] buf = File.ReadAllBytes(chunkFiles[i]);
                outStream.Write(buf, 0, buf.Length);
                File.Delete(chunkFiles[i]);
            }
    }

    // ==================== 安装（不变） ====================

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
# 等待游戏退出
Start-Sleep -Seconds 2
do {{
    Start-Sleep -Milliseconds 500
}} while (Get-Process -Name '{Path.GetFileNameWithoutExtension(exeName)}' -ErrorAction SilentlyContinue)

Write-Host '正在解压...'
try {{
    Expand-Archive -Path '{zipPath.Replace("'", "''")}' -DestinationPath '{gameDir.Replace("'", "''")}' -Force
    Write-Host '更新完成'
}} catch {{
    Write-Host ""更新失败: $_""
    Start-Sleep -Seconds 5
    exit 1
}}

Remove-Item '{zipPath.Replace("'", "''")}' -Force -ErrorAction SilentlyContinue
Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue

Write-Host '重新启动...'
Start-Process '{Path.Combine(gameDir, exeName).Replace("'", "''")}'
";

        File.WriteAllText(ps1Path, script, System.Text.Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1Path}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        });
    }

    // ==================== 工具方法 ====================

    private void SetStatus(string msg)
    {
        if (downloadStatusText != null) downloadStatusText.text = msg;
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (downloadStatusText != null) downloadStatusText.gameObject.SetActive(false);
    }

    // ==================== JSON 解析 ====================

    private static string ExtractTagName(string json)
    {
        return ExtractStringField(json, "tag_name");
    }

    private static string ExtractDownloadUrl(string json)
    {
        var marker = "\"assets\":[";
        var idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + marker.Length;
        var depth = 1;
        var end = start;
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
