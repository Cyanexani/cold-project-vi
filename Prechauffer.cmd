@echo off
rem Traduit tous les shaders du jeu dans le cache AVANT de jouer, pour que le
rem jeu ne le fasse pas en pleine partie. A relancer apres chaque mise a jour
rem du traducteur de shaders (les shaders deja dans le cache sont sautes vite).
rem Le jeu est pris dans victory.toml (game_dir) ou VICTORY_GAME_DIR.
cd /d "%~dp0"
if not exist target\diag\victory.exe (
  echo target\diag\victory.exe introuvable : compiler d'abord avec
  echo   cargo build --profile diag -p victory --features diagnostics
  pause
  exit /b 1
)
echo Prechauffage des shaders : quelques minutes la premiere fois.
target\diag\victory.exe translate --cache %*
echo.
pause
