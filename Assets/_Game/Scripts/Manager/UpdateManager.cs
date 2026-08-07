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

        // ── Step 1: 获取文件大小 ──────────────────
        SetStatus("正在连接...");
        long fileSize = 0;
        using (var headReq = UnityWebRequest.Head(_downloadUrl))
        {
            headReq.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            headReq.timeout = 10;
            yield return headReq.SendWebRequest();

            if (headReq.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"连接失败: {headReq.error}");
                if (updateButton != null) updateButton.interactable = true;
                StartCoroutine(HideStatusAfterDelay(3f));
                yield break;
            }

            string cl = headReq.GetResponseHeader("Content-Length");
            if (!long.TryParse(cl, out fileSize) || fileSize <= 0)
            {
                SetStatus("获取文件大小失败");
                if (updateButton != null) updateButton.interactable = true;
                StartCoroutine(HideStatusAfterDelay(3f));
                yield break;
            }
        }

        string acceptRanges = ""; // 服务端支不支持无所谓，GitHub 支持
        int threads = Mathf.Clamp(downloadThreads, 1, 16);
        long chunkSize = fileSize / threads;

        // ── Step 2: 同时启动所有分块请求 ────────────
        var requests = new UnityWebRequest[threads];
        var handlers = new DownloadHandlerFile[threads];
        var chunkFiles = new string[threads];

        for (int i = 0; i < threads; i++)
        {
            long start = i * chunkSize;
            long end = (i == threads - 1) ? fileSize - 1 : start + chunkSize - 1;

            chunkFiles[i] = tempZip + $".part{i}";

            var req = UnityWebRequest.Get(_downloadUrl);
            req.SetRequestHeader("User-Agent", $"{repoName}-Updater");
            req.SetRequestHeader("Range", $"bytes={start}-{end}");
            var handler = new DownloadHandlerFile(chunkFiles[i]);
            handler.removeFileOnAbort = true;
            req.downloadHandler = handler;

            // 关键：在同一帧内全部 SendWebRequest，不 yield 等待，实现并发
            var op = req.SendWebRequest();
            // SendWebRequest 返回的 AsyncOperation 只需要存起来等就行
            requests[i] = req;
            handlers[i] = handler;
        }

        // ── Step 3: 等待全部完成，实时显示进度 ────────
        bool anyFailed = false;
        while (true)
        {
            int done = 0;
            for (int i = 0; i < threads; i++)
            {
                if (requests[i] == null) { done++; continue; }
                if (requests[i].isDone)
                {
                    if (requests[i].result != UnityWebRequest.Result.Success)
                        anyFailed = true;
                    requests[i].Dispose();
                    requests[i] = null;
                    done++;
                }
            }

            float pct = (float)done / threads * 100f;
            SetStatus($"下载中... {pct:F0}% ({done}/{threads})");

            if (done >= threads) break;
            yield return null;
        }

        // 清理 handler 引用
        for (int i = 0; i < threads; i++)
        {
            if (requests[i] != null) { requests[i].Dispose(); requests[i] = null; }
            handlers[i] = null;
        }

        if (anyFailed)
        {
            SetStatus("下载失败，请重试");
            for (int i = 0; i < threads; i++)
                if (File.Exists(chunkFiles[i])) File.Delete(chunkFiles[i]);
            if (updateButton != null) updateButton.interactable = true;
            StartCoroutine(HideStatusAfterDelay(3f));
            yield break;
        }

        // ── Step 4: 合并分块文件 ────────────────────
        SetStatus("正在合并文件...");
        using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
        {
            for (int i = 0; i < threads; i++)
            {
                if (!File.Exists(chunkFiles[i])) continue;
                byte[] buf = File.ReadAllBytes(chunkFiles[i]);
                outStream.Write(buf, 0, buf.Length);
                File.Delete(chunkFiles[i]);
            }
        }

        // ── Step 5: 安装 ────────────────────────────
        SetStatus("准备安装，游戏即将关闭...");
        WriteAndLaunchUpdater(tempZip);
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
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
