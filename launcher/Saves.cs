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
            y += 48;
            var share = new Pill { Text = L("Share in the gallery", "Partilhar na galeria", "Partager dans la galerie"), Location = new Point(X, y), Width = 200 };
            share.Click += (s, e) => ShareSave();
            var gallery = new Pill { Text = L("Open the save gallery", "Abrir a galeria de saves", "Ouvrir la galerie"), Location = new Point(X + 210, y), Width = 200 };
            gallery.Click += (s, e) => OpenUrl(GalleryUrl);
            page.Controls.Add(share); page.Controls.Add(gallery);
            return y + 48;
        }

        // The public save gallery (server/hardware-worker): a shared save waits until edupode approves it.
        const string GalleryUrl = "https://vi-hardware.edupodept.workers.dev/saves";
        const string GalleryEndpoint = "https://vi-hardware.edupodept.workers.dev/v1/saves";
        const int MaxSaveUpload = 1024 * 1024;

        void ShareSave()
        {
            if (!HasFiles(SavesDir)) { MessageBox.Show(this, L("There is no save yet: play until the game saves once.", "Ainda nao ha save: joga ate o jogo gravar uma vez.", "Pas encore de sauvegarde : joue jusqu'à ce que le jeu sauvegarde une fois."), "Julia's Launcher"); return; }
            string title, note;
            using (var f = new Form { Text = L("Share in the gallery", "Partilhar na galeria", "Partager dans la galerie"), ClientSize = new Size(560, 300), BackColor = Theme.Back, ForeColor = Theme.Text,
                Font = Theme.Ui(9.75f), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = Icon })
            {
                f.Controls.Add(new Label { Location = new Point(20, 16), Size = new Size(520, 40), ForeColor = Theme.Dim, BackColor = Theme.Back,
                    Text = L("Your save goes to the public gallery once edupode has approved it. Everyone can download it.",
                             "O teu save vai para a galeria publica depois de o edupode o aprovar. Qualquer pessoa o pode descarregar.",
                             "Ta sauvegarde arrive dans la galerie publique une fois qu'edupode l'a validée. Tout le monde pourra la télécharger.") });
                f.Controls.Add(new Label { Location = new Point(20, 62), AutoSize = true, BackColor = Theme.Back, Text = L("Title (where you are in the story)", "Titulo (onde estas na historia)", "Titre (où tu en es dans l'histoire)") });
                var t = new TextBox { Location = new Point(20, 84), Width = 520, MaxLength = 80 };
                f.Controls.Add(new Label { Location = new Point(20, 118), AutoSize = true, BackColor = Theme.Back, Text = L("Note (optional)", "Nota (opcional)", "Note (facultatif)") });
                var n = new TextBox { Location = new Point(20, 140), Size = new Size(520, 70), MaxLength = 300, Multiline = true };
                var ok = new Pill { Main = true, Text = L("Share", "Partilhar", "Partager"), Location = new Point(20, 236), Size = new Size(200, 40) };
                var cancel = new Pill { Text = L("Cancel", "Cancelar"), Location = new Point(230, 240), Size = new Size(160, 34) };
                ok.Click += (s, e) => { if (t.Text.Trim().Length >= 3) { f.DialogResult = DialogResult.OK; f.Close(); } };
                cancel.Click += (s, e) => f.Close();
                f.Controls.Add(t); f.Controls.Add(n); f.Controls.Add(ok); f.Controls.Add(cancel);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                title = t.Text.Trim(); note = n.Text.Trim();
            }
            var zip = Path.Combine(Path.GetTempPath(), "vi-save-share.zip");
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                ZipFile.CreateFromDirectory(SavesDir, zip, CompressionLevel.Optimal, false);
                var data = File.ReadAllBytes(zip);
                if (data.Length > MaxSaveUpload)
                {
                    MessageBox.Show(this, L("This save is too big for the gallery (over 1 MB).", "Este save e grande demais para a galeria (mais de 1 MB).", "Cette sauvegarde est trop grosse pour la galerie (plus de 1 Mo)."), "Julia's Launcher");
                    return;
                }
                System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(GalleryEndpoint);
                req.Method = "POST";
                req.ContentType = "application/zip";
                req.UserAgent = "Julias-Launcher";
                req.Timeout = 60000;
                req.Headers["X-VI-Id"] = (hw ?? ReadHardware()).Id;
                req.Headers["X-VI-Title"] = Uri.EscapeDataString(title);
                req.Headers["X-VI-Note"] = Uri.EscapeDataString(note);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var r = (System.Net.HttpWebResponse)req.GetResponse()) { }
                Log(L("Save shared: it shows in the gallery once approved.", "Save partilhado: aparece na galeria depois de aprovado.", "Sauvegarde partagée : elle apparaîtra dans la galerie une fois validée."));
                MessageBox.Show(this, L("Save shared: it shows in the gallery once approved.", "Save partilhado: aparece na galeria depois de aprovado.", "Sauvegarde partagée : elle apparaîtra dans la galerie une fois validée."), "Julia's Launcher");
            }
            catch (System.Net.WebException e)
            {
                var r = e.Response as System.Net.HttpWebResponse;
                MessageBox.Show(this, r != null && (int)r.StatusCode == 429
                    ? L("Too many saves shared this hour: try again later.", "Demasiados saves partilhados nesta hora: tenta mais tarde.", "Trop de sauvegardes partagées cette heure-ci : réessaie plus tard.")
                    : L("The save could not be shared: ", "Nao deu para partilhar o save: ", "Impossible de partager la sauvegarde : ") + e.Message, "Julia's Launcher");
            }
            catch (Exception ex) { MessageBox.Show(this, L("The save could not be shared: ", "Nao deu para partilhar o save: ", "Impossible de partager la sauvegarde : ") + ex.Message, "Julia's Launcher"); }
            finally { try { File.Delete(zip); } catch (Exception) { } }
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
