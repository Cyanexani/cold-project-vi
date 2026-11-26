@echo off
rem Test de config : lance le jeu, mesure 5 minutes dans le monde, fait un rapport dans rapports\.
rem Config test: runs the game, measures 5 minutes in the world, writes a report in rapports\.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tester.ps1" %*
