//! Le lanceur de VIctory : choisir un mode et des réglages, lancer le jeu,
//! suivre ses images/s, montrer la partie sur Discord, et repartir d'un
//! endroit gardé (sauvegardes par endroit).

#![windows_subsystem = "windows"]

mod discord;
mod partie;
mod reglages;
mod sauvegardes;

use std::sync::mpsc::{Receiver, Sender};
use std::time::Duration;

use eframe::egui;

use partie::Partie;
use reglages::{MODES, Options, Reglages};

fn main() -> eframe::Result<()> {
    let fenetre = egui::ViewportBuilder::default()
        .with_inner_size([760.0, 640.0])
        .with_min_inner_size([560.0, 480.0])
        .with_title("VIctory — lanceur");
    eframe::run_native(
        "VIctory — lanceur",
        eframe::NativeOptions { viewport: fenetre, ..Default::default() },
        Box::new(|_| Ok(Box::new(Lanceur::new()))),
    )
}

struct Lanceur {
    r: Reglages,
    partie: Option<Partie>,
    message: String,
    discord: Option<(Sender<discord::Ordre>, Receiver<bool>)>,
    discord_id: String,
    discord_relie: bool,
    nom_sauvegarde: String,
    sauvegardes: Vec<String>,
    a_remettre: Option<String>,
    journal_ouvert: bool,
}

impl Lanceur {
    fn new() -> Lanceur {
        let r = Reglages::charger();
        let mut l = Lanceur {
            r,
            partie: None,
            message: String::new(),
            discord: None,
            discord_id: String::new(),
            discord_relie: false,
            nom_sauvegarde: String::new(),
            sauvegardes: sauvegardes::liste(),
            a_remettre: None,
            journal_ouvert: false,
        };
        l.relier_discord();
        l
    }

    fn relier_discord(&mut self) {
        let id = self.r.id_discord().unwrap_or_default();
        if id == self.discord_id {
            return;
        }
        self.discord_id = id.clone();
        self.discord_relie = false;
        // L'ancien fil s'arrête quand on lâche son canal.
        self.discord = (!id.is_empty()).then(|| discord::demarrer(id));
        self.presence();
    }

    fn en_cours(&self) -> bool {
        self.partie.as_ref().is_some_and(|p| p.en_cours())
    }

    fn lancer(&mut self) {
        if let Some(nom) = self.a_remettre.clone()
            && let Err(e) = sauvegardes::remettre(&nom) {
                self.message = format!("Sauvegarde non remise : {e}");
                return;
            }
        let logs = reglages::racine_du_projet()
            .map(|r| r.join("logs"))
            .unwrap_or_else(|| std::env::temp_dir().join("victory-logs"));
        match Partie::lancer(&self.r.exe, &self.r.jeu, &self.r.options.console, &self.r.options.variables(), logs) {
            Ok(p) => {
                self.message = format!("Partie lancée ; journal : {}", p.journal.display());
                self.partie = Some(p);
            }
            Err(e) => self.message = e,
        }
        self.r.garder();
    }

    /// Ce que Discord montre, d'après la partie.
    fn presence(&self) {
        let Some((tx, _)) = &self.discord else { return };
        let ordre = match &self.partie {
            Some(p) if p.en_cours() => {
                let e = p.etat.lock().unwrap_or_else(|e| e.into_inner()).clone();
                let ou = if e.monde { "Dans le monde" } else { "Menus et chargements" };
                let mode = MODES.get(self.r.mode).map_or("Réglages perso", |m| m.nom);
                let debut = p.debut.duration_since(std::time::UNIX_EPOCH).ok().map(|d| d.as_secs() as i64);
                discord::Ordre::Montrer(discord::Presence {
                    nom: self.r.titre.clone(),
                    details: ou.to_string(),
                    etat: if e.fps > 0.0 { format!("{:.1} images/s · {mode}", e.fps) } else { mode.to_string() },
                    debut,
                })
            }
            _ => discord::Ordre::Effacer,
        };
        let _ = tx.send(ordre);
    }
}

impl eframe::App for Lanceur {
    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let ctx = ui.ctx().clone();
        ctx.request_repaint_after(Duration::from_millis(500));
        if let Some((_, rx)) = &self.discord {
            while let Ok(r) = rx.try_recv() {
                self.discord_relie = r;
            }
        }
        self.presence();
        let en_cours = self.en_cours();

        egui::Panel::bottom("bas").show(ui, |ui| {
            ui.horizontal(|ui| {
                if en_cours {
                    if ui.button("Arrêter le jeu").clicked()
                        && let Some(p) = &self.partie {
                            p.arreter();
                        }
                } else if ui
                    .add(egui::Button::new(egui::RichText::new("Lancer le jeu").size(18.0)))
                    .clicked()
                {
                    self.lancer();
                }
                ui.label(&self.message);
            });
        });

        egui::CentralPanel::default().show(ui, |ui| {
            egui::ScrollArea::vertical().show(ui, |ui| {
                ui.heading(format!("{} — VIctory", self.r.titre));
                self.vue_partie(ui);
                ui.separator();
                ui.add_enabled_ui(!en_cours, |ui| {
                    self.vue_modes(ui);
                    ui.separator();
                    self.vue_options(ui);
                    ui.separator();
                    self.vue_sauvegardes(ui);
                    ui.separator();
                    self.vue_chemins(ui);
                });
            });
        });
    }

    fn on_exit(&mut self, _gl: Option<&eframe::glow::Context>) {
        self.r.garder();
        if let Some((tx, _)) = &self.discord {
            let _ = tx.send(discord::Ordre::Effacer);
            std::thread::sleep(Duration::from_millis(300));
        }
    }
}

impl Lanceur {
    fn vue_partie(&mut self, ui: &mut egui::Ui) {
        let Some(p) = &self.partie else {
            ui.label("Aucune partie en cours.");
            return;
        };
        let e = p.etat.lock().unwrap_or_else(|e| e.into_inner()).clone();
        let duree = p.depuis.elapsed().as_secs();
        ui.horizontal(|ui| {
            let couleur = if e.fps >= 6.0 {
                egui::Color32::from_rgb(90, 200, 110)
            } else if e.fps >= 3.0 {
                egui::Color32::from_rgb(230, 190, 70)
            } else {
                egui::Color32::from_rgb(230, 100, 90)
            };
            ui.label(egui::RichText::new(format!("{:.1} images/s", e.fps)).size(26.0).color(couleur));
            ui.vertical(|ui| {
                ui.label(format!("{:.0} ms par image · {} dessins", e.ms, e.dessins));
                ui.label(format!(
                    "{} · {} min {:02} s",
                    if e.monde { "dans le monde" } else { "menus et chargements" },
                    duree / 60,
                    duree % 60
                ));
            });
        });
        if let Some(fin) = &e.fin {
            ui.colored_label(egui::Color32::from_rgb(230, 190, 70), format!("Partie terminée : {fin}"));
        }
        egui::CollapsingHeader::new("Journal (dernières lignes)")
            .open(Some(self.journal_ouvert))
            .show(ui, |ui| {
                egui::ScrollArea::vertical().max_height(180.0).stick_to_bottom(true).show(ui, |ui| {
                    for l in e.lignes.iter().rev().take(60).rev() {
                        ui.monospace(l);
                    }
                });
            })
            .header_response
            .clicked()
            .then(|| self.journal_ouvert = !self.journal_ouvert);
    }

    fn vue_modes(&mut self, ui: &mut egui::Ui) {
        ui.strong("Mode");
        let avant = self.r.mode;
        egui::ComboBox::from_id_salt("mode")
            .width(320.0)
            .selected_text(MODES.get(self.r.mode).map_or("Réglages perso", |m| m.nom))
            .show_ui(ui, |ui| {
                for (i, m) in MODES.iter().enumerate() {
                    ui.selectable_value(&mut self.r.mode, i, m.nom).on_hover_text(m.info);
                }
            });
        if self.r.mode != avant {
            let mut o = Options { en_plus: self.r.options.en_plus.clone(), dossier_fsr: self.r.options.dossier_fsr.clone(), ..Options::default() };
            if let Some(m) = MODES.get(self.r.mode) {
                (m.appliquer)(&mut o);
            }
            self.r.options = o;
        }
        if let Some(m) = MODES.get(self.r.mode) {
            ui.weak(m.info);
        }
    }

    fn vue_options(&mut self, ui: &mut egui::Ui) {
        let avant = self.r.options.clone();
        let o = &mut self.r.options;
        ui.strong("Réglages");
        egui::Grid::new("options").num_columns(2).spacing([16.0, 6.0]).show(ui, |ui| {
            ui.label("Console");
            ui.horizontal(|ui| {
                ui.radio_value(&mut o.console, "series-s".to_string(), "Series S");
                ui.radio_value(&mut o.console, "series-x".to_string(), "Series X");
            });
            ui.end_row();

            ui.label("Résolution interne");
            ui.horizontal(|ui| {
                let mut fixee = o.echelle.is_some();
                ui.checkbox(&mut fixee, "fixer");
                if fixee {
                    let mut e = o.echelle.unwrap_or(0.5);
                    ui.add(egui::Slider::new(&mut e, 0.2..=1.0).fixed_decimals(2).text("de la sortie"));
                    o.echelle = Some(e);
                } else {
                    o.echelle = None;
                    ui.weak("celle du jeu");
                }
            });
            ui.end_row();

            ui.label("Agrandissement");
            ui.checkbox(&mut o.upscaler_du_jeu, "upscaler temporel du jeu (au lieu du FSR)");
            ui.end_row();

            ui.label("Dessin");
            ui.checkbox(&mut o.sans_dessin, "rien (ni pipelines ni shaders : écran noir, test de la mémoire)");
            ui.end_row();

            ui.label("Textures");
            ui.checkbox(&mut o.sans_textures, "aucune (surfaces grises, pour tester)");
            ui.end_row();

            ui.label("Textures sur la carte");
            ui.horizontal(|ui| {
                let mut limite = o.textures_pourcent.is_some();
                ui.checkbox(&mut limite, "limiter");
                if limite {
                    let mut p = o.textures_pourcent.unwrap_or(50);
                    ui.add(egui::Slider::new(&mut p, 30..=95).suffix(" %"));
                    o.textures_pourcent = Some(p);
                } else {
                    o.textures_pourcent = None;
                    ui.weak("85 % (défaut)");
                }
            });
            ui.end_row();

            ui.label("Éclairage");
            ui.vertical(|ui| {
                ui.checkbox(&mut o.lumiere_secours, "lumière de secours (zones à l'ombre éclairées)");
                ui.checkbox(&mut o.eclairage_remplacement, "ambiance et ombres lointaines de remplacement");
            });
            ui.end_row();

            ui.label("Compteur à l'écran");
            ui.checkbox(&mut o.compteur, "afficher");
            ui.end_row();

            ui.label("FSR d'AMD (expérimental)");
            ui.horizontal(|ui| {
                ui.add(egui::TextEdit::singleline(&mut o.dossier_fsr).hint_text(r"vide = étirement simple ; D:\FidelityFX").desired_width(260.0));
            });
            ui.end_row();

            ui.label("Diagnostic");
            ui.checkbox(&mut o.mesure_carte, "chronométrer la carte (plus lent)");
            ui.end_row();
        });
        ui.label("Variables en plus (une par ligne, VICTORY_…=valeur) :");
        ui.add(egui::TextEdit::multiline(&mut o.en_plus).desired_rows(2).desired_width(f32::INFINITY).code_editor());
        if *o != avant {
            // Des réglages changés à la main : ce n'est plus le mode tel quel.
            let tel_quel = MODES.get(self.r.mode).map(|m| {
                let mut t = Options { en_plus: o.en_plus.clone(), dossier_fsr: o.dossier_fsr.clone(), ..Options::default() };
                (m.appliquer)(&mut t);
                t
            });
            if tel_quel.as_ref() != Some(&*o) {
                self.r.mode = usize::MAX;
            }
        }
    }

    fn vue_sauvegardes(&mut self, ui: &mut egui::Ui) {
        ui.strong("Sauvegardes par endroit");
        ui.weak("Garde l'état du jeu à un endroit (cellule, cafétéria, bateau...) pour y revenir au prochain lancement.");
        ui.horizontal(|ui| {
            ui.add(egui::TextEdit::singleline(&mut self.nom_sauvegarde).hint_text("nom de l'endroit").desired_width(200.0));
            if ui.button("Garder l'état actuel").clicked() {
                self.message = match sauvegardes::garder(&self.nom_sauvegarde) {
                    Ok(()) => format!("Gardé sous « {} »", sauvegardes::nom_propre(&self.nom_sauvegarde)),
                    Err(e) => format!("Pas gardé : {e}"),
                };
                self.sauvegardes = sauvegardes::liste();
            }
        });
        ui.horizontal_wrapped(|ui| {
            ui.label("Au lancement :");
            ui.radio_value(&mut self.a_remettre, None, "continuer comme le jeu l'a laissé");
            for n in &self.sauvegardes {
                ui.radio_value(&mut self.a_remettre, Some(n.clone()), n);
            }
        });
    }

    fn vue_chemins(&mut self, ui: &mut egui::Ui) {
        egui::CollapsingHeader::new("Chemins et Discord").show(ui, |ui| {
            egui::Grid::new("chemins").num_columns(2).spacing([16.0, 6.0]).show(ui, |ui| {
                ui.label("victory.exe");
                ui.add(egui::TextEdit::singleline(&mut self.r.exe).desired_width(440.0));
                ui.end_row();
                ui.label("Jeu (.exe)");
                ui.add(egui::TextEdit::singleline(&mut self.r.jeu).desired_width(440.0));
                ui.end_row();
                ui.label("Nom affiché");
                ui.add(egui::TextEdit::singleline(&mut self.r.titre).desired_width(200.0));
                ui.end_row();
                ui.label("Présence Discord");
                ui.horizontal(|ui| {
                    ui.checkbox(&mut self.r.discord, "« Joue à Grand Theft Auto VI »");
                    if !self.r.discord {
                        ui.weak("coupée");
                    } else if self.discord_relie {
                        ui.colored_label(egui::Color32::from_rgb(90, 200, 110), "relié");
                    } else {
                        ui.weak("en attente de Discord");
                    }
                });
                ui.end_row();
            });
            ui.weak("La présence utilise la fiche du jeu que Discord connaît déjà : rien à créer. Discord doit être ouvert sur ce PC.");
        });
        self.relier_discord();
    }
}
