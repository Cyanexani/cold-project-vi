//! Une partie : `victory.exe run <jeu> --execute`, son journal dans
//! `logs\partie-<date>.txt`, et ce qu'on y lit en direct (images/s).

use std::io::{Read, Seek, SeekFrom};
use std::path::PathBuf;
use std::process::{Child, Command, Stdio};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant, SystemTime};

/// Ce que dit la dernière ligne de vitesse du journal.
#[derive(Clone, Default)]
pub struct Etat {
    pub fps: f32,
    pub ms: f32,
    pub dessins: u32,
    /// Dans le monde (plus de 1 000 dessins par image), sinon menus.
    pub monde: bool,
    /// Les dernières lignes du journal, sans couleurs.
    pub lignes: Vec<String>,
    /// La partie s'est terminée, avec ce code.
    pub fin: Option<String>,
}

pub struct Partie {
    enfant: Arc<Mutex<Child>>,
    pub etat: Arc<Mutex<Etat>>,
    pub journal: PathBuf,
    pub debut: SystemTime,
    pub depuis: Instant,
}

impl Partie {
    pub fn lancer(
        exe: &str,
        jeu: &str,
        console: &str,
        variables: &[(String, String)],
        logs: PathBuf,
    ) -> Result<Partie, String> {
        if !std::path::Path::new(exe).is_file() {
            return Err(format!("{exe} introuvable : compiler d'abord victory (profil diag)"));
        }
        if !std::path::Path::new(jeu).is_file() {
            return Err(format!("{jeu} introuvable"));
        }
        std::fs::create_dir_all(&logs).map_err(|e| e.to_string())?;
        let quand = horodatage();
        let journal = logs.join(format!("partie-lanceur-{quand}.txt"));
        let sortie = std::fs::File::create(&journal).map_err(|e| e.to_string())?;
        let erreurs = sortie.try_clone().map_err(|e| e.to_string())?;
        let dossier = std::path::Path::new(exe)
            .ancestors()
            .nth(3)
            .map(|p| p.to_path_buf())
            .unwrap_or_else(|| PathBuf::from("."));
        let mut cmd = Command::new(exe);
        cmd.args(["run", jeu, "--execute", "--console", console])
            .current_dir(dossier)
            .stdin(Stdio::null())
            .stdout(Stdio::from(sortie))
            .stderr(Stdio::from(erreurs));
        // Rien d'une partie précédente ou de la fenêtre qui a lancé le lanceur.
        for (k, _) in std::env::vars() {
            if k.starts_with("VICTORY_") {
                cmd.env_remove(k);
            }
        }
        for (k, v) in variables {
            cmd.env(k, v);
        }
        let enfant = cmd.spawn().map_err(|e| format!("lancement impossible : {e}"))?;
        let enfant = Arc::new(Mutex::new(enfant));
        let etat = Arc::new(Mutex::new(Etat::default()));
        lire(journal.clone(), enfant.clone(), etat.clone());
        Ok(Partie { enfant, etat, journal, debut: SystemTime::now(), depuis: Instant::now() })
    }

    pub fn arreter(&self) {
        let _ = self.enfant.lock().unwrap_or_else(|e| e.into_inner()).kill();
    }

    pub fn en_cours(&self) -> bool {
        self.etat.lock().unwrap_or_else(|e| e.into_inner()).fin.is_none()
    }
}

/// Un fil qui suit le journal et l'état du processus, chaque demi-seconde.
fn lire(journal: PathBuf, enfant: Arc<Mutex<Child>>, etat: Arc<Mutex<Etat>>) {
    std::thread::spawn(move || {
        let mut lu = 0u64;
        let mut reste = String::new();
        loop {
            if let Ok(mut f) = std::fs::File::open(&journal)
                && f.seek(SeekFrom::Start(lu)).is_ok() {
                    let mut octets = Vec::new();
                    if f.read_to_end(&mut octets).is_ok() {
                        lu += octets.len() as u64;
                        reste.push_str(&String::from_utf8_lossy(&octets));
                    }
                }
            if let Some(fin) = reste.rfind('\n') {
                let complet: String = reste.drain(..=fin).collect();
                let mut e = etat.lock().unwrap_or_else(|e| e.into_inner());
                for ligne in complet.lines() {
                    let ligne = sans_couleurs(ligne);
                    if let Some(v) = vitesse(&ligne) {
                        (e.fps, e.ms, e.dessins) = v;
                        // Les dessins sautés comptent aussi (mode sans dessin).
                        e.monde = v.2 + sautes(&ligne) > 1000;
                    }
                    e.lignes.push(ligne);
                }
                let trop = e.lignes.len().saturating_sub(300);
                e.lignes.drain(..trop);
            }
            let fini = enfant
                .lock()
                .unwrap_or_else(|e| e.into_inner())
                .try_wait()
                .ok()
                .flatten();
            if let Some(code) = fini {
                etat.lock().unwrap_or_else(|e| e.into_inner()).fin = Some(match code.code() {
                    Some(0) => "fermé normalement".into(),
                    Some(c) => format!("arrêté (code {c:#x})"),
                    None => "arrêté".into(),
                });
                return;
            }
            std::thread::sleep(Duration::from_millis(500));
        }
    });
}

/// `2.3 fps, 431.0 ms/frame (...), 4500 draws/frame` → (2.3, 431.0, 4500).
fn vitesse(ligne: &str) -> Option<(f32, f32, u32)> {
    let i = ligne.find(" fps, ")?;
    let fps = ligne[..i].rsplit(' ').next()?.parse().ok()?;
    let apres = &ligne[i + 6..];
    let ms = apres.split(' ').next()?.parse().ok()?;
    let j = apres.find(" draws/frame")?;
    let dessins = apres[..j].rsplit(' ').next()?.parse().ok()?;
    Some((fps, ms, dessins))
}

/// `dropped 4858 without a host pipeline` → 4858.
fn sautes(ligne: &str) -> u32 {
    ligne
        .find("dropped ")
        .and_then(|i| ligne[i + 8..].split(' ').next())
        .and_then(|n| n.parse().ok())
        .unwrap_or(0)
}

fn sans_couleurs(ligne: &str) -> String {
    let mut s = String::with_capacity(ligne.len());
    let mut dans = false;
    for c in ligne.chars() {
        match (dans, c) {
            (false, '\u{1b}') => dans = true,
            (true, 'm') => dans = false,
            (true, _) => {}
            (false, c) => s.push(c),
        }
    }
    s
}

fn horodatage() -> String {
    let s = SystemTime::now().duration_since(SystemTime::UNIX_EPOCH).map_or(0, |d| d.as_secs());
    // Heure UTC, assez pour des noms de fichiers dans l'ordre.
    let (j, r) = (s / 86_400, s % 86_400);
    let (a, m, d) = civil(j as i64);
    format!("{a:04}{m:02}{d:02}-{:02}{:02}{:02}", r / 3600, r % 3600 / 60, r % 60)
}

/// Jours depuis 1970 → (année, mois, jour), d'après Howard Hinnant.
fn civil(z: i64) -> (i64, u32, u32) {
    let z = z + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    (yoe + era * 400 + i64::from(m <= 2), m, d)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn lit_la_ligne_de_vitesse() {
        let l = "2026-11-25T14:28:54Z  INFO victory_gpu::host: 2.4 fps, 425.0 ms/frame (123.8 preparing: copies 80.1 ms 197.8 MB; 213.0 on the GPU), 4495 draws/frame, dropped 257";
        assert_eq!(vitesse(l), Some((2.4, 425.0, 4495)));
    }

    #[test]
    fn compte_les_dessins_sautes() {
        assert_eq!(sautes("..., 0 draws/frame, dropped 4858 without a host pipeline"), 4858);
        assert_eq!(sautes("rien"), 0);
    }

    #[test]
    fn enleve_les_couleurs() {
        assert_eq!(sans_couleurs("\u{1b}[2mA\u{1b}[0m B"), "A B");
    }

    #[test]
    fn date_civile() {
        assert_eq!(civil(0), (1970, 1, 1));
        assert_eq!(civil(20_782), (2026, 11, 25));
    }
}
