VI Launcher 0.2.0: the big revamp.

- New look (Cold Project style). New pages: Shaders (live), Hardware & ID, Updates, Advanced Tweaks.
- Installer: one administrator prompt, then Windows is set up in the background. Nothing to compile.
- Updates: checked on GitHub every 5 minutes, signed, installed on their own while the game is closed.
- Fixes:
  - PLAY no longer gets stuck behind a crashed game. It shows CLOSE AND PLAY instead.
  - "could not find Cargo.toml" is gone when the launcher is outside the source folder.
  - Resizable BAR is detected the way the game sees it.
  - Virtual memory turns on reliably.
  - Dumps with several game executables work.
  - Settings are no longer lost.
- New presets: Ultra Low (very little memory) and Custom, with profile codes to copy and paste.
- AMD cards: detected on their own; they play with Belobug's VIctory-Belobug-AMD (all credit to Belobug).
- Send errors to Discord: makes the package for the "Send bugs here please" channel.
- Hardware sharing (off by default): only hardware, to a database only edupode can read.
