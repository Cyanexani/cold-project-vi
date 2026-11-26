@echo off
rem O launcher do cgbj tal e qual (Play-VIctory.cmd pareto-residency), so com a pasta do jogo
rem e a cache ja traduzida neste PC (a dele por defeito comecaria vazia: horas de shaders).
setlocal
set "VICTORY_GAME_DIR=%USERPROFILE%\Downloads\dump private build"
set "VICTORY_CACHE_DIR=%USERPROFILE%\.codex\visualizations\2026\11\22\01a2020d-43d5-7ec3-a9e8-d56142dd6f82\victory-xtx-run\cache-cgbj-release"
call "%~dp0Play-VIctory.cmd" pareto-residency
exit /b %errorlevel%
