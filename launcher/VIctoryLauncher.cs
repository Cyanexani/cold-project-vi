// VI Launcher (edupode): one window to set up and play the edupode build of VIctory.
// Built with the C# compiler that ships with Windows (.NET Framework 4.x):
//   launcher\build-launcher.cmd   ->   "VI Launcher.exe" in the repository's root.
// No game files are included or downloaded; the player points it at their own dump.
// English by default; the button in the side bar switches to Portuguese (saved in launcher.ini).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // What ends the launcher is written down (%TEMP%i-launcher-error.txt) for a bug report.
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "vi-launcher-error.txt"), DateTime.Now + "  " + e.ExceptionObject + Environment.NewLine); } catch (Exception) { }
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }

    // A preset: render scale and the switches it turns on.
    class Mode
    {
        public string Name, Short, Tag, Description;
        public string Scale;      // null: native 1080p
        public bool LightTextures;
        public bool Modest;       // smaller textures and buffers for 6-8 GB cards and 16 GB of RAM
        public Mode(string name, string tag, string shortLine, string description, string scale, bool light, bool modest)
        {
            Name = name; Tag = tag; Short = shortLine; Description = description; Scale = scale; LightTextures = light; Modest = modest;
        }
        public override string ToString() { return Name + " (" + Tag + ")"; }
    }

    // An app that holds video memory or RAM, all its processes together.
    class MemoryUser
    {
        public string Name;
        public long Vram;
        public long Ram;
        public bool Protected;
        public string Note = "";
    }

    partial class LauncherForm : Form
    {
        static bool Pt;
        static string L(string en, string pt) { return Pt ? pt : en; }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        readonly string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VIctory", "luz");
        readonly string settingsFile;
        // Where the shader cache and the game's saves live; launcher.ini's cache= and data= move them
        // (to share them with another way of starting VIctory).
        string cacheDir, dataDir;

        Banner banner;
        TextBox gameBox;
        Label gameStatus, buildStatus, modeInfo, vramSummary, smoothStatus;
        readonly List<ModeCard> cards = new List<ModeCard>();
        int modeIndex = -1;
        Toggle panelBox, borderlessBox, fps60Box, reserveBox, smoothBox, qualityBox, facesBox, loadingBox, fightsBox, warmBox;
        LoadBar loadBar;
        TextBox logBox;
        Pill playButton, prepareButton, buildButton;
        ListView vramList;
        readonly List<Panel> pages = new List<Panel>();
        readonly List<NavButton> nav = new List<NavButton>();

        bool rebar, rtxFrameGen;
        double vramGb, ramGb;
        string gpuName = "unknown GPU";
        string smoothState = "?";

        Mode[] modes;

        public LauncherForm()
        {
            settingsFile = Path.Combine(Path.GetDirectoryName(dataRoot), "launcher.ini");
            Pt = ReadSetting("lang") == "pt";
            cacheDir = ReadSetting("cache") ?? Path.Combine(dataRoot, "cache");
            dataDir = ReadSetting("data") ?? Path.Combine(dataRoot, "data");
            modes = new Mode[] {
                new Mode(L("Recommended", "Recomendado"), "720p", L("★ Tested on edupode's PC", "★ Testado no PC do edupode"),
                    L("The world drawn at 1280x720 and rebuilt to 1080p by the game's own upscaler, texture levels held (video memory under budget), 60 FPS clock. About 22-26 FPS in the open world and 35+ in cutscenes on an RTX 5070 Ti laptop (6-8 GB of video memory, ~20 GB of RAM).",
                      "O mundo desenhado a 1280x720 e reconstruido para 1080p pelo upscaler do proprio jogo, niveis de texturas guardados (VRAM dentro do limite), relogio de 60 FPS. Cerca de 22-26 FPS no mundo aberto e 35+ nas cutscenes num portatil RTX 5070 Ti (6-8 GB de VRAM, ~20 GB de RAM)."),
                    "0.6666667", true, false),
                new Mode(L("Fastest", "Mais rapido"), "540p", L("Most frames", "Mais frames"),
                    L("The world drawn at 960x540: the most frames, a softer picture. For slower CPUs.",
                      "O mundo desenhado a 960x540: mais frames, imagem mais suave. Para processadores mais lentos."),
                    "0.5", true, false),
                new Mode(L("Modest PC", "PC modesto"), "540p", L("6-8 GB cards, 16 GB RAM", "Placas 6-8 GB, 16 GB RAM"),
                    L("For 6-8 GB cards and 16 GB of RAM: 540p, textures over 1024 texels halved, fewer of the game's buffers on the card, smaller texture batches, the memory guards on. Less video memory and RAM, a softer picture.",
                      "Para placas de 6-8 GB e 16 GB de RAM: 540p, texturas acima de 1024 cortadas a metade, menos buffers do jogo na placa, lotes de texturas mais pequenos, protecoes de memoria ligadas. Menos VRAM e RAM, imagem mais suave."),
                    "0.5", true, true),
                new Mode(L("Sharp", "Nitido"), "1080p", L("12 GB+ cards", "Placas de 12 GB+"),
                    L("The world at full 1080p with held texture levels. Needs a strong GPU with 12 GB or more; slower in the open world.",
                      "O mundo a 1080p completos com niveis de texturas guardados. Precisa de uma placa forte com 12 GB ou mais; mais lento no mundo aberto."),
                    null, true, false),
                new Mode(L("Classic cgbj", "cgbj classico"), "1080p", L("For comparison", "Para comparar"),
                    L("The original cgbj settings at 1080p, no texture changes. For comparison.",
                      "As definicoes originais do cgbj a 1080p, sem mudancas nas texturas. Para comparar."),
                    null, false, false),
            };

            Text = "VI Launcher";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            ClientSize = new Size(1120, 940);
            MinimumSize = new Size(1000, 700);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.75f);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back };
            Controls.Add(content);
            pages.Add(PlayPage());
            pages.Add(SetupPage());
            pages.Add(MemoryPage());
            pages.Add(ControlsPage());
            pages.Add(HelpPage());
            foreach (var p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; content.Controls.Add(p); }
            Controls.Add(SideBar());

            LoadSettings();
            DetectSystem();
            Show(0);
            Refresh2();
            // A first run (nothing built or no game yet): the Setup page, checked.
            if (VictoryExe == null || !File.Exists(GameExe)) Show(1);
            // `--page N` opens on that page (0 Play, 1 Setup, 2 Performance, 3 Controls, 4 Help).
            var argv = Environment.GetCommandLineArgs();
            int page;
            for (int a = 1; a + 1 < argv.Length; a++)
                if (argv[a] == "--page" && int.TryParse(argv[a + 1], out page) && page >= 0 && page < pages.Count) Show(page);
        }

        // Work on other threads that reports back to the window starts once the window exists.
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // No game folder yet: look for the game on its own.
            if (!File.Exists(GameExe)) RunSteps(steps.Where(st => st.Fix == (Func<bool>)FindGame).ToArray());
            else CheckAll();
            new Thread(() => {
                var s = SmoothMotion.Status();
                try { BeginInvoke(new Action(() => { smoothState = s; UpdateBanner(); UpdateSmooth(); })); } catch (Exception) { }
            }) { IsBackground = true }.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            try { if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4); } catch (Exception) { }
        }

        // ---------------------------------------------------------------- side bar
        Control SideBar()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 216, BackColor = Theme.Side };
            var logo = new Label { Text = "VI", Font = new Font("Segoe UI Black", 28f), ForeColor = Theme.Pink, AutoSize = true, Location = new Point(18, 12), BackColor = Theme.Side };
            var logo2 = new Label { Text = "Launcher", Font = new Font("Segoe UI Light", 16f), ForeColor = Theme.Text, AutoSize = true, Location = new Point(22, 62), BackColor = Theme.Side };
            var by = new Label { Text = "by edupode", Font = new Font("Segoe UI", 8.5f), ForeColor = Theme.Dim, AutoSize = true, Location = new Point(24, 94), BackColor = Theme.Side };
            side.Controls.Add(logo); side.Controls.Add(logo2); side.Controls.Add(by);
            string[][] items = {
                new[] { "", L("Play", "Jogar") },
                new[] { "\uE713", L("Setup", "Preparar") },
                new[] { "", L("Performance", "Desempenho") },
                new[] { "", L("Controls", "Controlos") },
                new[] { "", L("Help", "Ajuda") },
            };
            for (int i = 0; i < items.Length; i++)
            {
                int k = i;
                var b = new NavButton { Glyph = items[i][0], Text = items[i][1], Location = new Point(4, 132 + i * 46) };
                b.Click += (s, e) => { Show(k); if (k == 1) CheckAll(); if (k == 2) ScanMemory(); };
                nav.Add(b);
                side.Controls.Add(b);
            }
            var lang = new Pill { Text = Pt ? "English" : "Portugues", Size = new Size(176, 34), Location = new Point(20, 0), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, BackColor = Theme.Side };
            lang.Click += (s, e) => { Pt = !Pt; SaveSettings(); Application.Restart(); };
            side.Controls.Add(lang);
            var repo = new Label { Text = "github.com/edupode/cold-project-vi", Font = new Font("Segoe UI", 8f), ForeColor = Theme.Dim, AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, BackColor = Theme.Side, Cursor = Cursors.Hand };
            repo.Click += (s, e) => { try { Process.Start("https://github.com/edupode/cold-project-vi/tree/edupode"); } catch (Exception) { } };
            side.Controls.Add(repo);
            side.Resize += (s, e) => { lang.Top = side.Height - 84; repo.Location = new Point(22, side.Height - 40); };
            return side;
        }

        void Show(int page)
        {
            for (int i = 0; i < pages.Count; i++) { pages[i].Visible = i == page; nav[i].Active = i == page; }
        }

        Panel Page()
        {
            return new Panel { BackColor = Theme.Back, AutoScroll = true, Padding = new Padding(24, 20, 24, 20) };
        }

        Label Small(string text, int x, int y, int width)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 20), ForeColor = Theme.Dim, BackColor = Theme.Back };
        }

        // ---------------------------------------------------------------- Play page
        Panel PlayPage()
        {
            var page = Page();
            const int X = 24, W = 852;
            int y = 20;
            banner = new Banner { Location = new Point(X, y), Size = new Size(W, 128), Title = "GTA VI  ·  VIctory",
                Subtitle = "VIctory + cgbj + AnyXboxSeries + Leonidas + VIctory-NVIDIA + fxceless + VIctory-7900XTX  —  " + L("remake by edupode", "remake por edupode") };
            page.Controls.Add(banner);
            y += 146;

            page.Controls.Add(new Heading(L("Game folder", "Pasta do jogo")) { Location = new Point(X, y), Width = W - 250 });
            var auto = new Pill { Main = true, Text = L("⚡  First-time auto-setup", "⚡  Preparar tudo (1a vez)"), Location = new Point(X + W - 240, y - 6), Size = new Size(240, 34) };
            auto.Click += (s, e) => { Show(1); DoEverything(); };
            page.Controls.Add(auto);
            y += 34;
            gameBox = new TextBox { Location = new Point(X, y + 3), Width = W - 312, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f) };
            gameBox.TextChanged += (s, e) => Refresh2();
            page.Controls.Add(gameBox);
            var detect = new Pill { Text = L("Auto-detect", "Detetar"), Location = new Point(X + W - 300, y), Width = 144 };
            detect.Click += (s, e) => RunSteps(steps.Where(st => st.Fix == (Func<bool>)FindGame).ToArray());
            page.Controls.Add(detect);
            var browse = new Pill { Text = L("Browse...", "Procurar..."), Location = new Point(X + W - 144, y), Width = 144 };
            browse.Click += (s, e) => {
                using (var d = new FolderBrowserDialog { Description = L("Choose the folder that holds game_scarlett_master.exe", "Escolhe a pasta que tem o game_scarlett_master.exe") })
                    if (d.ShowDialog(this) == DialogResult.OK) gameBox.Text = d.SelectedPath;
            };
            page.Controls.Add(browse);
            y += 38;
            gameStatus = Small("", X, y, W);
            page.Controls.Add(gameStatus);
            y += 30;

            page.Controls.Add(new Heading("VIctory") { Location = new Point(X, y), Width = W });
            y += 32;
            buildStatus = new Label { Location = new Point(X, y + 7), Size = new Size(W - 320, 20), ForeColor = Theme.Dim, BackColor = Theme.Back, AutoEllipsis = true };
            page.Controls.Add(buildStatus);
            buildButton = new Pill { Text = L("Build VIctory", "Compilar VIctory"), Location = new Point(X + W - 304, y), Width = 144 };
            buildButton.Click += (s, e) => Build();
            page.Controls.Add(buildButton);
            prepareButton = new Pill { Text = L("Prepare shaders", "Preparar shaders"), Location = new Point(X + W - 144, y), Width = 144 };
            prepareButton.Click += (s, e) => Prepare();
            page.Controls.Add(prepareButton);
            y += 50;

            page.Controls.Add(new Heading(L("Mode", "Modo")) { Location = new Point(X, y), Width = W });
            y += 34;
            int cw = (W - 4 * 10) / 5;
            for (int i = 0; i < modes.Length; i++)
            {
                int k = i;
                var c = new ModeCard { Title = modes[i].Name, Tag2 = modes[i].Tag, Line = modes[i].Short, Location = new Point(X + i * (cw + 10), y), Size = new Size(cw, 92) };
                c.Click += (s, e) => SelectMode(k);
                cards.Add(c);
                page.Controls.Add(c);
            }
            y += 100;
            modeInfo = new Label { Location = new Point(X, y), Size = new Size(W, 40), ForeColor = Theme.Dim, BackColor = Theme.Back };
            page.Controls.Add(modeInfo);
            y += 48;

            page.Controls.Add(new Heading(L("Options", "Opcoes")) { Location = new Point(X, y), Width = W - 230 });
            var best = new Pill { Text = L("★  Recommended setup", "★  Configuracao recomendada"), Location = new Point(X + W - 220, y - 4), Width = 220 };
            best.Click += (s, e) => Recommended();
            page.Controls.Add(best);
            y += 32;
            int half = W / 2;
            Func<string, int, int, bool, Toggle> T = (text, col, row, on) => {
                var t = new Toggle { Text = text, Checked = on, Location = new Point(X + col * half, y + row * 32), Width = half - 10 };
                page.Controls.Add(t);
                return t;
            };
            panelBox = T(L("★ FPS and diagnostics overlay (F3)", "★ Overlay de FPS e diagnostico (F3)"), 0, 0, true);
            borderlessBox = T(L("★ Borderless full screen", "★ Ecra inteiro sem bordas"), 0, 1, true);
            fps60Box = T(L("★ Unlock 30 FPS (60 FPS clock)", "★ Desbloquear 30 FPS (relogio de 60)"), 0, 2, true);
            smoothBox = T(L("NVIDIA Smooth Motion (RTX 40/50, set up for you)", "NVIDIA Smooth Motion (RTX 40/50, configurado por ti)"), 0, 3, true);
            reserveBox = T(L("Keep the most video memory for the game", "Guardar o maximo de VRAM para o jogo"), 0, 4, true);
            fightsBox = T(L("Crowds and fights: 2 GB of buffers on the card", "Multidoes e lutas: 2 GB de buffers na placa"), 1, 0, false);
            qualityBox = T(L("Anti-stretch: fewer stretched bodies (slower)", "Anti-esticados: menos corpos esticados (mais lento)"), 1, 1, false);
            facesBox = T(L("Rainbow-face fix (slower)", "Correcao das caras arco-iris (mais lento)"), 1, 2, false);
            loadingBox = T(L("Fast loading (experimental)", "Carregamento rapido (experimental)"), 1, 3, false);
            warmBox = T(L("Load the pipelines before the game opens", "Carregar as pipelines antes de o jogo abrir"), 1, 4, true);
            y += 5 * 32 + 4;
            explain = new Label { Location = new Point(X, y), Size = new Size(W, 40), ForeColor = Theme.Cyan, BackColor = Theme.Back,
                Text = L("Point at an option to see what it does. ★ marks what was tested on edupode's PC (RTX 5070 Ti laptop).", "Passa o rato por uma opcao para ver o que faz. ★ marca o que foi testado no PC do edupode (portatil RTX 5070 Ti).") };
            page.Controls.Add(explain);
            var tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
            foreach (var kv in Explanations())
            {
                var t = kv.Key; var text = kv.Value;
                tips.SetToolTip(t, text);
                t.MouseEnter += (s, e) => { explain.Text = text; };
            }
            y += 46;

            logBox = new TextBox { Location = new Point(X, y), Size = new Size(W - 290, 86), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.None, BackColor = Theme.Side, ForeColor = Theme.Dim, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9f) };
            page.Controls.Add(logBox);
            playButton = new Pill { Main = true, Text = L("PLAY", "JOGAR"), Location = new Point(X + W - 270, y + 12), Size = new Size(270, 60), Font = new Font("Segoe UI Black", 16f) };
            playButton.Click += (s, e) => Play();
            page.Controls.Add(playButton);
            loadBar = new LoadBar { Location = new Point(X, y + 92), Size = new Size(W, 26), Visible = false };
            page.Controls.Add(loadBar);
            page.Controls.Add(new Label { Location = new Point(X, y + 100), Size = new Size(10, 10), BackColor = Theme.Back });
            return page;
        }

        Label explain;

        // What each option does, and whether it is part of the setup tested on edupode's PC.
        Dictionary<Toggle, string> Explanations()
        {
            var d = new Dictionary<Toggle, string>();
            d[panelBox] = L("★ Shows FPS (now, last-minute average, whole-run average, worst), frame time, draws, video memory and RAM in the upper left corner. F3 hides it.",
                            "★ Mostra os FPS (agora, media do ultimo minuto, media total, pior), tempo por frame, draws, VRAM e RAM no canto superior esquerdo. F3 esconde.");
            d[borderlessBox] = L("★ A window without borders over the whole screen (test A: 22-24 FPS on edupode's PC). Needed for Smooth Motion and quick Alt+Tab.",
                                 "★ Janela sem bordas no ecra todo (teste A: 22-24 FPS no PC do edupode). Precisa-se para o Smooth Motion e para Alt+Tab rapido.");
            d[fps60Box] = L("★ The game asks for 30 FPS; this lets its clock run up to 60, so cutscenes and menus go past 30 when the PC can.",
                            "★ O jogo pede 30 FPS; isto deixa o relogio ir ate 60, por isso cutscenes e menus passam dos 30 quando o PC consegue.");
            d[smoothBox] = L("New: NVIDIA's driver frame generation for RTX 40/50. The launcher writes it into the driver's profile for victory.exe, no NVIDIA App needed. Doubles the frames shown; adds a little input lag.",
                             "Novo: a geracao de frames do driver NVIDIA para RTX 40/50. O launcher escreve-a no perfil do driver para o victory.exe, sem NVIDIA App. Duplica os frames mostrados; junta um pouco de atraso.");
            d[reserveBox] = L("New: asks Windows to reserve video memory for the game, so other apps are moved out first when memory runs short. Fewer black and flat textures.",
                              "Novo: pede ao Windows uma reserva de VRAM para o jogo, por isso as outras apps saem primeiro quando falta memoria. Menos texturas pretas e lisas.");
            d[fightsBox] = L("Keeps 2 GB of the game's buffers on the card instead of 1.5: for crowds and fights, where the FPS drops. Needs a 12 GB card. Still being tested.",
                             "Mantem 2 GB de buffers do jogo na placa em vez de 1,5: para multidoes e lutas, onde os FPS caem. Precisa de placa de 12 GB. Ainda em teste.");
            d[qualityBox] = L("Waits for every copy of the game's buffers before drawing: fewer stretched bodies ('spaghetti'), but several FPS slower.",
                              "Espera por todas as copias dos buffers do jogo antes de desenhar: menos corpos esticados ('esparguete'), mas varios FPS mais lento.");
            d[facesBox] = L("Waits for textures to finish uploading before use: fixes rainbow faces, costs FPS.",
                            "Espera que as texturas acabem de subir antes de as usar: corrige as caras arco-iris, custa FPS.");
            d[warmBox] = L("New: before the game starts, a loading bar makes every pipeline earlier sessions drew with, in separate processes, so the driver has them ready: no stutter the first time you see a place. Takes seconds once the cache is ready; after a VIctory or driver update it takes longer, once.",
                           "Novo: antes de o jogo arrancar, uma barra de carregamento prepara todas as pipelines usadas nas sessoes anteriores, em processos a parte, para o driver ja as ter: sem engasgos a primeira vez que ves um sitio. Demora segundos com a cache pronta; depois de atualizar o VIctory ou o driver demora mais, uma vez.");
            d[loadingBox] = L("Experimental: textures the game builds on the CPU no longer wait for the game's memory to settle. Shorter loading pauses; untested.",
                              "Experimental: as texturas que o jogo prepara no processador deixam de esperar que a memoria assente. Pausas de carregamento mais curtas; nao testado.");
            return d;
        }

        // The setup that worked on edupode's PC: 720p with light textures, borderless (test A), 60 FPS clock, overlay;
        // plus the new video-memory reserve and Smooth Motion.
        void Recommended()
        {
            SelectMode(0);
            panelBox.Checked = true; borderlessBox.Checked = true; fps60Box.Checked = true; reserveBox.Checked = true;
            smoothBox.Checked = rtxFrameGen;
            fightsBox.Checked = false; qualityBox.Checked = false; facesBox.Checked = false; loadingBox.Checked = false; warmBox.Checked = true;
            Log(L("Recommended setup: Recommended 720p, overlay, borderless, 60 FPS clock, video memory reserve" + (rtxFrameGen ? ", Smooth Motion." : "."),
                  "Configuracao recomendada: Recomendado 720p, overlay, sem bordas, relogio de 60 FPS, reserva de VRAM" + (rtxFrameGen ? ", Smooth Motion." : ".")));
        }

        void SelectMode(int k)
        {
            if (k < 0 || k >= modes.Length) return;
            modeIndex = k;
            for (int i = 0; i < cards.Count; i++) cards[i].Selected = i == k;
            Refresh2();
        }

        Mode Selected { get { return modeIndex >= 0 ? modes[modeIndex] : modes[0]; } }

        // ---------------------------------------------------------------- Performance page
        Panel MemoryPage()
        {
            var page = Page();
            const int X = 24, W = 852;
            int y = 20;
            page.Controls.Add(new Heading(L("Video memory and RAM", "Memoria de video e RAM")) { Location = new Point(X, y), Width = W });
            y += 32;
            vramSummary = new Label { Location = new Point(X, y), Size = new Size(W, 46), ForeColor = Theme.Text, BackColor = Theme.Back, Font = new Font("Segoe UI Semibold", 10.5f) };
            page.Controls.Add(vramSummary);
            y += 50;
            vramList = new ListView { Location = new Point(X, y), Size = new Size(W, 230), View = View.Details, CheckBoxes = true, FullRowSelect = true, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f), HeaderStyle = ColumnHeaderStyle.Nonclickable, OwnerDraw = true };
            vramList.Columns.Add(L("App", "App"), 230);
            vramList.Columns.Add(L("Video memory", "VRAM"), 120, HorizontalAlignment.Right);
            vramList.Columns.Add("RAM", 110, HorizontalAlignment.Right);
            vramList.Columns.Add(L("Note", "Nota"), 370);
            DarkHeader(vramList);
            vramList.ItemCheck += (s, e) => {
                var u = vramList.Items[e.Index].Tag as MemoryUser;
                if (u != null && u.Protected) e.NewValue = CheckState.Unchecked;
            };
            page.Controls.Add(vramList);
            y += 240;
            var refresh = new Pill { Text = L("Refresh", "Atualizar"), Location = new Point(X, y), Width = 140 };
            refresh.Click += (s, e) => ScanMemory();
            page.Controls.Add(refresh);
            var close = new Pill { Text = L("Close the checked apps", "Fechar as apps marcadas"), Location = new Point(X + 150, y), Width = 220 };
            close.Click += (s, e) => CloseChecked();
            page.Controls.Add(close);
            y += 52;

            page.Controls.Add(new Heading("NVIDIA Smooth Motion") { Location = new Point(X, y), Width = W });
            y += 32;
            smoothStatus = new Label { Location = new Point(X, y), Size = new Size(W, 44), ForeColor = Theme.Text, BackColor = Theme.Back };
            page.Controls.Add(smoothStatus);
            y += 46;
            var smoothOn = new Pill { Text = L("Turn it on now", "Ligar agora"), Location = new Point(X, y), Width = 160 };
            smoothOn.Click += (s, e) => ApplySmooth(true, true);
            page.Controls.Add(smoothOn);
            var smoothOff = new Pill { Text = L("Turn it off", "Desligar"), Location = new Point(X + 170, y), Width = 140 };
            smoothOff.Click += (s, e) => ApplySmooth(false, true);
            page.Controls.Add(smoothOff);
            y += 52;

            page.Controls.Add(new Heading(L("Getting the most video memory", "Ter o maximo de VRAM")) { Location = new Point(X, y), Width = W });
            y += 30;
            var tips = new Label { Location = new Point(X, y), Size = new Size(W, 250), ForeColor = Theme.Dim, BackColor = Theme.Back };
            tips.Text = string.Join("\r\n", Pt ? new[] {
                "Mais VRAM livre = mais texturas na placa = menos coisas pretas, texturas lisas e recarregamentos.",
                "•  NVIDIA App > Definicoes > Funcionalidades: desliga o Instant Replay e o overlay (o 'NVIDIA Overlay' pode ter 1-2 GB).",
                "•  Discord: Definicoes > Avancado > Aceleracao de hardware: desligada. Wallpaper Engine: pausa ou fecha.",
                "•  Browsers, emuladores e outros jogos: fecha-os enquanto jogas.",
                "•  Portateis com MUX (PredatorSense, Armoury Crate, Legion Vantage): no modo Hibrido/Optimus o ambiente de trabalho",
                "   corre na grafica integrada e liberta normalmente 0,5-1,5 GB na NVIDIA.",
                "•  'Guardar o maximo de VRAM' pede ao Windows uma reserva para o VIctory: quando outra app pede memoria, a dela sai primeiro.",
                "•  O Smooth Motion e escrito no perfil do driver para victory.exe (como o NVIDIA Profile Inspector): nao precisa da NVIDIA App.",
            } : new[] {
                "More free video memory = more textures on the card = fewer black objects, flat textures and reloads.",
                "•  NVIDIA App > Settings > Features: turn off Instant Replay and the overlay ('NVIDIA Overlay' can hold 1-2 GB).",
                "•  Discord: Settings > Advanced > Hardware acceleration: off. Wallpaper Engine: pause or quit it.",
                "•  Browsers, emulators and other games: close them while you play.",
                "•  Laptops with a MUX switch (PredatorSense, Armoury Crate, Legion Vantage): in Hybrid/Optimus mode the desktop",
                "   runs on the integrated GPU, which usually frees 0.5-1.5 GB on the NVIDIA card.",
                "•  'Keep the most video memory' asks Windows to reserve memory for VIctory: other apps are moved out first.",
                "•  Smooth Motion is written into the driver's profile for victory.exe (as NVIDIA Profile Inspector does): no NVIDIA App needed.",
            });
            page.Controls.Add(tips);
            return page;
        }

        static void DarkHeader(ListView list)
        {
            list.DrawColumnHeader += (s, e) => {
                using (var b = new SolidBrush(Theme.Side)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var b = new SolidBrush(Theme.Dim))
                using (var f = new Font("Segoe UI Semibold", 9.5f))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = e.Header.TextAlign == HorizontalAlignment.Right ? StringAlignment.Far : StringAlignment.Near };
                    var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                    e.Graphics.DrawString(e.Header.Text, f, b, r, sf);
                }
            };
            list.DrawItem += (s, e) => { e.DrawDefault = true; };
            list.DrawSubItem += (s, e) => { e.DrawDefault = true; };
        }

        static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "dwm", "csrss", "explorer", "System", "Idle", "svchost", "winlogon", "services", "lsass", "smss", "wininit",
            "fontdrvhost", "sihost", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "TextInputHost",
            "ctfmon", "RuntimeBroker", "audiodg", "Memory Compression", "Registry", "victory", "victory-ferramenta",
            "edu-launcher", "Edu's Remake", "VI Launcher", "MsMpEng", "NVDisplay.Container", "nvcontainer", "claude", "msedgewebview2", "conhost",
            "powershell", "pwsh", "cmd", "WindowsTerminal", "OpenConsole", "ApplicationFrameHost", "LockApp",
            "SecurityHealthSystray", "Taskmgr", "PredatorSense", "Secure System", "LsaIso", "spoolsv", "dllhost",
        };

        // Per-app video memory on the busiest card (the NVIDIA one), from the "GPU Process Memory" counters
        // Task Manager uses; and the card's total in use. Empty if the counters are not there.
        static List<MemoryUser> GpuUsers(out long cardUsed)
        {
            cardUsed = 0;
            var byLuid = new Dictionary<string, Dictionary<int, long>>();
            try
            {
                var data = new PerformanceCounterCategory("GPU Process Memory").ReadCategory()["Dedicated Usage"];
                foreach (InstanceData d in data.Values)
                {
                    // pid_1234_luid_0x00000000_0x0001ca5c_phys_0
                    var parts = d.InstanceName.Split('_');
                    if (parts.Length < 5 || parts[0] != "pid") continue;
                    int pid;
                    if (!int.TryParse(parts[1], out pid)) continue;
                    var luid = parts[3] + "_" + parts[4];
                    Dictionary<int, long> per;
                    if (!byLuid.TryGetValue(luid, out per)) byLuid[luid] = per = new Dictionary<int, long>();
                    long was;
                    per.TryGetValue(pid, out was);
                    per[pid] = was + d.RawValue;
                }
                var adapters = new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory()["Dedicated Usage"];
                string best = null;
                foreach (InstanceData d in adapters.Values)
                    if (d.RawValue > cardUsed) { cardUsed = d.RawValue; best = d.InstanceName; }
                if (best == null) return new List<MemoryUser>();
                var bp = best.Split('_');
                var key = bp.Length >= 3 ? bp[1] + "_" + bp[2] : "";
                Dictionary<int, long> card;
                if (!byLuid.TryGetValue(key, out card)) return new List<MemoryUser>();
                var users = new Dictionary<string, MemoryUser>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in card)
                {
                    string name;
                    try { name = Process.GetProcessById(kv.Key).ProcessName; } catch (Exception) { continue; }
                    MemoryUser u;
                    if (!users.TryGetValue(name, out u)) users[name] = u = new MemoryUser { Name = name };
                    u.Vram += kv.Value;
                }
                return users.Values.ToList();
            }
            catch (Exception) { return new List<MemoryUser>(); }
        }

        void ScanMemory()
        {
            vramSummary.Text = L("Looking...", "A ver...");
            new Thread(() => {
                long cardUsed;
                var users = GpuUsers(out cardUsed);
                var byName = users.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        MemoryUser u;
                        if (!byName.TryGetValue(p.ProcessName, out u)) byName[p.ProcessName] = u = new MemoryUser { Name = p.ProcessName };
                        u.Ram += p.PrivateMemorySize64;
                    }
                    catch (Exception) { }
                }
                long victory = 0;
                MemoryUser self;
                if (byName.TryGetValue("victory", out self)) victory = self.Vram;
                var shown = byName.Values.Where(u => u.Vram >= (40L << 20) || u.Ram >= (400L << 20)).OrderByDescending(u => u.Vram * 4 + u.Ram).ToList();
                foreach (var u in shown)
                {
                    u.Protected = ProtectedNames.Contains(u.Name);
                    if (u.Name.Equals("dwm", StringComparison.OrdinalIgnoreCase)) u.Note = L("Windows desktop: see the MUX tip below", "Ambiente de trabalho: ve a dica do MUX em baixo");
                    else if (u.Name.Equals("victory", StringComparison.OrdinalIgnoreCase)) u.Note = L("the game", "o jogo");
                    else if (u.Name.StartsWith("NVIDIA Overlay", StringComparison.OrdinalIgnoreCase)) u.Note = L("turn off Instant Replay / overlay in NVIDIA App", "desliga Instant Replay / overlay na NVIDIA App");
                    else if (u.Name.Equals("Discord", StringComparison.OrdinalIgnoreCase)) u.Note = L("or turn off its hardware acceleration", "ou desliga a aceleracao de hardware");
                    else if (u.Name.StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase)) u.Note = "Wallpaper Engine";
                    else if (u.Name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)) u.Note = L("part of other apps (protected)", "parte de outras apps (protegido)");
                    else if (u.Protected) u.Note = L("system or needed (protected)", "sistema ou necessario (protegido)");
                }
                var ram = new Microsoft.VisualBasic.Devices.ComputerInfo();
                double ramTotal = ram.TotalPhysicalMemory / 1073741824.0, ramFree = ram.AvailablePhysicalMemory / 1073741824.0;
                BeginInvoke(new Action(() => {
                    double gb = 1073741824.0;
                    var others = Math.Max(0, cardUsed - victory);
                    vramSummary.Text = cardUsed == 0
                        ? L("Could not read the video memory counters (they need Windows 10 1709 or later).", "Nao deu para ler os contadores de VRAM (precisam do Windows 10 1709 ou mais recente).")
                        : string.Format(L("Card: {0:0.0} of {1:0.0} GB in use  —  VIctory {2:0.0} GB, everything else {3:0.0} GB\r\nRAM: {4:0.0} of {5:0.0} GB free",
                                          "Placa: {0:0.0} de {1:0.0} GB em uso  —  VIctory {2:0.0} GB, o resto {3:0.0} GB\r\nRAM: {4:0.0} de {5:0.0} GB livres"),
                                        cardUsed / gb, vramGb, victory / gb, others / gb, ramFree, ramTotal);
                    vramSummary.ForeColor = others > (2L << 30) ? Theme.Warn : Theme.Text;
                    vramList.BeginUpdate();
                    vramList.Items.Clear();
                    foreach (var u in shown)
                    {
                        var item = new ListViewItem(new[] { u.Name, u.Vram > 0 ? (u.Vram >> 20) + " MB" : "-", (u.Ram >> 20) + " MB", u.Note }) { Tag = u };
                        if (u.Protected) item.ForeColor = Theme.Dim;
                        vramList.Items.Add(item);
                    }
                    vramList.EndUpdate();
                }));
            }) { IsBackground = true }.Start();
        }

        void CloseChecked()
        {
            var names = vramList.CheckedItems.Cast<ListViewItem>().Select(i => i.Tag as MemoryUser).Where(u => u != null && !u.Protected).Select(u => u.Name).ToList();
            if (names.Count == 0) { MessageBox.Show(this, L("Check the apps to close first.", "Marca primeiro as apps a fechar.")); return; }
            var ask = L("Close these apps? Unsaved work in them is lost.\r\n\r\n", "Fechar estas apps? O que nao estiver guardado nelas perde-se.\r\n\r\n") + string.Join("\r\n", names);
            if (MessageBox.Show(this, ask, "edu launcher", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            foreach (var n in names)
                foreach (var p in Process.GetProcessesByName(n))
                {
                    try { if (!p.CloseMainWindow() || !p.WaitForExit(1500)) p.Kill(); }
                    catch (Exception) { }
                }
            Thread.Sleep(800);
            ScanMemory();
        }

        // ---------------------------------------------------------------- Smooth Motion
        void UpdateSmooth()
        {
            if (smoothStatus == null) return;
            string state = smoothState == "on" ? L("ON for victory.exe", "LIGADO para o victory.exe")
                : smoothState == "off" ? L("off for victory.exe", "desligado para o victory.exe")
                : smoothState == "not set" ? L("not set yet (PLAY sets it when the option is on)", "ainda nao configurado (o JOGAR configura com a opcao ligada)")
                : smoothState;
            smoothStatus.Text = L("Driver frame generation: the card shows a made-up frame between two of the game's. Now: ", "Geracao de frames do driver: a placa mostra um frame inventado entre dois do jogo. Agora: ") + state
                + (rtxFrameGen ? "" : L("\r\nNeeds an RTX 40 or 50 series card and driver 571.86 or newer.", "\r\nPrecisa de uma placa RTX 40 ou 50 e do driver 571.86 ou mais recente."));
            smoothStatus.ForeColor = smoothState == "on" ? Theme.Good : Theme.Text;
        }

        void ApplySmooth(bool on, bool loud)
        {
            new Thread(() => {
                var err = SmoothMotion.Set(on);
                var s = SmoothMotion.Status();
                BeginInvoke(new Action(() => {
                    smoothState = s;
                    UpdateSmooth();
                    UpdateBanner();
                    if (err != null) Log("Smooth Motion: " + err);
                    else Log(on ? L("Smooth Motion is on for victory.exe (driver profile).", "Smooth Motion ligado para o victory.exe (perfil do driver).")
                                : L("Smooth Motion is off for victory.exe.", "Smooth Motion desligado para o victory.exe."));
                    if (loud && err != null) MessageBox.Show(this, "Smooth Motion: " + err, "edu launcher");
                }));
            }) { IsBackground = true }.Start();
        }

        // ---------------------------------------------------------------- Controls and Help
        Panel ControlsPage()
        {
            var page = Page();
            page.Controls.Add(new Heading(L("Keyboard and mouse", "Teclado e rato")) { Location = new Point(24, 20), Width = 852 });
            var grid = new ListView { Location = new Point(24, 54), Size = new Size(852, 620), View = View.Details, FullRowSelect = true, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f), HeaderStyle = ColumnHeaderStyle.Nonclickable, OwnerDraw = true };
            grid.Columns.Add(L("Key", "Tecla"), 190);
            grid.Columns.Add(L("Pad", "Comando"), 120);
            grid.Columns.Add(L("On foot", "A pe"), 240);
            grid.Columns.Add(L("In a vehicle (after F, high beep)", "Num veiculo (depois do F, apito agudo)"), 300);
            DarkHeader(grid);
            string[][] rows = Pt ? new[] {
                new[] { "W / A / S / D", "Analogico esq.", "Andar", "W acelerar, S travar/marcha-atras, A/D virar" },
                new[] { "I / K", "Analogico esq. cima/baixo", "(camara)", "Aviao: nariz baixo (I) / cima (K); levantar: W + K" },
                new[] { "Rato", "Analogico dir.", "Olhar (clica para capturar)", "Olhar" },
                new[] { "Botao esquerdo", "RT / RB", "Disparar", "Disparar" },
                new[] { "Botao direito", "LT", "Mirar / interagir", "" },
                new[] { "Shift esq.", "L3", "Correr", "Buzina / nitro" },
                new[] { "F", "Y + modo veiculo", "Entrar no veiculo", "Sair do veiculo" },
                new[] { "E ou Y", "Y", "Interagir, falar", "" },
                new[] { "Espaco / Enter", "A", "Saltar / escalar; luta: bloquear", "Travao de mao" },
                new[] { "Ctrl esq. / C / Backspace", "B", "Agachar / descansar", "Farois; aviao: direita" },
                new[] { "R ou X", "X", "Luta: soco/pontape", "Aviao: esquerda" },
                new[] { "Q", "RB", "Proteger-se", "" },
                new[] { "Tab (manter)", "LB", "Inventario / roda de armas", "Sacar arma" },
                new[] { "M", "View", "Mapa", "Mapa" },
                new[] { "V", "R3", "Olhar para tras", "Olhar para tras" },
                new[] { "Setas ou 1-4", "D-pad", "Telemovel, radio, parceiro", "" },
                new[] { "Esc", "Menu", "Pausa", "Pausa" },
                new[] { "Caps Lock", "-", "Acertar o modo veiculo", "" },
                new[] { "Alt ou F1", "-", "Soltar o rato", "" },
                new[] { "F3", "-", "Mostrar/esconder o overlay", "" },
                new[] { "F9", "-", "Gravar um frame (modo de gravacao)", "" },
            } : new[] {
                new[] { "W / A / S / D", "Left stick", "Move", "W accelerate, S brake/reverse, A/D steer" },
                new[] { "I / K", "Left stick up/down", "(camera)", "Aircraft nose down (I) / up (K); take off: W + K" },
                new[] { "Mouse", "Right stick", "Look (click to capture)", "Look" },
                new[] { "Left mouse button", "RT / RB", "Shoot", "Shoot" },
                new[] { "Right mouse button", "LT", "Aim / interact", "" },
                new[] { "Left Shift", "L3", "Run", "Horn / nitro" },
                new[] { "F", "Y + vehicle mode", "Enter vehicle", "Leave vehicle" },
                new[] { "E or Y", "Y", "Interact, talk", "" },
                new[] { "Space / Enter", "A", "Jump / climb; melee: block", "Handbrake" },
                new[] { "Left Ctrl / C / Backspace", "B", "Crouch / rest", "Headlights; aircraft: right" },
                new[] { "R or X", "X", "Melee: punch/kick", "Aircraft: left" },
                new[] { "Q", "RB", "Take cover", "" },
                new[] { "Tab (hold)", "LB", "Inventory / weapon wheel", "Draw weapon" },
                new[] { "M", "View", "Map", "Map" },
                new[] { "V", "R3", "Look back", "Look back" },
                new[] { "Arrows or 1-4", "D-pad", "Phone, radio, partner", "" },
                new[] { "Esc", "Menu", "Pause", "Pause" },
                new[] { "Caps Lock", "-", "Fix vehicle mode if out of sync", "" },
                new[] { "Alt or F1", "-", "Release the mouse", "" },
                new[] { "F3", "-", "Show/hide the overlay", "" },
                new[] { "F9", "-", "Record a frame (recording mode)", "" },
            };
            foreach (var r in rows) grid.Items.Add(new ListViewItem(r));
            page.Controls.Add(grid);
            page.Controls.Add(Small(L("An Xbox controller connected to the PC replaces all of this.", "Um comando de Xbox ligado ao PC substitui tudo isto."), 24, 684, 852));
            return page;
        }

        Panel HelpPage()
        {
            var page = Page();
            page.Controls.Add(new Heading(L("Help", "Ajuda")) { Location = new Point(24, 20), Width = 852 });
            var text = new Label { Location = new Point(24, 56), Size = new Size(852, 640), ForeColor = Theme.Text, BackColor = Theme.Back, Font = new Font("Segoe UI", 10.5f) };
            text.Text = string.Join("\r\n", Pt ? new[] {
                "Primeira vez: a pagina Preparar faz tudo isto por ti ('Fazer tudo por mim'). A mao:",
                "   1.  Instala o Rust (https://rustup.rs) e o Visual Studio Build Tools com C++ e um Windows SDK.",
                "   2.  Carrega em 'Compilar VIctory' (alguns minutos, uma vez).",
                "   3.  Se o Windows disser 'Acesso negado', a regra anti-ransomware do Defender bloqueou o exe novo. Numa PowerShell",
                "        de administrador: Add-MpPreference -AttackSurfaceReductionOnlyExclusions \"<esta pasta>\\target\"",
                "   4.  Escolhe a pasta do jogo e carrega em 'Preparar shaders' (~5 min; de novo depois de cada compilacao).",
                "   5.  Carrega em JOGAR.",
                "",
                "Para bons FPS",
                "   •  Separador Desempenho: fecha o que ocupa a placa e liga o Smooth Motion.",
                "   •  Portatil ligado a corrente, modo de desempenho.",
                "   •  Memoria virtual do Windows gerida pelo sistema; em sessoes longas, reinicia o jogo a cada ~45 minutos.",
                "",
                "Overlay (F3)",
                "   FPS agora, media do ultimo minuto (AVG 1M), media desde o inicio (ALL) e o pior meio segundo (LOW).",
                "",
                "Resizable BAR",
                "   O launcher verifica-o (nvidia-smi). Sem ele, a escrita direta na VRAM fica desligada; o resto funciona.",
                "",
                "Logs: " + Path.Combine(dataRoot, "logs"),
                "Guia: docs\\LUZ-24FPS.md   ·   Projeto: github.com/edupode/cold-project-vi (ramo edupode)",
                "",
                "Sobre: o VI Launcher e o remake do edupode, feito com o trabalho de:",
            }.Concat(Credits()).ToArray() : new[] {
                "First time: the Setup page does all of this for you ('Do everything for me'). By hand:",
                "   1.  Install Rust (https://rustup.rs) and Visual Studio Build Tools with the C++ workload and a Windows SDK.",
                "   2.  Press 'Build VIctory' (several minutes, once).",
                "   3.  If Windows says 'Access denied' when playing, Defender's ransomware rule blocked the new exe. In an",
                "        administrator PowerShell: Add-MpPreference -AttackSurfaceReductionOnlyExclusions \"<this folder>\\target\"",
                "   4.  Choose your game folder and press 'Prepare shaders' (about 5 minutes; again after each rebuild).",
                "   5.  Press PLAY.",
                "",
                "For good frame rates",
                "   •  Performance page: close what holds the card and turn Smooth Motion on.",
                "   •  Laptop plugged in, performance mode.",
                "   •  Windows virtual memory: system managed; in long sessions restart the game every 45 minutes or so.",
                "",
                "Overlay (F3)",
                "   FPS now, the last minute's average (AVG 1M), the average since the start (ALL) and the worst half second (LOW).",
                "",
                "Resizable BAR",
                "   The launcher checks for it (nvidia-smi). Without it the direct writes into video memory stay off; the rest works.",
                "",
                "Logs: " + Path.Combine(dataRoot, "logs"),
                "Guide: docs\\GUIDE-30FPS.md   ·   Project: github.com/edupode/cold-project-vi (edupode branch)",
                "",
                "About: VI Launcher is edupode's remake, built on the work of:",
            }.Concat(Credits()).ToArray());
            page.Controls.Add(text);
            return page;
        }

        // The projects inside this build, for the Help page.
        static IEnumerable<string> Credits()
        {
            return new[] {
                "   •  VIctory (Marvisak) — the Xbox Series emulator: github.com/Marvisak/VIctory",
                "   •  cgbj (cputroll/cgbj-reupload) — the performance build this one starts from",
                "   •  AnyXboxSeries — Xbox Series runtime work",
                "   •  Leonidas — fixes and research",
                "   •  VIctory-NVIDIA (trustno1-coder) — the lighting for NVIDIA cards",
                "   •  fxceless — rendering and performance fixes",
                "   •  VIctory-7900XTX (avatarmanz) — adaptations from the AMD fork",
                "   •  Cold Project VI (felicienradio) — the project page this remake follows",
            };
        }

        // ---------------------------------------------------------------- state
        string VictoryExe
        {
            get
            {
                foreach (var p in new[] { Path.Combine(root, "target", "diag", "victory.exe"), Path.Combine(root, "victory.exe") })
                    if (File.Exists(p)) return p;
                return null;
            }
        }

        string GameExe { get { return Path.Combine(gameBox.Text.Trim(), "game_scarlett_master.exe"); } }

        void Refresh2()
        {
            bool game = gameBox.Text.Trim().Length > 0 && File.Exists(GameExe);
            gameStatus.Text = game ? L("✓  Game found.", "✓  Jogo encontrado.") : L("Choose the folder of your dump.", "Escolhe a pasta do teu dump.");
            gameStatus.ForeColor = game ? Theme.Good : Theme.Dim;
            var exe = VictoryExe;
            buildStatus.Text = exe != null ? "✓  " + exe : L("Not built yet: press 'Build VIctory' (needs Rust and Visual Studio Build Tools).", "Ainda nao compilado: carrega em 'Compilar VIctory' (precisa do Rust e do Visual Studio Build Tools).");
            buildStatus.ForeColor = exe != null ? Theme.Good : Theme.Warn;
            modeInfo.Text = modeIndex < 0 ? "" : Selected.Description;
            bool ready = game && exe != null && !Running();
            playButton.Enabled = ready;
            prepareButton.Enabled = ready;
            playButton.Invalidate();
            prepareButton.Invalidate();
        }

        void UpdateBanner()
        {
            if (banner == null) return;
            var chips = new List<string>();
            var colors = new List<Color>();
            chips.Add(gpuName); colors.Add(Theme.Pink);
            chips.Add(string.Format(L("{0:0} GB VRAM", "{0:0} GB VRAM"), vramGb)); colors.Add(vramGb >= 10 ? Theme.Good : Theme.Warn);
            chips.Add(string.Format("{0:0} GB RAM", ramGb)); colors.Add(ramGb >= 24 ? Theme.Good : Theme.Warn);
            chips.Add("ReBAR " + (rebar ? L("on", "ligado") : L("off", "desligado"))); colors.Add(rebar ? Theme.Good : Theme.Warn);
            chips.Add("Smooth Motion " + (smoothState == "on" ? L("on", "ligado") : rtxFrameGen ? L("ready", "pronto") : "n/a")); colors.Add(smoothState == "on" ? Theme.Good : rtxFrameGen ? Theme.Cyan : Theme.Dim);
            banner.Chips = chips.ToArray();
            banner.ChipColors = colors.ToArray();
            banner.Invalidate();
        }

        void DetectSystem()
        {
            try
            {
                var smi = RunCapture("nvidia-smi", "--query-gpu=name,memory.total --format=csv,noheader,nounits");
                if (!string.IsNullOrEmpty(smi))
                {
                    var parts = smi.Split('\n')[0].Split(',');
                    gpuName = parts[0].Trim().Replace("NVIDIA GeForce ", "");
                    vramGb = double.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 1024.0;
                    var q = RunCapture("nvidia-smi", "-q -d MEMORY");
                    // BAR1 total as large as video memory (or 1 GB and more) means Resizable BAR is on.
                    var i = q.IndexOf("BAR1 Memory Usage", StringComparison.OrdinalIgnoreCase);
                    if (i >= 0)
                    {
                        var t = q.IndexOf("Total", i, StringComparison.OrdinalIgnoreCase);
                        var digits = new string(q.Substring(t).SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                        double bar;
                        if (double.TryParse(digits, out bar)) rebar = bar >= 1024;
                    }
                    rtxFrameGen = Regex.IsMatch(gpuName, @"RTX\s*(40|50)\d\d");
                }
                var mem = new Microsoft.VisualBasic.Devices.ComputerInfo();
                ramGb = mem.TotalPhysicalMemory / 1073741824.0;
            }
            catch (Exception) { }
            if (!rtxFrameGen) smoothBox.Checked = false;
            if (modeIndex < 0)
                SelectMode((vramGb > 0 && vramGb < 10) || (ramGb > 0 && ramGb < 24) ? 2 : 0);
            UpdateBanner();
            UpdateSmooth();
        }

        static string RunCapture(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                var s = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return s;
            }
            catch (Exception) { return ""; }
        }

        static bool Running() { return Process.GetProcessesByName("victory").Length > 0; }

        void Log(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), line); return; }
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n");
        }

        // ---------------------------------------------------------------- environment
        Dictionary<string, string> Environment2()
        {
            var e = new Dictionary<string, string>();
            Action<string, string> set = (k, v) => e[k] = v;
            set("VICTORY_GAME_DIR", gameBox.Text.Trim());
            set("VICTORY_CACHE_DIR", cacheDir);
            set("VICTORY_DATA_DIR", dataDir);
            set("VICTORY_LOG", "warn,victory_gpu::host=info,victory_gpu::copystat=info");
            // The cgbj base (Play-VIctory.cmd pareto-residency) for a 12 GB card.
            set("VICTORY_DISPLAY_MODE", "1920x1080");
            set("VICTORY_CARD_BUFFERS_MB", "1024");
            set("VICTORY_PIPELINE_BOOK", "1");
            set("VICTORY_RIPE_BLOCKS", "4");
            set("VICTORY_PRESSURE_FREE_MB", "1024");
            set("VICTORY_PRESSURE_CLEAR_MB", "2048");
            set("VICTORY_TEXTURE_RAM_GUARD", "0");
            set("VICTORY_CARD_ROOM_FOR_TARGETS_MB", "1792");
            set("VICTORY_AMBIENT", "1.6");
            set("VICTORY_TEXTURE_NO_WAIT", "1");
            set("VICTORY_RELAY", "2");
            set("VICTORY_WIDE_BACK", "1");
            set("VICTORY_CARD_REHASH_RUNS", "1");
            set("VICTORY_JOIN_COPIES", "1");
            set("VICTORY_INDIRECT_WINDOW", "1");
            set("VICTORY_RELAY_COPY", "1");
            set("VICTORY_CARD_STEPS", "2");
            set("VICTORY_INDIRECT_BATCH", "2");
            set("VICTORY_MAP_THREADS", "1");
            set("VICTORY_CARD_AWAKE", "1");
            set("VICTORY_SHOW_AFTER", "1");
            set("VICTORY_THIN", "1");
            set("VICTORY_JOIN_PARTS", "8");
            set("VICTORY_UPLOAD_AHEAD", "1");
            set("VICTORY_LEVELS_ON_DEMAND", "1");
            set("VICTORY_CARD_BUFFERS_MIN_KB", "64");
            // The NVIDIA fork's lighting, and the one wave64 water program that translates right.
            set("VICTORY_BVH_MISS", "1");
            set("VICTORY_AMBIENT_AIR", "1.5");
            set("VICTORY_AMBIENT_MOST", "2");
            set("VICTORY_NAN_SCRUB", "1");
            set("VICTORY_DYNAMIC_SLOTS", "524288");
            set("VICTORY_WAVE_WIDE_DIGESTS", "7099a57fded142613b44f410463d6be6");
            var skip = Path.Combine(root, "scripts", "rt_off_hashes.txt");
            if (File.Exists(skip)) set("VICTORY_SKIP_PIPELINES", "@" + skip);

            var m = Selected;
            if (m.Scale != null)
            {
                set("VICTORY_RENDER_SCALE", m.Scale);
                set("VICTORY_OWN_UPSCALER", "1");
            }
            if (m.LightTextures)
            {
                set("VICTORY_TEXTURE_MAX", "2048");
                set("VICTORY_LEVELS_ON_DEMAND", "0");
                set("VICTORY_CARD_BUFFERS_MB", "1536");
                set("VICTORY_POOL_FULLEST", "1");
                set("VICTORY_TEXTURE_STAGE_MB", "384");
                if (rebar) set("VICTORY_WINDOWS_BAR_MB", "1024");
            }
            if (m.Modest || (vramGb > 0 && vramGb < 10))
            {
                // 6-8 GB cards: less of the game's buffers on the card, smaller textures and batches.
                set("VICTORY_CARD_BUFFERS_MB", "768");
                set("VICTORY_TEXTURE_MAX", "1024");
                set("VICTORY_TEXTURE_STAGE_MB", "128");
                set("VICTORY_RIPE_BLOCKS", "2");
                set("VICTORY_CARD_ROOM_FOR_TARGETS_MB", "1024");
                e.Remove("VICTORY_WINDOWS_BAR_MB");
            }
            if (m.Modest || (ramGb > 0 && ramGb < 24))
            {
                // 16 GB of RAM: the memory guards as cgbj has them (texture prefetch held back early).
                e.Remove("VICTORY_PRESSURE_FREE_MB");
                e.Remove("VICTORY_PRESSURE_CLEAR_MB");
                e.Remove("VICTORY_TEXTURE_RAM_GUARD");
            }
            if (fightsBox.Checked && !m.Modest) set("VICTORY_CARD_BUFFERS_MB", "2048");
            if (panelBox.Checked) set("VICTORY_PANEL", "1");
            if (borderlessBox.Checked) { set("VICTORY_BORDERLESS", "1"); set("VICTORY_BORDERLESS_DPI", "0"); }
            if (fps60Box.Checked) set("VICTORY_FPS_MAX", "60");
            set("VICTORY_VRAM_RESERVE", reserveBox.Checked ? "max" : "0");
            if (qualityBox.Checked) { set("VICTORY_RELAY", "1"); set("VICTORY_CARD_STEPS", "0"); }
            if (facesBox.Checked) set("VICTORY_TEXTURE_NO_WAIT", "0");
            if (loadingBox.Checked) set("VICTORY_BAKE_NO_WAIT", "1");
            return e;
        }

        Process Start(string args, string logFile) { return Start(args, logFile, null); }

        Process Start(string args, string logFile, Action<string> onLine)
        {
            var psi = new ProcessStartInfo(VictoryExe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = root };
            foreach (var kv in Environment2()) psi.EnvironmentVariables[kv.Key] = kv.Value;
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            var writer = new StreamWriter(logFile, true, new UTF8Encoding(false)) { AutoFlush = true };
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            DataReceivedEventHandler sink = (s, e) => {
                if (e.Data == null) return;
                lock (writer) writer.WriteLine(e.Data);
                if (onLine != null) onLine(e.Data);
            };
            p.OutputDataReceived += sink;
            p.ErrorDataReceived += sink;
            p.Exited += (s, e) => { lock (writer) writer.Dispose(); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }

        void Busy(bool busy)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(Busy), busy); return; }
            playButton.Enabled = !busy; prepareButton.Enabled = !busy; buildButton.Enabled = !busy;
            playButton.Invalidate(); prepareButton.Invalidate(); buildButton.Invalidate();
            if (!busy) Refresh2();
        }

        // ---------------------------------------------------------------- actions
        void Play()
        {
            SaveSettings();
            if (Running()) { MessageBox.Show(this, L("VIctory is already running.", "O VIctory ja esta a correr.")); return; }
            long cardUsed;
            GpuUsers(out cardUsed);
            if (cardUsed > (2L << 30))
                Log(string.Format(L("Other apps already hold {0:0.0} GB of the card: the Performance page frees it.", "Outras apps ja ocupam {0:0.0} GB da placa: a pagina Desempenho liberta-a."), cardUsed / 1073741824.0));
            if (rtxFrameGen && smoothBox.Checked && smoothState != "on") ApplySmooth(true, false);
            else if (!smoothBox.Checked && smoothState == "on") ApplySmooth(false, false);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var log = Path.Combine(dataRoot, "logs", "session-" + stamp + ".log");
            Log(L("Starting ", "A iniciar ") + Selected + ". Log: " + log);
            Busy(true);
            var game = GameExe;
            bool warmFirst = warmBox.Checked;
            new Thread(() => {
                try
                {
                    if (warmFirst) WarmWithBar(log);
                    var p = Start("run \"" + game + "\" --console series-s --execute", log);
                    p.WaitForExit();
                    Log(L("Game closed. Preparing this session's pipelines for the next one...", "Jogo fechado. A preparar as pipelines desta sessao para a proxima..."));
                    var w = Start("warm --used", log);
                    w.WaitForExit();
                    Log(L("Done.", "Feito."));
                }
                catch (Exception ex)
                {
                    Log(L("Could not start: ", "Nao arrancou: ") + ex.Message + (ex.Message.Contains("denied") ? L("  (Defender: see Help)", "  (Defender: ve a Ajuda)") : ""));
                }
                Busy(false);
            }) { IsBackground = true }.Start();
        }

        // Makes the pipelines earlier sessions drew with (victory warm --used) and shows how far it is.
        void WarmWithBar(string log)
        {
            int total = 0;
            BeginInvoke(new Action(() => { loadBar.Visible = true; loadBar.Set(0, L("Loading pipelines...", "A carregar pipelines...")); }));
            var w = Start("warm --used", log, line => {
                var m = Regex.Match(line, @"; (\d+) to make");
                if (m.Success) total = int.Parse(m.Groups[1].Value);
                var p = Regex.Match(line, @"^(\d+) of (\d+), (\d+) s");
                if (p.Success)
                {
                    int n = int.Parse(p.Groups[1].Value), of = int.Parse(p.Groups[2].Value);
                    BeginInvoke(new Action(() => loadBar.Set(of > 0 ? (float)n / of : 1f, string.Format(L("Loading pipelines: {0} of {1}", "A carregar pipelines: {0} de {1}"), n, of))));
                }
                else if (m.Success)
                    BeginInvoke(new Action(() => loadBar.Set(total == 0 ? 1f : 0f, total == 0 ? L("Pipelines ready.", "Pipelines prontas.") : string.Format(L("Loading pipelines: 0 of {0}", "A carregar pipelines: 0 de {0}"), total))));
            });
            w.WaitForExit();
            Log(total == 0 ? L("Pipelines were already ready.", "As pipelines ja estavam prontas.") : string.Format(L("{0} pipelines loaded.", "{0} pipelines carregadas."), total));
            BeginInvoke(new Action(() => { loadBar.Set(1f, L("Starting the game...", "A abrir o jogo...")); }));
            Thread.Sleep(600);
            BeginInvoke(new Action(() => { loadBar.Visible = false; }));
        }

        void Prepare()
        {
            RunSteps(steps.Where(st => st.Fix == (Func<bool>)PrepareBlocking).ToArray());
        }

        void Build()
        {
            RunSteps(steps.Where(st => st.Fix == (Func<bool>)BuildBlocking).ToArray());
        }

        // ---------------------------------------------------------------- settings
        string ReadSetting(string key)
        {
            try
            {
                if (!File.Exists(settingsFile)) return null;
                foreach (var line in File.ReadAllLines(settingsFile))
                    if (line.StartsWith(key + "=")) return line.Substring(key.Length + 1);
            }
            catch (Exception) { }
            return null;
        }

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsFile)) return;
                foreach (var line in File.ReadAllLines(settingsFile))
                {
                    var i = line.IndexOf('=');
                    if (i < 0) continue;
                    var k = line.Substring(0, i); var v = line.Substring(i + 1);
                    switch (k)
                    {
                        case "game": gameBox.Text = v; break;
                        case "mode": int m; if (int.TryParse(v, out m)) SelectMode(m); break;
                        case "panel": panelBox.Checked = v == "1"; break;
                        case "borderless": borderlessBox.Checked = v == "1"; break;
                        case "fps60": fps60Box.Checked = v == "1"; break;
                        case "reserve": reserveBox.Checked = v == "1"; break;
                        case "smooth": smoothBox.Checked = v == "1"; break;
                        case "quality": qualityBox.Checked = v == "1"; break;
                        case "faces": facesBox.Checked = v == "1"; break;
                        case "loading": loadingBox.Checked = v == "1"; break;
                        case "fights": fightsBox.Checked = v == "1"; break;
                        case "warm": warmBox.Checked = v == "1"; break;
                    }
                }
            }
            catch (Exception) { }
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                Func<CheckBox, string> b = c => c.Checked ? "1" : "0";
                File.WriteAllLines(settingsFile, new[] {
                    "lang=" + (Pt ? "pt" : "en"), "cache=" + cacheDir, "data=" + dataDir,
                    "game=" + gameBox.Text.Trim(), "mode=" + modeIndex,
                    "panel=" + b(panelBox), "borderless=" + b(borderlessBox), "fps60=" + b(fps60Box), "reserve=" + b(reserveBox),
                    "smooth=" + b(smoothBox), "quality=" + b(qualityBox), "faces=" + b(facesBox), "loading=" + b(loadingBox), "fights=" + b(fightsBox), "warm=" + b(warmBox),
                });
            }
            catch (Exception) { }
        }
    }
}
