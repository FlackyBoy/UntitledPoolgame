# Tous les réglages

> Tous les réglages du jeu : où ils se trouvent, comment les modifier et ce que fait chaque valeur. Les valeurs indiquées sont celles par défaut du code ; celles de ton projet peuvent différer si elles ont déjà été réglées.

## Vue d'ensemble

| Type de réglage | Où | Créé par | Pour quoi |
|---|---|---|---|
| **Fichiers de réglages partagés** | `Assets/Resources/*.asset` | *Tools > Pool > Ensure Config Assets Exist* | Physique des billes, sensations caméra, effets d'empochage, apparition des pouvoirs, HUD, niveaux du menu, menu et pause, écran de chargement, trous vivants (Catch the Hole) |
| **Scène du menu** | `Assets/Scenes/MainMenu.unity` | *Tools > Pool > Create Main Menu Scene* | Le menu principal, première scène du jeu |
| **Écrans des menus** | `Assets/Resources/Menus/*.asset` | *Ensure Config Assets Exist*, modifiés dans *Tools > Pool > Menu Studio* | Accueil, pages du menu, réglages, pause, chargement : éléments, positions, textes, effets, Feel |
| **Séquences Feel** | `Assets/Feel/Sequences/*.prefab`, liées dans `Resources/GameFeelSettings` | *Tools > Pool > Text FX Studio* ou à la main | Son, vibration, texte du HUD, secousse… à un moment de la partie |
| **Un fichier par pouvoir** | `Assets/Powers/*.asset` | *Create > Pool > Powers > …* (ou l'outil ci-dessus pour la bille destructrice et l'aimant) | Nom et paramètres de chaque pouvoir |
| **Composants du joueur** | Prefab `Assets/Prefabs/PlayerLocal/New.prefab` | Déjà sur le prefab | Déplacement, visée, ragdoll, prise de la queue, combat |
| **Touches** | `Assets/InputManagers/InputSystem_Actions_Local.inputactions` | Déjà présent | Clavier, souris, manette |
| **Réglages d'éditeur** | `Assets/Editor/*.asset`, palettes et ambiances | Outils d'éditeur | Dimensions de table, outil de niveau |
| **Caméra (Cinemachine)** | Composant *Cinemachine Brain* de la caméra de chaque joueur | Déjà présent | Durée des transitions de caméra |

## Comment modifier un réglage

1. **Trouver le fichier** dans la fenêtre *Project* (chemins ci-dessous), ou le composant sur le prefab du joueur.
2. **Le sélectionner** : ses valeurs s'affichent dans l'*Inspector*. Survoler un champ affiche son aide (infobulle).
3. **Changer la valeur**. Pour un fichier `.asset`, c'est enregistré tout de suite (pensez à *File > Save Project* avant de commiter).

### Pendant que le jeu tourne (Play Mode)

- **Fichiers `.asset`** (Resources, pouvoirs) : la modification s'applique **immédiatement** et **reste** après l'arrêt du jeu. Pratique pour régler en jouant ; attention, il n'y a pas d'annulation automatique.
- **Composants d'un objet de la scène** : la modification s'applique immédiatement mais **est perdue** à l'arrêt du jeu (comportement normal d'Unity). Notez la valeur, arrêtez le jeu, puis reportez-la **sur le prefab**.
- **HUD** : les durées s'appliquent tout de suite ; les textes, couleurs et tailles au prochain lancement de la scène.
- **Menu, pause, écran des touches** (MenuSettings) : les durées s'appliquent tout de suite ; les textes, couleurs, polices et images au prochain lancement.

### Bonnes pratiques

- **Modifier le prefab, pas l'instance** : ouvrez `New.prefab` (double-clic) pour que les deux joueurs aient les mêmes réglages. Une valeur changée sur l'instance d'une scène apparaît en gras et ne vaut que pour cette scène.
- **Revenir aux valeurs par défaut** : *clic droit sur le composant > Reset*. Attention, ça réinitialise **tout** le composant, références comprises.
- **Fichier manquant** : le jeu ne plante pas. Il utilise les valeurs par défaut du code et affiche un avertissement dans la console ; relancez *Tools > Pool > Ensure Config Assets Exist*.
- **L'outil de création ne touche jamais un fichier existant** : il ne crée que ceux qui manquent. Pour repartir des valeurs par défaut, supprimez le fichier puis relancez l'outil.

## Fichiers de réglages partagés (Resources)

Tous dans `Assets/Resources`, créés par **Tools > Pool > Ensure Config Assets Exist**. Chargés au démarrage par les composants qui en ont besoin.

### PoolPhysicsSettings — comportement des billes

| Champ | Défaut | Effet |
|---|---|---|
| Slide Friction | 6 | Freinage tant que la bille glisse (juste après le choc, avant de rouler) |
| Friction | 2 | Résistance au roulement une fois qu'elle roule |
| Sleep Velocity Threshold | 0,01 | Vitesse sous laquelle la bille est arrêtée net |
| Off Table Drop Threshold | 0,15 | Chute (m) sous laquelle une bille sortie de la table compte comme empochée |

### PoolScreenJuiceSettings — sensations caméra et écran

| Groupe | Champs | Défauts |
|---|---|---|
| Vision brouillée (pouvoir) | couleur du voile, fréquence de clignotement, force de la secousse | blanc, 8 /s, 0,03 m |
| Charge du tir | rapprochement de la caméra, secousse maximale | 0,15 m, 0,012 m |
| Tir | secousse (force, durée) | 0,015 m, 0,1 s |
| Bille empochée | secousse (force, durée) | 0,02 m, 0,15 s |
| Faute | secousse (force, durée), flash (couleur, durée) | 0,04 m, 0,25 s, rouge, 0,3 s |
| Pouvoir ramassé | secousse (force, durée), flash (couleur, durée) | 0,02 m, 0,15 s, cyan, 0,2 s |

### PoolPotEffectSettings — empochage et poches

| Groupe | Champs | Défauts |
|---|---|---|
| Halo lumineux | couleur, intensité, portée, durée | or, 8, 0,6 m, 0,35 s |
| Aura qui monte | prefab (Cartoon FX *Magic Aura A*), durée active, marge de disparition, échelle, vitesse | 1,2 s, 4 s, 0,4, ×1,8 |
| Annonce de la 8 | intensité de surbrillance de la poche visée (sert aussi au trou visé par un pouvoir) | 6 |
| Trou visé par un pouvoir (*Pocket targeted by a power*) | couleur du cercle, rayon (× rayon de la poche), épaisseur, hauteur au-dessus de la poche, battements par seconde, amplitude du battement | blanc, ×1,3, 0,008 m, 0,004 m, 2,5, 0,12 |
| Poche fermée (pouvoir) | prefab du bouchon (facultatif), échelle | —, 1 |
| Ralenti | échelle de temps, durée | 0,3, 0,12 s |

### SpeedPoolSettings — mode Speed Pool

| Groupe | Champs | Défauts |
|---|---|---|
| Blanches | teinte de la blanche de J1, de J2 ; écart de chaque blanche par rapport au point de départ habituel (*Cue Spread*, part de la demi-largeur du tapis) | bleu, rouge, 0,5 |
| Blanche rentrée | délai avant son retour sur la table (*Cue Respawn Delay*, temps réel), à son point de départ ou au plus près s'il est occupé ; la dernière bille de son groupe que le joueur a rentrée ressort à sa place du rack (*Respot Own Ball On Scratch*) | 5 s, oui |

Créé par *Tools > Pool > Ensure Config Assets Exist* ; sans fichier, les valeurs par défaut s'appliquent.

### PoolPowerSpawnSettings — apparition des pouvoirs

| Groupe | Champs | Défauts |
|---|---|---|
| Pouvoirs distribués | *Available Powers* : la liste des assets de pouvoirs qui peuvent sortir | tous les pouvoirs du projet |
| Couleurs par catégorie | Attaque, Défense, Effet (caisses, halo de la bille, HUD) | rouge, bleu, vert |
| Caisses | nombre en même temps, délai de réapparition min / max | 3, 15 s, 30 s |
| Visuels des caisses | un prefab par catégorie, utilisé pour un pouvoir sans jeton à lui (rempli par *Générer les jetons de pouvoir* : un « ? » sur l'anneau de la catégorie) | jetons « ? » |
| Bille à pouvoir | intervalle de changement min / max, intensité et portée du halo | 20 s, 40 s, 4, 0,3 m |
| Visuels de la bille | un matériau par catégorie (facultatif) | — |
| Geste : pointer la cible | pendant le choix d'un trou ou d'un point de la table, le bras se tend vers l'endroit visé et l'index le pointe : oui / non, main droite ou gauche (avec la queue en main, cette main la lâche le temps du geste), bras tendu (part de sa longueur), limites du bras devant le corps (*Point Limits* : vers l'extérieur, à travers le corps, vers le haut, vers le bas ; quand le pointeur n'est plus sur le billard, le bras se baisse), fermeture des autres doigts (sans effet sur le personnage actuel, dont la chaîne de l'index porte les quatre doigts), du pouce, de l'index (négatif = plus tendu) | oui, droite, 0,9, (80° ; 30° ; 50° ; 75°), 85°, 40°, −5° |
| Geste : pouce levé | une fois le pouvoir envoyé (tous les pouvoirs), le poing devant la poitrine, pouce en l'air : oui / non, durée, position de la main par rapport à l'épaule (devant, hauteur, vers le milieu du corps), fermeture des doigts, du pouce (négatif = plus ouvert) ; position du coude par rapport à l'épaule (*Thumbs Up Elbow Offset* : devant, hauteur, vers le milieu du corps ; sous l'épaule, près du corps) ; pour les deux gestes : force du guide du coude (*Gesture Elbow Out*, 0 = il suit l'animation et peut rentrer dans le corps), ligne de diagnostic dans la console à chaque pouce levé (*Gesture Debug Logs*), durée pour lever le bras (*Gesture Blend Time*) et pour le redescendre (*Gesture Lower Time*) ; la main passe en arc devant le corps | oui, 1,2 s, (0,32 ; 0,02 ; 0,1) m pour un bras de 0,6 m (mis à l'échelle du personnage), 95°, −15°, (0,08 ; −0,3 ; −0,06) m, 0,7, oui, 0,3 s, 0,5 s |

Les pouvoirs n'apparaissent qu'en mode **Party**. Pour retirer un pouvoir du jeu sans le supprimer, enlevez-le de *Available Powers*.

### HungryHoleSettings — blanche-trou (mode Hungry Hole)

La blanche de chaque joueur est un trou qui avale les billes plus petites que lui et grandit (*Lancer sans le menu* : *Mode* = *HungryHole*).

| Groupe | Champs | Défauts |
|---|---|---|
| Taille des billes | la plus petite de chaque groupe (1 et 9), la plus grosse (7 et 15), la 8 ; les autres entre les deux selon leur numéro. Masse × taille^N (*Mass Exponent*) | ×0,7, ×1,3, ×1,6, 2 |
| Le trou (un disque noir) | tailles en multiples du rayon d'une bille normale, quelle que soit l'échelle de la table : taille au départ, gain par bille de son groupe avalée, perte par bille adverse avalée, taille minimale / maximale ; changement de taille en ressort (*Grow Stiffness* : rapidité, *Grow Bounce* : rebond, plus bas = il dépasse plus) ; anneau de croissance qui saute à la nouvelle taille (durée, épaisseur, dépassement) ; durée de la chute d'une bille avalée | ×0,8, +0,2, −0,2, ×0,6 / ×3,2 ; 260, 0,3 ; 0,5 s, 2 cm, 15 % ; 0,35 s |
| Manger l'autre trou | un trou mange l'autre s'il est au moins *Eat Hole Ratio* fois plus gros et passe sur son centre ; ce qu'il gagne alors ; taille du trou mangé quand il revient, en part de sa taille d'avant (*Eaten Size Factor*) | ×1,2, +0,5, ×0,6 |
| Glisse | ralentissement du trou (m/s², une bille qui roule ralentit d'environ 0,5) | 0,6 |
| Juice | « gloups » du trou (*Gulp Amount* : de combien il gonfle, *Gulp Time* ; le disque noir bombe *Disc Bump* fois plus que son contour), couleur d'un bon morceau (*Good Bite Color*, blanc : une de ses billes, un objet, la 8 au bon moment, l'autre trou) et d'un mauvais (*Bad Bite Color*, rouge : bille adverse, 8 trop tôt) pour le faisceau, le flash du cercle et la lumière, épaississement du cercle (*Ring Pulse*), onde de choc aux couleurs du joueur (*Shockwave*, portée en rayons du trou, durée, épaisseur), faisceau de lumière qui jaillit du trou (*Beam* ; *Beam Prefab* : un effet à toi à la place du faisceau généré, créé au centre du trou à *Beam Prefab Scale* diamètres, qui le suit et prend la couleur de la bouchée ; sinon : hauteur en mètres, largeur en rayons du trou, durée, opacité, intensité et portée de la lumière), secousse de l'écran du joueur (force, durée ; à partir de *Shake Min Size*, entière à *Shake Full Size*, en rayons de bille), gros morceau à partir de *Big Bite Size* : flash de l'écran à la couleur du joueur et arrêt sur image (*Hitstop Time*) ; rotation d'un objet mangé (°/s). Seul au clavier, l'écran unique joue tout pour les deux trous. Son, vibration, texte : moments Feel *Hungry Hole Swallow* et *Hungry Hole Ate Hole* | +100 %, 0,45 s, ×1,5, blanc / rouge, ×4, oui ×3 0,4 s 1,5 cm, oui 4 m ×0,9 0,3 s 75 % lumière 0 / 0,8 m, 0,12 / 0,25 s, 0,6 / 2, 1,2 : 25 % / 70 ms, 540 |
| Cercle du joueur | couleur J1, couleur J2, épaisseur (0 = pas de cercle) | bleu, rouge, 6 mm |
| Objets à manger | *Props* : la liste des objets possibles (prefab, *Size* = largeur en rayons de bille, *Weight* = chance relative) ; nombre posé au début ; un objet mangé réapparaît ailleurs au hasard après *Prop Respawn Delay* (s), en surgissant du tapis (*Prop Pop Time*) ; hauteur maximale (rayons de bille) ; gain du trou par objet mangé (taille × facteur) ; espace libre autour d'un objet posé | 17 objets du pack Forest Bar (fruits et légumes, verres, bouteille, billets, pot de fleurs, cône, carton, caisse, tonneau, plante, 3 arbres), 10, 2 à 6 s, 0,3 s, 8, 0,15, 1,5 |
| Diagnostic | *Debug Logs* : une ligne `[HungryHole]` par bille avalée, objet mangé, poche, victoire | oui |

Une bille n'est avalée que si elle est plus petite que le trou. Avec les valeurs par défaut, le trou avale les plus petites billes (×0,7) dès le départ ; il faut 3 billes à soi pour les plus grosses (×1,3) et 5 pour la 8 (×1,6). Les objets sont mis à l'échelle à leur apparition d'après leur vraie taille : un prefab immense arrive quand même à la taille demandée. Leurs colliders sont remplacés par une capsule : les billes rebondissent dessus, et un trou trop petit aussi. Les règles de Speed Pool (couleurs des blanches, délai après une blanche rentrée…) viennent de `SpeedPoolSettings`.

### SnakePoolSettings — file de billes (mode Snake Pool)

Les billes ramassées suivent la blanche en file, la blanche adverse la brise (*Lancer sans le menu* : *Mode* = *SnakePool*). Créé par *Ensure Config Assets Exist*. Le reste (couleur des blanches, délai après une blanche rentrée) vient de `SpeedPoolSettings`.

| Groupe | Champs | Défauts |
|---|---|---|
| File | espace entre deux billes, en rayons de bille (*Link Gap*) ; temps pour qu'une bille ramassée rejoigne sa place (*Join Time*) ; hauteur du saut d'une bille qui ressort d'une poche (*Pocket Hop*, en rayons) ; la blanche garde sa vitesse en ramassant (*Keep Speed On Collect*, décoché : elle rebondit sur la bille) | 0,15, 0,25 s, 3, oui |
| Briser | vitesse minimale de la blanche adverse pour briser la file (*Min Break Speed*, m/s : une file qui balaie une blanche arrêtée ne se brise pas) ; part de la vitesse de la blanche donnée à la bille touchée (*Break Kick*) ; part de leur élan que gardent les billes lâchées (*Released Speed*) | 0,15, 0,7, 1 |
| Finale | *Finale Speed* : vitesse (m/s) à laquelle la file s'engouffre dans la poche derrière la blanche, une fois la 8 au bout (plus lent = plus de temps pour que l'adversaire la brise) | 1,2 |
| Blanche rentrée | *Scratch Drops Chain* : sans la 8 au bout, toute la file tombe (décoché : elle attend et repart avec la blanche) | oui |
| Diagnostic | *Debug Logs* : une ligne `[SnakePool]` par bille ramassée, file brisée, bille sortie de poche, victoire | oui |

### PowerWashPoolSettings — crasse du tapis (mode Power Wash Pool)

Le tapis est crasseux, les billes de chaque joueur le lavent en roulant (*Lancer sans le menu* : *Mode* = *PowerWashPool*). Créé par *Ensure Config Assets Exist*. Le reste (couleur des blanches, qui teinte aussi le tapis lavé, délai après une blanche rentrée) vient de `SpeedPoolSettings`.

| Groupe | Champs | Défauts |
|---|---|---|
| Partie | *Match Duration* : durée en secondes ; à la fin, le plus de tapis lavé gagne, et à égalité la partie continue jusqu'à ce que l'un passe devant | 180 |
| Crasse | nombre de cases sur la longueur du tapis (*Resolution*, la largeur suit : plus = bords plus nets) ; *Sharp Edges* : bords francs entre le sale et le propre (décoché : adoucis, plus flous) ; couleur (l'alpha règle à quel point elle cache le tapis) ; variation de teinte (*Dirt Noise*) et taille des taches (*Dirt Noise Scale*, m) ; opacité de la teinte du joueur sur le tapis lavé (*Washed Tint*) ; marge sans crasse autour des poches (*Pocket Clearance*, m, ces cases ne comptent pas) | 512, oui, brun 92 %, 0,35, 0,12, 0,22, 0,02 |
| Lavage | largeur de la trace d'une bille lente et d'une bille rapide (*Slow / Fast Trail Width*, en rayons de bille), vitesse de la trace la plus large (*Full Speed*, m/s) ; *Steal Washed* : repasser sur le tapis lavé par l'autre le reprend | 0,8, 1,6, 3, oui |
| Éclaboussure de la poche | Une de ses billes rentrée éclabousse le tapis pour son propriétaire, tournée vers le milieu du tapis (le tapis lavé de l'autre compris) : portée des gouttes les plus lointaines (*Pocket Splash Radius*, m) ; taille de la flaque au centre, en part de la portée (*Splash Puddle Share*) ; nombre de gouttes, chacune au bout d'une traînée (*Splash Drops*) ; ouverture de l'éventail des gouttes (*Splash Spread*, degrés, 360 = tout autour) ; temps que mettent les gouttes à retomber (*Splash Duration*, s ; la flaque est là dès que la bille rentre ; la partie n'est jugée qu'une fois toutes les éclaboussures retombées) | 0,4, 0,45, 14, 160°, 0,15 s |
| Juice de l'éclaboussure | peinture fraîche qui brille puis se fond dans la teinte du tapis lavé (*Fresh Paint Time*, s ; *Fresh Paint Whiten* : de combien elle est éclaircie) ; gerbe d'eau à la poche (*Splash Vfx*, rempli par *Ensure Config Assets Exist* avec *CFXR Water Splash (Smaller)* ; *Splash Vfx Scale* ; *Tint Splash Vfx* : à la couleur du joueur, décoché par défaut ; explosion d'eau : les parties de l'effet dont le nom commence par *Splash Vfx Boosted* (« Drops », « Foam ») ont *Splash Vfx Drop Count* fois plus de particules, jaillissent dans un cône de *Splash Vfx Spread* degrés, à *Splash Vfx Speed* fois leur vitesse ; le prefab du plugin n'est pas modifié, seulement sa copie à chaque éclaboussure) ; secousse (*Shake Magnitude*, *Shake Duration*) et flash (*Screen Flash Alpha*, *Screen Flash Duration*) de l'écran du joueur qui gagne l'éclaboussure, plus forts pour la 8 | 0,6 s, 0,35, CFXR, ×1,5, non, Drops + Foam ×10 40° ×0,8, 0,12 / 0,25 s, 20 % / 0,25 s |

Dans ce mode, l'effet de base d'une bille rentrée (halo et aura de la poche, secousse des deux écrans) est coupé : seule l'éclaboussure joue.
| La 8 | *Mud Trail Width* : largeur de la boue qu'elle laisse en roulant (0 = elle ne salit pas) ; *Eight Splash Radius* : portée de l'éclaboussure (même forme) gagnée par le dernier à l'avoir touchée quand elle rentre, ce qui arrête la partie | 1,2, 0,6 |
| Diagnostic | *Debug Logs* : lignes `[PowerWash]` (bille rentrée, la 8, fin de partie) | oui |

### CatchTheHoleSettings — trous vivants (mode Catch the Hole)

Les trous qui bougent, grandissent et rétrécissent sur le tapis, en mode **Catch the Hole** (menu Just for fun, ou *Lancer sans le menu* réglé sur *CatchTheHole*). Le menu debug (*Catch the Hole*) les lance aussi dans une autre partie.

| Groupe | Champs | Défauts |
|---|---|---|
| Apparence | *Plain Hole* : un disque noir parfait, comme Hungry Hole (décoché : le trou déchiré du météore) ; *Look* : l'asset du pouvoir Trou dont le trou reprend la taille de base, le temps de chute et, sans *Plain Hole*, la forme, le bord et le decal brûlé (rempli par *Ensure Config Assets Exist*) ; *Smoke* : garder sa fumée (trou de météore seulement) | oui, `TableHolePower`, non |
| Nombre de trous | au départ, maximum en même temps, un de plus toutes les N billes rentrées (0 = jamais), un de plus toutes les N secondes (0 = jamais), temps d'ouverture d'un nouveau trou | 1, 3, 5, 0, 0,8 s |
| Taille | rayon minimal et maximal (la forme a des éclats un peu plus longs), vitesse de variation (0 = taille fixe) | 5 cm, 12 cm, 0,12 |
| Croissance | chaque bille qui tombe dans un trou le fait grandir pour de bon (*Grow Per Ball*, en part de sa taille, jusqu'à *Max Growth*), en ressort (*Grow Stiffness*, *Grow Bounce*) avec un bump du disque (*Gulp Amount*, *Gulp Time*) ; une bille immobile qui réapparaît à côté ne compte pas | +15 %, +150 %, 260, 0,3, +60 %, 0,45 s |
| Déplacement | vitesse de croisière au début et au bout de l'accélération, accélération (plus faible = virages plus larges), distance de ralentissement avant une pause, durée maximale d'un trajet | 10 cm/s, 35 cm/s, 0,12 m/s², 15 cm, 8 s |
| Pauses | chance de s'arrêter à une cible, durée d'une pause (entre deux valeurs) | 0,35, 1 à 3 s |
| Écarts | bord du trou ↔ bandes et poches, bord à bord entre deux trous | 3 cm, 8 cm |
| Accélération | *Ramp* : aucune, déroulé (part des billes de couleur rentrées), minuteur (*Ramp Seconds*), les deux (le plus avancé) | les deux, 300 s |
| Diagnostic | *Debug Logs* : une ligne `[CatchTheHole]` par bille avalée (vitesse, tir en cours, décision) et par trou ouvert | oui |

### GameFlowSettings — niveaux et menu

La scène du menu (`MainMenu`) et la liste des **niveaux** proposés dans le menu, dans l'ordre d'affichage. Pour chaque niveau :

| Champ | Effet |
|---|---|
| Display Name | Nom sur la carte du menu et sur la carte postale du chargement |
| Scene Name | La scène du niveau : **la glisser** dans le champ depuis la fenêtre *Project*, ou cliquer sur le rond pour la choisir. Si elle n'est pas dans la liste des scènes du build, un avertissement s'affiche avec un bouton **Ajouter au build** |
| Picture | **Image du niveau** (une texture, importée en *Default* ou *Sprite*). Elle remplit la carte du menu et la carte postale ; vide = dessin de remplacement |
| Menu Line | Phrase courte sous le nom, sur la carte du menu |
| Twist | Le twist du décor, écrit sur la carte postale pendant le chargement |
| Coming Soon | Pas encore jouable : tampon « Bientôt », la carte ne lance rien |

Aussi : *Player Actions*, les actions des joueurs que l'écran des touches modifie dans le menu (avant que les joueurs existent).

**Ajouter un niveau jouable**

1. **Préparer la scène.** Elle contient :
   - une table avec `PoolMatchRules` ;
   - un `PlayerInputManager` dont le *Player Prefab* est `New`, avec `ScenePlayerSpawner` et ses points d'apparition.

   Le plus simple : dupliquer `BarSplitscreen` et changer le décor, ou passer par le Level Maker.
2. **Ajouter une ligne dans *Levels*** (le **+**) et la remplir ; pour *Scene Name*, glisser la scène dans le champ.
3. **Lancer *Tools > Pool > Sync Build Scenes (menu + levels)*.** La scène du menu passe en premier dans la liste des scènes du build, suivie des scènes des niveaux. Les niveaux *Coming Soon* et ceux sans scène sont ignorés ; une scène introuvable est signalée dans la console.
4. **Tester** : Play depuis la scène `MainMenu`, *Just for fun* ou *Multijoueur*, choisir le niveau puis le type de partie.

**L'image d'un niveau** : n'importe où dans `Assets/`, en PNG ou JPG, plutôt en paysage (environ 1280 × 720). La glisser dans *Picture* : elle est recadrée pour remplir la carte, sans être déformée.

**Un niveau « Bientôt »** n'a pas besoin de scène. Le jour où elle existe : remplir *Scene Name*, décocher *Coming Soon*, relancer *Sync Build Scenes*.

**Lancer Play directement dans un niveau** (sans passer par `MainMenu`) affiche le menu sur place ; choisir ce même niveau démarre la partie sans écran de chargement.

### LoadingScreenSettings — écran de chargement

Tout ce qu'affiche l'écran de chargement (variante « Le retour des billes ») :
- **Textes** : tampon (« EN ROUTE »), titre des astuces, « Tout est prêt ! », « J{0} : appuie sur {1} » (`{0}` = joueur, `{1}` = touche), « J{0} : prêt ! », « C'est parti ! », étiquette du haut (`{0}` = mode, `{1}` = joueurs) ;
- **Astuces** : la liste, une au hasard au départ puis à tour de rôle ;
- **Messages** pendant le chargement (« On cire les queues… ») : la liste, à tour de rôle ;
- **Déroulé** : attendre que chaque joueur appuie avant de démarrer (oui), durée minimale de l'écran (2,5 s), délais entre astuces et entre messages, durée de « C'est parti ! ».

Le nom, l'image et le twist du niveau viennent de GameFlowSettings.

### Menu Studio — les écrans des menus

Chaque écran est un fichier `Assets/Resources/Menus/<écran>.asset`, créé avec la disposition d'origine par *Ensure Config Assets Exist* et modifié dans **Tools > Pool > Menu Studio** (mode d'emploi : [Outils > Menu Studio](outils.html#menu-studio)) :

| Écran | Ce que c'est |
|---|---|
| Title | l'accueil (les six billes) |
| NewGame, Load | les emplacements de sauvegarde |
| Level | le choix du niveau (les cartes des niveaux viennent de GameFlowSettings) |
| Mode | le type de partie et la carte des règles |
| Multiplayer, Lobby | Local / En ligne, puis l'arrivée des joueurs |
| Settings | le cadre de *Réglages > Touches* (menu et pause) |
| Pause, PauseConfirm | la pause et la confirmation du retour au menu |
| Loading | l'écran de chargement |

### MenuSettings — couleurs, polices, messages

Les couleurs et les polices de tous les menus (et de l'écran de chargement), et les textes que le jeu écrit lui-même : messages (« Bientôt ! », « Salut J2 ! »…), formats (« {0} - prêt », « J{0} a mis la partie en pause »…), noms des actions de l'écran des touches, durées. Les champs des écrans (titres, billes, notes, pilules de la pause…) ne servent plus que de **valeurs de départ** quand un écran est créé ou réinitialisé : les écrans se modifient dans Menu Studio.

| Groupe | Ce qu'on règle |
|---|---|
| Couleurs | Tapis, bois, texte clair et son ombre, bille crème, encre, fond des cartes, accents jaune et rouge, cube de craie (carte des règles), J1, J2 |
| Polices | Texte courant et gros titres (vide = Titan One / Bowlby One) |
| Fond du menu | Une image à la place du bar dessiné (recadrée pour remplir l'écran), les lumières floues du bar (oui / non), un logo à la place du titre écrit et sa hauteur |
| Accueil | Les deux lignes du titre (« Untitled », « POOL GAME »), les six billes, « vise une bille, tire ! », les aides de touches en bas |
| Nouvelle partie / Charger | Titres, « Emplacement {0} », « Libre », « Vide », les notes du cube, les messages |
| Choix du niveau | « On joue où ? », le tampon et le message « Bientôt », les étiquettes « Just for fun » et « Multijoueur · Local » |
| Type de partie | « Quel type de partie ? », les quatre billes (Classique, Pouvoirs, 9-ball, 14.1) avec leurs règles, la ligne d'option (« Premier à {0} », pouvoirs ou non) |
| Multijoueur et salon | Titres, billes Local et En ligne, « Qui joue ce soir ? », « C'est parti », « J{0} appuie ! », « {0} - prêt », noms des appareils, « Salut J2 ! » |
| Pause | Titre, les trois pilules (Reprendre, Réglages, Menu principal), « J{0} a mis la partie en pause », la confirmation et ses deux pilules |
| Réglages > Touches | Titre, onglets (le premier est actif, les autres reçoivent « · bientôt »), en-têtes de colonnes, boutons, et le nom de chaque action |
| Durées | Petits messages (1,4 s), vitesse du tir sur une bille (×1), clignotement de « vise une bille » (0,7 s) |

**Une bille** (accueil, types de partie, pilules de la pause) se règle avec :
- son texte ;
- une petite ligne dessous (vide = aucune) ;
- le numéro écrit dessus ;
- sa couleur ;
- pleine ou rayée.

**Les noms des actions** de l'écran des touches se retrouvent par leur *Id* : `Move`, `Look`, `Pause`, ou le nom d'une action du joueur (`Interact`, `Attack`, `Punch`, `PunchRight`, `Kick`, `Block`, `Next`, `Sprint`, `Jump`). Une ligne absente de la liste garde son nom par défaut. Les touches elles-mêmes se changent dans le jeu (*Réglages > Touches*), pas ici.

- `{0}` est remplacé par le numéro (emplacement, joueur), le score ou l'appareil.
- Un format mal écrit (accolade en trop) s'affiche tel quel au lieu de casser le menu.

### Texte animé — Text FX Studio, balises, TextFxSettings

**Le plus simple : *Tools > Pool > Text FX Studio*** : choisir un texte du jeu, poser les effets avec des boutons, voir le résultat en direct et l'enregistrer (mode d'emploi : [Outils > Text FX Studio](outils.html#text-fx-studio)).

**Les balises** : tous les textes des réglages d'interface (MenuSettings, HudSettings, LoadingScreenSettings, noms des niveaux…) acceptent des **balises d'animation**, écrites comme du texte enrichi. Exemple : `<shake>FAUTE !</shake>`, `C'est <wave>parti</wave> !`, `<rainbow a=0.5>Bravo</rainbow>`.

| Balise | Effet, tant que le texte est affiché |
|---|---|
| `<wave>` | les lettres ondulent de haut en bas |
| `<bounce>` | les lettres rebondissent l'une après l'autre |
| `<shake>` | les lettres tremblent (colère, choc) |
| `<wiggle>` | chaque lettre gigote doucement de son côté |
| `<pulse>` | les lettres grossissent et rapetissent |
| `<swing>` | les lettres se balancent |
| `<rainbow>` | les couleurs défilent sur les lettres |

| Balise | Apparition des lettres à l'intérieur |
|---|---|
| `<pop>` | grossissent avec un petit rebond |
| `<drop>` | tombent d'en haut et rebondissent |
| `<fade>` | apparaissent en fondu |
| `<slide>` | glissent depuis la droite |
| `<grow>` | poussent depuis le bas |

- **Fermer la balise** (`</wave>`) arrête l'effet. Sans fermeture, il court jusqu'à la fin du texte.
- **Force et vitesse** dans la balise : `a=` (force) et `s=` (vitesse), 1 = normal. `<wave a=2 s=0.5>` : deux fois plus haut, deux fois plus lent.
- **Machine à écrire** (astuces et twist du chargement) : `<pause=0.5>` attend une demi-seconde, `<speed=2>` tape deux fois plus vite jusqu'à `</speed>`.
- **Les balises de texte d'Unity** restent utilisables et se combinent : `<b>`, `<color=#ff4d3d>`, `<size=120%>`…
- **Ce qui s'anime tout seul** :
  - **Lettre par lettre à l'arrivée** : les interjections du HUD, le nom du pouvoir ramassé, « J{0} gagne ! », les messages et « J{0} a mis la partie en pause », les règles des modes, les messages du chargement.
  - **En machine à écrire** : les astuces et le twist.

**TextFxSettings** (`Assets/Resources/TextFxSettings.asset`), lu à chaque image : les changements se voient tout de suite en jeu.

| Groupe | Ce qu'on règle |
|---|---|
| Effets permanents | Force et vitesse de chaque balise (`wave`, `shake`…) |
| Apparition | Apparition par défaut des lettres (pop), durée (0,35 s), décalage entre deux lettres (0,03 s) |
| Machine à écrire | Lettres par seconde (40), pause après `. ! ? …` (0,3 s), après `, ; :` (0,12 s) |
| Disparition | Durée (0,25 s), décalage entre deux lettres (0,015 s) |

### MovementSettings — déplacement de R&D (élan, saut, rebords)

En R&D (branche `feature/deplacements-combat`) : ne s'applique qu'au joueur qui a le composant *Local Player Locomotion*, c'est-à-dire la variante `New_PlayerVariant`. Lu à chaque image, donc réglable en jouant. Essai : *Tools > Pool > R&D > Créer l'arène de test*, puis Play.

| Groupe | Ce qu'on règle | Défauts |
|---|---|---|
| Marche et course | vitesse de marche et de sprint, prise de vitesse, freinage, contrôle en l'air | 4,5 et 7,5 m/s, 28 et 16 m/s², 0,35 |
| Saut | hauteur, gravité, marge après un bord, appui gardé avant d'atterrir | 1,1 m, −22 m/s², 0,12 s, 0,12 s |
| Escalade | hauteur min et max d'un rebord, distance de recherche, durée | 0,8 m, 2,1 m, 0,6 m, 0,6 s |
| Corps mou (mains libres) | force et maintien des bras, force du buste, vitesse de passage mou ↔ rigide, penché du buste et son lissage | 0,45, 0,15, 0,8, 6, 0,08 m, 8 |
| Vue | roulis en pas de côté, balancement et sa fréquence | 2,5°, 0,025 m, 1,9 pas/s |

Sur le composant *Local Player Locomotion* lui-même : les noms des actions (Move, Sprint, Jump) et *Trace Movement*, qui écrit dans la console les sauts, rebords trouvés ou refusés (avec la raison) et escalades.

### CombatSettings — combat de R&D (coups, encaissement, garde, endurance)

En R&D : ne s'applique qu'avec la case *Boxing Punches* cochée sur *Local Unarmed Melee*, c'est-à-dire sur la variante `New_PlayerVariant`.

**Un seul outil prépare tout** : *Tools > Pool > R&D > Préparer le joueur de R&D*. Il met sur la variante le déplacement, les poings de boxe et la jauge d'encaissement, puis crée ou reconstruit les calques d'animation. On peut le relancer sans risque. *Créer l'arène de test* l'appelle aussi. Ces valeurs remplacent celles du composant pour le poing. La garde et la portée du poing, ainsi que tout le coup de pied (spartiate compris), restent réglés sur le composant (voir [Combat](config.html#config:combat)). Lu à chaque image.

| Groupe | Ce qu'on règle | Défauts |
|---|---|---|
| Coups animés | garde et coups joués par des animations (case *Use Animations* ; décochée = coups procéduraux en IK), nom du calque d'animation, vitesse des animations, fondu entre deux, fenêtre où le coup peut toucher (début, fin, en part de l'animation), moment où l'autre main peut enchaîner, moment du retour en garde, inversion gauche/droite, correction de direction de chaque poing (rotation du buste, la console donne la valeur à mettre) | oui, « R&D Combat », ×1,3, 0,06 s, 0,2–0,55, 0,4, 0,85, non, 0° / 0° |
| Garde | temps pendant lequel les poings restent levés près du menton après le dernier coup (0 = pas de garde), temps pour la lever, pour la baisser | 1,5 s, 0,08 s, 0,35 s |
| Poing fermé et poignet | repli des doigts et du pouce, rotation du poignet pendant le coup (vertical en garde → paume vers le bas à l'impact ; négatif si ça tourne à l'envers), où finissent les poings sur le côté (1 = parallèles, plus petit = vers l'axe du corps) | 85°, 40°, 80°, 0,45 |
| Nervosité du poing | mise en garde avant de partir (premier coup, garde pas encore levée), durée de la frappe, bras tendu, retour en garde, forme (0 = direct, 1 = crochet large) | 0,05 s, 0,07 s, 0,03 s, 0,12 s, 0,2 |
| Le corps derrière le coup | rotation du buste, avancée de l'épaule, engagement du corps | 18°, 0,16 m, 0,14 m |
| Force | déséquilibre et force du coup | 2, 650 N |
| Ressenti dans la vue | roulis et avancée de la vue quand le poing part, leur durée ; secousse quand il touche | 1,6°, 0,035 m, 0,16 s ; 1 |
| Garde (Ctrl gauche / LT) | garde activée ; vitesse en garde ; demi-angle couvert devant ; part de jauge d'un poing / d'un pied bloqué ; part de la force qui passe ; coût d'un poing / d'un pied sur la jauge de garde ; délai et vitesse de remontée ; durée de la garde brisée ; coup de pied spartiate sur une garde (la brise seulement, ou passe à travers) ; contre-attaque : délai, vitesse, multiplicateur | oui ; ×0,6 ; 70° ; 0,1 / 0,5 ; 0,3 ; 0,25 / 0,45 ; 1 s, 0,5/s ; 1,5 s ; la brise ; 0,6 s, ×1,5, ×1,5 |
| Endurance de celui qui frappe | endurance activée ; coût d'un poing, d'un pied, supplément du spartiate ; délai et vitesse de remontée ; seuil de fatigue, vitesse et force d'un poing fatigué | oui ; 0,15, 0,3, +0,25 ; 0,6 s, 0,45/s ; 30 %, ×0,75, ×0,7 |
| Coups localisés | multiplicateur de jauge pour la tête, le corps (buste, bassin, bras), les jambes ; animations de coup reçu, leur fondu, inversion gauche/droite ; au K.O., poussée dans le sens du dernier coup et force des muscles (0 = tout mou) | ×1,5, ×1, ×0,7 ; oui, 0,1 s, non ; 3 m/s, 0,15 |
| Encaissement | jauge activée ; remplissage par coup de poing, par coup de pied ; temps de l'équilibre renforcé après un coup (titube sans tomber), résistance des muscles et écart toléré pendant ce temps ; seuil « sonné » ; vitesse, dérive et tangage de la vue d'un joueur sonné, animation « Stunned » ; délai et vitesse de descente de la jauge ; durée du K.O. ; protection au réveil | oui ; 0,14, 0,3 ; 0,7 s, 1, 0,6 m ; 60 % ; ×0,55, 0,25 m/s, 4°, oui ; 1,5 s, 0,2/s ; 3 s ; 2 s |

Pendant un coup et tant que la garde est levée, les bras restent rigides, au lieu de quitter progressivement le « corps mou » du déplacement : un bras encore mou suivait le poing en retard. La garde se baisse dès qu'on prend un objet ou la queue, qu'on vise ou qu'on tombe. Les doigts ne se replient que sur un personnage humanoïde.

**Coups animés** : la garde (`Humanoid@IdleCombat`), le poing gauche (`Humanoid@Jab1`) et le poing droit (`Humanoid@RightHand1`, pack PROTOFACTOR) sont joués sur un calque « R&D Combat » du contrôleur d'animation du joueur, limité au haut du corps (masque `Assets/Animations/Combat/RnD_UpperBody.mask` : buste, bras, doigts ; ni la tête ni les jambes). Ce calque se crée avec **Tools > Pool > R&D > Préparer le joueur de R&D** (à relancer pour le reconstruire). Son poids est 0 par défaut : le joueur normal et les mannequins, qui partagent le contrôleur, ne changent pas. Sans ce calque, un avertissement s'affiche dans la console et les coups procéduraux restent. En mode animé, les réglages *Garde* (durées), *Force* et *Ressenti dans la vue* servent toujours ; *Poing fermé et poignet*, *Nervosité du poing* et *Le corps derrière le coup* ne servent plus. Avec *Debug Logs*, la console donne pour chaque coup le moment où la main va le plus vite, pour placer la fenêtre de touche.

**Encaissement** : la jauge est le composant *Local Hit Gauge* de la cible. L'outil *Préparer le joueur de R&D* l'ajoute à la variante, et il s'ajoute seul au premier coup sur les autres corps (mannequins…). Sa case *Debug Logs* écrit dans la console « titube », « SONNÉ », « K.O. ! » et « réveil ». Seuls les coups d'un joueur qui a *Boxing Punches* comptent : les coups de queue et les objets lancés font tomber comme avant. Repère, poings au corps : 0,14 par poing, il faut 8 poings d'affilée pour un K.O. (5 pour être sonné). À la tête (×1,5), il en faut 5.

**Animations d'encaissement** : *Tools > Pool > R&D > Préparer le joueur de R&D* crée aussi trois calques, au poids 0 par défaut :
- « R&D Sonné (haut) » et « R&D Sonné (jambes) » : `Humanoid@Stunned`. Les jambes ne le jouent qu'à l'arrêt.
- « R&D Coup reçu » : `Humanoid@GetHit{Front,Back,Left,Right}{Light,Heavy}Combat`, choisi selon le côté d'où vient le coup. La version lourde sert pour la tête ou quelqu'un de sonné.

Les masques sont dans `Assets/Animations/Combat/`. Ordre des calques : sonné, coups, coup reçu, pour qu'un coup reçu passe devant un coup donné.

**Chute au K.O.**, selon la zone du dernier coup :
- tête : il part en arrière ;
- corps : il se plie ;
- jambes : elles se dérobent.

### GameFeelSettings — Feel aux moments de la partie

Associe un **moment de la partie** à une **séquence Feel** (un prefab avec un *MMF Player*) :
- **moments** : bille rentrée (une des siennes), **bille adverse rentrée** (*Opponent Ball Potted*, 8-ball), blanche rentrée, faute, tir, tir plein, pouvoir ramassé, BOUM, début de tour, victoire, **coup reçu** (poing ou pied qui touche un joueur, sur l'écran de celui qui le reçoit ; l'intensité est multipliée par la force du coup), **K.O.** (jauge d'encaissement pleine, sur l'écran de celui qui tombe), **garde brisée** (sur l'écran de celui dont la garde casse), **impact de météore** (pouvoir Trou, pour les deux joueurs ; à ajouter en fin de liste, *Table Hole Impact*), **pouvoir lancé** (*Power Activated*, sur l'écran du lanceur), **attaque reçue** (*Power Received*, sur l'écran de la cible, au moment où l'attaque est lancée), **pouvoir refusé** (*Power Refused*, touche pressée au mauvais moment), **le trou avale** (*Hungry Hole Swallow*, Hungry Hole : une bille ou un objet ; l'intensité suit la taille avalée, sur l'écran du propriétaire du trou), **un trou mange l'autre** (*Hungry Hole Ate Hole*, sur l'écran du mangeur), **le trou grandit** (*Hungry Hole Grow*, intensité selon le gain) ; ces trois moments sont à ajouter en fin de liste ; Snake Pool : **une bille rejoint la file** (*Snake Grow*, sur l'écran du propriétaire de la file), **une file est brisée** (*Snake Cut*, sur l'écran de celui qui la brise ; pleine intensité à partir de 4 billes lâchées) ; Power Wash Pool : **jet de lavage** (*Power Wash Splash*, une bille rentrée ou la 8 lave autour de la poche, sur l'écran de celui qui gagne le jet ; intensité selon la portée, 1 pour 0,4 m) ;
- **pour chaque ligne** : le moment, la séquence, l'intensité (1 = normale). Plusieurs lignes pour le même moment jouent toutes ;
- **aussi** : un interrupteur général et le seuil du « tir plein » (0,95).

**Créer une séquence** : depuis Text FX Studio (*Créer la séquence Feel*), qui fait le prefab dans `Assets/Feel/Sequences/` avec le texte et l'ajoute à GameFeelSettings ; ou à la main (un objet avec *MMF Player*, glissé dans la liste). On complète ensuite la séquence dans l'Inspector de Feel avec n'importe quel feedback : son, vibration de la manette, arrêt sur image, particules, lumière…

**Deux feedbacks à nous**, dans la catégorie *Untitled Pool* du bouton *Add new feedback* :

| Feedback | Effet |
|---|---|
| Texte du HUD (TextFx) | un gros mot sur le HUD, avec ses balises, sur l'écran du joueur concerné, de celui dont c'est le tour, de J1, de J2 ou des deux ; couleur, rayons derrière |
| Secousse et flash écran | secousse de la caméra et flash de couleur, par notre propre système de caméra (les secousses de caméra de Feel entreraient en conflit avec les caméras du jeu) ; l'intensité de la séquence multiplie la force |

Les effets déjà codés (secousses de faute, interjections du HUD…) restent en place : une séquence Feel s'y ajoute. Pour en remplacer un, vider le texte correspondant dans HudSettings.

### HudSettings — UI en jeu

| Groupe | Ce qu'on règle |
|---|---|
| Polices | Police des textes et police des titres (vide = Titan One / Bowlby One) |
| Taille | Taille globale du HUD (1 = calé pour une moitié de 1920 × 1080), taille du bloc des pouvoirs en haut à droite (*Power Slot Scale*, 1,35 : anneaux, icônes, nom, touche, second pouvoir), taille des interjections, de « À toi ! », des bandeaux |
| Couleurs | J1, J2, encre des contours, fond des cartes, mise en avant (jaune), danger (rouge), « BOUM ! », voile du joueur qui attend, jauge (début, milieu, fin) |
| Textes : tour et plaque | « À toi ! », « Tour de J{0} », « J{0} », groupe à décider, « Prochaine », « Prolongation ! » (Power Wash Pool, à la place du chrono), « Puissance », touche du pouvoir |
| Textes : bandeaux d'aide | Main libre, poche visée / à viser pour la 8, trop loin, poche annoncée / à annoncer, bille destructrice armée, choix du trou d'un pouvoir (trou visé / aucun), aimant prêt |
| Texte de plaque « Groupe identique » | *Groups Disguised Text* : remplace les billes restantes pendant l'effet |
| Bandeau du choix sur la table | *Pick Table Banner Format* (pouvoir Trou) |
| Interjections | Liste tirée au hasard quand le joueur rentre une de ses billes (*Pot Shouts*), une autre quand il rentre une bille de l'adversaire (*Opponent Ball Pot Shouts* ; liste vide = rien), blanche rentrée, faute, main libre, tir plein, pouvoir lancé (« Envoyé ! » pour une Attaque, « Activé ! » pour un Effet ou une Défense), « BOUM ! » ; interrupteur des interjections de bille rentrée ; seuil du tir plein (0,95) |
| Deux pouvoirs en stock | Texte sous le pouvoir quand il y en a deux (« Molette · ◀ ▶ »), badge de niveau (« N{0} »), interjection du passage au niveau supérieur (« NIVEAU {0} ! »), et du pouvoir ramassé mais perdu (« Déjà plein ! », vide = rien) |
| Carte sponsor | Afficher ou non, en-tête, suffixe du nom (™), ligne « Pouvoir {0} · Q / B pour le lancer », noms des catégories |
| Pouvoir lancé : carte chez l'adversaire | Quand un joueur lance un pouvoir, une carte arrive sur l'écran de l'autre joueur (par la droite). Afficher ou non, aussi pour les Effets et Défenses ou non, en-têtes (« J{0} T'ENVOIE », « J{0} ACTIVE »), ligne sous le nom (« Tout de suite ! », « Au début de ton tour… », « Pour son prochain coup »), durée (2,6 s) |
| Pouvoir refusé | Texte sous la pastille du pouvoir quand la touche est pressée au mauvais moment : rien en stock (vide = la pastille tremble sans texte), pendant son tour seulement, pendant le tour adverse seulement, billes qui roulent, placement de la blanche ou annonce de la 8 en cours ; durée (1,4 s) |
| Fin de partie | « J{0} gagne ! », « Revanche », aide des touches, confettis |
| Durées et mouvements | Interjections (1,3 s), rayons (1,1 s), carte sponsor (3,1 s), temps minimal à l'écran avant qu'une interjection ou une carte soit remplacée par la suivante (*Min Shout Time* 0,6 s, *Min Card Time* 1,2 s), délai faute → main libre (1,3 s), secousse (durée, force), balancement de « À toi ! » (hauteur, période), voile du joueur qui attend |

- `{0}` est remplacé par le numéro du joueur, la poche ou la catégorie du pouvoir.
- **Vider un texte** désactive l'interjection ou le bandeau correspondant.
- Un format mal écrit (accolade en trop) s'affiche tel quel au lieu de casser le HUD.
- **Un message à la fois** : sur un même écran, une interjection, une carte sponsor ou une carte « pouvoir reçu » qui arrive pendant qu'une autre du même genre est affichée attend son tour (au plus deux en attente, les plus anciennes sont abandonnées). En haut à droite, le bloc des pouvoirs, le message de refus et la carte « pouvoir reçu » s'empilent.

## Pouvoirs (un fichier par pouvoir)

Dans `Assets/Powers`. Champs communs, qui décident **quand le pouvoir peut être lancé** (touche Q / B) :

- **Power Name** : nom affiché.
- **Hud Icon** : l'icône du pouvoir dans le HUD (blanche sur fond transparent), assortie à son jeton, générée par *Tools > Pool > Générer les jetons de pouvoir* dans `Assets/Textures/PowerHudIcons`. Vide = l'icône de sa catégorie. Un vrai dessin peut la remplacer.
- **Token Prefab** : le jeton posé sur la table pour ce pouvoir (une forme 3D qui le représente, sur un anneau à la couleur de sa catégorie), fait par *Tools > Pool > Générer les jetons de pouvoir*. Vide = le prefab de la catégorie. Le jeton tourne et flotte grâce à *PowerTokenFloat*, réglable sur le prefab : vitesse de rotation (70°/s), inclinaison (8° sur les jetons générés), amplitude (1,2 cm) et durée (1,8 s) du flottement.
- **Requires Own Turn** : ne peut être lancé que pendant son propre tour (pouvoirs d'Effet).
- **Allow During Own Turn** (coché par défaut) : pour un pouvoir utilisable hors de son tour (*Requires Own Turn* décoché). Coché, lancé pendant son propre tour, il attend le début du tour adverse. Décoché, il est refusé pendant son tour et ne part que pendant le tour de l'adversaire.
- **Allow While Balls Moving** (coché par défaut) : peut être lancé pendant que les billes roulent. Décoché, il est refusé jusqu'à l'arrêt des billes.

En solo (une seule manette ou un seul clavier pour les deux côtés), les deux règles de tour ne s'appliquent pas, sinon un pouvoir refusé pendant son tour ne pourrait jamais partir. Un appui refusé fait trembler la pastille du pouvoir avec la raison (textes dans HudSettings, *Pouvoir refusé*).

| Pouvoir | Fichier | Réglages propres |
|---|---|---|
| Tir boosté | `BoostedShotPower` | Shot Power Multiplier (1,6) : puissance du prochain tir |
| Vision brouillée | `VisionImpairPower` | Sensitivity Multiplier (0,3) : sensibilité de la visée de l'adversaire |
| Commandes inversées | `InvertedControlsPower` | Sensitivity Multiplier (1,8) |
| Poche fermée | `ClosePocketPower` | — (poche choisie par le joueur ; le visuel du bouchon est dans PoolPotEffectSettings) |
| Trou | `TableHolePower` | **Trous** : Hole Radius (7,5 cm), Level 1 Holes (1), Level 2 Holes (3), répartis au hasard : Level 2 Spread (0,2 m : rayon du cercle où tombent la plupart des trous), Level 2 Spread Jitter (0,3 : la distance varie de 70 % à 130 % du rayon), Level 2 Inside Chance (0,3 : chance qu'un trou tombe à l'intérieur du cercle), Level 2 Center Hole (non : un trou toujours sur le point visé), Level 2 Min Gap (2 cm entre deux trous). **Durée** : Duration (*Opponent Turn* = tour de l'adversaire, *Next Shot*, *Whole Match*, *Turns*), Turns (2), comptée à partir de l'ouverture du trou. Lancé pendant son propre tour, le trou ne s'ouvre qu'au début du tour de l'adversaire (l'endroit est marqué en attendant, aux couleurs du bord : *Rim Color / Width*). **Météore** : Meteor Prefab (*CFXR Fire*), Meteor Scale (0,25), Fall Height (2,5 m), Fall Side Offset (1,5 m), Fall Time (0,55 s), Level 2 Stagger (0,18 s), Impact Prefab (*CFXR3 Fire Explosion B*), Impact Scale (0,25), Impact Lifetime (3 s). **Le trou** : une forme d'explosion tirée au hasard à chaque impact — Shape Roughness (0,35 : contour plus ou moins bosselé), Shape Spikes (5 éclats plus longs), Shape Spike Length (0,5 × le rayon) ; les billes tombent selon cette forme. Edge Softness (0,45 : bord fondu vers le brûlé, 0 = net), Ember Amount (0,7 : braises qui suivent le bord, plus ou moins chaudes au hasard), Ember Color. Hole Prefab (facultatif, à la place de la forme générée, tourné au hasard), Hole Prefab Scale, Hole Color, **Scorch Decal** (contour brûlé projeté sur le tapis : matériau *TableHoleScorch* généré par *Ensure Config Assets Exist* ; demande la fonction *Decal* dans le renderer URP, voir plus bas), Scorch Scale (×2,2 le diamètre), Scorch Depth (2 cm), Scorch Opacity (1), Scorch Stretch (0,25 : étirement au hasard ; le decal est aussi tourné au hasard), Rim Color / Width (cercle de bord sans decal, et cercles qui marquent l'endroit visé en attendant), Smoke Prefab (*CFXR Smoke Source 3D*), Smoke Scale (0,3), Smoke Rate (1 : débit de la fumée, multiplie celui du prefab), Smoke Duration (0 : fume tant que le trou est ouvert, sinon s'éteint au bout de ce temps), Sink Time (0,35 s), Close Time (0,3 s). **Impact** : Blast Speed (0,6 m/s) / Radius (0,3 m), Shake Magnitude / Duration, Flash Color / Alpha / Duration, Rumble Low / High / Duration (vibration des manettes). *Requires Own Turn* décoché à la création (attaque) |
| Groupe identique | `IdenticalGroupsPower` | Look (*Colorless* : aspect uni · *Like Ball* : aspect d'une bille), Look Ball Number (8 : bille copiée, 1 à 15), Colorless Material (facultatif), Colorless Color (gris clair, si pas de matériau). *Requires Own Turn* décoché à la création (attaque) |
| Bille destructrice | `BallBlastPower` | Explosion Prefab (*CFXR2 WW Explosion*), Explosion Scale (0,15), Explosion Lifetime (4 s), Spare Eight Ball (oui), Blast Speed (0,5 m/s, 0 = pas de souffle), Blast Radius (0,3 m) |
| Aimant | `MagnetPower` | Capture Radius (0,4 m : distance du trou où la bille est captée), Min Capture Speed (0,05 m/s : une bille immobile n'est pas captée), Pull Acceleration (1,5 m/s² : force d'attraction), Steering (4/s : correction de trajectoire, 0 = aucune), Max Pull Duration (4 s : lâche une bille coincée) ; *VFX dans la poche* : Pocket Vfx Prefab (*CFXR Electrified 3*, vide = aucun), Pocket Vfx Scale (0,2), Pocket Vfx Height (0,02 m), Pocket Vfx Fade Out (1,5 s) |

**Decal du Trou** : le contour brûlé est un decal URP. Il ne s'affiche que si le renderer utilisé a la fonction *Decal* : sélectionner `Assets/Settings/PC_Renderer` (et `Mobile_Renderer`), *Add Renderer Feature > Decal*. La technique *Automatic* convient ; elle a un petit coût de rendu, payé deux fois en écran partagé. Pour un autre aspect, mettre son propre matériau de decal dans *Scorch Decal*. La texture générée (`Assets/Textures/Decals/TableHoleScorch.png`) peut être régénérée par l'outil quand sa version change.

**Pouvoirs qui visent un trou** (Poche fermée, Aimant) : le choix du trou se règle sur *Local Pool Power Controller* (voir *Composants du joueur*) et le cercle dans PoolPotEffectSettings. L'asset de l'aimant est créé par *Ensure Config Assets Exist*, comme celui de la bille destructrice.

**Ajouter un pouvoir existant** : clic droit dans `Assets/Powers` > *Create > Pool > Powers > …*, renseigner les champs, puis l'ajouter à *Available Powers* de PoolPowerSpawnSettings.

## Composants du joueur (prefab)

Sur `Assets/Prefabs/PlayerLocal/New.prefab`. Chaque champ a une infobulle dans l'Inspector.

| Composant | Rôle | Principaux réglages |
|---|---|---|
| **Local Fps Player Controller** | Déplacement et regard | Vitesse (5), gravité (−20), sensibilité souris (0,1) et manette (150), angles de regard min / max (±85°) |
| **Local Pool Aim Controller** | Visée et tir | Voir le détail ci-dessous |
| **Local Player Ragdoll Controller** | Chute et relevé | Délai de relevé, distance et hauteur de la vue pendant la chute, durées de recul et de retour de la caméra, ragdoll perdu (traversée de mur, sous le sol), diagnostics |
| **Local Cue Holder** | Ramasser et tenir la queue | Portée et angle pour ramasser, prises des mains, posture (accroupi, penché), durées, coudes |
| **Local Player Hand Controller** | Objets tenus et lancer | Portée de ramassage (2 m), puissance de lancer min / max (3 / 12), vitesse de charge |
| **Local Object Hands** | Porter les objets (hors queue) | Léger ou lourd, poses, corps, durées |
| **Local Cue Melee** | Coups de queue | Voir [Combat](config.html#config:combat) |
| **Local Unarmed Melee** | Poings et pieds | Voir [Combat](config.html#config:combat) |
| **Local Pool Power Controller** | Lancer son pouvoir, choisir un trou | Actions utilisées (*Next* : Q / B pour lancer ou annuler, *Interact* : E / Y pour valider le trou) |
| **Local Pocket Pointer** (ajouté tout seul) | Choisir une poche en la visant (pouvoirs qui visent un trou, annonce de la 8) ou un point de la table (pouvoir Trou, cercle d'aperçu aux couleurs de *Pocket pick: ring* de PoolPotEffectSettings) | *Max Point Angle* (8° : écart toléré entre le pointeur et une poche), *Pointer Size* (8 px : pointeur blanc) |
| **Local Character Animation Controller** | Paramètres de l'Animator | Noms des paramètres, échelles de marche, inclinaison en tournant |
| **Cue Charge Slide** (sur la queue) | Glissement de la queue dans les mains | Recul à la charge (0,15 m), longueur qui dépasse derrière la main (0,1 m), détection automatique de l'axe |

### Visée et tir (Local Pool Aim Controller)

| Groupe de l'Inspector | Ce qu'on règle |
|---|---|
| Detection | Distance à la blanche pour entrer en visée (3,5 m) |
| Aim camera | Distance et hauteur de la caméra autour de la bille, vitesse de rotation souris / manette |
| Shot | Puissance min / max, vitesse de charge |
| Spin | Vitesse de déplacement et étendue du point de frappe |
| Trajectory preview | Lignes de trajectoire (longueurs, couleurs) |
| Held cue and body while aiming | Avancée des mains (*Max Hand Stretch* 0,75 m), penché du buste (*Max Body Lean* 0,45 m, *Body Lean Bend*), décalage et inclinaison de la queue, écart pointe-bille (*Aim Tip Gap*), diagnostics *Trace Aim Pose* / *Trace Aim Input* |
| Stretched shot | Coup allongé : portée en plus (*Max Stretch Reach* 0,6 m), montée du bassin, flexion du buste, jambe levée, tremblement de la visée (1,5°), pieds tenus au sol ; bassin retenu au bord du tapis et au-dessus de la table, compensation par le buste, lissage |
| Out of reach | Main avant qui lâche la queue hors de portée, marge avant de lâcher (0,1 m), durée (0,25 s) |
| Body positioning while aiming | Distance de la table au corps (*Table Clearance Margin* 0,5 m), orientation du corps |
| Ball-in-hand placement | Hauteur de la vue du dessus, vitesse de déplacement de la bille, *Placement Clear Above* (rien n'est dessiné au-dessus de la table) |
| Input | Noms des actions utilisées |

## Combat

Paramètres des composants de combat, dans l'ordre de l'Inspector. Un composant déjà placé garde ses valeurs quand les valeurs par défaut changent (*clic droit > Reset* pour les reprendre). Unpin = déséquilibre PuppetMaster (repère : 5 fait généralement tomber).

### Coup de queue — Local Cue Melee

#### Références

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Action Map Name | Action map du joueur dans l'asset d'input | Player |
| Attack Action Name | Action qui charge (maintenir) et déclenche (relâcher) le coup | Attack |
| View Transform | Caméra du joueur, dont le coup suit le repère (vide = trouvée seule) | — |
| Full Body IK | IK du personnage (vide = trouvé seul ; sans lui, le buste ne suit pas) | — |

#### Direction (rotation pendant la charge)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Turn Threshold | Rotation à faire pendant la charge pour choisir le coup : droite = balayage vers la gauche, gauche = vers la droite, haut = vertical ; en dessous = estoc | 12° |
| Thrust Stance Delay | Temps sans rotation avant que la queue passe en position de tir (estoc) | 0,2 s |
| Pose Blend Time | Fondu quand la pose change (position de tir → balayage) | 0,1 s |

#### Retour de la vue à la frappe

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Return View On Strike | Retour de la vue vers la cible à la frappe : 1 = complet, 0 = la vue ne bouge pas | 1 |
| Max View Return | Rotation maximale imposée à la vue | 120° |
| Assist Angle | Demi-angle du cône dans lequel un adversaire attire la vue (0 = pas d'assistance) | 40° |
| Assist Range | Distance maximale de l'adversaire visé | 3 m |

#### Charge et arcs

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Charge Time | Temps de maintien pour la charge maximale | 0,8 s |
| Min Windup | Part de l'armé déjà là sans charge | 0,45 |
| Shoulder Pivot | Centre de rotation de la queue : 1 = épaules (grands gestes), 0 = entre les mains | 1 |
| Sweep Windup | Balayages : angle d'armé à pleine charge | 100° |
| Sweep Follow Through | Balayages : angle atteint de l'autre côté | 110° |
| Overhead Windup | Vertical : angle d'armé vers le haut | 95° |
| Overhead Follow Through | Vertical : angle atteint vers le bas | 70° |

#### Estoc (position de tir)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Thrust Pull Back | Recul de la queue le long de son axe à pleine charge | 0,25 m |
| Thrust Reach | Avancée de la queue à la frappe (portée de l'estoc) | 0,5 m |
| Stance Side | Position de tir : décalage des mains à droite des épaules | 0,12 m |
| Stance Height | Position de tir : hauteur des mains par rapport aux épaules | −0,25 m |
| Stance Forward | Position de tir : distance des mains devant les épaules | 0,15 m |
| Stance Blend Time | Temps pour passer en position de tir | 0,15 s |

#### Corps

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Sweep Torso Twist | Part de l'angle du balayage reprise par le buste | 0,4 |
| Overhead Torso Bend | Part de l'angle du vertical reprise par le buste (il se plie) | 0,25 |
| Windup Lean Back | Recul du corps à pleine charge | 0,1 m |
| Strike Lean | Engagement du corps dans le coup | 0,18 m |

#### Rythme

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Strike Time | Durée de la frappe | 0,13 s |
| Follow Through Hold | Pause en fin de geste | 0,08 s |
| Recover Time | Retour à la pose de port | 0,3 s |
| Cooldown | Délai minimal entre deux coups | 0,25 s |

#### Touche et force

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Muscle Layers | Couches des muscles des ragdolls (vide = Ragdoll) | Ragdoll |
| Hit Radius | Épaisseur de la zone de touche le long de la queue | 0,2 m |
| Sub Steps | Tests de touche par image (évite de traverser un bras) | 4 |
| Min Unpin / Max Unpin | Déséquilibre sans charge / à pleine charge | 1 / 8 |
| Min Force / Max Force | Force sans charge / à pleine charge | 400 / 2500 N |
| Guaranteed Knockdown At | Charge à partir de laquelle la cible tombe à coup sûr | 0,9 |
| Upward Bias | Part de la force vers le haut | 0,15 |
| Hitstop Duration | Micro-ralenti à l'impact, pour les deux joueurs (0 = désactivé) | 0,06 s |
| Hitstop Time Scale | Vitesse du temps pendant ce ralenti | 0,05 |
| Show Charge Indicator | Jauge et flèche de direction provisoires | oui |
| Debug Logs | Chaque coup et chaque touche dans la console | oui |

### Poing et pied — Local Unarmed Melee

#### Références

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Action Map Name | Action map du joueur | Player |
| Punch Action Name | Action du coup de poing (clic gauche / LB) ; poing gauche en R&D | Punch |
| Kick Action Name | Action du coup de pied (F / RT) | Kick |
| View Transform | Caméra du joueur (vide = trouvée seule) | — |
| Full Body IK | IK du personnage, obligatoire (vide = trouvé seul) | — |
| Fps Camera | Caméra Cinemachine FPS pour le zoom du spartiate (vide = celle dont le nom contient « FPS ») | — |

#### R&D Boxe

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Boxing Punches | Un bouton par main (Punch = gauche, Punch Right = droite). Au premier coup, les deux poings fermés montent en garde près du menton et y restent un moment. Chaque poing part de la garde à l'appui, presque droit vers l'axe du corps, plus sec, en tournant le poignet, avec le corps derrière, puis y revient ; la vue l'accompagne. Durées, forme, force et ressenti : **CombatSettings** (page Configuration). Coché seulement sur la variante de R&D ; décoché = poings en crochet qui alternent sur un seul bouton, comme avant | non |
| Punch Right Action Name | Action du poing droit (clic droit / RB) | PunchRight |

Avec cette case, ne servent plus pour le poing : *Windup Time*, *Jab Strike Time*, *Hold Time*, *Recover Time*, *Jab Torso Twist*, *Shoulder Reach*, *Punch Lean*, *Fist Spread*, *Jab Unpin* et *Jab Force*. Le point de départ du coup (*Start Forward / Height / Outward*) sert aussi de position de garde. Leurs équivalents sont dans CombatSettings. Le coup de pied garde ses propres réglages.

**Coups animés** (CombatSettings > *Use Animations*, après *Tools > Pool > R&D > Préparer le joueur de R&D*) : la garde et les deux poings sont des animations de boxe (pack PROTOFACTOR) au lieu d'être construits en IK. Le coup touche le long de la vraie main, pendant la fenêtre *Hit Window Start / End* de l'animation. L'autre main enchaîne à partir de *Chain From*, et le retour en garde commence à *Return At*. Si le poing gauche joue le coup du droit, cocher *Swap Hands*.

**Frapper là où l'on regarde** (CombatSettings, sous les coups animés). Les animations frappent toujours à la hauteur où elles ont été enregistrées. Le poing animé est donc incliné vers le regard, vers le haut ou vers le bas. Comme le coup touche le long de la vraie main, regarder la tête vise la tête et regarder bas vise les jambes. Pour la gauche et la droite, il n'y a rien à faire : le corps tourne déjà avec la vue.
- *Punch Pitch Follow* (0,8) : part de l'inclinaison de la vue que suit le poing. 0 = hauteur de l'animation, 1 = suit tout le regard.
- *Punch Pitch Spine Share* (0,5) : part prise par le buste qui se penche. Le reste passe par le bras qui frappe, à l'épaule. Plus de buste = coup plus « engagé », plus de bras = corps plus droit.
- *Punch Pitch Max Down / Up* (60° / 40°) : au-delà, le poing ne suit plus.
- **Réglage** : avec *Debug Logs*, la console compare après chaque coup la pente du poing à celle de la vue (« slope … vs view … ») et dit s'il faut monter ou baisser *Punch Pitch Follow*.
- Le coup de pied suit déjà un peu le regard vertical : *Kick Pitch Follow* (0,4), sur Local Unarmed Melee.

**La tête pendant les coups** (CombatSettings, même groupe) : la tête garde l'orientation que lui donne le Look At IK du joueur (elle suit le regard), y compris pendant la garde et les coups. Les animations de boxe mettent le buste de profil, jusqu'à 90° pour le jab, et la limite du Look At (*Clamp Weight Head*, 0,5 sur le prefab) le faisait abandonner : la tête partait avec le buste.
- *Combat Head Clamp* (0) : la limite utilisée pendant la garde et les coups. 0 = aucune limite. La valeur du prefab revient hors combat.

**Jauge d'encaissement** (CombatSettings > *Encaissement*) : les coups de ce joueur ne font plus tomber directement. Ils remplissent la jauge de la cible.
- Tant qu'elle n'est pas pleine, la cible titube.
- Au-delà de *Dazed From*, elle est sonnée : plus lente, elle dérive sur le côté et sa vue tangue.
- Pleine, ou sur un coup de pied spartiate, elle est K.O. pendant *Knock Out Time*.
- Pour des K.O. plus rapides, monter *Punch Damage* / *Kick Damage*. Pour des combats plus longs, monter *Drain Rate*. Si un coup fait encore tomber avant que la jauge soit pleine, monter *Stagger Drift* (écart toléré) ou *Stagger Resistance*. S'il ne fait plus assez tituber, baisser *Stagger Resistance*.

**Garde** (CombatSettings > *Garde*, maintenir Ctrl gauche / LT) :
- **Ce qu'elle couvre** : les coups de face (dans *Block Angle*), à la tête et au corps. Les jambes, les côtés et le dos ne sont pas couverts.
- **Coup bloqué** : il ne remplit plus que *Blocked Damage* de la jauge (*Blocked Kick Damage* pour un pied, qui passe en partie) et ne pousse plus que de *Blocked Force*.
- **Jauge de garde** : chaque coup bloqué lui coûte *Block Cost Punch / Kick*.
- **Coup de pied spartiate sur une garde** : avec *Spartan Breaks Guard Only* (coché par défaut), il brise la garde sans K.O. ni projection. Décoché, il passe à travers.
- **Garde brisée** : jauge de garde vide, ou coup de pied spartiate. Le joueur ne peut plus garder pendant *Guard Break Time* et les coups suivants comptent normalement sur sa jauge d'encaissement. La rupture elle-même n'y retire rien : le coup qui vide la jauge de garde est encore bloqué.
- **Contre-attaque** : un poing donné dans *Counter Window* après un blocage part *Counter Speed* fois plus vite et compte *Counter Damage* fois plus.
- **Équilibrage** :
  - si tout le monde garde tout le temps, monter *Block Cost Punch* ou baisser *Block Move Speed* ;
  - si la garde ne sert à rien, monter *Block Regen Rate*.

**Endurance** (CombatSettings > *Endurance de celui qui frappe*) : chaque coup coûte de l'endurance (*Punch Stamina* 0,15, *Kick Stamina* 0,3, plus *Spartan Stamina* pour un spartiate). Sans assez d'endurance, le coup ne part pas.
- Sous *Tired Below*, les poings sont plus lents (*Tired Speed*) et plus faibles (*Tired Damage*).
- Repère : environ 6 poings d'affilée, dont les 2 derniers fatigués, puis une pause d'environ 2 s pour tout récupérer.
- Pour des rafales plus longues, baisser *Punch Stamina* ou monter *Stamina Regen Rate*.

**Coups localisés** (CombatSettings > *Coups localisés*) : la zone touchée change ce que le coup remplit (*Head / Body / Leg Damage*), l'animation de coup reçu (lourde à la tête) et la chute du K.O. :
- tête : il s'écroule en arrière ;
- corps : il se plie ;
- jambes : ses jambes se dérobent.

Si le personnage réagit du mauvais côté, cocher *Hit Reaction Swap Sides*. Pour une chute plus spectaculaire, monter *Knock Out Push*.

#### Taille des membres

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Reference Arm Length | Bras pour lequel les distances du poing sont écrites ; elles sont mises à l'échelle du bras réel (↑ = gestes plus petits) | 0,6 m |
| Reference Leg Length | Idem pour la jambe et le pied | 0,9 m |
| Max Limb Extension | Distance maximale d'une cible, en part de la longueur du membre (1 = tendu à fond) | 0,95 |

#### Appui

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Tap Time | Pied : relâché avant ce délai = coup simple, au-delà = chargé | 0,18 s |
| Charge Time | Pied : temps de charge jusqu'au maximum | 0,7 s |
| Windup Time | Temps pour mettre le poing en garde / lever le genou ; le poing part ensuite tout seul | 0,08 s |

#### Poing

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Start Forward | Départ (garde) : distance du poing devant l'épaule | 0,12 m |
| Start Height | Départ : hauteur du poing par rapport à l'épaule | 0,08 m |
| Start Outward | Départ : décalage vers l'extérieur (négatif = devant la poitrine) | 0,05 m |
| Mid Forward | Mi-course : avancée en part de la portée (↓ = crochet plus large) | 0,5 |
| Mid Outward | Mi-course : écart vers l'extérieur (trop grand = bras tendu sur le côté) | 0,2 m |
| Mid Height | Mi-course : hauteur par rapport à l'épaule | 0,06 m |
| Punch Reach | Arrivée : distance du poing devant les épaules | 0,6 m |
| End Height | Arrivée : hauteur par rapport à l'épaule | 0 m |
| Fist Spread | Arrivée : écart entre les deux poings en largeurs d'épaules (1 = bras parallèles, < 1 = vers le centre) | 1,1 |
| Punch Pitch Follow | Part du regard haut/bas reprise par la hauteur du coup | 0,3 |
| Elbow Out | Force qui écarte le coude sur le côté | 0,6 |
| Fist Align | Orientation du poing : 1 = jointures vers la cible, paume vers le bas ; 0 = celle de l'animation | 1 |
| Fist Roll | Rotation du poignet en plus (90 = poing vertical) | 0° |
| Jab Torso Twist | Rotation du buste dans le coup | 12° |
| Shoulder Reach | Avancée de l'épaule qui frappe au bout du coup (allonge) ; le poing va plus loin d'autant (0 = l'épaule ne bouge pas) | 0,1 m |
| Punch Lean | Engagement du corps | 0,12 m |

#### Pied

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Kick Reach | Distance du pied devant la hanche, jambe tendue | 0,95 m |
| Kick Height | Hauteur du pied par rapport à la hanche, jambe tendue | 0,1 m |
| Kick Pitch Follow | Part du regard haut/bas reprise par la hauteur du coup | 0,4 |
| Chamber Forward | Genou levé : distance du pied devant la hanche | 0,12 m |
| Chamber Height | Genou levé : hauteur du pied par rapport à la hanche | −0,2 m |
| Chamber Raise | Genou levé : hauteur ajoutée à pleine charge | 0,12 m |
| Knee Forward | Force qui tire le genou vers l'avant | 0,6 |
| Foot Sole Forward | 1 = semelle face à la cible, orteils vers le haut ; 0 = orientation de l'animation | 1 |
| Kick Lean Back | Recul du buste pendant le coup de pied | 0,25 m |
| Kick Lunge | Pas en avant pendant un coup de pied simple | 0,2 m |
| Spartan Lunge | Pas en avant à pleine charge | 0,55 m |

#### Rythme

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Jab Strike Time | Durée de la frappe du poing | 0,09 s |
| Kick Strike Time | Durée de la frappe d'un coup de pied simple | 0,12 s |
| Charged Kick Strike Time | Durée de la frappe d'un coup de pied chargé | 0,15 s |
| Hold Time | Pause bras tendu | 0,05 s |
| Kick Hold Time | Pause jambe tendue | 0,12 s |
| Recover Time | Retour à la pose normale (et relâchement du bras précédent quand on enchaîne) | 0,2 s |
| Cooldown | Délai après un coup terminé (l'enchaînement des poings n'attend pas) | 0,05 s |

#### Touche

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Muscle Layers | Couches des muscles des ragdolls (vide = Ragdoll) | Ragdoll |
| Fist Radius | Zone de touche autour du poing | 0,15 m |
| Foot Radius | Zone de touche autour du pied | 0,2 m |
| Sub Steps | Tests de touche par image | 3 |

#### Force

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Jab Unpin | Déséquilibre d'un coup de poing | 1,5 |
| Jab Force | Force d'un coup de poing | 500 N |
| Kick Unpin | Déséquilibre d'un coup de pied simple | 2,5 |
| Kick Force | Force d'un coup de pied simple | 900 N |
| Charged Kick Unpin | Déséquilibre d'un coup de pied à pleine charge | 8 |
| Charged Kick Force | Force d'un coup de pied à pleine charge | 2500 N |
| Upward Bias | Part de la force vers le haut | 0,15 |

#### Coup de pied spartiate (pied chargé)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Spartan Min Charge | Charge à partir de laquelle le coup de pied devient spartiate (chute garantie, projection, zoom) | 0,8 |
| Launch Speed | Vitesse de projection de tout le corps de la cible vers l'arrière | 9 m/s |
| Launch Lift | Vitesse vers le haut ajoutée à la projection | 2,5 m/s |
| Slow Mo Zoom | Champ de vision retiré à la caméra de l'attaquant au contact (0 = pas de zoom). Le nom date de l'ancien ralenti, retiré | 12° |
| Spartan Zoom Duration | Durée du zoom, retour compris | 0,5 s |

#### Divers

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Hitstop Duration | Micro-ralenti à l'impact de chaque coup, spartiate compris (0 = désactivé) | 0,05 s |
| Hitstop Time Scale | Vitesse du temps pendant ce ralenti | 0,05 |
| Show Charge Indicator | Jauge de charge provisoire du coup de pied | oui |
| Debug Logs | Coups, touches, longueurs de membres mesurées dans la console | oui |

En jeu, joueur sélectionné, la vue *Scene* dessine le trajet du poing (jaune) et sa cible (rouge) pendant un coup.

## Lancer une scène sans le menu

Une scène qui contient une table de billard, lancée directement depuis l'éditeur, affiche le menu principal : c'est lui qui démarre la partie. Pour une scène de test (l'arène de R&D par exemple), on peut s'en passer : *Add Component > Pool > Lancer sans le menu* (`SkipMainMenu`), sur n'importe quel objet de la scène.

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Mode | Règles de la partie démarrée toute seule (*Party* = 8-ball avec les pouvoirs, *SpeedPool* = les deux joueurs tirent en même temps, *CatchTheHole* = 8-ball avec des trous vivants, réglés dans `CatchTheHoleSettings`, *HungryHole* = la blanche est un trou, réglée dans `HungryHoleSettings`, *SnakePool* = les billes ramassées suivent la blanche en file, réglé dans `SnakePoolSettings`, *PowerWashPool* = les billes lavent le tapis crasseux, réglé dans `PowerWashPoolSettings`) | Party |
| Party Mode | Avec *SpeedPool* : *Classic* = sans pouvoirs, *Pouvoirs* = avec les caisses de pouvoirs. *Party* a toujours les pouvoirs. | Classic |
| Target Score | Score à atteindre, si 14.1 | 150 |

Le composant n'a aucun effet quand le niveau est lancé depuis le menu. Le retirer (ou le désactiver) fait revenir le menu.

## Touches

**Par le joueur, en jeu** : Menu principal > Réglages (ou Pause > Réglages). Les touches choisies sont des *binding overrides* de l'Input System, gardées dans les `PlayerPrefs` (clé `UntitledPoolGame.KeyBindings`, registre Windows de l'utilisateur) et appliquées à chaque joueur qui rejoint. « Réinitialiser » revient aux touches du fichier ci-dessous.

**Les touches par défaut** : `Assets/InputManagers/InputSystem_Actions_Local.inputactions` : double-clic pour ouvrir l'éditeur d'*Input Actions*. Chaque action (*Attack*, *Interact*, *Next*…) a une liaison clavier / souris et une manette ; on peut en ajouter, en retirer ou en changer, puis *Save Asset*.

Le récapitulatif des touches actuelles est dans [Contrôles](gdd.html#controles) (Game design). Si vous changez une touche affichée par le HUD (pouvoir, valider), changez aussi le texte correspondant dans HudSettings.

**Menu debug** (test) : clic gauche maintenu + 4 clics droits rapides, lus directement sur la souris (pas une action de l'*Input Actions*). Le composant `DebugMenu` est créé tout seul dans toute scène de partie ; *Right Clicks To Toggle* (4) et *Combo Window* (1,5 s) se règlent sur l'objet « Debug Menu » en Play mode seulement. En éditeur il liste tous les assets de pouvoir du projet ; dans un build, ceux de PoolPowerSpawnSettings.

## Caméra (Cinemachine)

- **Durée des transitions** (entrée et sortie de visée, retour après une chute) : composant *Cinemachine Brain* de la caméra de chaque joueur, champ *Default Blend*.
- **Vue pendant une chute** : réglée dans *Local Player Ragdoll Controller* (voir plus haut), plus dans Cinemachine.

## Réglages d'éditeur

| Fichier | Où | Ouvert par | Ce qu'on règle |
|---|---|---|---|
| PoolTableAssetSettings | `Assets/Editor` | *Tools > Pool > Select Custom Table Settings* | Modèle de table, dimensions du tapis (2,24 × 1,12 m), hauteur du tapis, bandes, diamètre des billes (57 mm), rayon des poches |
| LevelMakerSettings | Assets du Level Maker | Fenêtre de l'outil de niveau | Prefabs joueur / table / queue, dossier des niveaux, profil de post-process, sol provisoire, palette de la salle, pas de la grille, hauteur du sol, nombre de queues, pas de rotation de la table, rotation des props et des escaliers à Ctrl + molette (*Prop Wheel Degrees*, 2° par unité de défilement ; Ctrl + Alt + molette = crans fixes de 45°) |
| Palettes (PrefabPalette) | Où vous les créez (*Create > Pool > Level Maker Palette*) | Étape Salle | Variantes de murs, portes, fenêtres, piliers, sols, plafonds, escaliers, garde-corps, props ; axe des murs, retournement, largeur de module, ajustement des dalles |
| Ambiances (AmbiencePreset) | Créées par l'étape Ambiance (*Create > Pool > Level Maker Ambience*) | Étape Ambiance | Scénario d'éclairage, ciel, lumière ambiante, lumière principale, post-process, brouillard, teinte et intensité des lumières du décor |

Le détail de l'outil de niveau est dans [Outils > Level Maker](outils.html#level-maker), et tous les outils de *Tools > Pool* dans [Outils](outils.html).

## Pour aller plus loin

- [Technique > Configuration → rendu](tech.html#config) : comment une valeur réglée dans un fichier finit par changer l'image (chargement, simulation, rendu).
