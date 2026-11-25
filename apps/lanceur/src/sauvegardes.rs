//! Les sauvegardes par endroit : une copie du dossier `saves` du jeu
//! (quelques centaines de Ko) sous un nom (« cellule », « bateau »...),
//! remise avant un lancement pour repartir de là.

use std::path::{Path, PathBuf};

/// Le dossier des sauvegardes du jeu.
pub fn du_jeu() -> Option<PathBuf> {
    let base = std::env::var_os("VICTORY_DATA_DIR")
        .map(PathBuf::from)
        .or_else(|| std::env::var_os("LOCALAPPDATA").map(|d| PathBuf::from(d).join(r"victory\data")))?;
    Some(base.join(r"7952987A\saves"))
}

/// Le dossier où le lanceur garde les siennes.
pub fn gardees() -> Option<PathBuf> {
    std::env::var_os("LOCALAPPDATA").map(|d| PathBuf::from(d).join(r"victory\sauvegardes-lanceur"))
}

/// Les noms gardés, triés.
pub fn liste() -> Vec<String> {
    let Some(d) = gardees() else { return Vec::new() };
    let mut noms: Vec<String> = std::fs::read_dir(d)
        .into_iter()
        .flatten()
        .flatten()
        .filter(|e| e.path().is_dir())
        .filter_map(|e| e.file_name().into_string().ok())
        .filter(|n| !n.starts_with('_'))
        .collect();
    noms.sort();
    noms
}

/// Un nom de dossier sûr.
pub fn nom_propre(nom: &str) -> String {
    nom.trim()
        .chars()
        .map(|c| if c.is_alphanumeric() || c == '-' || c == ' ' { c } else { '_' })
        .collect::<String>()
        .trim()
        .to_string()
}

/// Garde l'état actuel du jeu sous `nom` (remplace une copie du même nom).
pub fn garder(nom: &str) -> Result<(), String> {
    let nom = nom_propre(nom);
    if nom.is_empty() {
        return Err("donner un nom".into());
    }
    let (src, dst) = (du_jeu().ok_or("pas de dossier du jeu")?, gardees().ok_or("pas de dossier")?.join(&nom));
    if !src.is_dir() {
        return Err(format!("{} n'existe pas encore : lancer le jeu une fois", src.display()));
    }
    let _ = std::fs::remove_dir_all(&dst);
    copier(&src, &dst).map_err(|e| e.to_string())
}

/// Remet la copie `nom` à la place des sauvegardes du jeu ; celles d'avant
/// vont dans `_avant-restauration`.
pub fn remettre(nom: &str) -> Result<(), String> {
    let base = gardees().ok_or("pas de dossier")?;
    let src = base.join(nom);
    let jeu = du_jeu().ok_or("pas de dossier du jeu")?;
    if !src.is_dir() {
        return Err(format!("pas de sauvegarde « {nom} »"));
    }
    if jeu.is_dir() {
        let avant = base.join("_avant-restauration");
        let _ = std::fs::remove_dir_all(&avant);
        copier(&jeu, &avant).map_err(|e| e.to_string())?;
        std::fs::remove_dir_all(&jeu).map_err(|e| e.to_string())?;
    }
    copier(&src, &jeu).map_err(|e| e.to_string())
}

fn copier(src: &Path, dst: &Path) -> std::io::Result<()> {
    std::fs::create_dir_all(dst)?;
    for e in std::fs::read_dir(src)? {
        let e = e?;
        let vers = dst.join(e.file_name());
        if e.file_type()?.is_dir() {
            copier(&e.path(), &vers)?;
        } else {
            std::fs::copy(e.path(), vers)?;
        }
    }
    Ok(())
}
