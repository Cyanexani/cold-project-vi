It runs 24-26 fps, all controls have been mapped,currently using -locallights
On the Open world i get about 10-15fps
Everything seems to be loading
I need help to fix volumetric shaders _Tiled
It's running on 12gb vram and 32gb ddr5
Currently it uses about 10-11gb on vram and 27gb ddr5
I got a laptop with a rtx 5070 ti capped at 145w and a Intel i9 ultra 275hx
Hope this helps anyone, missions are already possible and doesn't crash even after long sessions
on menus it goes from 30-32/33fps
# VIctory on an RX 7900 XTX

> **Ramo `edupode`:** cgbj + luz do fork VIctory-NVIDIA, afinado para uma RTX 5070 Ti portátil (12 GB):
> 24-26 FPS no mundo aberto. Como montar, teclas e o que falta: [docs/LUZ-24FPS.md](docs/LUZ-24FPS.md)
> (`Jogar-Luz.cmd`).
> **English guide (setup and how to get close to 30 FPS): [docs/GUIDE-30FPS.md](docs/GUIDE-30FPS.md).**
>
> **Latest (2026-11-27), RTX 5070 Ti Laptop 12 GB, Core Ultra 9 275HX, 32 GB, Series S profile:**
> - **`Jogar-Luz.cmd tudo`: every optimization at once** (720p + game upscaler, texture levels held, textures
>   over 2048 halved, 1.5 GB of buffers compared on the card, BAR windows, 60 FPS clock, fullest-block texture
>   packing, up to 384 MB of textures staged a frame instead of 192 so they load sooner). **Measured: cutscenes
>   35-38 FPS** (above the game's 30 thanks to the 60 FPS clock), open world 14-22 while new areas stream in.
>   While streaming, texture refreshes wait 40-55 ms a frame for the game's memory remaps, and the pass over the
>   descriptor heap takes ~9 ms a frame: the next targets. Those waits are the refreshes of the game's one-shot
>   decoration bakes (the others already read around a remap); `carregar` (`VICTORY_BAKE_NO_WAIT=1`, experimental)
>   lets them read around it too. `caras` adds waiting for memory remaps against rainbow faces (costs some FPS).
> - `Jogar-Luz.cmd 720p texturas`: open world **20-26 FPS** (was 14-15), cutscenes ~30. Video memory now
>   **under** the system's budget (7.6 of 8.6 GB; was ~1 GB over), textures waiting ~130 (was 6,000-15,000).
>   The big win: `VICTORY_LEVELS_ON_DEMAND=0` (host copies hold only the texture levels the game shows).
> - `Jogar-Luz.cmd 720p texturas cpu` (and `... cpu 60`): the same plus 10 helper threads (`VICTORY_WIDE_THREADS`) and the write
>   watch asked early (`VICTORY_ASK_EARLY=2`): measured no gain (write watch 5.0 ms and refreshes 8.6-10 ms
>   a frame either way). The CPU is the limit now (~27 ms preparing, ~10 ms
>   copying back per frame; the card ~0 ms).
> - Water fixed (only the `_LocalLights` volumetrics shader translated wave-wide); `_Tiled` still off (black
>   squares), being debugged with a test of its tile-bounds part and the frame recorder.
> - Refresh checks: each of the ~7,000 per frame looked through every range asked of the write watch that
>   frame (a linear scan, quadratic in all); now a binary search over the sorted ranges. Under test.
> - `60` switch (`VICTORY_FPS_MAX=60`): the console's frame clock at 60 a second instead of the 30 the game asks
>   for, so menus and light scenes can pass 30 FPS. **Measured: menus at 60 FPS** (2026-11-27); gameplay stays
>   CPU-bound (~20-26 in the open world). The `qualidade` switch (relay 1, no card steps) did not stop characters'
>   black flickers and cost a lot (8-14 FPS in a cutscene, ~35-41 ms a frame waiting for the card).
> - Borderless window for NVIDIA Smooth Motion exists (`VICTORY_BORDERLESS=1`) but is off: it dropped the game
>   to 1-2 FPS on a 2560x1600 screen at 150 %. Being tested: `VICTORY_BORDERLESS_DPI=0` keeps the window unaware
>   of the screen's scaling, to tell whether the scaling or the borderless flip is the cause.
> - Keyboard/mouse: click to capture the mouse (Alt/F1 releases), F enters vehicles and switches W/S to the
>   triggers (beep), full table in the guide.

**This repository contains no game files, code or assets.** You need your own
copy of the game. It is not affiliated with or endorsed by the game's developer
or publisher.




edupode
