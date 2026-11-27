Julia's Launcher 0.3.1: the game starts again when the Windows user name has accents.

- Fix: with a user name like "élève", a damaged cache path (C:\Users\Ã©lÃ¨ve...) in launcher.ini stopped the game at once ("Access denied"), whatever the preset. The launcher now repairs such paths on its own.
- With accents in the user folder, the shader cache goes to C:\VIctory\cache (a plain path, as the game needs). Prepare the shaders once more after updating.
- French: a new FR button next to EN and PT (since 0.3.0).
