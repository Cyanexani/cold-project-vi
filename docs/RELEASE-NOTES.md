Julia's Launcher 0.3.9: the rainbow-texture fix for real, the 720p option no longer closes the game, and Resizable BAR in the NVIDIA driver turns on.

- **Rainbow textures, for real this time.** 0.3.7 announced the fix, but it was left out of that build by mistake: sorry. It is in 0.3.9. A texture's staging memory, reused from another texture, is now emptied before use, so a texture that is not read yet shows nothing for a moment instead of another texture's bytes.

- **"720p" closed the game at start (0.3.6 and 0.3.7).** The game stops with its own fatal error when the console says the screen is 1280x720, and Belobug's AMD engine crashed with a 720p output. Every session at 720p ended that way. The option is now **World at 720p (lighter)**: the screen stays 1080p and the world is drawn at 720p at most. If you picked 720p, just press PLAY again.
- **"Resizable BAR in the NVIDIA driver" stayed red.** NVIDIA's driver lets only administrators change its Resizable BAR settings, so "Turn on" was refused without a word. Windows now asks for administrator rights once, and the step turns green.
- The Discord post links this release's own page (it showed the previous version's card).

Is it safe? [docs/SAFETY.md](https://github.com/felicienradio/cold-project-vi/blob/main/docs/SAFETY.md) · [Code signing policy](https://github.com/felicienradio/cold-project-vi/blob/main/docs/CODE-SIGNING-POLICY.md)
