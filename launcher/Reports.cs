// Julia's Launcher: "Send errors to Discord". The player picks launcher errors or game errors, ticks what
// happened and writes a line; the launcher puts the right files in a zip on the Desktop (user name
// taken out of every path), opens the folder and asks them to send it in the Discord channel.
// Nothing is sent by the launcher itself.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    partial class LauncherForm
    {
        void ReportErrors()
        {
            var f = new Form { Text = L("Send errors to Discord", "Enviar erros no Discord"), ClientSize = new Size(640, 560), BackColor = Theme.Back, ForeColor = Theme.Text,
                Font = Theme.Ui(9.75f), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = Icon };
            const int X = 20, W = 600;
            int y = 16;
            f.Controls.Add(new Heading(L("What kind of problem?", "Que tipo de problema?")) { Location = new Point(X, y), Width = W });
            y += 32;
            var launcherKind = new OptionCard { Text = L("Launcher errors", "Erros do launcher"), Line = L("The launcher closes, a button does nothing, Setup or an update fails", "O launcher fecha, um botao nao faz nada, o Preparar ou um update falha"), Location = new Point(X, y), Size = new Size(W, 58) };
            y += 64;
            var gameKind = new OptionCard { Text = L("Game errors", "Erros do jogo"), Line = L("The game closes or freezes, black squares, shaders, low FPS", "O jogo fecha ou congela, quadrados pretos, shaders, FPS baixos"), Location = new Point(X, y), Size = new Size(W, 58), Checked = true };
            launcherKind.CheckedChanged += (s, e) => { if (launcherKind.Checked) gameKind.Checked = false; else if (!gameKind.Checked) launcherKind.Checked = true; };
            gameKind.CheckedChanged += (s, e) => { if (gameKind.Checked) launcherKind.Checked = false; else if (!launcherKind.Checked) gameKind.Checked = true; };
            f.Controls.Add(launcherKind); f.Controls.Add(gameKind);
            y += 70;
            f.Controls.Add(new Heading(L("What happened? (tick any)", "O que aconteceu? (marca o que for)")) { Location = new Point(X, y), Width = W });
            y += 30;
            string[] picks = {
                L("My shader is not rendering properly", "Os shaders nao aparecem bem"),
                L("Virtual memory not turning on", "A memoria virtual nao liga"),
                L("Resizable BAR shows off but it is on", "O Resizable BAR aparece desligado mas esta ligado"),
                L("The game closes or crashes", "O jogo fecha ou crasha"),
                L("PLAY does nothing / stays grey", "O JOGAR nao faz nada / fica cinzento"),
                L("Preparing shaders fails", "Preparar os shaders falha"),
                L("Low FPS or stutter", "FPS baixos ou engasgos"),
                L("Black squares or wrong colours", "Quadrados pretos ou cores erradas"),
                L("Frame generation test (how it ran)", "Teste da geracao de frames (como correu)"),
                L("AMD card: tried the edupode engine", "Placa AMD: experimentei o motor edupode"),
            };
            var boxes = new List<Toggle>();
            for (int i = 0; i < picks.Length; i++)
            {
                var t = new Toggle { Text = picks[i], Location = new Point(X + (i % 2) * 300, y + (i / 2) * 30), Width = 296 };
                boxes.Add(t); f.Controls.Add(t);
            }
            y += (picks.Length + 1) / 2 * 30 + 8;
            f.Controls.Add(Small(L("In your words (what you did, what you saw):", "Por palavras tuas (o que fizeste, o que viste):"), X, y, W));
            y += 22;
            var words = new TextBox { Location = new Point(X, y), Size = new Size(W, 70), Multiline = true, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Ui(9.5f) };
            f.Controls.Add(words);
            y += 82;
            var make = new Pill { Main = true, Text = L("Make the package", "Criar o pacote"), Location = new Point(X + W - 200, y), Size = new Size(200, 40) };
            var cancel = new Pill { Text = L("Cancel", "Cancelar"), Location = new Point(X + W - 330, y + 3), Size = new Size(120, 34) };
            cancel.Click += (s, e) => f.Close();
            make.Click += (s, e) => {
                make.Enabled = false; make.Text = L("Collecting...", "A juntar...");
                bool game = gameKind.Checked;
                var ticked = boxes.Where(b => b.Checked).Select(b => b.Text).ToList();
                var said = words.Text.Trim();
                new Thread(() => {
                    string zip = null, summary = null, error = null;
                    try { zip = MakeReport(game, ticked, said, out summary); }
                    catch (Exception ex) { error = ex.Message; }
                    try
                    {
                        BeginInvoke(new Action(() => {
                            f.Close();
                            if (zip == null) { MessageBox.Show(this, L("The package could not be made: ", "Nao deu para criar o pacote: ") + error, "Julia's Launcher"); return; }
                            try { Clipboard.SetText(summary); } catch (Exception) { }
                            try { Process.Start("explorer.exe", "/select,\"" + zip + "\""); } catch (Exception) { }
                            var msg = string.Format(L("Done: {0}\r\n\r\nSend this zip on Discord, in the channel \"{1}\".\r\nA short summary is already copied: paste it with the zip.\r\n\r\nOpen Discord now?",
                                                      "Feito: {0}\r\n\r\nEnvia este zip no Discord, no canal \"{1}\".\r\nUm resumo curto ja esta copiado: cola-o junto com o zip.\r\n\r\nAbrir o Discord agora?"), Path.GetFileName(zip), DiscordChannel);
                            if (MessageBox.Show(this, msg, L("Send it on Discord", "Envia no Discord"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                OpenUrl(DiscordInvite);
                        }));
                    }
                    catch (Exception) { }
                }) { IsBackground = true }.Start();
            };
            f.Controls.Add(make); f.Controls.Add(cancel);
            f.ClientSize = new Size(640, y + 60);
            f.ShowDialog(this);
        }

        // The package: a folder and its zip on the Desktop. Returns the zip's path.
        string MakeReport(bool game, List<string> ticked, string said, out string summary)
        {
            var h = hw ?? ReadHardware();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var name = "VI-" + (game ? "game" : "launcher") + "-errors-" + h.Id.Replace("VI-", "") + "-" + stamp;
            var dir = Path.Combine(desktop, name);
            Directory.CreateDirectory(dir);
            string activity = "", options = "";
            Invoke(new Action(() => {
                activity = logBox.Text;
                options = string.Join("\r\n", Environment2().Where(kv => kv.Key != "VICTORY_GAME_DIR").Select(kv => kv.Key + "=" + kv.Value).ToArray());
            }));
            var missing = Missing();
            var sb = new StringBuilder();
            sb.AppendLine("Julia's Launcher error report");
            sb.AppendLine("ID: " + h.Id);
            sb.AppendLine("Kind: " + (game ? "game errors" : "launcher errors"));
            sb.AppendLine("Launcher: v" + Updater.Version + "   Engine: " + EngineName + "   Preset: " + Selected.Name);
            sb.AppendLine("GPU: " + h.Gpu + " (" + h.VramMb / 1024 + " GB, driver " + h.Driver + ")   " + RebarText() + "   VIctory runs on: " + probeAdapter);
            sb.AppendLine("CPU: " + h.Cpu + "   RAM: " + h.RamGb + " GB   Board: " + h.BoardMaker + " " + h.Board);
            sb.AppendLine("Windows: " + h.Os + "   Disk: " + h.Disk + (h.Laptop ? "   laptop" : ""));
            sb.AppendLine("Missing: " + (missing.Count == 0 ? "nothing" : string.Join(", ", missing.ToArray())));
            sb.AppendLine("Shaders cached: " + shaderFiles + (exeProblem != null ? "   Game file: " + exeProblem : ""));
            if (ticked.Count > 0) sb.AppendLine("Ticked: " + string.Join("; ", ticked.ToArray()));
            if (said.Length > 0) sb.AppendLine("In their words: " + said);
            summary = Scrub(sb.ToString());
            File.WriteAllText(Path.Combine(dir, "summary.txt"), summary + "\r\nOptions:\r\n" + Scrub(options));
            File.WriteAllText(Path.Combine(dir, "launcher-activity.txt"), Scrub(activity));
            CopyScrubbed(settingsFile, Path.Combine(dir, "launcher.ini"));
            CopyScrubbed(Program.ErrorFile, Path.Combine(dir, "launcher-errors.txt"));
            var logs = Path.Combine(dataRoot, "logs");
            if (Directory.Exists(logs))
            {
                // Launcher errors: the builds and preparations; game errors: also the last three sessions.
                var wanted = Directory.GetFiles(logs, "*.log").Where(p => !Path.GetFileName(p).StartsWith("session-")).ToList();
                if (game) wanted.AddRange(Directory.GetFiles(logs, "session-*.log").OrderByDescending(File.GetLastWriteTime).Take(3));
                foreach (var p in wanted) CopyScrubbed(p, Path.Combine(dir, Path.GetFileName(p)));
            }
            if (game)
            {
                // The game folder's programs (names and sizes) and what `victory gpu` says.
                var listing = new StringBuilder();
                try
                {
                    string folder = null;
                    Invoke(new Action(() => folder = gameBox.Text.Trim()));
                    if (Directory.Exists(folder))
                        foreach (var p in Directory.GetFiles(folder).Where(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                            listing.AppendLine(string.Format("{0,14:N0}  {1}", new FileInfo(p).Length, Path.GetFileName(p)));
                }
                catch (Exception) { }
                File.WriteAllText(Path.Combine(dir, "game-folder.txt"), listing.ToString());
                if (OwnVictoryExe != null) File.WriteAllText(Path.Combine(dir, "victory-gpu.txt"), Scrub(RunCapture(OwnVictoryExe, "gpu")));
            }
            var zip = dir + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, false);
            return zip;
        }

        // Paths name the player (C:\Users\<name>): the name is taken out.
        static string Scrub(string text)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var t = (text ?? "").Replace(home, "%USERPROFILE%");
            // Other spellings of the profile path (C:/Users/<name>, a short name); a bare word is left
            // alone (the name may be part of other words).
            return System.Text.RegularExpressions.Regex.Replace(t, @"(?i)([\\/]Users[\\/])[^\\/\r\n""']+", "$1<user>");
        }

        // At most the last 20 MB of a log, scrubbed.
        static void CopyScrubbed(string from, string to)
        {
            try
            {
                if (!File.Exists(from)) return;
                using (var f = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (f.Length > (20L << 20)) f.Seek(-(20L << 20), SeekOrigin.End);
                    File.WriteAllText(to, Scrub(new StreamReader(f).ReadToEnd()));
                }
            }
            catch (Exception) { }
        }
    }
}
