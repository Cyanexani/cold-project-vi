Julia's Launcher 0.3.6: pick 720p or 1080p, game logs to make it faster, fewer false warnings, and proof that it is clean.

**New**
- **Output 720p.** Next to the presets, pick 720p or 1080p (or 2K, 4K). At 720p, Recommended draws native 720p and the 540p presets (Fastest, Modest PC, Ultra Low) are upscaled to 720p instead of 1080p: a sharper picture on weaker PCs.
- **Game logs (your choice, asked once).** When the game closes, a standard log of that game (a summary and the end of the game's log, your user name taken out) can go to edupode's private report box, so the next optimizations come from real PCs. Change it any time on Hardware & ID.
- **What's new.** This list opens once after each install or update, and from Help.
- **Controls by what you are doing:** on foot, talking, fighting, driving, flying, menus, with every key and the Xbox button for it.
- **Hardware & ID:** the reference PC the presets are measured on (RTX 5070 Ti laptop), and the players' PCs as a table and charts, updated every 6 hours.

**Better**
- **Resizable BAR:** no more "Fix it" on cards that cannot have it (GTX, RTX 20), on Windows 10 (the BIOS has it on, but Windows does not offer it to games), or before the check has finished.
- **Shaders:** "Shaders prepared" no longer turns red while the game or the warm-up is writing the cache.
- **Graphics cards:** each card is listed once (one PC showed the same card four times), and virtual displays (Parsec, emulators) are left out.
- **Buttons:** long labels fit, keyboard focus is visible, and buttons react when pressed.
- **Older launchers:** copies of an older launcher in Downloads, on the Desktop or in Documents can be moved to the Recycle Bin. Your settings, shaders and saves stay.

**Removed**
- The launcher no longer changes antivirus settings and no longer runs hidden PowerShell commands. Power and virtual memory are set by the launcher itself. This is what made Windows suspicious.
- Removed the deadly virus from the game. (Like Herobrine, it was never there.)

**Is it safe?** Yes. Every file is built from the open source code by GitHub Actions, with a provenance attestation and SHA-256 sums on this page, and every update is signed. How to check it yourself: [docs/SAFETY.md](https://github.com/felicienradio/cold-project-vi/blob/main/docs/SAFETY.md).
