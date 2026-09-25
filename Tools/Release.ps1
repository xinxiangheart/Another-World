<#
.SYNOPSIS
    Another World 一键发版：改 Inspector 版本号 -> commit -> push -> 等 workflow 自动打 tag / 构建 / 上传 Release。
.DESCRIPTION
    流程规格见 Docs/发版流程.md。默认先打印将执行的动作并要求确认；确认过再跑用 -Yes。
    退出码：0 成功，1 失败。
.EXAMPLE
    Tools\Release.ps1 -Version 0.2.2
.EXAMPLE
    Tools\Release.ps1 -Version 0.2.2 -Yes
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$RepoPath   = 'C:\Users\22589\Documents\GitHub\Another-World',
    [string]$RunnerPath = 'C:\actions-runner',
    [string]$Owner      = 'xinxiangheart',
    [string]$RepoName   = 'Another-World',
    [int]   $TimeoutMinutes = 40,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$sceneRel = 'Assets/_Game/Scenes/Welcome.unity'
$tag      = "v$Version"
$asset    = 'Another-World-Windows.zip'
$ua       = 'Mozilla/5.0'

function Info($m) { Write-Host "[发版] $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "[发版][注意] $m" -ForegroundColor Yellow }
function Ok($m)   { Write-Host "[发版][完成] $m" -ForegroundColor Green }
function Die($m)  { Write-Host "[发版][失败] $m" -ForegroundColor Red; exit 1 }

# ── 1. 前置检查 ───────────────────────────────────────────────
if (-not (Test-Path (Join-Path $RepoPath '.git'))) { Die "找不到 git 仓库：$RepoPath" }
$scene = Join-Path $RepoPath $sceneRel
if (-not (Test-Path $scene)) { Die "找不到场景文件：$scene" }

Push-Location $RepoPath
try {
    # 分支必须是 main（workflow 只监听 main 的 push）
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne 'main') { Die "当前分支是 $branch，发版必须基于 main" }

    # 读 / 校验 Inspector 版本号
    $raw  = [IO.File]::ReadAllText($scene)
    $pat  = '(?m)^  currentVersion:[^\r\n]*$'
    $hits = [regex]::Matches($raw, $pat)
    if ($hits.Count -ne 1) { Die "Welcome.unity 里 currentVersion 期望恰好 1 处，实际 $($hits.Count) 处" }
    $oldVersion = ($hits[0].Value -replace '^  currentVersion:\s*', '').Trim()
    if ($oldVersion -eq $Version) { Die "版本号没变（当前已是 $Version）—— workflow 需要真实的文件改动才会触发" }

    # 远端同名 tag 已存在 -> workflow 会整段跳过构建
    if (git ls-remote --tags origin "refs/tags/$tag") { Die "远端已存在 tag $tag；换版本号，或先在 GitHub 网页端 Tags 里删掉它" }

    # 其它未提交改动不会进包（构建只打包 push 出去的那个提交）
    $dirty = @(git -c core.quotepath=false status --porcelain | Where-Object { $_ -and ($_ -notmatch [regex]::Escape($sceneRel)) })
    if ($dirty.Count -gt 0) {
        Warn "工作区还有 $($dirty.Count) 处未提交改动，它们不会被包含在本次发布包里："
        $dirty | ForEach-Object { Write-Host "         $_" }
    }

    # ── 2. 打印将执行的动作 + 确认 ────────────────────────────
    $runner = Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue
    Write-Host ""
    Info "即将执行："
    if (-not $runner) { Write-Host "         0) self-hosted runner 没在跑 -> 先拉起 $RunnerPath\run.cmd（隐藏窗口）" }
    Write-Host "         1) $sceneRel  currentVersion: $oldVersion -> $Version"
    Write-Host "         2) git commit -m `"$Version`" && git push origin main"
    Write-Host "         3) 等 workflow 打 tag $tag、构建、上传 $asset（超时 $TimeoutMinutes 分钟）"
    Write-Host ""
    if (-not $Yes) {
        $ans = Read-Host "确认执行？(y/N)"
        if ($ans -ne 'y' -and $ans -ne 'Y') { Write-Host "已取消"; exit 0 }
    }

    # ── 3. 确保 runner 在跑（它不在时任务会一直 queued） ──────
    if (-not $runner) {
        $runCmd = Join-Path $RunnerPath 'run.cmd'
        if (-not (Test-Path $runCmd)) { Die "runner 没在跑，且找不到 $runCmd" }
        Info "拉起 runner：$runCmd"
        Start-Process -FilePath $runCmd -WorkingDirectory $RunnerPath -WindowStyle Hidden
        $wait = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $wait -and -not (Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 3 }
        if (Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue) { Info "runner 已就绪" }
        else { Warn "60 秒内没看到 Runner.Listener，继续发版（runner 迟到也能接到任务）" }
    }

    # ── 4. 改版本号 -> commit -> push ─────────────────────────
    [IO.File]::WriteAllText($scene, [regex]::Replace($raw, $pat, "  currentVersion: $Version"), (New-Object Text.UTF8Encoding($false)))
    git add -- $sceneRel
    if ($LASTEXITCODE -ne 0) { Die "git add 失败" }
    git commit -q -m $Version
    if ($LASTEXITCODE -ne 0) { Die "git commit 失败" }
    git push origin main 2>&1 | ForEach-Object { Write-Host "         $_" }
    if ($LASTEXITCODE -ne 0) { Die "git push 失败（若被自动提交抢先，先 git pull --rebase 再重试）" }
    Info "已推送：$((git log --oneline -1).Trim())"

    # ── 5. 等 tag 建立（workflow 读 Inspector 版本号后自建） ──
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline -and -not (git ls-remote --tags origin "refs/tags/$tag")) { Start-Sleep -Seconds 10 }
    if (-not (git ls-remote --tags origin "refs/tags/$tag")) {
        Die "$TimeoutMinutes 分钟内没等到 tag $tag；看 https://github.com/$Owner/$RepoName/actions"
    }
    Info "tag $tag 已建立，等构建 + 上传（v0.2.1 实测：构建约 2 分钟、360 MB 上传约 2 分钟）"

    # ── 6. 等 Release 资产挂上（走页面，不占 API 限额） ───────
    $assetsUrl = "https://github.com/$Owner/$RepoName/releases/expanded_assets/$tag"
    $deadline  = (Get-Date).AddMinutes($TimeoutMinutes)
    $assetOk   = $false
    while ((Get-Date) -lt $deadline) {
        try {
            $html = (Invoke-WebRequest -Uri $assetsUrl -UseBasicParsing -UserAgent $ua -TimeoutSec 25).Content
            if ($html -match [regex]::Escape($asset)) { $assetOk = $true; break }
        } catch { }
        Start-Sleep -Seconds 15
    }
    if (-not $assetOk) {
        Die "$TimeoutMinutes 分钟内没等到资产 $asset；看 $RunnerPath\_diag\Worker_*.log 与 $RunnerPath\run-stdout.log"
    }

    # ── 7. 验收 ───────────────────────────────────────────────
    $dl = "https://github.com/$Owner/$RepoName/releases/download/$tag/$asset"
    Ok "$tag 发布完成：https://github.com/$Owner/$RepoName/releases/tag/$tag"
    Write-Host "         资产：$dl"
    try {
        $head = Invoke-WebRequest -Uri $dl -Method Head -UseBasicParsing -UserAgent $ua -MaximumRedirection 5 -TimeoutSec 60
        Write-Host "         校验：HTTP $($head.StatusCode)，$([string]::Join(',', $head.Headers['Content-Length'])) 字节"
    } catch { Warn "资产已挂上，但下载链接 HEAD 校验失败：$($_.Exception.Message)" }
}
finally { Pop-Location }
