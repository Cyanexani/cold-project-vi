# Tests à faire en jeu

Ce qui a été codé sans être essayé en jeu, et comment l'essayer. Chaque
lancement :

```powershell
target\diag\victory.exe run "D:\chemin\vers\game_scarlett_master.exe" --execute
```

Les options se mettent avant, dans la même fenêtre PowerShell, par exemple
`$env:VICTORY_TEXTURE_NO_WAIT = '1'`.

**Plus besoin d'options de base** : depuis le 24/11 au soir, la commande
sans rien devant fait déjà ce que faisaient `VICTORY_TEXTURE_NO_WAIT=1`,
`VICTORY_CARD_BUFFERS_MIN_MB=1` et `VICTORY_CARD_BUFFERS_MB=2048`. Pour
revenir à l'ancien comportement d'une option : la mettre à `0` (ou 16 et
512 pour les deux tailles). `VICTORY_SHADER_LOOP_LIMIT=32` reste un essai
à part : il coupe les boucles des shaders, donc il peut abîmer l'image.

## 0. Les nouveautés de vitesse (à essayer d'abord)

Ce qui change sans rien régler :
- **Plus d'attente de 500 ms par image** : quand 16 pipelines ou plus sont
  en retard, une image n'attend plus qu'ils soient prêts (avant : chaque
  image du monde attendait 500 ms pour rien). Ancien comportement :
  `$env:VICTORY_PIPELINE_WAIT_BACKLOG = '0'`.
- **Fabrication des pipelines plus rapide** : les ateliers tournent à la
  même priorité que le jeu. Ancien : `$env:VICTORY_PIPELINE_LOW_PRIORITY = '1'`.
- **Le cache de pipelines (1 Go) n'est plus réécrit toutes les 20 s**
  pendant qu'il en reste à fabriquer : au plus toutes les 3 minutes, et à
  la fermeture. Le journal dit combien de temps prend chaque écriture
  (`host pipeline library saved ... ms=`).
- **Le cache des shaders ne repart plus de zéro à chaque recompilation de
  victory.exe** : il ne dépend plus que du code du traducteur de shaders.
  Le premier lancement après cette mise à jour retraduit tout une dernière
  fois (laisser tourner jusqu'à « being made » proche de 0), ensuite non.
- **Lumière de secours par défaut** (`VICTORY_RT_MISS`) tant que le
  constructeur de ray tracing est éteint : les visages tachetés et les
  intérieurs noirs venaient sûrement de rayons testés contre une structure
  jamais construite. Ancien : `$env:VICTORY_RT_MISS = '0'`.

- **Surfaces du décor (PS_DeferredTextured)** : un essai de les traduire
  rendait les personnages flous (essai du 25/11 : avec
  `VICTORY_SKIP_PIPELINES=78f3f2fc53a69531,38bf0814e721a1b3`, cheveux et
  visages nets). Correction retirée : ils restent refusés.
- **Flaques (PS_PuddleDetect)** : se traduisent maintenant (le type de la
  texture dont ils lisent la taille est pris sur une voisine de la même
  table). Si les flaques ou le sol mouillé ont l'air faux :
  `$env:VICTORY_SKIP_PIPELINES = '7d0f14ce44746d7e,6b6db9f1a526daeb'` et
  comparer.

À regarder : le titre de la fenêtre (fps, ms/frame) une fois le cache
rempli, et dans le journal les lignes « waiting for pipelines » (doivent
tomber de 500 ms à presque rien) et « being made » (doivent descendre
beaucoup plus vite). Me donner le journal :

```powershell
target\diag\victory.exe run "D:\chemin\vers\game_scarlett_master.exe" --execute 2> logs\essai-vitesse.txt
```

## Relais (VICTORY_RELAY=2, VICTORY_WIDE_BACK=1) : essayé, à ne pas utiliser

Essai du 25/11 à la cellule : bien plus lent. La recopie vers le jeu passe
de ~65 à 270-700 ms par image, les passes sur le tas de vues de ~36 à
400-1 180 ms, et les menus tombent de 30 à 2-3 fps. Il reste éteint par
défaut. Attention : une option posée avec `$env:` reste active dans la
fenêtre PowerShell jusqu'à sa fermeture.

## 0 bis. Avant tout : laisser le cache se remplir

Après une mise à jour du traducteur, le premier lancement recompile presque
tous les programmes graphiques : l'image reste noire ou cassée pendant
plusieurs minutes. Laisser tourner jusqu'à ce que la ligne « being made »
du journal descende vers 0, fermer, puis relancer. Le deuxième lancement
doit recharger les programmes.

## 1. Carte des vues (VIcky) : plus de textures ?

Comparer deux lancements au même endroit :
- normal ;
- avec `$env:VICTORY_HEAP_MAP = '0'` (l'ancien comportement).

Attendu : plus de textures et moins de zones grises ou noires avec la carte.

## 2. Programmes wave64 sans boucle

Nouveau par défaut : les programmes console en vagues de 64 lanes **sans
boucle** sont découpés « à l'échelle de la vague » pour tourner en vagues de
32. Le fork l'avait coupé partout parce que deux programmes de particules à
barrières **dans des boucles** bloquaient la carte ; ceux-là restent exclus.

**Personnages (PS_PedTexturedCutOutTint, 10 programmes)** : ils utilisent
la section ordonnée de la console (POPS : les pixels superposés attendent
leur tour). Ils se traduisent maintenant, sans cette attente. À regarder :
cheveux, cils, yeux. Si quelque chose clignote ou se mélange mal sur un
personnage : `$env:VICTORY_SKIP_PIPELINES = 'a76619e2dcb45652,bb3f113d13bd2788'`
et comparer.

**Textures sous pression** : quand la carte dépasse 90 % de son budget,
une texture inutilisée est libérée après 1 s et 2 images (5 s et 4 images
sinon). Si des textures disparaissent puis reviennent :
`$env:VICTORY_TEXTURE_SWEEP = '0'` pour comparer.

**Volumes d'eau** : les 2 programmes CS_TransmissionVolumetrics (somme
64 bits sur la vague, maintenant suivie) se traduisent avec
`$env:VICTORY_WAVE_WIDE = '1'` seulement : leurs branches élargies sont dans
des boucles, le cas qui avait gelé la carte.

**Transparence, cheveux, silhouettes** : 4 programmes de calcul en vagues
de 64 (dont CS_AlphTransmissionPrefilterMip, la transparence) restent
refusés par défaut. Avec `$env:VICTORY_WAVE_WIDE = '1'`, 2 d'entre eux se
traduisent (essai hors jeu). C'est l'essai à faire pour voir si les
cheveux et les personnes transparentes changent.

À surveiller : une perte de la carte graphique (écran figé puis « device
removed » dans le journal). Si ça arrive : `$env:VICTORY_WAVE_WIDE = '0'`
et me donner le journal.

Attendu : moins de lignes « shader does not translate » et « host pipeline
refused » dans le journal.

## 3. Cheveux baveux

VIcky dit que les cheveux sont nets avant les commits wave64 du fork et
baveux après. Essai : `$env:VICTORY_WAVE_LOCAL = 'idiom'` (seulement les
formes exactes des programmes wave64, aucune approximation). Si les cheveux
redeviennent nets, la cause est une forme approchée.

## 4. Lumière de secours (zones noires)

Maintenant active par défaut (voir 0). Comparer avec
`$env:VICTORY_RT_MISS = '0'` pour voir la différence. Le test rayon/nœud émulé répond « rien
touché ». Les programmes de tracé du jeu calculent alors la lumière comme si
rien ne la bloquait : les zones aujourd'hui noires devraient s'éclairer (sans
ombres de ray tracing).

Attendu : intérieurs et faces à l'ombre visibles. Possible aussi : tout trop
clair. Me dire ce que tu vois.

## 5. Ray tracing complet

Dans l'ordre, chacun dans son lancement :
1. `$env:VICTORY_BVH_BUILDER = '1'` et `$env:VICTORY_BVH_QUARANTINE = '1'` :
   le constructeur tourne sans rien réécrire dans la mémoire du jeu.
   Chercher dans le journal « the ray-tracing structure builder runs » ou
   « stays off » (avec le nombre de programmes manquants).
2. Si ça tourne sans planter : `VICTORY_BVH_BUILDER=1` seul.

Avec le constructeur allumé, la lumière de secours s'éteint toute seule.
Ne pas ajouter `VICTORY_RT_MISS=1`.

## 6. Logo Rockstar et murs invisibles

À observer, et me signaler avec le journal :
- le logo de l'intro est-il toujours mal éclairé ? Avec `VICTORY_RT_MISS=1` ?
- y a-t-il des murs invisibles dans le monde (VIcky en voit beaucoup) ?

## Le journal

Le journal complet est ce que le jeu écrit dans la fenêtre PowerShell. Pour
le garder dans un fichier :

```powershell
target\diag\victory.exe run "D:\chemin\vers\game_scarlett_master.exe" --execute 2> logs\essai.txt
```
