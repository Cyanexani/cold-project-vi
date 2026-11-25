//! La présence Discord : le nom affiché est celui de l'application Discord
//! de l'utilisateur (« Joue à … ») ; on y met où en est la partie, le mode,
//! les images/s et le temps de jeu. Si Discord n'est pas ouvert, on réessaie
//! plus tard sans rien bloquer.

use std::sync::mpsc::{Receiver, Sender, channel};
use std::time::{Duration, Instant};

use discord_rich_presence::activity::{Activity, Timestamps};
use discord_rich_presence::{DiscordIpc, DiscordIpcClient};

/// Ce qu'on montre.
#[derive(Clone, PartialEq)]
pub struct Presence {
    /// Le nom du jeu (« Joue à … » ; Discord prend sinon celui de l'application).
    pub nom: String,
    pub details: String,
    pub etat: String,
    /// Début de la partie (secondes Unix), pour le temps écoulé.
    pub debut: Option<i64>,
}

pub enum Ordre {
    Montrer(Presence),
    Effacer,
}

/// Le fil Discord pour l'application `id` ; renvoie de quoi lui parler et
/// où il dit s'il est relié.
pub fn demarrer(id: String) -> (Sender<Ordre>, Receiver<bool>) {
    let (ordres, recus) = channel::<Ordre>();
    let (relie_tx, relie_rx) = channel::<bool>();
    std::thread::spawn(move || boucle(id, recus, relie_tx));
    (ordres, relie_rx)
}

fn boucle(id: String, ordres: Receiver<Ordre>, relie: Sender<bool>) {
    let mut client: Option<DiscordIpcClient> = None;
    let mut voulu: Option<Presence> = None;
    let mut montre: Option<Presence> = None;
    let mut essai = Instant::now() - Duration::from_secs(60);
    loop {
        match ordres.recv_timeout(Duration::from_secs(2)) {
            Ok(Ordre::Montrer(p)) => voulu = Some(p),
            Ok(Ordre::Effacer) => voulu = None,
            Err(std::sync::mpsc::RecvTimeoutError::Timeout) => {}
            Err(_) => {
                if let Some(c) = client.as_mut() {
                    let _ = c.close();
                }
                return;
            }
        }
        if client.is_none() && essai.elapsed() > Duration::from_secs(15) {
            essai = Instant::now();
            let mut c = DiscordIpcClient::new(&id);
            if c.connect().is_ok() {
                client = Some(c);
                montre = None;
                let _ = relie.send(true);
            }
        }
        let Some(c) = client.as_mut() else { continue };
        if voulu == montre {
            continue;
        }
        let envoye = match &voulu {
            Some(p) => {
                // Le type par défaut est « Joue à » : la partie compte comme un jeu.
                let mut a = Activity::new()
                    .name(&p.nom)
                    .details(&p.details)
                    .state(&p.etat);
                if let Some(t) = p.debut {
                    a = a.timestamps(Timestamps::new().start(t));
                }
                c.set_activity(a)
            }
            None => c.clear_activity(),
        };
        if envoye.is_ok() {
            montre = voulu.clone();
        } else {
            // Discord fermé entre-temps : on se reconnectera.
            client = None;
            let _ = relie.send(false);
        }
    }
}
