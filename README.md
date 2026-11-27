<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&height=180&color=0:ff5fa2,50:ffc371,100:5f6dff&text=Cold%20Project%20VI&fontColor=ffffff&fontSize=54&fontAlignY=36&desc=edupode%20remake%20%E2%80%94%20GTA%20VI%20(Xbox%20Series)%20on%20an%20RTX%20laptop&descAlignY=58&animation=fadeIn" alt="Cold Project VI - edupode remake" width="100%"/>

<a href="#where-it-stands"><img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=20&pause=1200&color=FF5FA2&center=true&vCenter=true&width=680&lines=Open+world+22-26+fps%2C+cutscenes+35%2B%2C+menus+60;NVIDIA+Smooth+Motion+set+up+for+you;Pipelines+loaded+before+the+game+opens;VI+Launcher%3A+one+window+to+build+and+play;Missions+playable%2C+long+sessions+without+crashes" alt="animation"/></a>

<p>
  <a href="#quick-start-vi-launcher"><img src="https://img.shields.io/badge/%F0%9F%8E%AE%20VI%20Launcher-start%20here-ff5fa2?style=for-the-badge" alt="VI Launcher"/></a>
  <a href="docs/GUIDE-30FPS.md"><img src="https://img.shields.io/badge/Guide-English-C8102E?style=for-the-badge" alt="Guide"/></a>
  <a href="README.pt.md"><img src="https://img.shields.io/badge/Leia%20em-Portugu%C3%AAs-006600?style=for-the-badge" alt="Portugues"/></a>
  <a href="https://github.com/edupode"><img src="https://img.shields.io/badge/by-%40edupode-222?style=for-the-badge&logo=github" alt="by edupode"/></a>
</p>

<p>
  <img src="https://img.shields.io/badge/fps-22--26%20open%20world%20%7C%2060%20menus-ff5f6d?style=flat-square" alt="fps"/>
  <img src="https://img.shields.io/badge/VRAM-6--8%20GB%20of%2012-ffc371?style=flat-square" alt="VRAM"/>
  <img src="https://img.shields.io/badge/RAM-~20%20GB-ffc371?style=flat-square" alt="RAM"/>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=flat-square&logo=windows" alt="Windows"/>
  <img src="https://img.shields.io/badge/GPU-NVIDIA%20RTX-76B900?style=flat-square&logo=nvidia" alt="NVIDIA RTX"/>
  <img src="https://img.shields.io/badge/Rust-pinned-B7410E?style=flat-square&logo=rust" alt="Rust"/>
  <img src="https://img.shields.io/badge/license-GPL--3.0-845ec2?style=flat-square" alt="GPL-3.0"/>
</p>

<p>
  <a href="#quick-start-vi-launcher"><img src="https://img.shields.io/badge/%E2%96%B6%20Run-222?style=for-the-badge" alt="Run"/></a>
  <a href="#modes"><img src="https://img.shields.io/badge/%E2%9A%A1%20Modes-222?style=for-the-badge" alt="Modes"/></a>
  <a href="#controls"><img src="https://img.shields.io/badge/%F0%9F%95%B9%20Controls-222?style=for-the-badge" alt="Controls"/></a>
  <a href="#where-it-stands"><img src="https://img.shields.io/badge/%F0%9F%93%8B%20Status-222?style=for-the-badge" alt="Status"/></a>
</p>

</div>

> [!NOTE]
> **This is a remake by [@edupode](https://github.com/edupode)** of the Cold Project VI idea, internally
> called **"luz"** (Portuguese for *light*, after the lighting work it started with). It combines
> [VIctory](https://github.com/Marvisak/VIctory), the cgbj build (cputroll/cgbj-reupload), Leonidas's and
> AnyXboxSeries's work, the VIctory-NVIDIA fork (trustno1-coder), fxceless's rendering fixes and adaptations from an AMD fork
> (avatarmanz/VIctory-7900XTX), tuned and extended for an NVIDIA laptop. It is not the upstream Cold Project
> VI (felicienradio/cold-project-vi), whose page inspired this one.

> [!IMPORTANT]
> **This repository contains no game files**: no executable, data or extracted shaders. You need your own copy
> of the game. Not affiliated with or endorsed by the game's developer or publisher.

### From the author

> It runs 24-26 fps, all controls have been mapped,currently using -locallights  
> On the Open world i get about 22-24fps  
> Everything seems to be loading ok  
> I need help to fix volumetric shaders _Tiled (i already know how to fix them but it will take some time)  
> It's running on 12gb vram and 32gb ddr5  
> Currently it uses about 6-8gb on vram and 20-21gb of ddr5  
> it used to use 10-12gb on vram and 27-29gb of ram of ddr5  
> I got a laptop with a rtx 5070 ti capped at 145w and a Intel i9 ultra 275hx  
> Hope this helps anyone, missions are already possible and doesn't crash even after long sessions  
> on menus it goes from 30-32/33fps now its 60 fixed  

GTA VI from **your own Xbox Series dump**, on Windows with an NVIDIA RTX card, through
[VIctory](https://github.com/Marvisak/VIctory) (this branch: the cgbj build with the VIctory-NVIDIA
lighting and many changes of our own). **No game files are included.**

## Quick start: VI Launcher

1. Get this branch (`git clone`, or **Code → Download ZIP** on GitHub) and build the launcher; it needs nothing
   but Windows:
   ```powershell
   git clone -b edupode https://github.com/edupode/cold-project-vi.git
   cd cold-project-vi
   launcher\build-launcher.cmd
   ```
2. Open **`VI Launcher.exe`** and press **⚡ First-time auto-setup** (or the **Setup** page). It installs Rust and the
   C++ Build Tools with winget, builds VIctory with a progress bar, adds the Defender exception (Windows asks
   first), finds your game folder on its own, prepares the shaders, sets Windows to best performance and
   system-managed virtual memory, and turns Smooth Motion on. By hand, the same steps are:
   - **Build VIctory** (once, several minutes);
   - choose your **game folder** (the one with `game_scarlett_master.exe`);
   - **Prepare shaders** (about 5 minutes; again after each rebuild);
   - press **★ Recommended setup** and **PLAY**.

If Windows says "Access denied", Defender's ransomware rule blocked the new exe. In an administrator
PowerShell: `Add-MpPreference -AttackSurfaceReductionOnlyExclusions "<repo folder>\target"`.

<p align="center"><img src="launcher/screenshot.png" alt="VI Launcher" width="760"/></p>

The launcher checks your GPU, video memory, RAM, Resizable BAR and Smooth Motion support and sets everything
else. Point at any option to see what it does; **★** marks what was tested on the author's PC. On cards with
less than 10 GB or PCs with less than 24 GB of RAM it picks the **Modest PC** mode.
English by default, **Português** in the side bar. `Jogar-Luz.cmd` does the same from a command line
(`Jogar-Luz.cmd tudo`).

### What the launcher does for you

| | |
|---|---|
| **Loading bar before the game** | Before the game opens (before the first menu), every pipeline earlier sessions drew with is made in separate processes, so the driver already has them: no stutter the first time you see a place. Seconds once the cache is ready. |
| **NVIDIA Smooth Motion** | Written into the driver's own profile for `victory.exe` (as NVIDIA Profile Inspector does): **no NVIDIA App needed**. RTX 40/50, driver 571.86+. The card shows a generated frame between two of the game's. |
| **Performance page** | Lists the apps holding your video memory and RAM (NVIDIA Overlay, Discord, Wallpaper Engine, browsers...) and closes the ones you tick. Windows only gives the game what other apps leave free. |
| **Video memory reserve** | VIctory asks Windows to reserve video memory for itself, so other apps are moved out first when memory runs short. |
| **Setup page** | Every first-run step checked (✓/✗) with a button that does it, and **Do everything for me**. Resizable BAR lives in the BIOS/UEFI: the guide checks UEFI boot, shows where the options are and can restart into the BIOS (after a yes). |
| **FPS overlay (F3)** | FPS now, the last minute's average (AVG 1M), the whole run's (ALL) and the worst half second (LOW), frame time, draws, video memory and RAM. |

## Modes

| Mode | What it does | RTX 5070 Ti laptop |
|---|---|---|
| **★ Recommended - 720p** (+ ★ crowds and fights: the author's daily setup) | World at 1280x720 + the game's own upscaler to 1080p, texture levels held, 60 FPS clock, borderless | open world 22-26 FPS, crowds 14-17, cutscenes 35+, menus 60 |
| Fastest - 540p | Softer picture, most frames; for slower CPUs | a few FPS more |
| Modest PC - 540p, light | For 6-8 GB cards and 16 GB of RAM: less video memory and RAM | |
| Sharp - 1080p | Full resolution | slower in the open world |
| Classic cgbj | The original cgbj settings | for comparison |

Options: ★ FPS overlay (F3), ★ borderless full screen (test A: 22-24 FPS on the author's PC; needed by Smooth
Motion), ★ the 60 FPS clock, pipelines loaded before the game (new), NVIDIA Smooth Motion, the video memory
reserve, crowds and fights (2 GB of the game's buffers on the card; 12 GB cards and up), anti-stretch (fewer
stretched bodies, slower), rainbow-face fix (slower), fast loading (experimental).

## Controls

Keyboard and mouse are mapped to the Xbox pad (a connected controller replaces them). Click the game window
to capture the mouse (Alt/F1 releases it). **F** enters vehicles and switches W/S to accelerate/brake (high
beep); in aircraft hold **W** (throttle) and **K** (nose up) to take off, **I** for nose down. Full table:
the launcher's Controls tab or [docs/GUIDE-30FPS.md](docs/GUIDE-30FPS.md).

## Before and after

| | Before (cgbj on this laptop) | Now (VI Launcher, Recommended) |
|---|---|---|
| Menus and map | 30-33 FPS | **60 FPS** |
| Open world | 10-15 FPS | **22-26 FPS** |
| Cutscenes | ~26 FPS | **35-38 FPS** |
| Video memory | ~12 GB (over the system's budget) | **6-8 GB** |
| RAM | 27-29 GB | **20-21 GB** |

## FAQ

**The first cutscene lags a lot.** On the first run the game draws everything for the first time: thousands of
pipelines are made and the textures of the whole scene stream in. It runs at 7-15 FPS there, dropping to 2 now
and then, and then settles at 21-25. Press *Prepare shaders* in the launcher first; each session also prepares
the pipelines it used for the next one, so later runs start much faster.

**Is a controller supported?** Yes, an Xbox controller connected to the PC replaces the keyboard and mouse.

**Can it generate more frames, like DLSS Frame Generation?** DLSS FG needs the game's motion vectors, depth
and a picture without the HUD, and VIctory cannot yet tell those apart among the game's buffers. NVIDIA
**Smooth Motion** is the same idea done by the driver from the finished picture (2x), and the launcher turns
it on for you. Tools such as Lossless Scaling can make 3x-4x from any window, but from a base of ~22 FPS that
adds visible lag and artefacts: the real gains still come from the CPU work per frame.

**Why black or flat objects even on a strong PC?** Two causes: textures that have not reached the card yet
(the game's memory is copied to the card by the CPU, and video memory runs out: the Performance page and the
video memory reserve help), and the `_Tiled` volumetrics shader, still being fixed (see below).

## Where it stands

- Works: intro, prison, missions, open world, boats; long sessions without crashes.
- Video memory under the system's budget (6-8 GB used on a 12 GB card), ~20 GB of RAM.
- The CPU is the limit: crowds and fights now run at 14-17 FPS (were 10-15), with 6,000-8,000 draws a frame,
  50-90 MB of the game's memory copied and ~10 ms copied back each frame.
- New: copies of textures whose memory the game has already freed are let go at once (about 1 GB of video
  memory sat in them), and VIctory reserves its video memory with Windows.
- `_Tiled` (water volumetrics): off for now (black squares when on). F9 in the recording mode captures a
  frame, which can be replayed on WARP (`VICTORY_REPLAY_WARP=1`) as one wave of 64 and as two of 32 to find
  the instruction where the two disagree. (Fixed: on recent Windows builds D3D12Core swaps a command list's
  method table when it is closed, which made the recorder crash at start.)
- Known problems: stretched bodies for a frame or two ("spaghetti"), rainbow faces now and then, black
  flickers on characters, no ray tracing.

More: [docs/GUIDE-30FPS.md](docs/GUIDE-30FPS.md) (English guide and what each setting does),
[docs/LUZ-24FPS.md](docs/LUZ-24FPS.md) (Portuguese notes), [docs/replay.md](docs/replay.md) (frame recorder).
Earlier upstream notes: [docs/archive/README-upstream.md](docs/archive/README-upstream.md).

---

Maintained by **@edupode** on GitHub: <https://github.com/edupode>
