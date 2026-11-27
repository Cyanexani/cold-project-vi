**Julia's Launcher {tag} is out** 🔧
**Fixes**
🛠️ 8 GB cards (RTX 4060, 3060 Ti...) ran at 3-15 FPS: "Crowds and fights" put 2 GB of the game's buffers on cards under 11 GB and video memory spilled into RAM. Only 11 GB+ cards get it now.
📁 A game folder with only the .exe (without the ~100 GB of game data) is flagged before PLAY, instead of the game closing at once.
🎮 Radeon: if the edupode engine fails to start twice in a row, the launcher switches to Belobug's AMD engine on its own.
✅ PLAY waits while shaders are still being prepared, and shaders only show "ready" when they really are.
📨 Send errors > **Send to edupode** sends your report in one click; then paste your hardware ID here (it is copied for you).
➕ Everything from v0.3.1 too: accented Windows user names, French.

⚠️ **Install it from zero**
1. Close the launcher and the game. Uninstall the old one (Windows Settings > Apps > Julia's Launcher) and delete its folder (C:\Games\VI).
2. Download **VI-Setup-{version}.exe** from https://github.com/edupode/cold-project-vi/releases/latest and run it.
3. **Setup > Do everything for me**: it prepares the shaders again (about 5 minutes, ~12 GB free on C:). Then hit PLAY.
Your saves stay: they are in %LOCALAPPDATA%\VIctory, not in the launcher's folder.

Still stuck? Use **Help > Send errors** in the launcher 💙
