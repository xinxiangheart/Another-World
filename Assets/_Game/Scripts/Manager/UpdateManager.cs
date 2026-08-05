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
/// 挂在 Welcome 场景的 Canvas/Background 上，版本号在 Inspector 里直接改。
/// </summary>
public class UpdateManager : MonoBehaviour
{
    [Header("版本配置")]
    [Tooltip("当前游戏版本号，发版时在这里修改即可")]
    public string currentVersion = "0.1.0";

    [Header("GitHub 仓库")]
    public string repoOwner = "xinxiangheart";
    public string repoName = "Another-World";

    [Header("UI - 版本显示")]
    [Tooltip("显示\"当前版本：xxx\"的 Text")]
    public TMP_Text versionText;

    [Header("UI - 更新")]
    [Tooltip("\"有新版本，点此更新\" 按钮")]
    public Button updateButton;
    [Tooltip("更新按钮上的文字（可选，用于改文案）")]
    public TMP_Text updateButtonText;
    [Tooltip("下载百分比文字，下载时实时显示\"下载中...50%\"，检测/错误信息也显示在这里")]
    public TMP_Text downloadStatusText;

    private string _latestTag;
    private string _downloadUrl;

    private void Awake()
    {
        // 优先从 Resources/version.txt 读取版本号（CI 构建时自动写入）
        // 编辑器/本地开发时回退到 Inspector 值
        var versionAsset = Resources.Load<TextAsset>("version");
        if (versionAsset != null && !string.IsNullOrWhiteSpace(versionAsset.text))
        {
            currentVersion = versionAsset.text.Trim();
        }
    }

    private void Start()
    {
        // 版本显示
        if (versionText != null)
            versionText.text = $"当前版本：{currentVersion}";

        // 初始隐藏更新按钮和下载状态
        if (updateButton != null)
            updateButton.gameObject.SetActive(false);
        if (downloadStatusText != null)
            downloadStatusText.gameObject.SetActive(false);

        // 绑定按钮事件
        if (updateButton != null)
            updateButton.onClick.AddListener(OnUpdateClicked);

        // 开始检测
        StartCoroutine(CheckForUpdates());
    }

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

            // 去掉 v 前缀比较版本号
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

            // 发现新版本 → 显示更新按钮
            UnityEngine.Debug.Log($"[UpdateManager] 发现新版本 {_latestTag}");
            SetStatus($"最新版本：{_latestTag}");

            if (updateButton != null)
                updateButton.gameObject.SetActive(true);

            if (updateButtonText != null)
                updateButtonText.text = "有新版本，点此更新";
        }
    }

    private void OnUpdateClicked()
    {
        if (string.IsNullOrEmpty(_downloadUrl)) return;
        if (downloadStatusText != null)
            downloadStatusText.gameObject.SetActive(true);
        StartCoroutine(DownloadAndInstall());
    }

    private IEnumerator DownloadAndInstall()
    {
        if (updateButton != null)
            updateButton.interactable = false;

        var tempZip = Path.Combine(Application.temporaryCachePath, $"update-{_latestTag}.zip");

        using (var req = UnityWebRequest.Get(_downloadUrl))
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
                // 2秒后隐藏错误信息
                StartCoroutine(HideStatusAfterDelay(2f));
                yield break;
            }
        }

        SetStatus("准备安装，游戏即将关闭...");

        // 写入 PowerShell 更新脚本并启动
        WriteAndLaunchUpdater(tempZip);

        // 等一帧让 UI 刷新，然后退出
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    private void WriteAndLaunchUpdater(string zipPath)
    {
        // 游戏根目录 = dataPath 上一级（Application.dataPath 指向 xxx_Data 文件夹）
#if UNITY_STANDALONE_WIN
        var gameDir = Path.GetDirectoryName(Application.dataPath);
        var exeName = "Another-World.exe";
#else
        var gameDir = Path.GetDirectoryName(Application.dataPath);
        var exeName = "Another-World"; // Mac/Linux fallback
#endif

        var ps1Path = Path.Combine(Application.temporaryCachePath, "update.ps1");

        var script = $@"
# 等待游戏进程退出
Start-Sleep -Seconds 2
do {{
    Start-Sleep -Milliseconds 500
}} while (Get-Process -Name '{Path.GetFileNameWithoutExtension(exeName)}' -ErrorAction SilentlyContinue)

Write-Host '正在解压更新...'
try {{
    Expand-Archive -Path '{zipPath.Replace("'", "''")}' -DestinationPath '{gameDir.Replace("'", "''")}' -Force
    Write-Host '更新完成'
}} catch {{
    Write-Host ""更新失败: $_""
    Start-Sleep -Seconds 5
    exit 1
}}

Write-Host '清理临时文件...'
Remove-Item '{zipPath.Replace("'", "''")}' -Force -ErrorAction SilentlyContinue
Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue

Write-Host '重新启动游戏...'
Start-Process '{Path.Combine(gameDir, exeName).Replace("'", "''")}'
";

        File.WriteAllText(ps1Path, script, System.Text.Encoding.UTF8);

        // 启动 PowerShell 静默执行
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1Path}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        });
    }

    private void SetStatus(string msg)
    {
        if (downloadStatusText != null)
            downloadStatusText.text = msg;
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (downloadStatusText != null)
            downloadStatusText.gameObject.SetActive(false);
    }

    // ---- JSON 手动解析（不依赖第三方库） ----

    private static string ExtractTagName(string json)
    {
        return ExtractStringField(json, "tag_name");
    }

    private static string ExtractDownloadUrl(string json)
    {
        // 找到 assets 数组
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

        // 在 assets 里找 .zip 的 browser_download_url
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
