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
| Mémoire | ❌ 24 Go réservés, 5,8 Go en RAM sur un PC de 16 Go (Windows échange avec le disque) ; carte à 10 Go sur 11, 5,2 Go débordent dans la RAM |
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

**Avant de jouer, et après chaque mise à jour du traducteur de shaders** :
double-cliquer sur `Prechauffer.cmd` (ou `victory.exe translate --cache`).
Il traduit d'avance tous les shaders du jeu (25 674 sur environ 26 000)
dans le cache, comme une partie le ferait, pour que le jeu ne le fasse pas
en pleine partie : quelques minutes la première fois. Sans lui, le premier
lancement les traduit au fil de l'eau : image noire ou cassée pendant
plusieurs minutes. Recompiler le reste de `victory.exe` ne vide plus ce
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

### Les modes essayés pour que la carte n'attende plus

La carte reste inactive environ 40 % du temps : le fil qui lui envoie le
travail attend qu'elle ait fini chaque envoi (environ 30 fois par image)
avant de préparer le suivant. Le fork d'origine a des modes qui envoient la
suite sans attendre. Sur ce PC (16 Go), ils sont plus lents : le travail en
vol demande plus de mémoire, qui déborde déjà.

```mermaid
xychart-beta
    title "Temps par image à la cellule (ms), plus bas = mieux"
    x-axis ["Normal", "VICTORY_LATE_WAIT=3", "VICTORY_RELAY=2 + WIDE_BACK"]
    y-axis "ms" 0 --> 1000
    bar [540, 850, 900]
```

| Par image | Normal | Attente tardive | Relais |
|---|---|---|---|
| Recopie vers le jeu | ~70 ms | 170 à 300 ms | 270 à 700 ms |
| Menus | 30 fps | — | 2 à 3 fps |

Ils restent éteints par défaut.

### Les débuts de plan

À chaque changement de plan, des centaines de nouvelles textures arrivent :
la passe qui les crée et les envoie à la carte coûte alors de 150 à
1 600 ms par image (35 ms une fois la scène chargée). C'est ce qui donne les
personnages en basse qualité et les blocs pixelisés des premières secondes.
Leur préparation (lecture, empreinte, détuilage) tourne maintenant sur
plusieurs cœurs (`VICTORY_WIDE_TEXTURES=0` : sur un seul, comme avant).

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

### Shaders

Le jeu contient 26 084 shaders ; une scène en utilise environ 2 000.
Chiffres du 26/11 (traduction hors jeu de tout `shaders.rpf`, carte en
vagues de 32 comme une RTX 3060).

```mermaid
xychart-beta
    title "Shaders traduits sur tout le jeu, par sorte (%)"
    x-axis ["Sommets et pixels", "Calcul", "Mesh", "Ensemble"]
    y-axis "%" 95 --> 100
    bar [99.99, 98.8, 100, 99.87]
```

| Sorte | Traduits | Au total | % |
|---|---|---|---|
| Sommets et pixels | 14 246 | 14 247 | 99,99 % |
| Calcul | 2 650 | 2 682 | 98,8 % |
| Mesh | 8 765 | 8 765 | 100 % |
| **Ensemble** | **25 661** | **25 694** | **99,87 %** |

S'y ajoutent 195 shaders de géométrie (voxels, 0,7 % du jeu) dont les
ressources ne sont pas encore suivies : en les comptant, 98,4 % des
26 084 shaders se traduisent.

```mermaid
xychart-beta
    title "Shaders traduits sur tout le jeu (%), au fil des jours"
    x-axis ["25/11", "26/11"]
    y-axis "%" 99 --> 100
    bar [99.68, 99.87]
```

Débloqués le 26/11 : **40 surfaces** (murs et sols PS_TexturedDeferred,
PS_TexturedLit, reflets de miroir, flaques, 3 mesh de terrain et d'ombres).
Elles choisissaient entre deux échantillonneurs fixes (répéter ou bloquer
au bord), ce qu'un handle D3D12 ne peut pas faire : chaque lecture de
texture est maintenant faite avec les deux et le bon résultat gardé.
Les 24 et 25/11 : flaques (2), simulation d'eau (1), transparence (2),
personnages à section ordonnée POPS (10, cheveux et cils).

Les 33 encore refusés, par cause :

```mermaid
pie showData
    title Shaders encore refusés sur tout le jeu (33)
    "Vagues de 64 partageant la LDS (particules, traînées, eau, ombres de cheveux)" : 24
    "Descripteur lu comme donnée (traceurs d'ombres RT)" : 5
    "Masques de lanes (eau, ombres)" : 3
    "Sommets et pixels (voxels)" : 1
```

Les 24 « vagues de 64 » partagent la mémoire de groupe entre plusieurs
vagues : c'est l'étape 8 de [docs/wave64-on-wave32.md](docs/wave64-on-wave32.md),
encore ouverte (risque de bloquer la carte). Dans la scène de la cellule,
il en manque 6.

### Ray tracing

Les commits `6a2d9f9` à `7b0f736` du fork sont intégrés : les programmes
d'ombre et de tracé se traduisent, et le test rayon/boîte et rayon/triangle du
matériel est émulé. La perte de la carte graphique de ces versions venait
d'une forme « wave64 » des shaders de particules, qui met des barrières de
groupe dans des branches et des boucles. Elle est maintenant active par défaut
pour les programmes **sans boucle**, et pour ceux dont les branches élargies
sont toutes hors des boucles (`VICTORY_WAVE_WIDE=1` : partout, `0` : nulle
part).
Le ray tracing reste désactivé par défaut (`VICTORY_BVH_BUILDER=1`). Tant
qu'il l'est, une **lumière de secours** est active : le test rayon/nœud
répond « rien touché », ce qui éclaire les zones à l'ombre sans ombres de
ray tracing (`VICTORY_RT_MISS=0` la coupe). Les cinématiques de nuit restent
très sombres : leurs visages ne sont éclairés que par les contours. Détails :
[docs/fork-ray-tracing.md](docs/fork-ray-tracing.md).

## Ce qui reste à faire

1. Vitesse : réduire la passe des débuts de plan, les copies et la recopie,
   et la mémoire de la carte ; garder sur le disque les textures préparées
   d'une partie à l'autre si elles coûtent (objectif 10 images/s).
2. Traduire les shaders encore refusés : groupes de plusieurs vagues de 64,
   surfaces, volumes d'eau sans risque de blocage.
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
| `VICTORY_TEXTURE_SWEEP=0` | ne libère jamais les textures inutilisées |
| `VICTORY_SKIP_PIPELINES=<hash>,...` | saute les dessins des programmes listés (pour comparer) |
| `VICTORY_WIDE_TEXTURES=0` | prépare les textures sur un seul cœur, comme avant |
| `VICTORY_LATE_WAIT=0` | attend la carte après chaque envoi, comme avant (par défaut : envoie la suite sans attendre, 450-480 ms par image au lieu de 560-610) |
| `VICTORY_INDIRECT_WINDOW=0` | vérifie les dessins indirects un par un, comme avant (par défaut : jusqu'à 64 d'un coup, plus rapide et les murs reviennent) |
| `VICTORY_WIDE_REFRESH=1` | copies de la mémoire du jeu sur plusieurs cœurs (en essai) |
| `VICTORY_RELAY=2`, `VICTORY_LATE_WAIT=3` | envoi sans attendre la carte : plus lent sur un PC de 16 Go |
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
