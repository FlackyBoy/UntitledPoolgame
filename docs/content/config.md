# Configuration

> Tous les réglages du jeu : où ils se trouvent, comment les modifier et ce que fait chaque valeur. Les valeurs indiquées sont celles par défaut du code ; celles de ton projet peuvent différer si elles ont déjà été réglées.

## Vue d'ensemble

| Type de réglage | Où | Créé par | Pour quoi |
|---|---|---|---|
| **Fichiers de réglages partagés** | `Assets/Resources/*.asset` | *Tools > Pool > Ensure Config Assets Exist* | Physique des billes, sensations caméra, effets d'empochage, apparition des pouvoirs, HUD, niveaux du menu, menu et pause, écran de chargement |
| **Scène du menu** | `Assets/Scenes/MainMenu.unity` | *Tools > Pool > Create Main Menu Scene* | Le menu principal, première scène du jeu |
| **Écrans des menus** | `Assets/Resources/Menus/*.asset` | *Ensure Config Assets Exist*, modifiés dans *Tools > Pool > Menu Studio* | Accueil, pages du menu, réglages, pause, chargement : éléments, positions, textes, effets, Feel |
| **Séquences Feel** | `Assets/Feel/Sequences/*.prefab`, liées dans `Resources/GameFeelSettings` | *Tools > Pool > Text FX Studio* ou à la main | Son, vibration, texte du HUD, secousse… à un moment de la partie |
| **Un fichier par pouvoir** | `Assets/Powers/*.asset` | *Create > Pool > Powers > …* (ou l'outil ci-dessus pour la bille destructrice) | Nom et paramètres de chaque pouvoir |
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
| Annonce de la 8 | intensité de surbrillance de la poche visée | 6 |
| Poche fermée (pouvoir) | prefab du bouchon (facultatif), échelle | —, 1 |
| Ralenti | échelle de temps, durée | 0,3, 0,12 s |

### PoolPowerSpawnSettings — apparition des pouvoirs

| Groupe | Champs | Défauts |
|---|---|---|
| Pouvoirs distribués | *Available Powers* : la liste des assets de pouvoirs qui peuvent sortir | tous les pouvoirs du projet |
| Couleurs par catégorie | Attaque, Défense, Effet (caisses, halo de la bille, HUD) | rouge, bleu, vert |
| Caisses | nombre en même temps, délai de réapparition min / max | 3, 15 s, 30 s |
| Visuels des caisses | un prefab par catégorie (facultatif) | — |
| Bille à pouvoir | intervalle de changement min / max, intensité et portée du halo | 20 s, 40 s, 4, 0,3 m |
| Visuels de la bille | un matériau par catégorie (facultatif) | — |

Les pouvoirs n'apparaissent qu'en mode **Party**. Pour retirer un pouvoir du jeu sans le supprimer, enlevez-le de *Available Powers*.

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

**Tools > Pool > Menu Studio** est l'éditeur graphique des écrans. Chaque écran est un fichier `Assets/Resources/Menus/<écran>.asset`, créé avec la disposition d'origine par *Ensure Config Assets Exist* :

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

**La fenêtre**
- **En haut** :
  - la liste des écrans ;
  - les boutons pour ajouter une **bille**, une **carte**, un **texte**, une **image**, un **panneau** ou une **pilule** ;
  - Dupliquer, Supprimer, Derrière / Devant ;
  - *▶ Rejouer les effets* ;
  - la grille ;
  - *Réinitialiser l'écran*.
- **À gauche** : la liste des éléments, de celui dessiné derrière à celui dessiné devant, plus les réglages de l'écran (⚙). Un ▸ marque les éléments qui ont une action.
- **Au centre** : l'écran, dessiné comme dans le jeu.
  - **Cliquer** sur un élément le choisit ; le **glisser** le place.
  - Le **coin jaune** change sa taille.
  - **Flèches** : déplacer d'un demi-pourcent (Maj : 2 %). **Suppr** : supprimer. **Ctrl+D** : dupliquer.
  - La grille aimante au demi-pourcent ; Maj en glissant pour s'en passer.
- **À droite** : toutes les propriétés de l'élément choisi. Chaque changement se voit tout de suite et s'annule avec Ctrl+Z.

**Brouillon** : on travaille toujours sur une copie de l'écran. Rien ne change dans le jeu tant qu'on n'a pas cliqué sur **Enregistrer**.
- Dès qu'il y a une modification, un **bandeau orange** l'indique, avec *Enregistrer* et *Abandonner*. Le titre de la fenêtre prend une étoile.
- **Changer d'écran, fermer la fenêtre ou lancer le Play** avec un brouillon en cours ouvre une demande : enregistrer, abandonner, ou rester / garder le brouillon.
- **Le brouillon survit** au rechargement des scripts et au passage en Play. Pendant le Play, le jeu montre la version enregistrée.
- ***Réinitialiser l'écran*** et les séquences Feel créées passent aussi par le brouillon ; il faut enregistrer pour les garder.

**Les propriétés d'un élément**

| Groupe | Ce qu'on règle |
|---|---|
| Place | centre (en % de l'écran), taille (en pixels d'un écran 1080p), inclinaison, visible ou non |
| Apparence | couleur (bille, fond de carte ou de panneau, teinte d'image), bille rayée, numéro, image, bordure, arrondi |
| Texte | texte et petite ligne (balises de texte animé acceptées), tailles, couleur, police titre, ombre, lettres qui arrivent une à une |
| Action | ce que fait le choix : ouvrir un écran, retour, emplacement de sauvegarde, Just for fun, Local, En ligne, C'est parti, lancer un type de partie (avec ses règles), réglages, quitter, et pour la pause : reprendre, menu principal, confirmer ; grossissement quand on vise |
| Effets | arrivée (pop, chute, montée, glissé de gauche ou de droite, fondu, tour), son délai et sa durée ; mouvement permanent (flotte, pulse, se balance, gigote, tourne), force et vitesse |
| Feel | séquence jouée quand l'élément est **visé**, et quand il est **choisi**. Le bouton crée une séquence vide dans `Assets/Feel/Sequences/` et la relie ; on la remplit dans l'Inspector de Feel (son, vibration…) |

**Les réglages d'un écran** (⚙) :
- le fond : le bar dessiné (avec ou sans lumières, avec ou sans table), une couleur, une image, ou un voile (pause) ;
- la bille blanche et la queue, avec leur position et leur taille ;
- l'écran ouvert par Retour ;
- une séquence Feel jouée à l'ouverture.

**Les éléments que le jeu remplit** gardent leur nom ; on peut les déplacer, les redimensionner et changer leurs couleurs, mais pas les renommer :

| Nom | Écran | Contenu |
|---|---|---|
| `context` | Level, Mode, Loading | l'étiquette jaune (Just for fun · Le bar…) |
| `levels` | Level | la rangée de cartes des niveaux, centrée sur la zone |
| `rules` | Mode | la carte des règles du type de partie visé |
| `slot1`, `slot2` et `slot1-label`, `slot2-label` | Lobby | les ronds des joueurs et leur texte |
| `aim-hint` | Title, Mode… | le texte qui clignote |
| `panel` | Settings | le cadre de l'écran des touches (place, taille, couleurs, titre) |
| `who` | Pause | qui a mis en pause (sa couleur suit le joueur) |
| `postcard`, `tip`, `status`, `ready`, `gutter`, `go` | Loading | la carte postale, l'astuce, le message, les joueurs prêts, la gouttière, « C'est parti ! » |

**Ordre des choix** : sur les écrans avec la queue, on vise la bille la plus proche dans la direction poussée. Dans la pause, on passe d'un choix à l'autre dans l'ordre de la liste.

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

**Les noms des actions** de l'écran des touches se retrouvent par leur *Id* : `Move`, `Look`, `Pause`, ou le nom d'une action du joueur (`Interact`, `Attack`, `Punch`, `Kick`, `Next`, `Sprint`, `Jump`). Une ligne absente de la liste garde son nom par défaut. Les touches elles-mêmes se changent dans le jeu (*Réglages > Touches*), pas ici.

- `{0}` est remplacé par le numéro (emplacement, joueur), le score ou l'appareil.
- Un format mal écrit (accolade en trop) s'affiche tel quel au lieu de casser le menu.

### Texte animé — Text FX Studio, balises, TextFxSettings

**Le plus simple : *Tools > Pool > Text FX Studio*.** La fenêtre permet de :
- **choisir un texte du jeu** dans la liste du haut (HUD, menu, chargement, niveaux), ou d'écrire un texte libre ;
- **ajouter des effets avec des boutons** : sélectionner des lettres puis cliquer sur `wave`, `shake`, `pop`… (sans sélection, tout le texte). Les curseurs *Force* et *Vitesse* règlent la balise posée ;
- **voir le résultat en direct**, même sans lancer le jeu :
  - en lecture fixe, avec les lettres qui arrivent ou en machine à écrire ;
  - sur un fond de bar, de tapis ou de carte ;
  - avec la police, la taille, la couleur et le contour du jeu ;
  - en boucle ou avec la sortie ;
- **enregistrer** le texte dans le réglage d'origine (*Enregistrer dans le jeu*, annulable avec Ctrl+Z) ;
- **régler TextFxSettings** en bas de la fenêtre, avec l'effet visible tout de suite ;
- **créer une séquence Feel** avec ce texte pour un moment de la partie (voir *GameFeelSettings*). *Tester sur le HUD* l'affiche en jeu pendant le Play.

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

### GameFeelSettings — Feel aux moments de la partie

Associe un **moment de la partie** à une **séquence Feel** (un prefab avec un *MMF Player*) :
- **moments** : bille rentrée, blanche rentrée, faute, tir, tir plein, pouvoir ramassé, BOUM, début de tour, victoire ;
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
| Taille | Taille globale du HUD (1 = calé pour une moitié de 1920 × 1080), taille des interjections, de « À toi ! », des bandeaux |
| Couleurs | J1, J2, encre des contours, fond des cartes, mise en avant (jaune), danger (rouge), « BOUM ! », voile du joueur qui attend, jauge (début, milieu, fin) |
| Textes : tour et plaque | « À toi ! », « Tour de J{0} », « J{0} », groupe à décider, « Prochaine », « Puissance », touche du pouvoir |
| Textes : bandeaux d'aide | Main libre, poche visée / à viser pour la 8, trop loin, poche annoncée / à annoncer, bille destructrice armée |
| Interjections | Liste tirée au hasard quand une bille rentre, blanche rentrée, faute, main libre, tir plein, pouvoir lancé, « BOUM ! » ; interrupteur des interjections de bille rentrée ; seuil du tir plein (0,95) |
| Carte sponsor | Afficher ou non, en-tête, suffixe du nom (™), ligne « Pouvoir {0} », noms des catégories |
| Fin de partie | « J{0} gagne ! », « Revanche », aide des touches, confettis |
| Durées et mouvements | Interjections (1,3 s), rayons (1,1 s), carte sponsor (3,1 s), délai faute → main libre (1,3 s), secousse (durée, force), balancement de « À toi ! » (hauteur, période), voile du joueur qui attend |

- `{0}` est remplacé par le numéro du joueur, la poche ou la catégorie du pouvoir.
- **Vider un texte** désactive l'interjection ou le bandeau correspondant.
- Un format mal écrit (accolade en trop) s'affiche tel quel au lieu de casser le HUD.

## Pouvoirs (un fichier par pouvoir)

Dans `Assets/Powers`. Champs communs : **Power Name** (nom affiché) et **Requires Own Turn** (ne peut être lancé que pendant son tour).

| Pouvoir | Fichier | Réglages propres |
|---|---|---|
| Tir boosté | `BoostedShotPower` | Shot Power Multiplier (1,6) : puissance du prochain tir |
| Vision brouillée | `VisionImpairPower` | Sensitivity Multiplier (0,3) : sensibilité de la visée de l'adversaire |
| Commandes inversées | `InvertedControlsPower` | Sensitivity Multiplier (1,8) |
| Poche fermée | `ClosePocketPower` | — (le visuel du bouchon est dans PoolPotEffectSettings) |
| Bille destructrice | `BallBlastPower` | Explosion Prefab (*CFXR2 WW Explosion*), Explosion Scale (0,15), Explosion Lifetime (4 s), Spare Eight Ball (oui), Blast Speed (0,5 m/s, 0 = pas de souffle), Blast Radius (0,3 m) |

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
| **Local Cue Melee** | Coups de queue | Voir *Réglages combat* (onglet de la page TODO & GDD) |
| **Local Unarmed Melee** | Poings et pieds | Voir *Réglages combat* |
| **Local Pool Power Controller** | Lancer son pouvoir | Action utilisée (*Next* : F / B) |
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

## Touches

**Par le joueur, en jeu** : Menu principal > Réglages (ou Pause > Réglages). Les touches choisies sont des *binding overrides* de l'Input System, gardées dans les `PlayerPrefs` (clé `UntitledPoolGame.KeyBindings`, registre Windows de l'utilisateur) et appliquées à chaque joueur qui rejoint. « Réinitialiser » revient aux touches du fichier ci-dessous.

**Les touches par défaut** : `Assets/InputManagers/InputSystem_Actions_Local.inputactions` : double-clic pour ouvrir l'éditeur d'*Input Actions*. Chaque action (*Attack*, *Interact*, *Next*…) a une liaison clavier / souris et une manette ; on peut en ajouter, en retirer ou en changer, puis *Save Asset*.

Le récapitulatif des touches actuelles est dans l'onglet **Contrôles** de la page *TODO & GDD*. Si vous changez une touche affichée par le HUD (pouvoir, valider), changez aussi le texte correspondant dans HudSettings.

## Caméra (Cinemachine)

- **Durée des transitions** (entrée et sortie de visée, retour après une chute) : composant *Cinemachine Brain* de la caméra de chaque joueur, champ *Default Blend*.
- **Vue pendant une chute** : réglée dans *Local Player Ragdoll Controller* (voir plus haut), plus dans Cinemachine.

## Réglages d'éditeur

| Fichier | Où | Ouvert par | Ce qu'on règle |
|---|---|---|---|
| PoolTableAssetSettings | `Assets/Editor` | *Tools > Pool > Select Custom Table Settings* | Modèle de table, dimensions du tapis (2,24 × 1,12 m), hauteur du tapis, bandes, diamètre des billes (57 mm), rayon des poches |
| LevelMakerSettings | Assets du Level Maker | Fenêtre de l'outil de niveau | Prefabs joueur / table / queue, dossier des niveaux, profil de post-process, sol provisoire, palette de la salle, pas de la grille, hauteur du sol, nombre de queues, pas de rotation de la table |
| Palettes (PrefabPalette) | Où vous les créez (*Create > Pool > Level Maker Palette*) | Étape Salle | Variantes de murs, portes, fenêtres, piliers, sols, plafonds, escaliers, garde-corps, props ; axe des murs, retournement, largeur de module, ajustement des dalles |
| Ambiances (AmbiencePreset) | Créées par l'étape Ambiance (*Create > Pool > Level Maker Ambience*) | Étape Ambiance | Scénario d'éclairage, ciel, lumière ambiante, lumière principale, post-process, brouillard, teinte et intensité des lumières du décor |

Le détail de l'outil de niveau est sur la page **Outil de niveau**.

## Pour aller plus loin

- *Doc technique > Configuration* : comment une valeur réglée dans un fichier finit par changer l'image (chargement, simulation, rendu).
- *TODO & GDD > Réglages combat* : chaque champ des coups de queue, poings et pieds.
