# VIctory : avancement (version privée)

Copie de travail privée du fork [fxceless/victory-gta6](https://github.com/fxceless/victory-gta6)
(commit `578d459`), lui-même basé sur [VIctory](https://github.com/Marvisak/VIctory).
VIctory fait tourner la version Xbox Series de Grand Theft Auto VI directement
sous Windows, à partir des fichiers du jeu que l'utilisateur fournit.

**Ce dépôt ne contient aucun fichier du jeu** (ni exécutable, ni données, ni
shaders extraits). Les journaux de lancement et les dumps restent en local
(`logs/`, ignoré par git) : ils contiennent des données du jeu.

Mis à jour le 24/11/2026.

## Le PC de test

- Windows 11, carte NVIDIA, **16 Go de RAM** (fichier d'échange de 32 Go).
- Environ 7 Go sont pris hors du jeu : Discord 1,6 Go, Chrome 1,5 Go, l'app
  Claude 1,4 Go, Edge WebView 0,4 Go, Defender 0,4 Go, Overwolf 0,2 Go, le
  noyau Windows et les pilotes environ 1 Go. Fermer Discord, Chrome et
  Overwolf avant de lancer libère environ 3,3 Go.

## Lancer

```powershell
cargo build --profile diag -p victory --features diagnostics
target\diag\victory.exe run "D:\GTA6 - Propre copie\game_scarlett_master.exe" --execute
```

## Où en est le jeu

| Étape | État |
|-------|------|
| Démarrage, menus, écran titre | fonctionne |
| Cinématique d'intro | se joue, mais **le logo Rockstar est mal éclairé** (bug déjà présent avant les travaux mémoire) |
| Premier passage jouable (prologue) | s'affiche : personnages, textures, ciel, extérieur au soleil |
| Lumière dans l'ombre | **manque** : le ray tracing du jeu ne tourne pas encore, tout ce que le soleil ne touche pas est noir |
| Images par seconde dans le monde | 2 à 3,5 |
| Mémoire | **trop élevée pour 16 Go** : 18 à 23 Go privés après 3 à 5 minutes, et en hausse |
| Stabilité | sans options, 5 minutes sans plantage ; avec les options mémoire, plantage par manque de mémoire entre 2 et 3 minutes (voir plus bas) |

## Mesures mémoire (5 minutes, jusque dans le monde)

| Lancement | Mémoire privée au plus haut | Copies de la mémoire du jeu | Fin |
|-----------|-----------------------------|-----------------------------|-----|
| Sans options | 22,9 Go, toujours en hausse | 4,2 Go | arrêté à 5 min, pas de plantage |
| `VICTORY_POOL_COMMIT=1` + `VICTORY_INPLACE=1`, blocs de 512 Mo | 18,6 Go | 4,5 Go | plantage (RAM épuisée) |
| idem + fenêtres « à trous » | 12,7 Go | quasi aucune à ce moment | plantage à 107 s (RAM épuisée) |
| idem, blocs de 64 Mo | 17,8 Go | 4,4 Go | plantage à 168 s (mémoire réservable de Windows épuisée) |

Ce qu'on a appris :

- Le pilote NVIDIA refuse d'ouvrir le pool du jeu (6,1 Go) en une seule
  section. Découpé en blocs (`VICTORY_POOL_BLOCK_MB`, 64 par défaut), chaque
  bloc s'ouvre.
- **Ouvrir une section côté carte graphique la verrouille entièrement en
  RAM**, même les pages jamais touchées. Les sections ouvertes comptent donc
  maintenant dans la limite `VICTORY_INPLACE_MB` (4096 par défaut).
- La plupart des fenêtres de 4 Mo refusées ne sont que **partiellement
  mappées** par le jeu (803 fenêtres, 3,4 Go), très peu ont leurs pages dans le
  désordre (32, 140 Mo). Les fenêtres partielles dont les pages suivent une
  même section sont maintenant lues sans copie (`VICTORY_INPLACE_HOLES=0`
  revient à la copie).
- Pointer les tuiles d'un buffer réservé vers une section **fait perdre la
  carte NVIDIA** : impossible de couvrir les pages dispersées par cette voie.
  Test : `cargo test -p victory-gpu card_probe -- --ignored --nocapture`.
- Avec `VICTORY_POOL_COMMIT=1`, Windows réserve d'un coup 6,1 Go de plus dans
  sa mémoire réservable : sur ce PC, cela fait planter plus tôt que sans
  option. **Pour l'instant, mieux vaut lancer sans les options mémoire.**

## Ce qui reste à faire

Mémoire et vitesse
1. Réduire la mémoire : les fenêtres à trous qui ne suivent pas une seule
   section (2,6 Go encore copiés), les textures côté hôte (environ 3 à 4,5 Go),
   les buffers partagés avec la carte (environ 7 Go réservés).
2. Mesurer ce qui ralentit chaque image (copies, textures, carte graphique)
   et attaquer le plus gros.

Image
3. Régler `VICTORY_PIPELINE_WAIT_MS` (500 ms par défaut) pour supprimer les
   trous à l'image, et vérifier que le cache des pipelines se remplit d'un
   lancement à l'autre (le patch d'attente est déjà dans le code).
4. Traduire les 14 shaders encore refusés : 8 « masque de lanes lu comme une
   donnée », 2 « réduction wave sans toutes les lanes », 2 « descripteur lu
   comme une donnée », 1 « échange de masque de lanes », 1 « section
   whole-wave ».
5. Corriger les 4 programmes de calcul refusés par le pilote NVIDIA
   (« Paramètre incorrect ») : un lancement avec `VICTORY_D3D12_DEBUG=1`
   donnera la raison.
6. Corriger l'éclairage du logo Rockstar dans l'intro.
7. Lumière de secours : repérer le programme qui assemble l'éclairage final
   et éclaircir les zones noires en attendant le ray tracing.

Gros chantier
8. Le ray tracing : reprendre les commits du fork `6a2d9f9` et `004bf69`. La
   perte de la carte graphique de ces versions est déjà expliquée par le
   fork (commit `e9765a7`) : une forme « wave64 » des shaders de particules
   met des barrières de groupe dans des branches, et la carte reste bloquée.
   Elle est désactivée sauf avec `VICTORY_WAVE_WIDE=1`. Il faut intégrer
   `6a2d9f9` à `7b0f736` dans cette branche.

## Options utiles

| Variable | Effet |
|----------|-------|
| `VICTORY_POOL_COMMIT=1` | réserve le pool du jeu d'un coup (nécessaire pour l'ouvrir sur la carte) |
| `VICTORY_POOL_BLOCK_MB` | taille des blocs du pool (64 par défaut) |
| `VICTORY_INPLACE=1` | lit la mémoire du jeu sans copie quand c'est possible |
| `VICTORY_INPLACE_MB` | mémoire verrouillée au plus pour cela (4096 par défaut) |
| `VICTORY_INPLACE_HOLES=0` | recopie les fenêtres partiellement mappées |
| `VICTORY_PIPELINE_WAIT_MS` | attente maximale des pipelines par image (500 par défaut) |
| `VICTORY_D3D12_DEBUG=1` | couche de débogage Direct3D (build `diag`), nomme les erreurs |
| `VICTORY_BVH_BUILDER=1` | active la construction des structures de ray tracing |

Le README anglais (`README.md`) décrit le fork d'origine et VIctory.
