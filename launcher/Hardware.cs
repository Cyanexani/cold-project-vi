// VI Launcher: the PC's hardware, its permanent ID, the hardware-only report (opt-in, write-only),
// the engine an AMD card plays with (Belobug's AMD fork), and frame generation for each card.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VIctoryLauncher
{
    partial class LauncherForm
    {
        // ---------------------------------------------------------------- cards and engines
        bool hasNvidia, hasRadeon, rtx30;
        string driverVersion = "";
        string radeonName = "";

        // Belobug's AMD fork of VIctory (GPL-3.0): github.com/Belobug/VIctory-Belobug-AMD.
        public const string AmdRepo = "https://github.com/Belobug/VIctory-Belobug-AMD";
        public const string AmdBranch = "a-publier";
        // The commit the launcher was checked with; Setup builds this one.
        public const string AmdCommit = "4eb6cfdf9e61cb112f1f811344ec0dd309eddd6c";

        // Beside the other data, so it never depends on where the launcher sits (Downloads, Program Files).
        string AmdEngineDir { get { return Path.Combine(Path.GetDirectoryName(dataRoot), "engines", "amd"); } }

        // launcher.ini's engine=: auto (an AMD card without an NVIDIA one plays with the AMD engine), edupode, amd.
        string EngineChoice { get { var v = ReadSetting("engine"); return v == "amd" || v == "edupode" ? v : "auto"; } }

        bool UseAmdEngine
        {
            get
            {
                var c = EngineChoice;
                return c == "amd" || (c == "auto" && (hasRadeon && !hasNvidia || probeVendor == "1002"));
            }
        }

        string EngineName { get { return UseAmdEngine ? "AMD · Belobug" : "edupode"; } }

        // The graphics cards Windows lists (names only).
        void DetectCards()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility FROM Win32_VideoController"))
                    foreach (ManagementObject o in s.Get())
                    {
                        var name = (o["Name"] as string ?? "").Trim();
                        if (name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) hasNvidia = true;
                        // A Radeon card of its own (RX, Pro), not the graphics inside a Ryzen processor.
                        if (Regex.IsMatch(name, @"Radeon\s*(RX|Pro|VII)", RegexOptions.IgnoreCase)) { hasRadeon = true; radeonName = name; }
                    }
            }
            catch (Exception) { }
            rtx30 = Regex.IsMatch(gpuName, @"RTX\s*30\d\d");
            if (!hasNvidia && hasRadeon) gpuName = radeonName.Replace("AMD ", "");
        }

        // `victory gpu` (this repository's build): the card a run gets and whether D3D12 offers it
        // Resizable BAR (GPU upload heaps). Without the build, the BIOS side (nvidia-smi) stands.
        void ProbeGpu()
        {
            var exe = OwnVictoryExe;
            if (exe == null) return;
            new Thread(() => {
                var text = RunCapture(exe, "gpu");
                var kv = new Dictionary<string, string>();
                foreach (var line in text.Split('\n'))
                {
                    var i = line.IndexOf('=');
                    if (i > 0) kv[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                string v;
                if (!kv.TryGetValue("gpu_upload_heap", out v)) return;
                try
                {
                    BeginInvoke(new Action(() => {
                        rebarHeap = v == "true" ? 1 : 0;
                        kv.TryGetValue("adapter", out probeAdapter);
                        kv.TryGetValue("vendor", out probeVendor);
                        probeAdapter = probeAdapter ?? ""; probeVendor = probeVendor ?? "";
                        string mb;
                        double d;
                        if (vramGb <= 0 && kv.TryGetValue("dedicated_mb", out mb) && double.TryParse(mb, out d)) vramGb = d / 1024.0;
                        if (gpuName == "unknown GPU" && probeAdapter.Length > 0) gpuName = probeAdapter;
                        rebar = rebarHeap == 1;
                        UpdateBanner();
                        Refresh2();
                        Log(string.Format(L("Graphics card for VIctory: {0}; Resizable BAR for the game: {1}.", "Placa grafica do VIctory: {0}; Resizable BAR para o jogo: {1}."),
                            probeAdapter, rebarHeap == 1 ? L("yes", "sim") : rebarBios ? L("on in the BIOS, not offered by Windows (see Setup)", "ligado na BIOS, o Windows nao o oferece (ve o Preparar)") : L("no", "nao")));
                    }));
                }
                catch (Exception) { }
            }) { IsBackground = true }.Start();
        }

        // Windows' graphics preference for victory.exe: on a laptop with two cards the run must get
        // the dedicated one (`victory gpu` names the one it gets).
        bool GpuPreferenceOk()
        {
            if (probeAdapter.Length == 0) return true;
            if (hasNvidia && probeAdapter.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (!hasNvidia && hasRadeon && !Regex.IsMatch(probeAdapter, @"Radeon\s*(RX|Pro|VII)", RegexOptions.IgnoreCase)) return false;
            return true;
        }

        // "GpuPreference=2;" (high performance) for every victory.exe the launcher may start.
        bool SetGpuPreference()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                    foreach (var exe in OwnExeCandidates.Concat(new[] {
                                                Path.Combine(AmdEngineDir, "target", "diag", "victory.exe"), Path.Combine(AmdEngineDir, "victory.exe") }))
                        k.SetValue(exe, "GpuPreference=2;");
                Log(L("Windows now gives victory.exe the high-performance graphics card (restart the game to apply).", "O Windows da agora ao victory.exe a placa grafica de alto desempenho (reinicia o jogo para aplicar)."));
                return true;
            }
            catch (Exception e) { Log("GPU preference: " + e.Message); return false; }
        }

        bool GpuPreferenceSet()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                {
                    var exe = VictoryExe;
                    if (k == null || exe == null) return false;
                    var v = k.GetValue(exe) as string;
                    return v != null && v.Contains("GpuPreference=2");
                }
            }
            catch (Exception) { return false; }
        }

        // The AMD fork's switches (its README): the renderer patches its measured run had on, the
        // frame interval that lets the title pass 30 FPS, keyboard and mouse for GTA, 1080p output.
        Dictionary<string, string> AmdEnvironment()
        {
            var e = new Dictionary<string, string>();
            Action<string, string> set = (k, v) => e[k] = v;
            set("VICTORY_GAME_DIR", gameBox.Text.Trim());
            set("VICTORY_CACHE_DIR", Path.Combine(dataRoot, "cache-amd"));
            set("VICTORY_DATA_DIR", dataDir);
            set("VICTORY_LOG", "warn");
            set("VICTORY_PORT_CONSTANTS", "1");
            set("VICTORY_PORT_DESCRIPTORS", "1");
            set("VICTORY_PORT_CBV", "1");
            set("VICTORY_PORT_GUARD", "1");
            set("VICTORY_PORT_JOURNAL", "1");
            set("VICTORY_OUTPUT", "1920x1080");
            set("VICTORY_WINDOW_MAX", "1920x1080");
            set("VICTORY_KBM_GTA", "1");
            // AMD cards run 64-lane waves natively: the wave-wide form is the console's own.
            set("VICTORY_WAVE_WIDE", "1");
            if (fps60Box.Checked && !Selected.UltraLow) set("VICTORY_FRAME_INTERVAL_MS", "16.667");
            // The switches both builds share.
            if (panelBox.Checked) set("VICTORY_PANEL", "1");
            if (borderlessBox.Checked) { set("VICTORY_BORDERLESS", "1"); set("VICTORY_BORDERLESS_DPI", "0"); }
            var m = Selected;
            var scale = m.Custom ? custom.Scale : m.Scale;
            if (scale != null) { set("VICTORY_RENDER_SCALE", scale); set("VICTORY_OWN_UPSCALER", "1"); }
            return e;
        }

        // ---------------------------------------------------------------- frame generation, per card
        string NvSmoothDll { get { return Path.Combine(root, "tools", "nvsmooth30", "version.dll"); } }

        bool FrameGenAvailable { get { return rtxFrameGen || (rtx30 && File.Exists(NvSmoothDll)); } }

        string FrameGenLine()
        {
            if (rtxFrameGen) return L("NVIDIA Smooth Motion (RTX 40/50, set up for you)", "NVIDIA Smooth Motion (RTX 40/50, configurado por ti)");
            if (rtx30) return File.Exists(NvSmoothDll) ? L("NVSmooth30 for RTX 30 (experimental)", "NVSmooth30 para RTX 30 (experimental)") : L("RTX 30: NVSmooth30 comes with the next release", "RTX 30: o NVSmooth30 vem na proxima versao");
            return L("Not for this card yet (FSR 3 frame generation is planned)", "Ainda nao para esta placa (geracao de frames FSR 3 em preparacao)");
        }

        string FrameGenChip()
        {
            if (rtxFrameGen) return "Smooth Motion " + (smoothState == "on" ? L("On", "ligado") : L("ready", "pronto"));
            if (rtx30 && File.Exists(NvSmoothDll)) return "NVSmooth30 " + (smoothBox.Checked ? L("On", "ligado") : L("off", "desligado"));
            return L("Frame gen n/a", "Geracao de frames n/d");
        }

        // Before PLAY: the driver profile (Smooth Motion), and on RTX 30 NVSmooth30's version.dll
        // beside victory.exe (taken away again when the game closes: RemoveNvSmooth).
        void ApplyFrameGen()
        {
            if (rtxFrameGen)
            {
                if (smoothBox.Checked && smoothState != "on") ApplySmooth(true, false);
                else if (!smoothBox.Checked && smoothState == "on") ApplySmooth(false, false);
                return;
            }
            if (!rtx30 || !File.Exists(NvSmoothDll) || VictoryExe == null) return;
            var beside = Path.Combine(Path.GetDirectoryName(VictoryExe), "version.dll");
            try
            {
                if (smoothBox.Checked)
                {
                    File.Copy(NvSmoothDll, beside, true);
                    if (smoothState != "on") ApplySmooth(true, false);
                    Log(L("NVSmooth30 (experimental) is on for this session.", "NVSmooth30 (experimental) ligado nesta sessao."));
                }
                else RemoveNvSmooth();
            }
            catch (Exception e) { Log("NVSmooth30: " + e.Message); }
        }

        void RemoveNvSmooth()
        {
            try
            {
                if (VictoryExe == null) return;
                var beside = Path.Combine(Path.GetDirectoryName(VictoryExe), "version.dll");
                if (File.Exists(beside)) File.Delete(beside);
            }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- hardware, ID and the report
        class HardwareInfo
        {
            public string Id = "", Gpu = "", GpuVendor = "", Driver = "", Cpu = "", Board = "", BoardMaker = "", Os = "", Disk = "";
            public int VramMb, RamGb, Cores, Threads;
            public bool Laptop, RebarBios;
            public int RebarHeap = -1;
        }

        HardwareInfo hw;
        Label idLabel;
        TextBox hwText, hwJson;
        CheckBox shareBox;
        Label shareStatus;
        ComboBox engineBox;

        // Where the hardware report goes (server\hardware-worker). Empty: the database is not online
        // yet, and nothing is sent.
        public const string HardwareEndpoint = "";

        static string Wmi(string query, string field)
        {
            try
            {
                using (var s = new ManagementObjectSearcher(query))
                    foreach (ManagementObject o in s.Get())
                    {
                        var v = o[field];
                        if (v != null && v.ToString().Trim().Length > 0) return v.ToString().Trim();
                    }
            }
            catch (Exception) { }
            return "";
        }

        static string WmiIn(string scope, string query, string field)
        {
            try
            {
                using (var s = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query)))
                    foreach (ManagementObject o in s.Get())
                    {
                        var v = o[field];
                        if (v != null) return v.ToString().Trim();
                    }
            }
            catch (Exception) { }
            return "";
        }

        HardwareInfo ReadHardware()
        {
            var h = new HardwareInfo();
            // The ID: a hash of parts that stay when Windows is installed again (the board's UUID and
            // serial, the CPU's ID); none of them leaves the PC.
            var uuid = Wmi("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");
            var serial = Wmi("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
            var cpuId = Wmi("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");
            h.Id = MakeId(uuid + "|" + serial + "|" + cpuId);
            h.Gpu = hasNvidia || !hasRadeon ? gpuName : radeonName;
            h.GpuVendor = hasNvidia ? "NVIDIA" : hasRadeon ? "AMD" : probeVendor;
            h.Driver = driverVersion;
            h.VramMb = (int)(vramGb * 1024);
            h.RebarBios = rebarBios;
            h.RebarHeap = rebarHeap;
            h.Cpu = Wmi("SELECT Name FROM Win32_Processor", "Name");
            int n;
            if (int.TryParse(Wmi("SELECT NumberOfCores FROM Win32_Processor", "NumberOfCores"), out n)) h.Cores = n;
            if (int.TryParse(Wmi("SELECT NumberOfLogicalProcessors FROM Win32_Processor", "NumberOfLogicalProcessors"), out n)) h.Threads = n;
            h.RamGb = (int)Math.Round(ramGb);
            h.BoardMaker = Wmi("SELECT Manufacturer FROM Win32_BaseBoard", "Manufacturer");
            h.Board = Wmi("SELECT Product FROM Win32_BaseBoard", "Product");
            h.Os = Wmi("SELECT Caption FROM Win32_OperatingSystem", "Caption") + " " + Wmi("SELECT BuildNumber FROM Win32_OperatingSystem", "BuildNumber");
            // The system drive's kind: 3 HDD, 4 SSD (Storage's MSFT_PhysicalDisk).
            var media = WmiIn(@"\\.\root\Microsoft\Windows\Storage", "SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='0'", "MediaType");
            h.Disk = media == "4" ? "SSD" : media == "3" ? "HDD" : "unknown";
            var chassis = Wmi("SELECT ChassisTypes FROM Win32_SystemEnclosure", "ChassisTypes");
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                    foreach (ManagementObject o in s.Get())
                    {
                        var types = o["ChassisTypes"] as ushort[];
                        if (types != null && types.Any(t => t == 8 || t == 9 || t == 10 || t == 14 || t == 31 || t == 32)) h.Laptop = true;
                    }
            }
            catch (Exception) { }
            return h;
        }

        // VI-XXXX-XXXX: the first 40 bits of a SHA-256 in base 32 (no 0/O, 1/I).
        static string MakeId(string parts)
        {
            const string digits = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(parts + "|cold-project-vi"));
            ulong v = 0;
            for (int i = 0; i < 5; i++) v = v << 8 | hash[i];
            var sb = new StringBuilder("VI-");
            for (int i = 7; i >= 0; i--)
            {
                sb.Append(digits[(int)(v >> (i * 5) & 31)]);
                if (i == 4) sb.Append('-');
            }
            return sb.ToString();
        }

        static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // Exactly what the report holds: hardware and the ID, nothing else.
        static string HardwareJson(HardwareInfo h)
        {
            return "{\n"
                + "  \"schema\": 1,\n"
                + "  \"id\": " + Json(h.Id) + ",\n"
                + "  \"gpu\": " + Json(h.Gpu) + ",\n"
                + "  \"gpu_vendor\": " + Json(h.GpuVendor) + ",\n"
                + "  \"gpu_driver\": " + Json(h.Driver) + ",\n"
                + "  \"vram_mb\": " + h.VramMb + ",\n"
                + "  \"rebar_bios\": " + (h.RebarBios ? "true" : "false") + ",\n"
                + "  \"rebar_d3d12\": " + (h.RebarHeap == 1 ? "true" : h.RebarHeap == 0 ? "false" : "null") + ",\n"
                + "  \"cpu\": " + Json(h.Cpu) + ",\n"
                + "  \"cpu_cores\": " + h.Cores + ",\n"
                + "  \"cpu_threads\": " + h.Threads + ",\n"
                + "  \"ram_gb\": " + h.RamGb + ",\n"
                + "  \"board_maker\": " + Json(h.BoardMaker) + ",\n"
                + "  \"board\": " + Json(h.Board) + ",\n"
                + "  \"os\": " + Json(h.Os) + ",\n"
                + "  \"disk\": " + Json(h.Disk) + ",\n"
                + "  \"laptop\": " + (h.Laptop ? "true" : "false") + "\n"
                + "}";
        }

        // A random secret kept only on this PC: the database takes a newer report for this ID only
        // with it, so knowing someone's ID is not enough to change their entry.
        string ShareSecret()
        {
            var file = Path.Combine(Path.GetDirectoryName(dataRoot), "hardware-secret.txt");
            try
            {
                if (File.Exists(file)) { var s = File.ReadAllText(file).Trim(); if (s.Length == 64) return s; }
                var bytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
                var hex = string.Concat(bytes.Select(b => b.ToString("x2")).ToArray());
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, hex);
                return hex;
            }
            catch (Exception) { return null; }
        }

        // Sends the report (POST only; the answer is read for its status and nothing else), at most
        // once a day, when the player turned sharing on.
        void StartHardwareShare()
        {
            if (ReadSetting("share") != "1" || HardwareEndpoint.Length == 0) return;
            var last = ReadSetting("share_sent");
            if (last == DateTime.UtcNow.ToString("yyyy-MM-dd")) return;
            new Thread(() => {
                try
                {
                    Thread.Sleep(15000);
                    var h = ReadHardware();
                    var secret = ShareSecret();
                    if (secret == null) return;
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                    var req = (HttpWebRequest)WebRequest.Create(HardwareEndpoint);
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.UserAgent = "VI-Launcher";
                    req.Timeout = 15000;
                    req.Headers["X-VI-Secret"] = secret;
                    var body = Encoding.UTF8.GetBytes(HardwareJson(h));
                    req.ContentLength = body.Length;
                    using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                    using (var r = (HttpWebResponse)req.GetResponse())
                        if ((int)r.StatusCode / 100 == 2) WriteSetting("share_sent", DateTime.UtcNow.ToString("yyyy-MM-dd"));
                }
                catch (Exception) { }
            }) { IsBackground = true }.Start();
        }

        // ---------------------------------------------------------------- Hardware & ID page
        Panel HardwarePage()
        {
            var page = Page();
            const int X = 28, W = 900;
            int y = 18;
            page.Controls.Add(new Heading(L("Your ID", "O teu ID")) { Location = new Point(X, y), Width = W });
            y += 32;
            idLabel = new Label { Location = new Point(X, y), Size = new Size(300, 40), Font = Theme.Mono(18f), ForeColor = Theme.Cyan, BackColor = Theme.Back, Text = "VI-····-····" };
            page.Controls.Add(idLabel);
            var copy = new Pill { Text = L("Copy ID", "Copiar ID"), Location = new Point(X + 310, y + 3), Size = new Size(120, 34) };
            copy.Click += (s, e) => { if (hw != null) { Clipboard.SetText(hw.Id); Log(L("ID copied: ", "ID copiado: ") + hw.Id); } };
            page.Controls.Add(copy);
            y += 44;
            page.Controls.Add(new Label { Location = new Point(X, y), Size = new Size(W, 38), ForeColor = Theme.Dim, BackColor = Theme.Back, Font = Theme.Ui(9f),
                Text = L("Made from parts of your PC (the motherboard and the processor): it stays the same after reinstalling Windows. Put it in your bug reports.",
                         "Feito a partir de pecas do teu PC (placa-mae e processador): fica igual depois de reinstalar o Windows. Poe-no nos teus relatorios de erros.") });
            y += 44;

            page.Controls.Add(new Heading(L("Hardware", "Hardware")) { Location = new Point(X, y), Width = W });
            y += 32;
            hwText = new TextBox { Location = new Point(X, y), Size = new Size(W, 150), Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Mono(9f) };
            page.Controls.Add(hwText);
            y += 162;

            page.Controls.Add(new Heading(L("Engine", "Motor")) { Location = new Point(X, y), Width = W });
            y += 32;
            engineBox = new ComboBox { Location = new Point(X, y), Width = 420, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Card, ForeColor = Theme.Text };
            engineBox.Items.AddRange(new object[] {
                L("Automatic (AMD cards: Belobug's AMD engine)", "Automatico (placas AMD: motor AMD do Belobug)"),
                L("edupode (NVIDIA)", "edupode (NVIDIA)"),
                L("AMD · VIctory-Belobug-AMD by Belobug", "AMD · VIctory-Belobug-AMD do Belobug"),
            });
            engineBox.SelectedIndex = EngineChoice == "edupode" ? 1 : EngineChoice == "amd" ? 2 : 0;
            engineBox.SelectedIndexChanged += (s, e) => {
                WriteSetting("engine", engineBox.SelectedIndex == 1 ? "edupode" : engineBox.SelectedIndex == 2 ? "amd" : "auto");
                Log(L("Engine: ", "Motor: ") + EngineName);
                UpdateBanner(); Refresh2();
            };
            page.Controls.Add(engineBox);
            y += 34;
            page.Controls.Add(new Label { Location = new Point(X, y), Size = new Size(W, 40), ForeColor = Theme.Dim, BackColor = Theme.Back, Font = Theme.Ui(9f),
                Text = L("AMD cards play with VIctory-Belobug-AMD, Belobug's fork tuned for Radeon (github.com/Belobug/VIctory-Belobug-AMD, GPL-3.0). All credit for it is Belobug's. Setup builds it once.",
                         "As placas AMD jogam com o VIctory-Belobug-AMD, o fork do Belobug afinado para Radeon (github.com/Belobug/VIctory-Belobug-AMD, GPL-3.0). Todo o credito e do Belobug. O Preparar compila-o uma vez.") });
            y += 48;

            page.Controls.Add(new Heading(L("Hardware sharing (only hardware)", "Partilha de hardware (so hardware)")) { Location = new Point(X, y), Width = W });
            y += 32;
            shareBox = new OptionCard { Text = L("Share my hardware with edupode", "Partilhar o meu hardware com o edupode"), Line = L("Off by default. Only the list below; never logs, options, paths, names or your IP.", "Desligado por defeito. So a lista em baixo; nunca logs, opcoes, caminhos, nomes nem o teu IP."),
                Location = new Point(X, y), Size = new Size(W, 58), Checked = ReadSetting("share") == "1" };
            shareBox.CheckedChanged += (s, e) => {
                WriteSetting("share", shareBox.Checked ? "1" : "0");
                if (shareBox.Checked) { WriteSetting("share_sent", ""); StartHardwareShare(); }
                RefreshHardware();
            };
            page.Controls.Add(shareBox);
            y += 66;
            shareStatus = new Label { Location = new Point(X, y), Size = new Size(W, 40), ForeColor = Theme.Dim, BackColor = Theme.Back, Font = Theme.Ui(9f) };
            page.Controls.Add(shareStatus);
            y += 44;
            page.Controls.Add(Small(L("Exactly what is sent:", "Exatamente o que e enviado:"), X, y, W));
            y += 22;
            hwJson = new TextBox { Location = new Point(X, y), Size = new Size(W, 300), Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, ForeColor = Theme.Dim, Font = Theme.Mono(8.5f), ScrollBars = ScrollBars.Vertical };
            page.Controls.Add(hwJson);
            page.Controls.Add(new Label { Location = new Point(X, y + 310), Size = new Size(10, 10), BackColor = Theme.Back });
            return page;
        }

        void RefreshHardware()
        {
            new Thread(() => {
                var h = ReadHardware();
                try
                {
                    BeginInvoke(new Action(() => {
                        hw = h;
                        idLabel.Text = h.Id;
                        hwText.Text = string.Join("\r\n", new[] {
                            L("Graphics card ", "Placa grafica ") + ": " + h.Gpu + (h.Driver.Length > 0 ? "  (driver " + h.Driver + ")" : "") + "  ·  " + h.VramMb / 1024 + " GB",
                            "Resizable BAR : " + RebarText() + (probeAdapter.Length > 0 ? "  ·  " + L("VIctory runs on ", "o VIctory corre na ") + probeAdapter : ""),
                            L("Processor     ", "Processador   ") + ": " + h.Cpu + "  ·  " + h.Cores + L(" cores, ", " nucleos, ") + h.Threads + " threads",
                            "RAM           : " + h.RamGb + " GB",
                            L("Motherboard   ", "Placa-mae     ") + ": " + h.BoardMaker + " " + h.Board,
                            "Windows       : " + h.Os,
                            L("System disk   ", "Disco         ") + ": " + h.Disk + (h.Laptop ? L("  ·  laptop", "  ·  portatil") : ""),
                            L("Engine        ", "Motor         ") + ": " + EngineName,
                        });
                        hwJson.Text = HardwareJson(h).Replace("\n", "\r\n");
                        shareStatus.Text = HardwareEndpoint.Length == 0
                            ? L("The database is not online yet: nothing is sent, even with sharing on.", "A base de dados ainda nao esta online: nada e enviado, mesmo com a partilha ligada.")
                            : !shareBox.Checked ? L("Off: nothing is sent.", "Desligado: nada e enviado.")
                            : L("On: sent at most once a day, to a database only edupode can read. Turn it off any time.", "Ligado: enviado no maximo uma vez por dia, para uma base de dados que so o edupode le. Desliga quando quiseres.");
                    }));
                }
                catch (Exception) { }
            }) { IsBackground = true }.Start();
        }
    }
}
