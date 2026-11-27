// edu launcher: one window to set up and play the edupode build of VIctory.
// Built with the C# compiler that ships with Windows (.NET Framework 4.x):
//   launcher\build-launcher.cmd   ->   VIctory-Launcher.exe in the repository's root.
// No game files are included or downloaded; the player points it at their own dump.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }

    // A preset: render scale and the switches it turns on.
    class Mode
    {
        public string Name;
        public string Description;
        public string Scale;      // null: native 1080p
        public bool LightTextures;
        public bool Modest;       // smaller textures and buffers for 6-8 GB cards and 16 GB of RAM
        public Mode(string name, string description, string scale, bool light, bool modest)
        {
            Name = name; Description = description; Scale = scale; LightTextures = light; Modest = modest;
        }
        public override string ToString() { return Name; }
    }

    class LauncherForm : Form
    {
        static readonly Color Back = Color.FromArgb(24, 22, 34);
        static readonly Color Panel = Color.FromArgb(36, 33, 52);
        static readonly Color Accent = Color.FromArgb(255, 92, 170);
        static readonly Color ColText = Color.FromArgb(235, 232, 245);
        static readonly Color Dim = Color.FromArgb(160, 155, 180);

        readonly string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        readonly string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VIctory", "luz");
        readonly string settingsFile;

        TextBox gameBox;
        Label gameStatus, buildStatus, systemLabel;
        ListBox modeList;
        Label modeInfo;
        CheckBox panelBox, borderlessBox, fps60Box, qualityBox, facesBox, loadingBox, fightsBox;
        TextBox logBox;
        Button playButton, prepareButton, buildButton;
        bool rebar;
        double vramGb, ramGb;
        string gpuName = "unknown";

        readonly Mode[] modes = new Mode[] {
            new Mode("Recommended - 720p", "The world drawn at 1280x720 and rebuilt to 1080p by the game's own upscaler, texture levels held (video memory under budget), 60 FPS clock. About 22-26 FPS in the open world and 35+ in cutscenes on an RTX 5070 Ti laptop (6-8 GB of video memory, ~20 GB of RAM).", "0.6666667", true, false),
            new Mode("Fastest - 540p", "The world drawn at 960x540: the most frames, a softer picture. For slower CPUs.", "0.5", true, false),
            new Mode("Modest PC - 540p, light", "For 6-8 GB cards and 16 GB of RAM: 540p, textures over 1024 texels halved, fewer of the game's buffers on the card, smaller texture batches, the memory guards on. Less video memory and RAM, a softer picture.", "0.5", true, true),
            new Mode("Sharp - 1080p", "The world at full 1080p with held texture levels. Needs a strong GPU with 12 GB or more; slower in the open world.", null, true, false),
            new Mode("Classic cgbj - 1080p", "The original cgbj settings at 1080p, no texture changes. For comparison.", null, false, false),
        };

        public LauncherForm()
        {
            settingsFile = Path.Combine(Path.GetDirectoryName(dataRoot), "launcher.ini");
            Text = "edu launcher - VIctory for GTA VI (your own Xbox Series dump)";
            ClientSize = new Size(940, 760);
            MinimumSize = new Size(940, 760);
            BackColor = Back;
            ForeColor = ColText;
            Font = new Font("Segoe UI", 9.75f);
            StartPosition = FormStartPosition.CenterScreen;

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.5f) };
            tabs.TabPages.Add(PlayPage());
            tabs.TabPages.Add(ControlsPage());
            tabs.TabPages.Add(HelpPage());
            Controls.Add(tabs);

            LoadSettings();
            DetectSystem();
            Refresh2();
        }

        // ---------------------------------------------------------------- Play tab
        TabPage PlayPage()
        {
            var page = new TabPage("Play") { BackColor = Back, ForeColor = ColText };
            int y = 14;
            var title = new Label { Text = "edu launcher  |  VIctory: GTA VI on PC from your own dump", Font = new Font("Segoe UI Semibold", 15f), ForeColor = Accent, AutoSize = true, Location = new Point(16, y) };
            page.Controls.Add(title);
            y += 42;

            page.Controls.Add(new Label { Text = "1. Game folder (the one with game_scarlett_master.exe)", AutoSize = true, Location = new Point(16, y), ForeColor = ColText });
            y += 24;
            gameBox = new TextBox { Location = new Point(16, y), Width = 720, BackColor = Panel, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle };
            gameBox.TextChanged += (s, e) => Refresh2();
            page.Controls.Add(gameBox);
            var browse = Flat("Browse...", 750, y - 2, 150);
            browse.Click += (s, e) => {
                using (var d = new FolderBrowserDialog { Description = "Choose the folder that holds game_scarlett_master.exe" })
                    if (d.ShowDialog(this) == DialogResult.OK) gameBox.Text = d.SelectedPath;
            };
            page.Controls.Add(browse);
            y += 30;
            gameStatus = new Label { AutoSize = true, Location = new Point(16, y), ForeColor = Dim };
            page.Controls.Add(gameStatus);
            y += 30;

            page.Controls.Add(new Label { Text = "2. VIctory (the emulator, built from this repository)", AutoSize = true, Location = new Point(16, y), ForeColor = ColText });
            y += 24;
            buildStatus = new Label { AutoSize = true, Location = new Point(16, y + 4), ForeColor = Dim };
            page.Controls.Add(buildStatus);
            buildButton = Flat("Build VIctory", 750, y, 150);
            buildButton.Click += (s, e) => Build();
            page.Controls.Add(buildButton);
            y += 34;
            systemLabel = new Label { AutoSize = true, Location = new Point(16, y), ForeColor = Dim };
            page.Controls.Add(systemLabel);
            y += 30;

            page.Controls.Add(new Label { Text = "3. Mode", AutoSize = true, Location = new Point(16, y), ForeColor = ColText });
            page.Controls.Add(new Label { Text = "Options", AutoSize = true, Location = new Point(470, y), ForeColor = ColText });
            y += 24;
            modeList = new ListBox { Location = new Point(16, y), Size = new Size(430, 118), BackColor = Panel, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10.5f) };
            foreach (var m in modes) modeList.Items.Add(m);
            modeList.SelectedIndexChanged += (s, e) => Refresh2();
            page.Controls.Add(modeList);

            int oy = y;
            panelBox = Check("FPS and diagnostics overlay (F3 hides it)", 470, oy, true); oy += 26;
            borderlessBox = Check("Borderless full screen (for NVIDIA Smooth Motion)", 470, oy, true); oy += 26;
            fps60Box = Check("Unlock the 30 FPS cap (60 FPS clock)", 470, oy, true); oy += 26;
            qualityBox = Check("Anti-stretch: fewer stretched bodies (slower)", 470, oy, false); oy += 26;
            facesBox = Check("Rainbow-face fix (slower)", 470, oy, false); oy += 26;
            loadingBox = Check("Fast loading (experimental)", 470, oy, false); oy += 26;
            fightsBox = Check("Crowds and fights: 2 GB of buffers on the card (12 GB+)", 470, oy, false);
            foreach (var c in new[] { panelBox, borderlessBox, fps60Box, qualityBox, facesBox, loadingBox, fightsBox }) page.Controls.Add(c);
            y += 190;
            modeInfo = new Label { Location = new Point(16, y - 70), Size = new Size(440, 100), ForeColor = Dim };
            page.Controls.Add(modeInfo);
            y += 70;

            prepareButton = Flat("Prepare shaders (first time, ~5 min)", 16, y, 300);
            prepareButton.Click += (s, e) => Prepare();
            page.Controls.Add(prepareButton);
            playButton = new Button { Text = "PLAY", Location = new Point(700, y - 6), Size = new Size(200, 46), BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 14f) };
            playButton.FlatAppearance.BorderSize = 0;
            playButton.Click += (s, e) => Play();
            page.Controls.Add(playButton);
            y += 52;
            logBox = new TextBox { Location = new Point(16, y), Size = new Size(884, 120), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Panel, ForeColor = Dim, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            page.Controls.Add(logBox);
            return page;
        }

        Button Flat(string text, int x, int y, int width)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 28), BackColor = Panel, ForeColor = ColText, FlatStyle = FlatStyle.Flat };
            b.FlatAppearance.BorderColor = Accent;
            return b;
        }

        CheckBox Check(string text, int x, int y, bool on)
        {
            return new CheckBox { Text = text, Checked = on, AutoSize = true, Location = new Point(x, y), ForeColor = ColText };
        }

        // ---------------------------------------------------------------- Controls tab
        TabPage ControlsPage()
        {
            var page = new TabPage("Controls") { BackColor = Back, ForeColor = ColText };
            var grid = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BackColor = Panel, ForeColor = ColText, Font = new Font("Segoe UI", 10f), HeaderStyle = ColumnHeaderStyle.Nonclickable };
            grid.Columns.Add("Key", 190);
            grid.Columns.Add("Pad", 90);
            grid.Columns.Add("On foot", 260);
            grid.Columns.Add("In a vehicle (after F, high beep)", 360);
            string[][] rows = {
                new[] { "W / A / S / D", "Left stick", "Move", "W accelerate, S brake/reverse, A/D steer or roll" },
                new[] { "I / K", "Left stick up/down", "(camera)", "Aircraft nose down (I) / up (K): to take off, hold W and K" },
                new[] { "Mouse", "Right stick", "Look (click the window to capture)", "Look" },
                new[] { "Left mouse button", "RT / RB", "Shoot", "Shoot" },
                new[] { "Right mouse button", "LT", "Aim / interact", "" },
                new[] { "Left Shift", "L3", "Run", "Horn / nitro" },
                new[] { "F", "Y + vehicle mode", "Enter vehicle", "Leave vehicle" },
                new[] { "E or Y", "Y", "Interact, talk", "" },
                new[] { "Space / Enter", "A", "Jump / climb; melee: block/dodge", "Handbrake" },
                new[] { "Left Ctrl / C / Backspace", "B", "Crouch / rest", "Headlights; aircraft: turn right" },
                new[] { "R or X", "X", "Melee: punch/kick", "Aircraft: turn left" },
                new[] { "Q", "RB", "Take cover", "" },
                new[] { "Tab (hold)", "LB", "Inventory / weapon wheel", "Draw weapon" },
                new[] { "M", "View", "Map", "Map" },
                new[] { "V", "R3", "Look back", "Look back" },
                new[] { "Arrows or 1-4", "D-pad", "Phone, radio, quick nav, partner", "" },
                new[] { "Esc", "Menu", "Pause", "Pause" },
                new[] { "Caps Lock", "-", "Fix vehicle mode if out of sync", "" },
                new[] { "Alt or F1", "-", "Release the mouse", "" },
                new[] { "F3", "-", "Show/hide the FPS overlay", "" },
            };
            foreach (var r in rows) grid.Items.Add(new ListViewItem(r));
            page.Controls.Add(grid);
            page.Controls.Add(new Label { Text = "  An Xbox controller connected to the PC replaces all of this.", Dock = DockStyle.Bottom, Height = 28, ForeColor = Dim });
            return page;
        }

        TabPage HelpPage()
        {
            var page = new TabPage("Help") { BackColor = Back, ForeColor = ColText };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Panel, ForeColor = ColText, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f) };
            text.Text = string.Join("\r\n", new[] {
                "First time:",
                "  1. Install Rust (https://rustup.rs) and Visual Studio Build Tools with the C++ workload and a Windows SDK.",
                "  2. Press 'Build VIctory' (several minutes, once).",
                "  3. If Windows says 'Access denied' when playing, Defender's ransomware rule blocked the new exe. In an",
                "     administrator PowerShell: Add-MpPreference -AttackSurfaceReductionOnlyExclusions \"<this folder>\\target\"",
                "  4. Choose your game folder, press 'Prepare shaders' (about 5 minutes; again after each rebuild).",
                "  5. Press PLAY.",
                "",
                "For good frame rates:",
                "  - Close other apps that use the GPU or RAM (emulators, other games, Wallpaper Engine, browsers).",
                "  - Plug the laptop in and use its performance mode.",
                "  - NVIDIA App > Graphics > Program settings > victory.exe: Smooth Motion On, Power: Prefer maximum performance.",
                "  - Windows virtual memory: System managed. Restart the game every 45 minutes or so in long sessions.",
                "",
                "Resizable BAR: the launcher checks for it (nvidia-smi). Without it the direct writes into video memory are",
                "left off; everything else works.",
                "",
                "Logs: " + Path.Combine(dataRoot, "logs"),
                "Guide: docs\\GUIDE-30FPS.md  |  Project: https://github.com/edupode/cold-project-vi/tree/edupode",
            });
            page.Controls.Add(text);
            return page;
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
            gameStatus.Text = game ? "OK: game found." : "Choose the folder of your dump.";
            gameStatus.ForeColor = game ? Color.LightGreen : Dim;
            var exe = VictoryExe;
            buildStatus.Text = exe != null ? "OK: " + exe : "Not built yet: press 'Build VIctory' (needs Rust and Visual Studio Build Tools).";
            buildStatus.ForeColor = exe != null ? Color.LightGreen : Color.Orange;
            var m = modeList.SelectedItem as Mode;
            modeInfo.Text = m == null ? "" : m.Description;
            bool ready = game && exe != null && !Running();
            playButton.Enabled = ready;
            prepareButton.Enabled = ready;
        }

        void DetectSystem()
        {
            try
            {
                var smi = RunCapture("nvidia-smi", "--query-gpu=name,memory.total --format=csv,noheader,nounits");
                if (!string.IsNullOrEmpty(smi))
                {
                    var parts = smi.Split('\n')[0].Split(',');
                    gpuName = parts[0].Trim();
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
                }
                var mem = new Microsoft.VisualBasic.Devices.ComputerInfo();
                ramGb = mem.TotalPhysicalMemory / 1073741824.0;
            }
            catch (Exception) { }
            systemLabel.Text = string.Format("PC: {0}, {1:0} GB video memory, {2:0} GB RAM, Resizable BAR {3}", gpuName, vramGb, ramGb, rebar ? "on" : "off or unknown");
            if (modeList.SelectedIndex < 0)
                modeList.SelectedIndex = (vramGb > 0 && vramGb < 10) || (ramGb > 0 && ramGb < 24) ? 2 : 0;
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
            set("VICTORY_CACHE_DIR", Path.Combine(dataRoot, "cache"));
            set("VICTORY_DATA_DIR", Path.Combine(dataRoot, "data"));
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

            var m = (modeList.SelectedItem as Mode) ?? modes[0];
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
            if (qualityBox.Checked) { set("VICTORY_RELAY", "1"); set("VICTORY_CARD_STEPS", "0"); }
            if (facesBox.Checked) set("VICTORY_TEXTURE_NO_WAIT", "0");
            if (loadingBox.Checked) set("VICTORY_BAKE_NO_WAIT", "1");
            return e;
        }

        Process Start(string args, string logFile)
        {
            var psi = new ProcessStartInfo(VictoryExe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = root };
            foreach (var kv in Environment2()) psi.EnvironmentVariables[kv.Key] = kv.Value;
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            var writer = new StreamWriter(logFile, true, new UTF8Encoding(false)) { AutoFlush = true };
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            DataReceivedEventHandler sink = (s, e) => { if (e.Data != null) lock (writer) writer.WriteLine(e.Data); };
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
            if (!busy) Refresh2();
        }

        // ---------------------------------------------------------------- actions
        void Play()
        {
            SaveSettings();
            if (Running()) { MessageBox.Show(this, "VIctory is already running."); return; }
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var log = Path.Combine(dataRoot, "logs", "session-" + stamp + ".log");
            Log("Starting " + modeList.SelectedItem + ". Log: " + log);
            Busy(true);
            var game = GameExe;
            new Thread(() => {
                try
                {
                    var p = Start("run \"" + game + "\" --console series-s --execute", log);
                    p.WaitForExit();
                    Log("Game closed. Preparing this session's pipelines for the next one...");
                    var w = Start("warm --used", log);
                    w.WaitForExit();
                    Log("Done.");
                }
                catch (Exception ex)
                {
                    Log("Could not start: " + ex.Message + (ex.Message.Contains("denied") ? "  (Defender: see the Help tab)" : ""));
                }
                Busy(false);
            }) { IsBackground = true }.Start();
        }

        void Prepare()
        {
            SaveSettings();
            var log = Path.Combine(dataRoot, "logs", "prepare.log");
            Log("Translating the game's shaders and preparing pipelines (about 5 minutes)... Log: " + log);
            Busy(true);
            var dir = gameBox.Text.Trim();
            new Thread(() => {
                try
                {
                    var p = Start("shaders \"" + dir + "\"", log);
                    p.WaitForExit();
                    var w = Start("warm --used", log);
                    w.WaitForExit();
                    Log("Shaders ready. You can press PLAY.");
                }
                catch (Exception ex) { Log("Failed: " + ex.Message); }
                Busy(false);
            }) { IsBackground = true }.Start();
        }

        void Build()
        {
            Log("Building VIctory (cargo build --locked --profile diag -p victory --features diagnostics); a console window shows the progress.");
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/k cargo build --locked --profile diag -p victory --features diagnostics && echo. && echo Build finished: close this window and return to the launcher.") { WorkingDirectory = root, UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex) { Log("Could not start cargo: " + ex.Message + ". Install Rust from https://rustup.rs"); }
        }

        // ---------------------------------------------------------------- settings
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
                        case "mode": int m; if (int.TryParse(v, out m) && m >= 0 && m < modes.Length) modeList.SelectedIndex = m; break;
                        case "panel": panelBox.Checked = v == "1"; break;
                        case "borderless": borderlessBox.Checked = v == "1"; break;
                        case "fps60": fps60Box.Checked = v == "1"; break;
                        case "quality": qualityBox.Checked = v == "1"; break;
                        case "faces": facesBox.Checked = v == "1"; break;
                        case "loading": loadingBox.Checked = v == "1"; break;
                        case "fights": fightsBox.Checked = v == "1"; break;
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
                    "game=" + gameBox.Text.Trim(), "mode=" + modeList.SelectedIndex,
                    "panel=" + b(panelBox), "borderless=" + b(borderlessBox), "fps60=" + b(fps60Box),
                    "quality=" + b(qualityBox), "faces=" + b(facesBox), "loading=" + b(loadingBox), "fights=" + b(fightsBox),
                });
            }
            catch (Exception) { }
        }
    }
}
