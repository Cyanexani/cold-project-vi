Julia's Launcher 0.3.2-edupode: felicienradio's 0.3.1 with edupode's fixes on top.

From 0.3.1 (felicienradio):
- The game starts again when the Windows user name has accents: damaged cache paths in launcher.ini are repaired on their own, and with accents in the user folder the shader cache goes to C:\VIctory\cache. Prepare the shaders once more after updating.
- French: an FR button next to EN and PT.

edupode's fixes from players' reports:
- 8 GB cards: "Crowds and fights" no longer puts 2 GB of the game's buffers on cards under 11 GB. It overfilled video memory and the game ran at about 3 FPS.
- A game folder with only the executable (not the ~100 GB of game data) is named before PLAY; the game used to close at once.
- Shaders count as prepared only when the game's shaders really were (a folder with 12 showed "ready").
- Radeon: if the edupode engine ends at start twice in a row (a "device removed" on an RX 6800), the launcher switches to Belobug's AMD engine on its own.
- PLAY waits while Setup is still preparing shaders, and NVIDIA-only steps are not tried on other cards.
- The edupode engine is the default for every card; Belobug's VIctory-Belobug-AMD stays the Radeon alternative (all credit to Belobug).
- More CPU cores, on by default (+1 FPS in the open world on the author's PC).
- Output resolution: 1080p, 1440p (2K) or 2160p (4K).
- Send errors: "Send to edupode" (a private report box), a GitHub issue filled in, or Discord; your hardware ID is copied to paste on Discord.
- No more false "not enough memory" warning before PLAY.
