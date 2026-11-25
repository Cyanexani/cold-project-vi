# Menu de lancement de VIctory : choisir un mode, le jeu demarre avec ses reglages.
# Le journal de la partie va dans logs\partie-<mode>-<date>.txt.
# Pas 'Stop' : PowerShell 5.1 prend chaque ligne qu'un programme écrit sur
# sa sortie d'erreur pour une erreur, et le script (et le jeu) s'arrêtaient.
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot
$exe = Join-Path $PSScriptRoot 'target\diag\victory.exe'
if (-not (Test-Path $exe)) {
    Write-Host "target\diag\victory.exe introuvable : compiler d'abord avec" -ForegroundColor Red
    Write-Host "  cargo build --profile diag -p victory --features diagnostics"
    Read-Host 'Entrée pour fermer'; exit 1
}
# Le jeu : game_dir de victory.toml, ou VICTORY_GAME_DIR.
$dir = $env:VICTORY_GAME_DIR
if (-not $dir -and (Test-Path 'victory.toml')) {
    $line = Select-String -Path 'victory.toml' -Pattern "^\s*game_dir\s*=\s*['""](.+)['""]" | Select-Object -First 1
    if ($line) { $dir = $line.Matches[0].Groups[1].Value }
}
$game = if ($dir) { Join-Path $dir 'game_scarlett_master.exe' } else { $null }
if (-not $game -or -not (Test-Path $game)) {
    Write-Host "Jeu introuvable : mettre game_dir dans victory.toml (modèle : victory.example.toml)." -ForegroundColor Red
    Read-Host 'Entrée pour fermer'; exit 1
}

# Chaque mode : nom, explication, console, réglages.
$modes = @(
    @{ Nom = 'Normal (recommandé)'; Info = 'Series S, réglages par défaut (données sur la carte en priorité).'; Console = 'series-s'; Env = @{} },
    @{ Nom = 'Normal + copie rapide'; Info = 'Copie de la mémoire du jeu sans passer par le noyau (en essai).'; Console = 'series-s'; Env = @{ VICTORY_COPY_PLAIN = '1' } },
    @{ Nom = 'Demi-résolution'; Info = 'Le jeu rend à 50 % puis agrandit : image plus floue, carte moins chargée.'; Console = 'series-s'; Env = @{ VICTORY_RENDER_SCALE = '0.5'; VICTORY_OWN_UPSCALER = '1' } },
    @{ Nom = 'Très basse résolution (test de fps max)'; Info = 'Rendu à 30 % : moche, pour voir combien de fps le PC peut atteindre.'; Console = 'series-s'; Env = @{ VICTORY_RENDER_SCALE = '0.3'; VICTORY_OWN_UPSCALER = '1' } },
    @{ Nom = 'Économie de textures'; Info = 'Moins de textures gardées sur la carte (50 % de sa mémoire) : textures plus floues, moins de mémoire.'; Console = 'series-s'; Env = @{ VICTORY_TEXTURE_ROOM_PERCENT = '50' } },
    @{ Nom = 'Anciens réglages mémoire'; Info = "Comme avant le 25/11 : textures d'abord sur la carte, sommets et constantes en RAM."; Console = 'series-s'; Env = @{ VICTORY_CARD_WINDOWS_PERCENT = '72'; VICTORY_CARD_WINDOWS_MB = '1536'; VICTORY_CARD_BUFFERS_MB = '2048'; VICTORY_CARD_VRAM_PERCENT = '92' } },
    @{ Nom = 'Éclairage de remplacement'; Info = 'Lumière ambiante et ombres lointaines de remplacement (zones sombres moins noires).'; Console = 'series-s'; Env = @{ VICTORY_AMBIENT_STUB = '1'; VICTORY_SHADOWMASK_STUB = '1' } },
    @{ Nom = 'Tout pour la vitesse'; Info = 'Demi-résolution + éclairage de remplacement + économie de textures + copie rapide.'; Console = 'series-s'; Env = @{ VICTORY_RENDER_SCALE = '0.5'; VICTORY_OWN_UPSCALER = '1'; VICTORY_AMBIENT_STUB = '1'; VICTORY_SHADOWMASK_STUB = '1'; VICTORY_TEXTURE_ROOM_PERCENT = '50'; VICTORY_COPY_PLAIN = '1' } },
    @{ Nom = 'Series X'; Info = 'Profil Series X : plus lourd, demande plus de mémoire.'; Console = 'series-x'; Env = @{} },
    @{ Nom = 'Mesure de la carte (diagnostic)'; Info = 'Chronomètre chaque commande de la carte : plus lent, pour les journaux.'; Console = 'series-s'; Env = @{ VICTORY_GPU_TIMING = '1' } },
    @{ Nom = 'Sans compteur'; Info = 'Normal, sans le compteur en haut à gauche.'; Console = 'series-s'; Env = @{ VICTORY_HUD = '0' } }
)

Clear-Host
Write-Host ''
Write-Host '   VIctory - GTA VI (Xbox Series) sur PC' -ForegroundColor Yellow
Write-Host '   ------------------------------------' -ForegroundColor Yellow
Write-Host ''
for ($i = 0; $i -lt $modes.Count; $i++) {
    Write-Host ("   {0,2}. {1}" -f ($i + 1), $modes[$i].Nom) -ForegroundColor White
    Write-Host ("       {0}" -f $modes[$i].Info) -ForegroundColor DarkGray
}
Write-Host ''
Write-Host '    P. Préchauffer les shaders (après une mise à jour)' -ForegroundColor White
Write-Host '    Q. Quitter' -ForegroundColor White
Write-Host ''
$choice = Read-Host '   Ton choix'
if ($choice -match '^[qQ]$') { exit 0 }
if ($choice -match '^[pP]$') {
    Write-Host 'Préchauffage des shaders : quelques minutes la première fois.'
    & $exe translate --cache 2>&1 | Out-Host
    Read-Host 'Entrée pour fermer'; exit 0
}
$n = 0
if (-not [int]::TryParse($choice, [ref]$n) -or $n -lt 1 -or $n -gt $modes.Count) {
    Write-Host 'Choix inconnu.' -ForegroundColor Red; Read-Host 'Entrée pour fermer'; exit 1
}
$mode = $modes[$n - 1]

# Les réglages d'un lancement précédent ne restent pas.
Get-ChildItem env: | Where-Object Name -like 'VICTORY_*' | Where-Object Name -ne 'VICTORY_GAME_DIR' | ForEach-Object { Remove-Item "env:$($_.Name)" }
foreach ($k in $mode.Env.Keys) { Set-Item "env:$k" $mode.Env[$k] }

New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'logs') | Out-Null
$slug = ($mode.Nom -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLower()
$log = Join-Path $PSScriptRoot ("logs\partie-{0}-{1}.txt" -f $slug, (Get-Date -Format 'yyyyMMdd-HHmm'))
Write-Host ''
Write-Host ("Lancement : {0}" -f $mode.Nom) -ForegroundColor Green
foreach ($k in $mode.Env.Keys) { Write-Host ("  {0}={1}" -f $k, $mode.Env[$k]) -ForegroundColor DarkGray }
Write-Host "À l'écran titre : A (ou Entrée). Journal : $log"
# Lancé à part, le journal écrit par Windows : PowerShell 5.1 arrêtait le
# script à la première ligne du jeu sur sa sortie d'erreur (et le jeu avec).
$ErrorActionPreference = 'Continue'
$p = Start-Process -FilePath $exe -ArgumentList 'run', ('"{0}"' -f $game), '--execute', '--console', $mode.Console `
    -WorkingDirectory $PSScriptRoot -RedirectStandardError $log -RedirectStandardOutput ($log + '.out') -NoNewWindow -PassThru
$p.WaitForExit()
Write-Host ''
Write-Host "Le jeu est fermé. Journal : $log"
Read-Host 'Entrée pour fermer'
