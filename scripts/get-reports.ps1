# Bug reports players sent from the launcher ("Send to edupode"), read from the Cloudflare D1 database with
# your own wrangler login (nobody else can read them: the Worker has no route that gives them out).
#   scripts\get-reports.ps1            lists the latest 30
#   scripts\get-reports.ps1 -Get 12    saves report 12 as reports\report-12-<id>.zip and opens the folder
#   scripts\get-reports.ps1 -Delete 12 deletes report 12
param([int]$Get = 0, [int]$Delete = 0, [int]$Last = 30)
$ErrorActionPreference = 'Stop'
$worker = Join-Path $PSScriptRoot '..\server\hardware-worker'
function D1([string]$sql) {
    Push-Location $worker
    try { $out = npx --yes wrangler d1 execute vi-hardware --remote --json --command $sql 2>$null | Out-String }
    finally { Pop-Location }
    $json = $out.Substring($out.IndexOf('['))
    return (ConvertFrom-Json $json)[0].results
}
if ($Delete -gt 0) {
    D1 "DELETE FROM reports WHERE n = $Delete" | Out-Null
    "Report $Delete deleted."
    return
}
if ($Get -gt 0) {
    $row = D1 "SELECT n, vi_id, kind, hex(zip) AS z FROM reports WHERE n = $Get"
    if (-not $row) { throw "No report $Get." }
    $hex = $row[0].z
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16) }
    $dir = Join-Path $PSScriptRoot '..\reports'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $file = Join-Path $dir ("report-{0}-{1}-{2}.zip" -f $row[0].n, $row[0].vi_id, $row[0].kind)
    [IO.File]::WriteAllBytes($file, $bytes)
    "Saved $file"
    Start-Process explorer.exe "/select,`"$((Resolve-Path $file).Path)`""
    return
}
D1 "SELECT n, created, vi_id, kind, bytes FROM reports ORDER BY n DESC LIMIT $Last" | Format-Table -AutoSize
