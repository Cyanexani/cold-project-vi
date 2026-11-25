@echo off
rem Menu de lancement de VIctory : double-cliquer.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Lancer.ps1"
