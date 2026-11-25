<div align="center">

<img src="https://capsule-render.vercel.app/api?type=waving&height=180&color=0:ff5f6d,50:ffc371,100:845ec2&text=Cold%20Project%20VI&fontColor=ffffff&fontSize=54&fontAlignY=36&desc=An%20Xbox%20Series%20game%20on%20PC&descAlignY=58&animation=fadeIn" alt="Cold Project VI" width="100%"/>

<a href="#where-the-game-stands"><img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=20&pause=1200&color=FFC371&center=true&vCenter=true&width=640&lines=The+prologue+is+playable%3A+you+control+the+heroine;2.3+fps+today%2C+measured+ceiling%3A+20;A+Rust+launcher+with+Discord+presence;Test+modes%3A+no+textures%2C+no+drawing" alt="animation"/></a>

<p>
  <a href="README.md"><img src="https://img.shields.io/badge/Fran%C3%A7ais-lire-0055A4?style=for-the-badge" alt="Français"/></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/English-current-C8102E?style=for-the-badge" alt="English"/></a>
  <a href="../../releases/latest"><img src="https://img.shields.io/badge/%E2%AC%87%20Download-launcher%20%2B%20build-2ea44f?style=for-the-badge" alt="Download"/></a>
</p>

<p>
  <img src="https://img.shields.io/badge/progress-59%25-ffc371?style=flat-square" alt="progress 59%"/>
  <img src="https://img.shields.io/badge/fps-2.3%20(measured%20max%2020)-ff5f6d?style=flat-square" alt="fps"/>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=flat-square&logo=windows" alt="Windows"/>
  <img src="https://img.shields.io/badge/GPU-RTX%2020%2B%20%2F%20mesh%20shaders-76B900?style=flat-square&logo=nvidia" alt="RTX 20+"/>
  <img src="https://img.shields.io/badge/Rust-1.98-B7410E?style=flat-square&logo=rust" alt="Rust"/>
  <img src="https://img.shields.io/badge/license-GPL--3.0-845ec2?style=flat-square" alt="GPL-3.0"/>
</p>

<p>
  <a href="#run-it"><img src="https://img.shields.io/badge/%E2%96%B6%20Run-222?style=for-the-badge" alt="Run"/></a>
  <a href="#the-launcher"><img src="https://img.shields.io/badge/%F0%9F%8E%AE%20Launcher-222?style=for-the-badge" alt="Launcher"/></a>
  <a href="#speed"><img src="https://img.shields.io/badge/%E2%9A%A1%20Speed-222?style=for-the-badge" alt="Speed"/></a>
  <a href="#picture"><img src="https://img.shields.io/badge/%F0%9F%96%BC%20Picture-222?style=for-the-badge" alt="Picture"/></a>
  <a href="#what-is-left"><img src="https://img.shields.io/badge/%F0%9F%93%8B%20To%20do-222?style=for-the-badge" alt="To do"/></a>
</p>

</div>

A working version of [VIctory](https://github.com/Marvisak/VIctory), which runs
the Xbox Series build of an open-world game natively on Windows from game
files you supply yourself. It starts from
[fxceless/victory-fork](https://github.com/fxceless/victory-fork) (up to
`4e17478`), with the view map of [Xeltax/VIcky](https://github.com/Xeltax/VIcky)
and work from many other forks (credited in the commits). The French
[README](README.md) has every measurement in full; this page is the summary.

> [!IMPORTANT]
> **This repository contains no game files**: no executable, data or extracted
> shaders. You need your own copy of the game. Not affiliated with or endorsed
> by the game's developer or publisher.

## Where the game stands

**About 59 % on [PortWatch VI](https://portwatch-vi.xyz/#method)'s scale:
playable** (you control the heroine in the prologue).

| Step | State |
|---|---|
| Boot, menus, title screen | ✅ works, 30 fps |
| Intro cutscene | ✅ plays through and displays correctly |
| Saves | ✅ the game saves and reloads a checkpoint |
| Prologue | ✅ playable: characters, textures, sky, sunlit exterior |
| Light in the shade | ⚠️ fallback light by default (the game's ray tracing does not run yet) |
| Frame rate in the world | ❌ 2.3 fps (431 ms a frame) on an RTX 3060 with 16 GB of RAM |
| Memory | ❌ about 21 GB committed on a 16 GB PC (Windows pages to disk) |
| Stability | ⚠️ 10 minutes without a crash in the longest measured run |

## Run it

**Easiest:** download the package from the
[latest release](../../releases/latest), unzip it, put your game folder in
`victory.toml` (`[paths] game_dir = '...'`), run `Prechauffer.cmd` once
(it translates every shader into the cache, a few minutes), then
`Lanceur.exe`.

**From source:**

```powershell
cargo build --profile diag -p victory --features diagnostics
target\diag\victory.exe translate --cache
target\diag\victory.exe run "<game folder>\<the game's exe>" --execute --console series-s
```

Requirements: Windows 10/11 x64 with AVX2, a D3D12 GPU with shader model 6.6
and mesh shaders (NVIDIA RTX 20 series or newer), 16 GB of RAM or more and a
**page file of at least 32 GB**, the Microsoft Visual C++ x64 redistributable.

### The launcher

`Lanceur.exe` (source in `apps/lanceur`, Rust + egui), in French:

- **ready-made modes** and **manual settings**: Series S or X, internal
  resolution (0.2 to 1.0), the game's own temporal upscaler, textures,
  lighting, on-screen counter, AMD FSR (experimental), extra `VICTORY_…`
  variables;
- **live fps** while playing (ms a frame, draws, world or menus, play time,
  last log lines); the full log goes to `logs\partie-lanceur-<date>.txt`;
- **Discord presence**: "Playing" and the game's name, from the entry Discord
  already has in its public list of games (nothing to create), with where you
  are, the fps, the mode and the elapsed time;
- **saves by place**: keep the current state under a name (cell, cafeteria,
  boat...) and start from it next time.

Build it with `cd apps\lanceur` then `cargo build --release`.

### Test modes

| Switch | Effect |
|---|---|
| `VICTORY_NO_TEXTURES=1` | the game's textures are neither sent to the GPU nor shown: every view reads a small grey texture of the same shape |
| `VICTORY_NO_DRAWS=1` | no pipelines, no shaders: every draw is dropped, the screen stays black (the counter shows); the game still runs, to measure the game alone |

## Speed

```mermaid
xychart-beta
    title "Frame time at the cell (ms), lower is better"
    x-axis ["Normal", "No textures + low resolution", "No drawing"]
    y-axis "ms" 0 --> 450
    bar [431, 333, 48]
```

| Per frame | Normal | No textures, low resolution | No drawing |
|---|---|---|---|
| fps | 2.3 | **3.0** (steady) | **19 to 24** |
| Our copies of game memory | 135 ms | 130 ms (180 MB a frame) | 0.4 ms |
| GPU | 207 ms | 112 ms | 0 |
| Copy-back into game memory | 50 ms | 34 ms | 0.1 ms |
| Game RAM (committed) | 21 GB | 14.4 GB | **6 GB** |

What this says:

1. **This PC tops out around 20 fps**: with nothing drawn, the game's own
   code (world, characters, streaming, sound) and our system services take
   42 to 53 ms a frame.
2. **The 15 GB that overflow the RAM are our graphics layer's**, not the
   game's (6 GB without drawing, 21 GB with).
3. **Hypothesis**: textures and resolution are no longer the problem. What
   is left does not depend on pixels: our copies of about 180 MB of game
   memory to the GPU every frame (130 ms, the biggest wall), our work around
   each indirect draw (about 2,000 clamp passes, each with a pipeline drain,
   about 110 ms), and a full RAM (1 GB free). The next real gain is to keep
   the game's data on the GPU instead of copying it every frame.

Tried on 25 November and dropped: grouped indirect clamps (GPU 207 → 239 ms,
a fence vanished), translated shaders noting missing pages in a branch
(1.9 fps), card copies turned off (CPU work 490 ms), ScarlettPort's runtime
as-is (black intro, under 1 fps here), upstream's page-marked copy-back
(about 100 conflicts on this branch).

### Other forks

| Fork | GPU | fps | Of use here |
|---|---|---|---|
| This one (25/11) | RTX 3060, 16 GB | **2.3** (measured) | — |
| [ScarlettPort](https://github.com/ZigZagBoi1/ScarlettPort) | RTX 3060, 32 GB advised | about 4 | copy-back for read-back buffers only; tried here: under 1 fps |
| [VItoriaBr](https://github.com/uiliannn/VItoriaBr) | RTX 5060 Ti, 40 GB | 3.5 to 6 | their GPU takes 50 to 65 ms a frame |
| [DexNom/VIctory](https://github.com/DexNom/VIctory) | RX 7900 XTX | 1.03 | ray tracing that really runs (Radeon) |
| [ztrx](https://github.com/Zettrox/ztrx) | RTX 3050 | 1 to 3 | prologue to the boat chase, 1 h 30 without a crash |

## Picture

- **Shaders**: 99.89 % of the game's 25,694 programs translate.
- **Upscaling**: as a Series S the game renders 1280×720 and asks FSR 4 for
  1920×1080; FSR 4 cannot run here, so a plain stretch stands in. An
  experimental port of upstream's module serves the game's calls with AMD's
  PC FSR 3.1 (`VICTORY_FIDELITYFX_DIR`, the official DLL from the FidelityFX
  SDK); it lost the GPU in its second test, so do not use it to play.
- **Ray tracing**: the builder and trace programs translate and the hardware
  ray test is emulated, but nothing is lit by ray tracing yet; a fallback
  light brightens shaded areas.

## What is left

1. Speed: keep the game's data on the GPU instead of copying it every frame;
   cheaper indirect draws; this PC tops out around 20 fps.
2. Memory: under 16 GB, so Windows stops paging.
3. Ray tracing in the game (DexNom's Radeon work is the lead).
4. Stability over a long session.

## Credits and license

[VIctory](https://github.com/Marvisak/VIctory) by Marvisak and contributors is
the base of everything (GPL-3.0-or-later, like this repository, see
`LICENSE`); [victory-fork](https://github.com/fxceless/victory-fork) by
fxceless; [VIcky](https://github.com/Xeltax/VIcky) by Xeltax; and the many
forks whose commits are taken here, named in each commit. Developed with
[Claude Code](https://claude.com/claude-code).
