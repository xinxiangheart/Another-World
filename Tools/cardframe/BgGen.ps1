# 背景出图：直连 ofoxai（OpenAI 兼容 /images/generations），PowerShell 版
function New-BgImage([string]$prompt, [string]$outPath, [string]$size = "1344x768", [string]$model = "qwen/qwen-image-3.0-pro") {
  $cfg = Get-Content "$env:USERPROFILE\.codex\imagegen-compat.json" -Raw | ConvertFrom-Json
  $p = $cfg.providers.ofoxai
  $body = @{ model = $model; prompt = $prompt; size = $size; n = 1 } | ConvertTo-Json -Depth 5
  $r = Invoke-RestMethod -Method Post -Uri ($p.base_url + "/images/generations") -Headers @{ Authorization = "Bearer " + $p.api_key } -ContentType "application/json" -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 300
  $d = $r.data[0]
  if ($d.b64_json) {
    [IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $outPath -Parent)).Path + "\" + (Split-Path $outPath -Leaf), [Convert]::FromBase64String($d.b64_json))
  } elseif ($d.url) {
    Invoke-WebRequest -Uri $d.url -OutFile $outPath -UseBasicParsing -TimeoutSec 300
  } else {
    throw "unexpected payload: $($r | ConvertTo-Json -Depth 4 -Compress)"
  }
  return $outPath
}
