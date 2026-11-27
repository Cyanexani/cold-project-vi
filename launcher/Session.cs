// VI Launcher: the game session (what keeps PLAY on or off, a stuck session closed) and the
// choice of the game's executable when a dump has more than one.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    enum GameState { None, Running, Stuck }

    partial class LauncherForm
    {
        [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr hwnd);

        // The game this launcher started, and the pipelines of its session made after it closed.
        Process gameProc, afterWarm;
        // When the game this launcher started lost its window (its process still there).
        DateTime? windowGone;
        bool gameHadWindow;

        // What is running now. A game is a victory.exe with a window, or the one this launcher
        // started; tools (replay, warm, shader translation) are victory.exe too, with no window.
        // Stuck: a window that has not answered for 5 seconds (a crash, an error dialog), or the
        // game this launcher started still there 15 seconds after its window closed.
        GameState State()
        {
            var state = GameState.None;
            foreach (var p in Process.GetProcessesByName("victory"))
            {
                try
                {
                    bool ours = gameProc != null && !gameProc.HasExited && p.Id == gameProc.Id;
                    var window = p.MainWindowHandle;
                    if (ours)
                    {
                        if (window != IntPtr.Zero) { gameHadWindow = true; windowGone = null; }
                        else if (gameHadWindow && windowGone == null) windowGone = DateTime.Now;
                    }
                    if (!ours && window == IntPtr.Zero) continue;
                    bool stuck = (window != IntPtr.Zero && IsHungAppWindow(window))
                        || (ours && windowGone != null && (DateTime.Now - windowGone.Value).TotalSeconds > 15);
                    if (stuck) return GameState.Stuck;
                    state = GameState.Running;
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            return state;
        }

        // Ends every game (and what it started) and the pipelines being made after a session.
        void CloseGames()
        {
            var ids = new List<int>();
            foreach (var p in Process.GetProcessesByName("victory"))
            {
                try
                {
                    bool ours = gameProc != null && !gameProc.HasExited && p.Id == gameProc.Id;
                    bool tool = afterWarm != null && !afterWarm.HasExited && p.Id == afterWarm.Id;
                    if (ours || tool || p.MainWindowHandle != IntPtr.Zero) ids.Add(p.Id);
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            foreach (var id in ids)
            {
                RunWait("taskkill", "/PID " + id + " /T /F", false);
                Log(string.Format(L("Closed VIctory (process {0}).", "VIctory fechado (processo {0})."), id));
            }
            gameProc = null; afterWarm = null; windowGone = null; gameHadWindow = false;
        }

        // ---------------------------------------------------------------- what is ready
        // Checked on a worker thread every few seconds; the window shows the result (Refresh2).
        volatile string exeProblem;
        volatile int shaderFiles;
        volatile bool pagefileOk = true, powerOk = true, gpuPrefOk = true, shadersOk = true;
        bool warnedThisSession;

        void StartChecks()
        {
            new Thread(() => {
                while (true)
                {
                    try { RunChecks(); } catch (Exception) { }
                    try { BeginInvoke(new Action(Refresh2)); } catch (Exception) { return; }
                    Thread.Sleep(8000);
                }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }

        void RunChecks()
        {
            string game = null;
            Invoke(new Action(() => game = GameExe));
            exeProblem = ExeProblem(game);
            int n = 0;
            try
            {
                var dir = Path.Combine(ActiveCacheDir, "shaders");
                if (Directory.Exists(dir)) n = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
            }
            catch (Exception) { }
            shaderFiles = n;
            shadersOk = ShadersReady();
            pagefileOk = PagefileOk();
            powerOk = BestPower() && SystemInformation.PowerStatus.PowerLineStatus != PowerLineStatus.Offline;
            gpuPrefOk = GpuPreferenceOk();
        }

        // Why the game's executable cannot be run, or null: a file that stopped downloading, a
        // placeholder, an encrypted package (VIctory's "malformed image: Could not read file magic").
        string ExeProblem(string path)
        {
            try
            {
                if (path == null || !File.Exists(path)) return null;
                var info = new FileInfo(path);
                var head = new byte[2];
                int got;
                using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) got = f.Read(head, 0, 2);
                if (info.Length < (1L << 20) || got < 2 || head[0] != (byte)'M' || head[1] != (byte)'Z')
                    return string.Format(L("{0} is not a working program ({1:N0} bytes): the dump is incomplete or still encrypted. Download or extract it again.",
                        "{0} nao e um programa que funcione ({1:N0} bytes): o dump esta incompleto ou ainda encriptado. Volta a descarregar ou extrair."), Path.GetFileName(path), info.Length);
                // Empty program libraries beside it end the game the same way.
                var empty = Directory.GetFiles(Path.GetDirectoryName(path), "*.dll").Where(d => new FileInfo(d).Length < 64).Select(Path.GetFileName).Take(3).ToArray();
                if (empty.Length > 0)
                    return string.Format(L("The dump has empty files ({0}): download or extract it again.", "O dump tem ficheiros vazios ({0}): volta a descarregar ou extrair."), string.Join(", ", empty));
            }
            catch (Exception) { }
            return null;
        }

        // What the game would run without, in words for the warning.
        List<string> Missing()
        {
            var m = new List<string>();
            if (!pagefileOk) m.Add(L("virtual memory managed by Windows", "memoria virtual gerida pelo Windows"));
            if (!powerOk) m.Add(L("best performance power (plugged in)", "energia em melhor desempenho (ligado a corrente)"));
            if (!rebar && ReadSetting("rebar_ack") != "1") m.Add("Resizable BAR");
            if (!gpuPrefOk) m.Add(L("the dedicated graphics card for VIctory", "a placa grafica dedicada para o VIctory"));
            if (!shadersOk) m.Add(L("prepared shaders", "shaders preparados"));
            return m;
        }

        // ---------------------------------------------------------------- the game's executable
        ComboBox exeBox;
        // The one chosen (launcher.ini's exe=), when the folder has several.
        string exeChoice;
        string exesOf;
        bool fillingExes;

        // The folder's game_scarlett*.exe, game_scarlett_master.exe first.
        static string[] GameExes(string dir)
        {
            try
            {
                if (dir.Length == 0 || !Directory.Exists(dir)) return new string[0];
                return Directory.GetFiles(dir, "game_scarlett*.exe")
                    .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n.Equals("game_scarlett_master.exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception) { return new string[0]; }
        }

        string GameExe
        {
            get
            {
                var dir = gameBox.Text.Trim();
                var all = GameExes(dir);
                var pick = exeChoice != null && all.Contains(exeChoice, StringComparer.OrdinalIgnoreCase) ? exeChoice
                    : all.Length > 0 ? all[0] : "game_scarlett_master.exe";
                return Path.Combine(dir, pick);
            }
        }

        ComboBox ExeBox(int x, int y, int width)
        {
            exeBox = new ComboBox {
                Location = new Point(x, y), Width = width, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card, ForeColor = Theme.Text, Visible = false,
            };
            exeBox.SelectedIndexChanged += (s, e) => {
                if (fillingExes || exeBox.SelectedItem == null) return;
                var name = (string)exeBox.SelectedItem;
                if (string.Equals(name, exeChoice, StringComparison.OrdinalIgnoreCase)) return;
                exeChoice = name;
                SaveSettings();
                Log(L("Game executable: ", "Executavel do jogo: ") + name);
                Refresh2();
            };
            return exeBox;
        }

        // The list follows the folder: shown only when there is a choice to make.
        void RefreshExes()
        {
            if (exeBox == null) return;
            var dir = gameBox.Text.Trim();
            var all = GameExes(dir);
            var key = dir + "|" + string.Join("|", all) + "|" + exeChoice;
            if (key == exesOf) return;
            bool listChanged = exesOf == null || !exesOf.StartsWith(dir + "|" + string.Join("|", all) + "|");
            exesOf = key;
            fillingExes = true;
            exeBox.Items.Clear();
            foreach (var n in all) exeBox.Items.Add(n);
            exeBox.Visible = all.Length > 1;
            if (all.Length > 0) exeBox.SelectedItem = Path.GetFileName(GameExe);
            fillingExes = false;
            if (all.Length > 1 && listChanged)
                Log(string.Format(L("The game folder has {0} executables; using {1} (change it beside the folder).",
                    "A pasta do jogo tem {0} executaveis; a usar o {1} (muda-o ao lado da pasta)."), all.Length, Path.GetFileName(GameExe)));
        }
    }
}
