Julia's Launcher 0.3.5: the launcher finds a complete game dump, and says what is missing.

- Auto-detect looks deeper and picks a folder where the game's files (shaders.rpf, levels.rpf...) sit beside game_scarlett_master.exe. A lone executable (for example in Downloads among other files) is only taken when nothing better is found, with a message saying what to move and where.
- Before PLAY: the same message when the game's files are not beside the executable.
- Preparing shaders: fewer than 1,000 translated shaders (a complete dump gives about 25,000) now fails with the reason, instead of saying "Shaders ready".
