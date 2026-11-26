@echo off
rem O launcher do cgbj tal e qual (Play-VIctory.cmd pareto-residency, sem alteracoes), com:
rem  - a build deste codigo que ja tem cache quente (shaders traduzidos e pipelines feitos):
rem    a release comecava com a cache vazia e ficava a 1 FPS a traduzir durante o jogo;
rem  - a pasta do jogo, a cache e os saves deste PC;
rem  - um log em victory-xtx-run\logs\cgbj-*.log (linhas de FPS e avisos).
setlocal
set "RUN=%USERPROFILE%\.codex\visualizations\2026\11\22\01a2020d-43d5-7ec3-a9e8-d56142dd6f82\victory-xtx-run"
set "VICTORY_GAME_DIR=%USERPROFILE%\Downloads\dump private build"
set "VICTORY_CACHE_DIR=%RUN%\cache"
set "VICTORY_DATA_DIR=%RUN%\data"
set "VICTORY_LOG=warn,victory_gpu::host=info,victory_gpu::copystat=info"
tasklist /FI "IMAGENAME eq victory.exe" | find /I "victory.exe" >nul && (
  echo Ja ha um VIctory a correr. Fecha-o primeiro ^(ou carrega em H no ciclo^).
  pause
  exit /b 1
)
if exist "%~dp0target\diag\victory.exe" copy /y "%~dp0target\diag\victory.exe" "%~dp0victory-3d-rtx-pareto-residency.exe" >nul
for /f %%t in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "STAMP=%%t"
set "LOG=%RUN%\logs\cgbj-%STAMP%.log"
echo A jogar com o launcher do cgbj. Log: %LOG%
call "%~dp0Play-VIctory.cmd" pareto-residency > "%LOG%" 2>&1
echo Terminado. Log: %LOG%
exit /b %errorlevel%
