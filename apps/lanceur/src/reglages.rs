//! Les réglages du lanceur, gardés entre deux lancements dans
//! `%LOCALAPPDATA%\victory\lanceur.toml`.

use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

/// Un mode tout prêt : un nom, une explication et ses réglages.
pub struct Mode {
    pub nom: &'static str,
    pub info: &'static str,
    pub appliquer: fn(&mut Options),
}

/// Les options d'une partie, traduites en variables `VICTORY_*`.
#[derive(Clone, Serialize, Deserialize, PartialEq)]
#[serde(default)]
pub struct Options {
    /// `series-s` ou `series-x`.
    pub console: String,
    /// Résolution interne fixée (part de la taille de sortie), ou celle du jeu.
    pub echelle: Option<f32>,
    /// L'upscaler temporel du jeu à la place du FSR (avec `echelle`).
    pub upscaler_du_jeu: bool,
    /// Le compteur en haut à gauche de l'image.
    pub compteur: bool,
    /// Rien de dessiné : ni pipelines ni shaders (test de la mémoire).
    pub sans_dessin: bool,
    /// Surfaces grises, sans les textures du jeu (test).
    pub sans_textures: bool,
    /// Part de la mémoire de la carte gardée pour les textures (%).
    pub textures_pourcent: Option<u32>,
    /// Lumière ambiante et ombres lointaines de remplacement.
    pub eclairage_remplacement: bool,
    /// Lumière de secours tant que le ray tracing ne tourne pas.
    pub lumiere_secours: bool,
    /// Le dossier du FSR d'AMD pour PC (expérimental).
    pub dossier_fsr: String,
    /// Chronométrer chaque commande de la carte (diagnostic, plus lent).
    pub mesure_carte: bool,
    /// Lignes `NOM=valeur` de plus.
    pub en_plus: String,
}

impl Default for Options {
    fn default() -> Self {
        Options {
            console: "series-s".into(),
            echelle: None,
            upscaler_du_jeu: false,
            compteur: true,
            sans_dessin: false,
            sans_textures: false,
            textures_pourcent: None,
            eclairage_remplacement: false,
            lumiere_secours: true,
            dossier_fsr: String::new(),
            mesure_carte: false,
            en_plus: String::new(),
        }
    }
}

impl Options {
    /// Les variables d'environnement de la partie.
    pub fn variables(&self) -> Vec<(String, String)> {
        let mut v: Vec<(String, String)> = Vec::new();
        let mut mettre = |k: &str, val: String| v.push((k.to_string(), val));
        if let Some(e) = self.echelle {
            mettre("VICTORY_RENDER_SCALE", format!("{e:.2}"));
        }
        if self.upscaler_du_jeu {
            mettre("VICTORY_OWN_UPSCALER", "1".into());
        }
        if !self.compteur {
            mettre("VICTORY_HUD", "0".into());
        }
        if self.sans_dessin {
            mettre("VICTORY_NO_DRAWS", "1".into());
        }
        if self.sans_textures {
            mettre("VICTORY_NO_TEXTURES", "1".into());
        }
        if let Some(p) = self.textures_pourcent {
            mettre("VICTORY_TEXTURE_ROOM_PERCENT", p.to_string());
        }
        if self.eclairage_remplacement {
            mettre("VICTORY_AMBIENT_STUB", "1".into());
            mettre("VICTORY_SHADOWMASK_STUB", "1".into());
        }
        if !self.lumiere_secours {
            mettre("VICTORY_RT_MISS", "0".into());
        }
        if !self.dossier_fsr.trim().is_empty() {
            mettre("VICTORY_FIDELITYFX_DIR", self.dossier_fsr.trim().to_string());
        }
        if self.mesure_carte {
            mettre("VICTORY_GPU_TIMING", "1".into());
        }
        for ligne in self.en_plus.lines() {
            if let Some((k, val)) = ligne.split_once('=') {
                let k = k.trim();
                if k.starts_with("VICTORY_") {
                    mettre(k, val.trim().to_string());
                }
            }
        }
        v
    }
}

/// Les modes de `Lancer.ps1`, repris.
pub const MODES: &[Mode] = &[
    Mode {
        nom: "Normal (recommandé)",
        info: "Series S, réglages par défaut : le meilleur mesuré (2,3 images/s à la cellule).",
        appliquer: |_| {},
    },
    Mode {
        nom: "Demi-résolution",
        info: "Le jeu rend à 50 % puis agrandit : image plus floue, carte moins chargée.",
        appliquer: |o| {
            o.echelle = Some(0.5);
            o.upscaler_du_jeu = true;
        },
    },
    Mode {
        nom: "Très basse résolution (test)",
        info: "Rendu à 30 % : moche, pour tester et voir combien d'images/s le PC atteint.",
        appliquer: |o| {
            o.echelle = Some(0.3);
            o.upscaler_du_jeu = true;
        },
    },
    Mode {
        nom: "Sans textures + très basse résolution (test)",
        info: "Surfaces grises et rendu à 30 % : pour tester le monde et les fps sans les textures.",
        appliquer: |o| {
            o.sans_textures = true;
            o.echelle = Some(0.3);
            o.upscaler_du_jeu = true;
        },
    },
    Mode {
        nom: "Sans dessin (test de la mémoire)",
        info: "Ni pipelines ni shaders : écran noir, le jeu tourne quand même. Pour voir si la RAM déborde encore.",
        appliquer: |o| o.sans_dessin = true,
    },
    Mode {
        nom: "Économie de textures",
        info: "Moins de textures gardées sur la carte (50 % de sa mémoire) : plus floues, moins de mémoire.",
        appliquer: |o| o.textures_pourcent = Some(50),
    },
    Mode {
        nom: "Éclairage de remplacement",
        info: "Lumière ambiante et ombres lointaines de remplacement (zones sombres moins noires).",
        appliquer: |o| o.eclairage_remplacement = true,
    },
    Mode {
        nom: "Tout pour la vitesse",
        info: "Demi-résolution + éclairage de remplacement + économie de textures.",
        appliquer: |o| {
            o.echelle = Some(0.5);
            o.upscaler_du_jeu = true;
            o.eclairage_remplacement = true;
            o.textures_pourcent = Some(50);
        },
    },
    Mode {
        nom: "Series X",
        info: "Profil Series X : plus lourd, demande plus de mémoire.",
        appliquer: |o| o.console = "series-x".into(),
    },
    Mode {
        nom: "Mesure de la carte (diagnostic)",
        info: "Chronomètre chaque commande de la carte : plus lent, pour les journaux.",
        appliquer: |o| o.mesure_carte = true,
    },
];

/// Ce que le lanceur garde entre deux lancements.
#[derive(Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct Reglages {
    /// Le nom affiché (fenêtre, Discord).
    pub titre: String,
    /// Montrer la partie sur Discord.
    pub discord: bool,
    /// L'identifiant de l'application Discord (vide : la fiche du jeu).
    pub discord_id: String,
    /// `victory.exe` (diag pour avoir les fps dans le journal).
    pub exe: String,
    /// L'exécutable du jeu.
    pub jeu: String,
    /// Le dernier mode choisi.
    pub mode: usize,
    pub options: Options,
}

impl Default for Reglages {
    fn default() -> Self {
        let racine = racine_du_projet();
        let exe = racine
            .as_ref()
            .map(|r| r.join(r"target\diag\victory.exe").display().to_string())
            .unwrap_or_default();
        let jeu = racine.as_deref().and_then(jeu_de_victory_toml).unwrap_or_default();
        Reglages {
            titre: "Cold Project VI".into(),
            discord: true,
            discord_id: String::new(),
            exe,
            jeu,
            mode: 0,
            options: Options::default(),
        }
    }
}

/// La fiche du jeu dans la liste publique des jeux que Discord reconnaît
/// (`/api/v9/applications/detectable`) : « Joue à » et son nom,
/// avec son icône, sans application à créer.
pub const DISCORD_JEU: &str = "1445160676522856458";

impl Reglages {
    /// L'identifiant à utiliser, ou rien si la présence est coupée.
    pub fn id_discord(&self) -> Option<String> {
        if !self.discord {
            return None;
        }
        let id = self.discord_id.trim();
        let id = if id.is_empty() { DISCORD_JEU } else { id };
        id.chars().all(|c| c.is_ascii_digit()).then(|| id.to_string())
    }
}

fn fichier() -> Option<PathBuf> {
    std::env::var_os("LOCALAPPDATA").map(|d| PathBuf::from(d).join(r"victory\lanceur.toml"))
}

impl Reglages {
    pub fn charger() -> Reglages {
        fichier()
            .and_then(|f| std::fs::read_to_string(f).ok())
            .and_then(|t| toml::from_str(&t).ok())
            .unwrap_or_default()
    }

    pub fn garder(&self) {
        let Some(f) = fichier() else { return };
        if let Some(d) = f.parent() {
            let _ = std::fs::create_dir_all(d);
        }
        if let Ok(t) = toml::to_string_pretty(self) {
            let _ = std::fs::write(f, t);
        }
    }
}

/// Le dossier du projet : celui qui a `victory.toml` ou `Cargo.toml` et
/// `target`, en remontant depuis le lanceur, puis depuis le dossier courant.
pub fn racine_du_projet() -> Option<PathBuf> {
    let departs = [
        std::env::current_exe().ok(),
        std::env::current_dir().ok(),
    ];
    for depart in departs.into_iter().flatten() {
        for d in depart.ancestors() {
            // Le dépôt, ou un paquet (le lanceur à côté de target\diag\victory.exe).
            if d.join("victory.example.toml").is_file()
                && (d.join("crates").is_dir() || d.join(r"target\diag\victory.exe").is_file())
            {
                return Some(d.to_path_buf());
            }
        }
    }
    None
}

/// `game_dir` de `victory.toml`, avec l'exécutable du jeu.
fn jeu_de_victory_toml(racine: &Path) -> Option<String> {
    let texte = std::fs::read_to_string(racine.join("victory.toml")).ok()?;
    let valeur: toml::Value = toml::from_str(&texte).ok()?;
    let dir = valeur.get("paths")?.get("game_dir")?.as_str()?;
    Some(Path::new(dir).join("game_scarlett_master.exe").display().to_string())
}
