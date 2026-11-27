// The Setup page: everything a first run needs, checked and done for the player. Each step says
// whether it is done and has a button that does it; "Do everything for me" runs the missing ones
// in order. Steps that change Windows ask for administrator rights (the UAC prompt) or a
// confirmation first; nothing restarts the PC without a yes.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VIctoryLauncher
{
    partial class LauncherForm
    {
        // 1: done, 0: to do, -1: not known (or nothing to do here).
        class Step
        {
            public string Title, Why, FixText;
            public Func<int> Check;
            public Func<bool> Fix;        // runs on a worker thread; true if it did its part
            public bool Auto = true;      // part of "Do everything for me"
            public Label Mark, Text, Detail;
            public Pill Button;
            public int State = -1;
        }

        readonly List<Step> steps = new List<Step>();
        LoadBar setupBar;
        Pill everything;
        volatile bool setupBusy;

        [DllImport("kernel32.dll")] static extern bool GetFirmwareType(out uint type);

        string CargoExe
        {
            get
            {
                var mine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "cargo.exe");
                return File.Exists(mine) ? mine : "cargo";
            }
        }

        static bool Uefi()
        {
            try { uint t; return GetFirmwareType(out t) && t == 2; } catch (Exception) { return false; }
        }

        // ---------------------------------------------------------------- page
        Panel SetupPage()
        {
            var page = Page();
            const int X = 24, W = 852;
            int y = 20;
            page.Controls.Add(new Heading(L("Setup: everything done for you", "Preparar: tudo feito por ti")) { Location = new Point(X, y), Width = W });
            y += 32;
            page.Controls.Add(new Label { Location = new Point(X, y), Size = new Size(W - 250, 40), ForeColor = Theme.Dim, BackColor = Theme.Back,
                Text = L("Each step checks your PC. Press its button, or 'Do everything for me' to run the missing ones in order. Windows asks before anything that needs administrator rights.",
                         "Cada passo verifica o teu PC. Carrega no botao dele, ou em 'Fazer tudo por mim' para correr os que faltam por ordem. O Windows pergunta antes de tudo o que precisa de administrador.") });
            everything = new Pill { Main = true, Text = L("Do everything for me", "Fazer tudo por mim"), Location = new Point(X + W - 236, y), Size = new Size(236, 44), Font = new Font("Segoe UI Semibold", 11f) };
            everything.Click += (s, e) => DoEverything();
            page.Controls.Add(everything);
            y += 52;
            setupBar = new LoadBar { Location = new Point(X, y), Size = new Size(W, 26), Visible = false };
            page.Controls.Add(setupBar);
            y += 34;

            DefineSteps();
            foreach (var st in steps)
            {
                var card = new Panel { Location = new Point(X, y), Size = new Size(W, 58), BackColor = Theme.Card };
                st.Mark = new Label { Location = new Point(12, 14), Size = new Size(30, 30), Font = new Font("Segoe UI Semibold", 15f), ForeColor = Theme.Dim, BackColor = Theme.Card, Text = "•" };
                st.Text = new Label { Location = new Point(48, 8), Size = new Size(W - 260, 22), Font = new Font("Segoe UI Semibold", 10.5f), ForeColor = Theme.Text, BackColor = Theme.Card, Text = st.Title };
                st.Detail = new Label { Location = new Point(48, 31), Size = new Size(W - 260, 20), ForeColor = Theme.Dim, BackColor = Theme.Card, Text = st.Why, AutoEllipsis = true };
                st.Button = new Pill { Text = st.FixText, Location = new Point(W - 196, 12), Size = new Size(184, 34), BackColor = Theme.Card };
                var me = st;
                st.Button.Click += (s, e) => RunSteps(new[] { me });
                card.Controls.Add(st.Mark); card.Controls.Add(st.Text); card.Controls.Add(st.Detail); card.Controls.Add(st.Button);
                var tips = new ToolTip { AutoPopDelay = 20000 };
                tips.SetToolTip(st.Detail, st.Why);
                page.Controls.Add(card);
                y += 64;
            }
            page.Controls.Add(new Label { Location = new Point(X, y), Size = new Size(10, 10), BackColor = Theme.Back });
            return page;
        }

        void DefineSteps()
        {
            steps.Clear();
            steps.Add(new Step {
                Title = L("Rust (builds VIctory)", "Rust (compila o VIctory)"),
                Why = L("The compiler VIctory is written for. Installed with winget (rustup).", "O compilador do VIctory. Instalado com o winget (rustup)."),
                FixText = L("Install", "Instalar"),
                Check = () => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "cargo.exe")) || RunCapture("where", "cargo").Contains("cargo") ? 1 : 0,
                Fix = () => Winget("Rustlang.Rustup", null),
            });
            steps.Add(new Step {
                Title = L("Visual Studio Build Tools (C++)", "Visual Studio Build Tools (C++)"),
                Why = L("The C++ linker and Windows SDK Rust needs. A large download (2-6 GB); Windows asks for permission.", "O linker de C++ e o Windows SDK de que o Rust precisa. Download grande (2-6 GB); o Windows pede autorizacao."),
                FixText = L("Install", "Instalar"),
                Check = () => HasCppTools() ? 1 : 0,
                Fix = () => Winget("Microsoft.VisualStudio.2022.BuildTools", "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"),
            });
            steps.Add(new Step {
                Title = L("Build VIctory", "Compilar o VIctory"),
                Why = L("Compiles the emulator from this folder (several minutes, once; again after an update).", "Compila o emulador a partir desta pasta (alguns minutos, uma vez; de novo depois de atualizar)."),
                FixText = L("Build", "Compilar"),
                Check = () => VictoryExe != null ? 1 : 0,
                Fix = BuildBlocking,
            });
            steps.Add(new Step {
                Title = L("Windows Defender exception", "Excecao do Windows Defender"),
                Why = L("Defender's ransomware rule blocks freshly built programs ('Access denied'). Adds this folder's 'target' to its exceptions; Windows asks for permission.",
                        "A regra anti-ransomware do Defender bloqueia programas acabados de compilar ('Acesso negado'). Junta a pasta 'target' desta pasta as excecoes; o Windows pede autorizacao."),
                FixText = L("Add it", "Adicionar"),
                Check = () => ReadSetting("defender") == "1" ? 1 : -1,
                Fix = () => {
                    var target = Path.Combine(root, "target");
                    bool ok = Elevated("powershell", "-NoProfile -ExecutionPolicy Bypass -Command \"Add-MpPreference -AttackSurfaceReductionOnlyExclusions '" + target.Replace("'", "''") + "'\"");
                    if (ok) WriteSetting("defender", "1");
                    return ok;
                },
            });
            steps.Add(new Step {
                Title = L("Your game folder", "A pasta do jogo"),
                Why = L("Looks for game_scarlett_master.exe in Downloads, Desktop, Documents, Games folders and the root of each drive.", "Procura o game_scarlett_master.exe em Transferencias, Ambiente de trabalho, Documentos, pastas Games e na raiz de cada disco."),
                FixText = L("Find it", "Encontrar"),
                Check = () => { string g = null; Invoke(new Action(() => g = GameExe)); return File.Exists(g) ? 1 : 0; },
                Fix = FindGame,
            });
            steps.Add(new Step {
                Title = L("Shaders prepared", "Shaders preparados"),
                Why = L("Translates the game's shaders and makes their pipelines once (about 5 minutes), so the game does not stop to do it.", "Traduz os shaders do jogo e prepara as pipelines uma vez (cerca de 5 minutos), para o jogo nao parar para isso."),
                FixText = L("Prepare", "Preparar"),
                Check = () => ShadersReady() ? 1 : 0,
                Fix = PrepareBlocking,
            });
            steps.Add(new Step {
                Title = L("Power: plugged in, best performance", "Energia: ligado a corrente, melhor desempenho"),
                Why = L("Sets Windows' power mode to 'Best performance'. A laptop on battery runs the CPU slower: plug it in (and use the maker's performance mode, e.g. PredatorSense).",
                        "Poe o modo de energia do Windows em 'Melhor desempenho'. Um portatil na bateria corre mais devagar: liga-o a corrente (e usa o modo de desempenho do fabricante, p. ex. PredatorSense)."),
                FixText = L("Set it", "Definir"),
                Check = () => BestPower() && SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Offline ? 1 : 0,
                Fix = () => { RunWait("powercfg", "/overlaysetactive ded574b5-45a0-4f42-8737-46345c09c238", false); return BestPower(); },
            });
            steps.Add(new Step {
                Title = L("Virtual memory managed by Windows", "Memoria virtual gerida pelo Windows"),
                Why = L("The game and VIctory commit 25+ GB; a fixed, small page file ends them. Windows asks for permission; it counts after a restart.",
                        "O jogo e o VIctory reservam 25+ GB; um ficheiro de paginacao fixo e pequeno fecha-os. O Windows pede autorizacao; conta depois de reiniciar."),
                FixText = L("Set it", "Definir"),
                Check = () => PagefileManaged() ? 1 : 0,
                Fix = () => Elevated("powershell", "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_ComputerSystem | Set-CimInstance -Property @{AutomaticManagedPagefile=$true}\""),
            });
            steps.Add(new Step {
                Title = "NVIDIA Smooth Motion",
                Why = L("Driver frame generation for RTX 40/50, written into the driver's profile for victory.exe (no NVIDIA App needed).", "Geracao de frames do driver para RTX 40/50, escrita no perfil do driver para o victory.exe (sem NVIDIA App)."),
                FixText = L("Turn on", "Ligar"),
                Check = () => !rtxFrameGen ? -1 : (SmoothMotion.Status() == "on" ? 1 : 0),
                Fix = () => { var err = SmoothMotion.Set(true); smoothState = SmoothMotion.Status(); if (err != null) Log("Smooth Motion: " + err); BeginInvoke(new Action(() => { UpdateBanner(); UpdateSmooth(); })); return err == null; },
            });
            steps.Add(new Step {
                Title = "Resizable BAR",
                Why = L("Lets the CPU write straight into all of the video memory (faster copies). It is switched on in the PC's BIOS/UEFI: the guide shows how.",
                        "Deixa o processador escrever diretamente em toda a VRAM (copias mais rapidas). Liga-se na BIOS/UEFI do PC: o guia mostra como."),
                FixText = L("Show me how", "Mostra-me como"),
                Check = () => rebar ? 1 : 0,
                Fix = () => { BeginInvoke(new Action(ShowRebarGuide)); return false; },
                Auto = false,
            });
        }

        // ---------------------------------------------------------------- running steps
        void CheckAll()
        {
            if (setupBusy) return;
            new Thread(() => {
                foreach (var st in steps)
                {
                    int s;
                    try { s = st.Check(); } catch (Exception) { s = -1; }
                    var me = st;
                    BeginInvoke(new Action(() => PaintStep(me, s)));
                }
            }) { IsBackground = true }.Start();
        }

        void PaintStep(Step st, int state)
        {
            st.State = state;
            st.Mark.Text = state == 1 ? "✓" : state == 0 ? "✗" : "•";
            st.Mark.ForeColor = state == 1 ? Theme.Good : state == 0 ? Theme.Warn : Theme.Dim;
            st.Button.Enabled = !setupBusy;
            st.Button.Invalidate();
        }

        void DoEverything()
        {
            RunSteps(steps.Where(s => s.Auto && s.State != 1).ToArray());
        }

        void RunSteps(Step[] which)
        {
            if (setupBusy || which.Length == 0) return;
            setupBusy = true;
            foreach (var st in steps) { st.Button.Enabled = false; st.Button.Invalidate(); }
            everything.Enabled = false; everything.Invalidate();
            new Thread(() => {
                int i = 0;
                foreach (var st in which)
                {
                    i++;
                    var me = st;
                    int now;
                    try { now = st.Check(); } catch (Exception) { now = -1; }
                    if (now == 1 && which.Length > 1) { BeginInvoke(new Action(() => PaintStep(me, 1))); continue; }
                    Progress(string.Format("{0} ({1}/{2})...", st.Title, i, which.Length), (float)(i - 1) / which.Length);
                    BeginInvoke(new Action(() => { me.Mark.Text = "…"; me.Mark.ForeColor = Theme.Cyan; }));
                    bool ok;
                    try { ok = st.Fix(); } catch (Exception e) { ok = false; Log(st.Title + ": " + e.Message); }
                    try { now = st.Check(); } catch (Exception) { now = -1; }
                    BeginInvoke(new Action(() => PaintStep(me, now)));
                    Log((now == 1 ? "✓ " : ok ? "• " : "✗ ") + st.Title);
                    // Nothing after a build can work without it.
                    if (now == 0 && st.Fix == (Func<bool>)BuildBlocking && which.Length > 1) break;
                }
                Progress(L("Done.", "Feito."), 1f);
                Thread.Sleep(800);
                setupBusy = false;
                BeginInvoke(new Action(() => {
                    setupBar.Visible = false;
                    if (loadBar != null) loadBar.Visible = false;
                    everything.Enabled = true; everything.Invalidate();
                    foreach (var st in steps) { st.Button.Enabled = true; st.Button.Invalidate(); }
                    Refresh2();
                }));
            }) { IsBackground = true }.Start();
        }

        // Both bars (Setup page and Play page) show the same work.
        void Progress(string text, float part)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, float>(Progress), text, part); return; }
            foreach (var bar in new[] { setupBar, loadBar })
                if (bar != null) { bar.Visible = true; bar.Set(part, text); }
        }

        // ---------------------------------------------------------------- the steps' work
        bool Winget(string id, string overrideArgs)
        {
            if (string.IsNullOrEmpty(RunCapture("where", "winget")))
            {
                Log(L("winget is missing (App Installer from the Microsoft Store). Opening the download page.", "Falta o winget (App Installer da Microsoft Store). A abrir a pagina de download."));
                OpenUrl(id.StartsWith("Rust") ? "https://rustup.rs" : "https://visualstudio.microsoft.com/visual-cpp-build-tools/");
                return false;
            }
            var args = "install --id " + id + " -e --silent --accept-source-agreements --accept-package-agreements";
            if (overrideArgs != null) args += " --override \"" + overrideArgs + "\"";
            Log("winget " + args);
            return RunWait("winget", args, false) == 0;
        }

        static bool HasCppTools()
        {
            var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (!File.Exists(vswhere)) return false;
            var s = RunCapture(vswhere, "-products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath");
            return !string.IsNullOrEmpty(s.Trim());
        }

        bool BuildBlocking()
        {
            var log = Path.Combine(dataRoot, "logs", "build.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log));
            int crates = 0;
            var psi = new ProcessStartInfo(CargoExe, "build --locked --profile diag -p victory --features diagnostics") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = root };
            var cargoBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin");
            psi.EnvironmentVariables["PATH"] = cargoBin + ";" + Environment.GetEnvironmentVariable("PATH");
            Log(L("Building VIctory... Log: ", "A compilar o VIctory... Log: ") + log);
            using (var writer = new StreamWriter(log, false))
            {
                Process p;
                try { p = Process.Start(psi); }
                catch (Exception e) { Log(L("Could not start cargo: ", "Nao deu para iniciar o cargo: ") + e.Message); return false; }
                DataReceivedEventHandler sink = (s, e) => {
                    if (e.Data == null) return;
                    lock (writer) writer.WriteLine(e.Data);
                    var m = Regex.Match(e.Data, @"^\s*Compiling (\S+)");
                    if (m.Success)
                    {
                        crates++;
                        Progress(string.Format(L("Building VIctory: {0} ({1} parts)", "A compilar o VIctory: {0} ({1} partes)"), m.Groups[1].Value, crates), Math.Min(0.95f, crates / 330f));
                    }
                };
                p.OutputDataReceived += sink; p.ErrorDataReceived += sink;
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                p.WaitForExit();
                Log(p.ExitCode == 0 ? L("VIctory built.", "VIctory compilado.") : L("The build failed: see ", "A compilacao falhou: ve ") + log);
                BeginInvoke(new Action(Refresh2));
                return p.ExitCode == 0;
            }
        }

        bool PrepareBlocking()
        {
            string dir = null;
            Invoke(new Action(() => { SaveSettings(); dir = gameBox.Text.Trim(); }));
            if (VictoryExe == null || !File.Exists(Path.Combine(dir, "game_scarlett_master.exe"))) { Log(L("Build VIctory and choose the game folder first.", "Compila o VIctory e escolhe a pasta do jogo primeiro.")); return false; }
            var log = Path.Combine(dataRoot, "logs", "prepare.log");
            Log(L("Translating the game's shaders and preparing pipelines (about 5 minutes)... Log: ", "A traduzir os shaders do jogo e a preparar pipelines (cerca de 5 minutos)... Log: ") + log);
            Progress(L("Translating the game's shaders (about 5 minutes)...", "A traduzir os shaders do jogo (cerca de 5 minutos)..."), 0.05f);
            var p = Start("shaders \"" + dir + "\"", log);
            p.WaitForExit();
            Progress(L("Preparing pipelines...", "A preparar pipelines..."), 0.6f);
            var w = Start("warm --used", log, line => {
                var m = Regex.Match(line, @"^(\d+) of (\d+), (\d+) s");
                if (m.Success)
                {
                    int n = int.Parse(m.Groups[1].Value), of = int.Parse(m.Groups[2].Value);
                    Progress(string.Format(L("Preparing pipelines: {0} of {1}", "A preparar pipelines: {0} de {1}"), n, of), 0.6f + 0.4f * n / Math.Max(1, of));
                }
            });
            w.WaitForExit();
            Log(L("Shaders ready. You can press PLAY.", "Shaders prontos. Podes carregar em JOGAR."));
            WriteSetting("shaders", "1");
            return p.ExitCode == 0;
        }

        bool ShadersReady()
        {
            if (ReadSetting("shaders") == "1") return true;
            try
            {
                var dir = Path.Combine(cacheDir, "shaders");
                return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Take(1000).Count() >= 1000;
            }
            catch (Exception) { return false; }
        }

        bool FindGame()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var places = new List<string> {
                Path.Combine(home, "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Path.Combine(home, "Games"),
            };
            foreach (var d in DriveInfo.GetDrives())
                if (d.DriveType == DriveType.Fixed && d.IsReady) { places.Add(d.RootDirectory.FullName); places.Add(Path.Combine(d.RootDirectory.FullName, "Games")); }
            Progress(L("Looking for game_scarlett_master.exe...", "A procurar o game_scarlett_master.exe..."), 0.3f);
            foreach (var place in places.Distinct())
            {
                var found = Search(place, 3);
                if (found != null)
                {
                    Invoke(new Action(() => { gameBox.Text = Path.GetDirectoryName(found); SaveSettings(); }));
                    Log(L("Game found: ", "Jogo encontrado: ") + Path.GetDirectoryName(found));
                    return true;
                }
            }
            Log(L("The game was not found: choose its folder with Browse on the Play page.", "O jogo nao foi encontrado: escolhe a pasta com Procurar na pagina Jogar."));
            return false;
        }

        static string Search(string dir, int depth)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                var f = Path.Combine(dir, "game_scarlett_master.exe");
                if (File.Exists(f)) return f;
                if (depth == 0) return null;
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (name.StartsWith("$") || name.Equals("Windows", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Program", StringComparison.OrdinalIgnoreCase) || name.Equals("AppData", StringComparison.OrdinalIgnoreCase)) continue;
                    var r = Search(sub, depth - 1);
                    if (r != null) return r;
                }
            }
            catch (Exception) { }
            return null;
        }

        static bool BestPower()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes"))
                {
                    var v = k == null ? null : k.GetValue("ActiveOverlayAcPowerScheme") as string;
                    return v != null && v.Equals("ded574b5-45a0-4f42-8737-46345c09c238", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception) { return false; }
        }

        static bool PagefileManaged()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"))
                {
                    var v = k == null ? null : k.GetValue("PagingFiles") as string[];
                    return v != null && v.Any(s => s.StartsWith("?:\\pagefile.sys", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception) { return false; }
        }

        // Runs a program with administrator rights (the UAC prompt) and waits; false if refused or failed.
        bool Elevated(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch (Win32Exception) { Log(L("Administrator rights were not given.", "Nao foram dadas permissoes de administrador.")); return false; }
        }

        static int RunWait(string file, string args, bool window)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = !window });
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (Exception) { return -1; }
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch (Exception) { }
        }

        void WriteSetting(string key, string value)
        {
            try
            {
                var lines = File.Exists(settingsFile) ? File.ReadAllLines(settingsFile).Where(l => !l.StartsWith(key + "=")).ToList() : new List<string>();
                lines.Add(key + "=" + value);
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                File.WriteAllLines(settingsFile, lines);
            }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- Resizable BAR
        void ShowRebarGuide()
        {
            bool uefi = Uefi();
            var f = new Form { Text = "Resizable BAR", ClientSize = new Size(760, 560), BackColor = Theme.Back, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = Icon };
            f.Controls.Add(new Heading(L("Turning on Resizable BAR", "Ligar o Resizable BAR")) { Location = new Point(20, 16), Width = 720 });
            var state = new Label { Location = new Point(20, 50), Size = new Size(720, 50), Font = new Font("Segoe UI Semibold", 10f), BackColor = Theme.Back,
                ForeColor = rebar ? Theme.Good : Theme.Warn,
                Text = (rebar ? L("✓ It is already on for your card.", "✓ Ja esta ligado na tua placa.") : L("✗ It is off (or the card does not report it).", "✗ Esta desligado (ou a placa nao o indica)."))
                    + "\r\n" + (uefi ? L("✓ Your PC starts in UEFI mode (needed).", "✓ O teu PC arranca em modo UEFI (necessario).") : L("✗ Your PC starts in Legacy/CSM mode: Resizable BAR needs UEFI (convert the disk with mbr2gpt first).", "✗ O teu PC arranca em modo Legacy/CSM: o Resizable BAR precisa de UEFI (converte o disco com o mbr2gpt primeiro).")) };
            f.Controls.Add(state);
            var text = new Label { Location = new Point(20, 108), Size = new Size(720, 360), ForeColor = Theme.Text, BackColor = Theme.Back };
            text.Text = string.Join("\r\n", Pt ? new[] {
                "O Resizable BAR deixa o processador escrever em toda a VRAM de uma vez. Liga-se na BIOS/UEFI, nao no Windows.",
                "",
                "1.  Atualiza a BIOS do PC/placa-mae e o driver da NVIDIA (RTX 30, 40 e 50 suportam-no).",
                "2.  Reinicia para a BIOS/UEFI (botao em baixo, ou F2/Del durante o arranque).",
                "3.  Liga 'Above 4G Decoding' e 'Re-Size BAR Support' (pode chamar-se 'Resizable BAR',",
                "     'Smart Access Memory' ou 'Clever Access Memory'). Desliga o CSM se estiver ligado.",
                "     Costuma estar em Advanced > PCI Subsystem, ou em Boot.",
                "4.  Guarda e sai (normalmente F10). De volta ao Windows, o launcher mostra 'ReBAR ligado'.",
                "",
                "Portateis: muitos ja trazem o ReBAR ligado; noutros so aparece depois de uma atualizacao da BIOS do",
                "fabricante (Acer, ASUS, Lenovo, MSI...). Procura 'BIOS update' na pagina de suporte do teu modelo.",
                "Se a opcao nao existir, o jogo funciona na mesma: o launcher so desliga a escrita direta na VRAM.",
            } : new[] {
                "Resizable BAR lets the CPU write into all of the video memory at once. It is switched on in the BIOS/UEFI, not in Windows.",
                "",
                "1.  Update your PC's/motherboard's BIOS and the NVIDIA driver (RTX 30, 40 and 50 cards support it).",
                "2.  Restart into the BIOS/UEFI (button below, or F2/Del while the PC starts).",
                "3.  Turn on 'Above 4G Decoding' and 'Re-Size BAR Support' (it may be called 'Resizable BAR',",
                "     'Smart Access Memory' or 'Clever Access Memory'). Turn CSM off if it is on.",
                "     It is usually under Advanced > PCI Subsystem, or under Boot.",
                "4.  Save and exit (usually F10). Back in Windows, the launcher shows 'ReBAR on'.",
                "",
                "Laptops: many have it on already; on others it only appears after the maker's BIOS update",
                "(Acer, ASUS, Lenovo, MSI...). Look for 'BIOS update' on your model's support page.",
                "If the option is not there, the game still works: the launcher only leaves the direct video-memory writes off.",
            });
            f.Controls.Add(text);
            var bios = new Pill { Main = true, Text = L("Restart into the BIOS/UEFI", "Reiniciar para a BIOS/UEFI"), Location = new Point(20, 490), Size = new Size(300, 44), Enabled = uefi && !rebar };
            bios.Click += (s, e) => {
                var ask = L("Save your work first. The PC restarts in 5 seconds and opens its BIOS/UEFI settings. Restart now?", "Guarda o teu trabalho primeiro. O PC reinicia em 5 segundos e abre as definicoes da BIOS/UEFI. Reiniciar agora?");
                if (MessageBox.Show(f, ask, "Resizable BAR", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    new Thread(() => Elevated("shutdown", "/r /fw /t 5")) { IsBackground = true }.Start();
            };
            f.Controls.Add(bios);
            var close = new Pill { Text = L("Close", "Fechar"), Location = new Point(600, 495), Size = new Size(140, 36) };
            close.Click += (s, e) => f.Close();
            f.Controls.Add(close);
            f.ShowDialog(this);
        }
    }
}
