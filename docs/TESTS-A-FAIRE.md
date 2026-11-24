# Tests à faire en jeu

Ce qui a été codé sans être essayé en jeu, et comment l'essayer. Chaque
lancement :

```powershell
target\diag\victory.exe run "D:\chemin\vers\game_scarlett_master.exe" --execute
```

Les options se mettent avant, dans la même fenêtre PowerShell, par exemple
`$env:VICTORY_TEXTURE_NO_WAIT = '1'`.

**Options de base pour tous les tests** (elles ont fait passer le jeu de
0,2-0,5 à environ 2 images/s) :

```powershell
$env:VICTORY_TEXTURE_NO_WAIT = '1'
$env:VICTORY_CARD_BUFFERS_MIN_MB = '1'
$env:VICTORY_CARD_BUFFERS_MB = '2048'
$env:VICTORY_SHADER_LOOP_LIMIT = '32'
```

## 0. Avant tout : laisser le cache se remplir

Après une mise à jour du traducteur, le premier lancement recompile presque
tous les programmes graphiques (701 refaits, 11 rechargés au dernier essai) :
l'image reste noire ou cassée pendant plusieurs minutes. Laisser tourner
jusqu'à ce que la ligne « being made » du journal descende vers 0, fermer,
puis relancer. Le deuxième lancement doit recharger les programmes.

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

`$env:VICTORY_RT_MISS = '1'` : le test rayon/nœud émulé répond « rien
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

Ne pas combiner avec `VICTORY_RT_MISS=1`.

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
