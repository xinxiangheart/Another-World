<#
.SYNOPSIS
    以「干净的用户级环境」启动 Another-World 的 Unity 编辑器（默认单实例，已开则不重复拉）。

.DESCRIPTION
    为什么需要这个脚本（2026-09-27 实测，踩过一次）：

    Codex / 沙箱化的 shell 里，继承到的环境块缺了一批「机器级 volatile 变量」——
    `ALLUSERSPROFILE`、`PUBLIC`、`CommonProgramFiles`、`COMPUTERNAME`… 全都没有
    （对照：explorer.exe 的环境块里这些都在，实测脚本 $env:TEMP\aw_pebenv*.ps1）。

    Unity 的包管理服务端（UPM server，Node 写的）里有这么一行：
        node_modules/@upm/core/lib/private/configuration/upm-config.js
        function getGlobalConfigRoot(){ ... path.join(process.env.ALLUSERSPROFILE,"Unity/config") }
    变量缺失 -> Node 抛 `The "path" argument must be of type string. Received undefined`
    -> 每个 UPM 请求都 500 -> 编辑器控制台满屏：
        [Package Manager] The "path" argument must be of type string. Received undefined
        [Package Manager Window] Error fetching package list.
    现场证据：`%LOCALAPPDATA%\Unity\Editor\upm.log` 里 `config:project:get-registries --> 500`，
    而正常时同一行是 `--> 200`。
    从资源管理器 / Unity Hub 启动**不会**有这个问题（那两处的环境是完整的），
    所以：**凡是从 Codex 的 shell 里拉起 Unity，都走这个脚本。**

    另一条别再踩的：UPM server「每个编辑器一个、父编辑器一死就自杀」——
        upm.log: Shutting down UnityPackageManager.exe: parent process [PID] is no longer running.
    所以「杀多余的 Unity 进程」会把**还在跑的那台**的包管理器一起带走（症状和上面一样，
    但 `Editor.log` 里连 `UpmClient::Connect` 都没有）。要换编辑器：先正常关旧的，再拉新的。

.PARAMETER ProjectPath
    工程根目录（默认本仓库）。

.PARAMETER UnityExe
    Unity 编辑器可执行文件（默认 2022.3.62f2c1 China 版）。

.PARAMETER Force
    已经在跑也强制换一台（会先 Stop-Process 并清 `Temp\UnityLockfile`；工程有未保存改动时别用）。

.EXAMPLE
    pwsh -NoProfile -File Tools\LaunchUnity.ps1
    pwsh -NoProfile -File Tools\LaunchUnity.ps1 -Force

.NOTES
    启动后自检：`%LOCALAPPDATA%\Unity\Editor\upm.log` 里不应再出现 `--> 500`，
    `Editor.log` 里应有 `[Package Manager] UpmClient::Connect` 且**没有**那句 "path" argument 报错。
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\Users\22589\Documents\GitHub\Another-World',
    [string]$UnityExe    = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $UnityExe)) { throw "找不到 Unity.exe：$UnityExe" }
if (-not (Test-Path -LiteralPath $ProjectPath)) { throw "找不到工程目录：$ProjectPath" }

# ---- 1) 已开着的编辑器：默认不抢（UPM server 是随父编辑器生死的一条命） ----
$running = @(Get-Process Unity -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    if (-not $Force) {
        Write-Host "已有 Unity 在跑（PID $(($running | ForEach-Object Id) -join ', ')），不重复拉起。要强制换一台加 -Force。" -ForegroundColor Yellow
        return
    }
    foreach ($proc in $running) {
        Write-Host "停止 Unity PID $($proc.Id)" -ForegroundColor Yellow
        Stop-Process -Id $proc.Id -Force
    }
    Start-Sleep -Seconds 5
    $lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $lock) { [IO.File]::Delete($lock) }
}

# ---- 2) 补齐沙箱里缺的机器级变量（值取自 explorer.exe 的环境块实测） ----
# 只补「当前为空」的，已存在的不动；作用域是 Process —— 只影响本脚本拉起的进程树，不落盘、不改系统。
$machineVars = [ordered]@{
    'ALLUSERSPROFILE'         = 'C:\ProgramData'                          # ← UPM 那句报错的直接开关
    'PUBLIC'                  = 'C:\Users\Public'
    'ProgramData'             = 'C:\ProgramData'
    'CommonProgramFiles'      = 'C:\Program Files\Common Files'
    'CommonProgramFiles(x86)' = 'C:\Program Files (x86)\Common Files'
    'CommonProgramW6432'      = 'C:\Program Files\Common Files'
    'DriverData'              = 'C:\Windows\System32\Drivers\DriverData'
    'OS'                      = 'Windows_NT'
    'COMPUTERNAME'            = [Environment]::MachineName
    'LOGONSERVER'             = '\\' + [Environment]::MachineName
    'SESSIONNAME'             = 'Console'
}
$patched = New-Object System.Collections.Generic.List[string]
foreach ($name in $machineVars.Keys) {
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name))) {
        [Environment]::SetEnvironmentVariable($name, [string]$machineVars[$name])
        $patched.Add($name)
    }
}
if ($patched.Count -gt 0) { Write-Host "补齐环境变量（$($patched.Count)）：$($patched -join ', ')" -ForegroundColor Cyan }
else { Write-Host '环境变量无需补齐。' -ForegroundColor Cyan }

# ---- 3) 拉起（可见窗口，供人操作；不主动抢焦点） ----
Write-Host "启动 Unity：$UnityExe -projectPath `"$ProjectPath`"" -ForegroundColor Green
$proc = Start-Process -FilePath $UnityExe -ArgumentList @('-projectPath', $ProjectPath) -PassThru
Write-Host "已启动 PID $($proc.Id)。自检：`n  get-content `"$env:LOCALAPPDATA\Unity\Editor\upm.log`"   # 不应有 --> 500"