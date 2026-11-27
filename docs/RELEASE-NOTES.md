Julia's Launcher 0.2.2

- 8 GB cards: "Crowds and fights" no longer puts 2 GB of the game's buffers on cards under 11 GB. It overfilled video memory and the game ran at about 3 FPS.
- A game folder with only the executable (not the ~100 GB of game data) is named before PLAY; the game used to close at once.
- Shaders count as prepared only when the game's shaders really were (a folder with 12 showed "ready").
- Radeon: if the edupode engine ends at start twice in a row (a "device removed" on an RX 6800), the launcher switches to Belobug's AMD engine on its own.

Julia's Launcher 0.2.1

- The edupode engine is the default for every card (NVIDIA, AMD, Intel): video memory from ~12 GB down to 6-8 GB. Belobug's VIctory-Belobug-AMD stays as an alternative for Radeon cards, with all credit to Belobug.
- ★ More CPU cores, on by default: half your cores copy the game's memory. +1 FPS in the open world and 2.3 ms less preparing each frame on the author's PC.
- Output resolution: 1080p, 1440p (2K) or 2160p (4K), beside the presets. 2K and 4K are new: tell us how they run.
- Send errors: "Send to edupode" sends the report in one click to a private box only edupode can read; "Open a GitHub issue" opens an issue already filled in; Discord as before.
- No more false "not enough memory" warning before PLAY: Windows-managed virtual memory that can still grow now counts.
- AMD cards: PLAY waits while Setup is still preparing shaders (starting the game then made it end at start), and NVIDIA-only steps are no longer tried.
- Bug reports: after "Send to edupode" your hardware ID is copied; paste it on Discord so edupode can find your reports.
- Updates: the newest signed release of the repository is found whatever branch it comes from.
