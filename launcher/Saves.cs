// Game saves to share: the title's save folder (data\7952987A\saves, a few hundred KB) exported to a zip on the
// Desktop, or a friend's zip imported over it. The emulated console user is the same on every PC, so a save
// made on one plays on another. Before an import the current saves are kept in a zip of their own
// (%LOCALAPPDATA%\VIctory\save-backups), and nothing is touched while the game runs.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    partial class LauncherForm
    {
        const string TitleId = "7952987A";

        string SavesDir { get { return Path.Combine(dataDir, TitleId, "saves"); } }
        string SaveBackups { get { return Path.Combine(Path.GetDirectoryName(dataRoot), "save-backups"); } }

        // The section on the Hardware & ID page; returns the y below it.
        int SavesSection(Control page, int X, int y, int W)
        {
            page.Controls.Add(new Heading(L("Game saves", "Saves do jogo", "Sauvegardes du jeu")) { Location = new Point(X, y), Width = W });
            y += 32;
            page.Controls.Add(new Label { Location = new Point(X, y), Size = new Size(W, 36), ForeColor = Theme.Dim, BackColor = Theme.Back, Font = Theme.Ui(9f),
                Text = L("Share your progress or play a friend's: export makes a zip on your Desktop; import puts a friend's zip in place (your own saves are kept in a backup first).",
                         "Partilha o teu progresso ou joga o de um amigo: exportar cria um zip no Ambiente de trabalho; importar poe o zip de um amigo no lugar (os teus saves ficam primeiro numa copia de seguranca).",
                         "Partage ta progression ou joue celle d'un ami : exporter crée un zip sur ton Bureau ; importer met le zip d'un ami à la place (tes sauvegardes sont d'abord gardées dans une copie de sécurité).") });
            y += 40;
            var export = new Pill { Text = L("Export my save", "Exportar o meu save", "Exporter ma sauvegarde"), Location = new Point(X, y), Width = 200 };
            export.Click += (s, e) => ExportSave();
            var import = new Pill { Text = L("Import a save...", "Importar um save...", "Importer une sauvegarde..."), Location = new Point(X + 210, y), Width = 200 };
            import.Click += (s, e) => ImportSave();
            var open = new Pill { Text = L("Open the saves folder", "Abrir a pasta dos saves", "Ouvrir le dossier"), Location = new Point(X + 420, y), Width = 200 };
            open.Click += (s, e) => { Directory.CreateDirectory(SavesDir); try { Process.Start("explorer.exe", "\"" + SavesDir + "\""); } catch (Exception) { } };
            page.Controls.Add(export); page.Controls.Add(import); page.Controls.Add(open);
            return y + 48;
        }

        static bool HasFiles(string dir) { return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any(); }

        void ExportSave()
        {
            if (!HasFiles(SavesDir)) { MessageBox.Show(this, L("There is no save yet: play until the game saves once.", "Ainda nao ha save: joga ate o jogo gravar uma vez.", "Pas encore de sauvegarde : joue jusqu'à ce que le jeu sauvegarde une fois."), "Julia's Launcher"); return; }
            var zip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VI-save-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip");
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                ZipFile.CreateFromDirectory(SavesDir, zip, CompressionLevel.Optimal, false);
                Log(L("Save exported: ", "Save exportado: ", "Sauvegarde exportée : ") + zip);
                try { Process.Start("explorer.exe", "/select,\"" + zip + "\""); } catch (Exception) { }
            }
            catch (Exception ex) { MessageBox.Show(this, L("The save could not be exported: ", "Nao deu para exportar o save: ", "Impossible d'exporter la sauvegarde : ") + ex.Message, "Julia's Launcher"); }
        }

        void ImportSave()
        {
            if (State() != GameState.None) { MessageBox.Show(this, L("Close the game first.", "Fecha o jogo primeiro.", "Ferme d'abord le jeu."), "Julia's Launcher"); return; }
            string zip;
            using (var d = new OpenFileDialog { Filter = "Zip (*.zip)|*.zip", Title = L("A save exported by the launcher", "Um save exportado pelo launcher", "Une sauvegarde exportée par le launcher") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                zip = d.FileName;
            }
            try
            {
                // Only what a save folder holds: the console user's folder, the title's container, files under it.
                using (var z = ZipFile.OpenRead(zip))
                {
                    var names = z.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
                    bool looksRight = names.Count > 0 && names.Count < 500 && z.Entries.Sum(e => e.Length) < (64L << 20)
                        && names.All(n => !n.Contains("..") && !n.StartsWith("/") && n.IndexOf(':') < 0)
                        && names.Any(n => n.IndexOf("7952987a", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!looksRight)
                    {
                        MessageBox.Show(this, L("This zip is not a save exported by the launcher.", "Este zip nao e um save exportado pelo launcher.", "Ce zip n'est pas une sauvegarde exportée par le launcher."), "Julia's Launcher");
                        return;
                    }
                }
                var ask = L("Replace your saves with this one? Yours are kept in a backup first (", "Substituir os teus saves por este? Os teus ficam primeiro numa copia de seguranca (", "Remplacer tes sauvegardes par celle-ci ? Les tiennes sont d'abord gardées dans une copie de sécurité (") + SaveBackups + ").";
                if (MessageBox.Show(this, ask, "Julia's Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                if (HasFiles(SavesDir))
                {
                    Directory.CreateDirectory(SaveBackups);
                    var backup = Path.Combine(SaveBackups, "saves-before-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
                    ZipFile.CreateFromDirectory(SavesDir, backup, CompressionLevel.Optimal, false);
                    Directory.Delete(SavesDir, true);
                    Log(L("Your saves were kept in ", "Os teus saves ficaram em ", "Tes sauvegardes sont gardées dans ") + backup);
                }
                Directory.CreateDirectory(SavesDir);
                ZipFile.ExtractToDirectory(zip, SavesDir);
                Log(L("Save imported. Press PLAY and load it from the game's menu.", "Save importado. Carrega em JOGAR e abre-o no menu do jogo.", "Sauvegarde importée. Appuie sur PLAY et charge-la depuis le menu du jeu."));
                MessageBox.Show(this, L("Save imported. Press PLAY and load it from the game's menu.", "Save importado. Carrega em JOGAR e abre-o no menu do jogo.", "Sauvegarde importée. Appuie sur PLAY et charge-la depuis le menu du jeu."), "Julia's Launcher");
            }
            catch (Exception ex) { MessageBox.Show(this, L("The save could not be imported: ", "Nao deu para importar o save: ", "Impossible d'importer la sauvegarde : ") + ex.Message, "Julia's Launcher"); }
        }
    }
}
