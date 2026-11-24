# VIctory : GTA VI (Xbox Series) sur PC, avancement

Version de travail de [VIctory](https://github.com/Marvisak/VIctory), qui fait
tourner la version Xbox Series de Grand Theft Auto VI directement sous Windows,
à partir des fichiers du jeu que l'utilisateur fournit. Elle part du fork
[fxceless/victory-gta6](https://github.com/fxceless/victory-gta6) (commit
`578d459`) et de ses commits suivants, jusqu'à `4e17478`, plus la « carte des
vues » de [Xeltax/VIcky](https://github.com/Xeltax/VIcky) (branche
`fx-heapmap`).

**Ce dépôt ne contient aucun fichier du jeu** : ni exécutable, ni données, ni
shaders extraits. Il faut sa propre copie du jeu. Ce projet n'est ni affilié à
Rockstar Games ou Take-Two Interactive, ni approuvé par eux.

Mis à jour le 25/11/2026.

## Crédits et licence

- **[VIctory](https://github.com/Marvisak/VIctory)**, par Marvisak et ses
  contributeurs, est la base de tout : le chargeur, les services du système,
  le rendu Direct3D 12 et le recompilateur de shaders. Licence
  GPL-3.0-or-later, comme ce dépôt (voir `LICENSE`).
- **[victory-gta6](https://github.com/fxceless/victory-gta6)**, par fxceless :
  le fork dont part ce travail (rendu du monde, ray tracing en cours).
- **[VIcky](https://github.com/Xeltax/VIcky)**, par Xeltax : la carte des vues
  pour la limite d'un million de vues des cartes NVIDIA.
- **[XCL](https://github.com/ivall/xcl)**, par ivall (GPL-2.0-only), a servi de
  référence pour le comportement du jeu sous une autre couche.
- Le texte d'origine, en anglais, est dans
  [docs/README-original.en.md](docs/README-original.en.md).
- Développé avec [Claude Code](https://claude.com/claude-code).

## Où en est le jeu

| Étape | État |
|-------|------|
| Démarrage, menus, écran titre | ✅ fonctionne |
| Cinématique d'intro | ⚠️ se joue en entier, mais le logo Rockstar est mal éclairé |
| Sauvegardes | ✅ le jeu sauvegarde et recharge un point de passage |
| Premier passage jouable (prologue) | ⚠️ s'affiche : personnages, textures, ciel, extérieur au soleil |
| Lumière dans l'ombre | ⚠️ lumière de secours par défaut : intérieurs et visages éclairés, mais certaines cinématiques restent presque noires (le ray tracing du jeu ne tourne pas encore) |
| Images par seconde dans le monde | ❌ environ 2 (500 à 570 ms par image), sans aucune option |
| Mémoire | ❌ 18 à 23 Go après 3 à 5 minutes, trop pour un PC de 16 Go |
| Stabilité | ⚠️ sans options, 5 minutes sans plantage ; avec les options mémoire, plantage entre 2 et 3 minutes |

```mermaid
pie showData
    title Avancement des étapes (sur 8)
    "Fonctionne" : 2
    "Partiel" : 4
    "Pas encore" : 2
```

## Lancer

```powershell
cargo build --profile diag -p victory --features diagnostics
target\diag\victory.exe run "D:\chemin\vers\game_scarlett_master.exe" --execute
```

Le chemin du jeu peut aussi être mis dans `victory.toml` (modèle :
`victory.example.toml`).

Les options de vitesse d'avant (`VICTORY_TEXTURE_NO_WAIT`,
`VICTORY_CARD_BUFFERS_MIN_MB`, `VICTORY_CARD_BUFFERS_MB`) sont maintenant
actives par défaut : la commande ci-dessus suffit.

Après une mise à jour **du traducteur de shaders**, le premier lancement les
retraduit : image noire ou cassée pendant plusieurs minutes. Le lancement
suivant les recharge. Recompiler le reste de `victory.exe` ne vide plus ce
cache (il ne dépend plus que du code du traducteur). Ce qui reste à essayer en jeu est décrit dans
[docs/TESTS-A-FAIRE.md](docs/TESTS-A-FAIRE.md).

## Vitesse

Temps par image dans le monde, en millisecondes (scène de la cellule, carte
RTX 3060, essais du 24/11).

```mermaid
xychart-beta
    title "Temps par image (ms), plus bas = mieux"
    x-axis ["Avant (cache + attente)", "Après les correctifs du 24/11"]
    y-axis "ms" 0 --> 1400
    bar [1316, 540]
```

```mermaid
xychart-beta
    title "Où passent les 540 ms d'une image (ms)"
    x-axis ["Carte graphique", "Préparation (copies)", "Recopie vers le jeu"]
    y-axis "ms" 0 --> 400
    bar [325, 140, 65]
```

| Par image | Avant | Après |
|---|---|---|
| Attente des pipelines | 500 ms à chaque image | 0 ms |
| Pipelines en cours de fabrication | 900 à 1 900, pendant des minutes | 0 une fois dans le monde |
| Carte graphique | 390 à 440 ms | 300 à 340 ms |
| Préparation (copies de la mémoire du jeu) | 230 à 280 ms | 130 à 160 ms |
| Recopie vers le jeu | 90 à 120 ms | 57 à 70 ms |

Ce qui a changé : une image n'attend plus des pipelines qui ne seront pas
prêts à temps, les pipelines se fabriquent à la priorité du jeu, le cache des
pipelines (1 Go) n'est plus réécrit toutes les 20 s, et le cache des shaders
survit aux recompilations.

Ce qui limite maintenant : la carte graphique (environ 325 ms), occupée à
57 % seulement, et sa mémoire presque pleine (10,8 Go sur 12). La prochaine
mesure (`VICTORY_GPU_TIMING=1`) dira quels programmes coûtent le plus. Pour
15 images/s il faut descendre sous 66 ms.

## Mémoire

PC de test : Windows 11, carte NVIDIA, **16 Go de RAM**, fichier d'échange de
32 Go.

### Mémoire du jeu au fil du temps

Mémoire privée du processus, en Go, mesurée toutes les 15 secondes. Le saut
vers 1 min 30 correspond à l'arrivée dans le monde.

```mermaid
xychart-beta
    title "Mémoire privée (Go), sans options"
    x-axis "secondes" [15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165, 180, 195, 210, 225, 240, 255, 270, 285, 300]
    y-axis "Go" 0 --> 24
    line [7.1, 7.7, 8.0, 8.2, 8.5, 12.7, 16.1, 16.4, 16.8, 16.7, 17.8, 17.7, 17.2, 17.9, 18.4, 18.6, 20.1, 21.6, 22.4, 22.4]
```

### Les lancements comparés

```mermaid
xychart-beta
    title "Mémoire privée au plus haut (Go)"
    x-axis ["Sans options", "Blocs 512 Mo", "512 Mo + trous", "Blocs 64 Mo"]
    y-axis "Go" 0 --> 24
    bar [22.9, 18.6, 12.7, 17.8]
```

```mermaid
xychart-beta
    title "Durée avant arrêt (secondes)"
    x-axis ["Sans options", "Blocs 512 Mo", "512 Mo + trous", "Blocs 64 Mo"]
    y-axis "secondes" 0 --> 320
    bar [305, 183, 107, 168]
```

| Lancement | Mémoire au plus haut | Copies de la mémoire du jeu | Fin |
|-----------|----------------------|-----------------------------|-----|
| Sans options | 22,9 Go, en hausse | 4,2 Go | arrêté à 5 min, pas de plantage |
| `VICTORY_POOL_COMMIT=1` + `VICTORY_INPLACE=1`, blocs de 512 Mo | 18,6 Go | 4,5 Go | plantage à 183 s (RAM épuisée) |
| idem + fenêtres « à trous » | 12,7 Go | presque aucune à ce moment | plantage à 107 s (RAM épuisée) |
| idem, blocs de 64 Mo | 17,8 Go | 4,4 Go | plantage à 168 s (mémoire réservable de Windows épuisée) |

### Où va la mémoire du jeu (lancement sans options, à 5 min)

Mémoire réservée par type, en Go (toute n'est pas en RAM à la fois).

```mermaid
pie showData
    title Mémoire réservée par le jeu (Go)
    "Vues des pages du jeu" : 7.4
    "Buffers partagés avec la carte" : 7.2
    "Tas du jeu" : 4.6
    "Autres allocations" : 4.3
    "Fenêtres de mémoire du jeu" : 2.4
    "Tas du processus" : 1.0
```

### La mémoire prise hors du jeu

Environ 7 Go sont déjà pris avant de lancer le jeu. Fermer Discord, Chrome et
Overwolf en libère environ 3,3 Go.

```mermaid
pie showData
    title Mémoire hors du jeu (Go)
    "Discord" : 1.6
    "Chrome" : 1.5
    "App Claude" : 1.4
    "Noyau Windows et pilotes" : 1.0
    "Edge WebView" : 0.4
    "Defender" : 0.4
    "Overwolf" : 0.2
```

### Ce qu'on a appris

- Le pilote NVIDIA refuse d'ouvrir le pool du jeu (6,1 Go) en une seule
  section. Découpé en blocs (`VICTORY_POOL_BLOCK_MB`, 64 par défaut), chaque
  bloc s'ouvre.
- **Ouvrir une section côté carte graphique la verrouille entièrement en
  RAM**, même les pages jamais touchées. Les sections ouvertes comptent donc
  dans la limite `VICTORY_INPLACE_MB` (4096 par défaut).
- La plupart des fenêtres de 4 Mo refusées ne sont que **partiellement
  mappées** par le jeu (803 fenêtres, 3,4 Go). Très peu ont leurs pages dans le
  désordre (32, 140 Mo). Les fenêtres partielles dont les pages suivent une
  même section sont maintenant lues sans copie (`VICTORY_INPLACE_HOLES=0`
  revient à la copie).
- Pointer les tuiles d'un buffer réservé vers une section **fait perdre la
  carte NVIDIA**. Les pages dispersées ne peuvent donc pas être couvertes par
  cette voie. Test : `cargo test -p victory-gpu card_probe -- --ignored --nocapture`.
- Avec `VICTORY_POOL_COMMIT=1`, Windows réserve d'un coup 6,1 Go de plus : sur
  ce PC, le jeu plante plus tôt. **Pour l'instant, mieux vaut lancer sans les
  options mémoire.**

## Image

### Shaders encore refusés par le traducteur

Mesuré en jeu le 25/11 (scène de la cellule) : 10 refusés sur environ 2 000
utilisés, contre 12 la veille. Les 2 programmes de flaques se traduisent
maintenant. Sur tout le jeu (26 084 programmes), 25 572 se traduisent hors
du jeu.

```mermaid
pie showData
    title Shaders refusés en jeu (10)
    "Calcul en vagues de 64 avec mémoire partagée (transparence)" : 4
    "Section whole-wave (eau)" : 2
    "Surfaces (interpolation, se dessinent mal si forcées)" : 2
    "Échange de masque de lanes (ombres)" : 1
    "Adresse d'un descripteur lue (simulation d'eau)" : 1
```

À cela s'ajoute **1 pipeline refusé par le pilote NVIDIA** (« Paramètre
incorrect »). Les 2 programmes de surfaces se traduisaient avec une
correction essayée le 25/11, mais ils rendaient les personnages flous : elle
est retirée.

### Ray tracing

Les commits `6a2d9f9` à `7b0f736` du fork sont intégrés : les programmes
d'ombre et de tracé se traduisent, et le test rayon/boîte et rayon/triangle du
matériel est émulé. La perte de la carte graphique de ces versions venait
d'une forme « wave64 » des shaders de particules, qui met des barrières de
groupe dans des branches et des boucles. Elle est maintenant active par défaut
pour les programmes **sans boucle** seulement (`VICTORY_WAVE_WIDE=1` : partout,
`0` : nulle part).
Le ray tracing reste désactivé par défaut (`VICTORY_BVH_BUILDER=1`). Détails :
[docs/fork-ray-tracing.md](docs/fork-ray-tracing.md).

## Ce qui reste à faire

1. Mesurer le temps de chaque programme sur la carte (`VICTORY_GPU_TIMING=1`)
   et réduire les plus coûteux (vitesse : objectif 15 images/s).
2. Traduire les shaders encore refusés (descripteurs lus comme des données,
   programmes wave64 avec boucles).
3. Faire tourner le ray tracing dans le jeu.
4. Réduire le travail de la carte graphique (vitesse).
5. Réduire la mémoire : fenêtres à trous qui ne suivent pas une seule section
   (2,6 Go encore copiés), textures côté PC (3 à 4,5 Go), buffers partagés
   avec la carte (environ 7 Go réservés).
6. Essayer en jeu le reste de [docs/TESTS-A-FAIRE.md](docs/TESTS-A-FAIRE.md)
   (carte des vues, cheveux, ray tracing, logo, murs).

## Options utiles

| Variable | Effet |
|----------|-------|
| `VICTORY_POOL_COMMIT=1` | réserve le pool du jeu d'un coup (nécessaire pour l'ouvrir sur la carte) |
| `VICTORY_POOL_BLOCK_MB` | taille des blocs du pool (64 par défaut) |
| `VICTORY_INPLACE=1` | lit la mémoire du jeu sans copie quand c'est possible |
| `VICTORY_INPLACE_MB` | mémoire verrouillée au plus pour cela (4096 par défaut) |
| `VICTORY_INPLACE_HOLES=0` | recopie les fenêtres partiellement mappées |
| `VICTORY_PIPELINE_WAIT_MS` | attente maximale des pipelines par image (500 par défaut) |
| `VICTORY_PIPELINE_WAIT_BACKLOG` | au-delà de ce nombre de pipelines en retard (16), une image ne les attend plus ; `0` : toujours attendre |
| `VICTORY_PIPELINE_LOW_PRIORITY=1` | fabrique les pipelines en priorité basse, comme avant |
| `VICTORY_SHADER_KEY_EXE=1` | cache des shaders lié à tout l'exécutable, comme avant |
| `VICTORY_GPU_TIMING=1` | temps de chaque commande sur la carte, par programme |
| `VICTORY_SKIP_PIPELINES=<hash>,...` | saute les dessins des programmes listés (pour comparer) |
| `VICTORY_MISSING_LOG=1` | `translate` : dit quelle instruction n'a pas de valeur et pourquoi |
| `VICTORY_D3D12_DEBUG=1` | couche de débogage Direct3D (build `diag`), nomme les erreurs |
| `VICTORY_BVH_BUILDER=1` | active la construction des structures de ray tracing |
| `VICTORY_WAVE_WIDE` | forme wave64 « vague entière » : par défaut sans boucle seulement ; `1` partout, `0` nulle part |
| `VICTORY_WAVE_LOCAL=idiom` | seulement les formes exactes des programmes wave64 |
| `VICTORY_RT_MISS` | lumière de secours (rayons qui ne touchent rien) : active par défaut tant que le constructeur est éteint ; `0` la coupe |
| `VICTORY_HEAP_MAP=0` | coupe la carte des vues de VIcky |
| `VICTORY_TEXTURE_NO_WAIT=0` | les textures attendent les remappages du jeu (avant : défaut) |
| `VICTORY_CARD_BUFFERS_MIN_MB` | taille dès laquelle la carte compare les buffers au lieu du processeur (1 Mo par défaut, avant 16) |
| `VICTORY_PROFILE=4` | profileur (build `diag`) |
