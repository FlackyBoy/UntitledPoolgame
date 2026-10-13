# Tous les réglages

> Tous les réglages du jeu : où ils se trouvent, comment les modifier et ce que fait chaque valeur. Les valeurs indiquées sont celles par défaut du code ; celles de ton projet peuvent différer si elles ont déjà été réglées.

## Vue d'ensemble

| Type de réglage | Où | Créé par | Pour quoi |
|---|---|---|---|
| **Fichiers de réglages partagés** | `Assets/Resources/*.asset` | *Tools > Pool > Ensure Config Assets Exist* | Physique des billes, sensations caméra, effets d'empochage, apparition des pouvoirs, HUD, niveaux du menu, menu et pause, écran de chargement |
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
- **moments** : bille rentrée, blanche rentrée, faute, tir, tir plein, pouvoir ramassé, BOUM, début de tour, victoire, **coup reçu** (poing ou pied qui touche un joueur, sur l'écran de celui qui le reçoit ; l'intensité est multipliée par la force du coup), **K.O.** (jauge d'encaissement pleine, sur l'écran de celui qui tombe), **garde brisée** (sur l'écran de celui dont la garde casse) ;
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
| Textes : bandeaux d'aide | Main libre, poche visée / à viser pour la 8, trop loin, poche annoncée / à annoncer, bille destructrice armée, choix du trou d'un pouvoir (trou visé / aucun), aimant prêt |
| Texte de plaque « Groupe identique » | *Groups Disguised Text* : remplace les billes restantes pendant l'effet |
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
| Poche fermée | `ClosePocketPower` | — (poche choisie par le joueur ; le visuel du bouchon est dans PoolPotEffectSettings) |
| Groupe identique | `IdenticalGroupsPower` | Look (*Colorless* : aspect uni · *Like Ball* : aspect d'une bille), Look Ball Number (8 : bille copiée, 1 à 15), Colorless Material (facultatif), Colorless Color (gris clair, si pas de matériau). *Requires Own Turn* décoché à la création (attaque) |
| Bille destructrice | `BallBlastPower` | Explosion Prefab (*CFXR2 WW Explosion*), Explosion Scale (0,15), Explosion Lifetime (4 s), Spare Eight Ball (oui), Blast Speed (0,5 m/s, 0 = pas de souffle), Blast Radius (0,3 m) |
| Aimant | `MagnetPower` | Capture Radius (0,4 m : distance du trou où la bille est captée), Min Capture Speed (0,05 m/s : une bille immobile n'est pas captée), Pull Acceleration (1,5 m/s² : force d'attraction), Steering (4/s : correction de trajectoire, 0 = aucune), Max Pull Duration (4 s : lâche une bille coincée) ; *VFX dans la poche* : Pocket Vfx Prefab (*CFXR Electrified 3*, vide = aucun), Pocket Vfx Scale (0,2), Pocket Vfx Height (0,02 m), Pocket Vfx Fade Out (1,5 s) |

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
| **Local Pool Power Controller** | Lancer son pouvoir, choisir un trou | Actions utilisées (*Next* : Q / B pour lancer ou annuler, *Interact* : E / Y pour valider le trou), *Max Point Angle* (8° : écart toléré entre le pointeur et un trou), *Pointer Size* (8 px : pointeur blanc) |
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
| LevelMakerSettings | Assets du Level Maker | Fenêtre de l'outil de niveau | Prefabs joueur / table / queue, dossier des niveaux, profil de post-process, sol provisoire, palette de la salle, pas de la grille, hauteur du sol, nombre de queues, pas de rotation de la table |
| Palettes (PrefabPalette) | Où vous les créez (*Create > Pool > Level Maker Palette*) | Étape Salle | Variantes de murs, portes, fenêtres, piliers, sols, plafonds, escaliers, garde-corps, props ; axe des murs, retournement, largeur de module, ajustement des dalles |
| Ambiances (AmbiencePreset) | Créées par l'étape Ambiance (*Create > Pool > Level Maker Ambience*) | Étape Ambiance | Scénario d'éclairage, ciel, lumière ambiante, lumière principale, post-process, brouillard, teinte et intensité des lumières du décor |

Le détail de l'outil de niveau est dans [Outils > Level Maker](outils.html#level-maker), et tous les outils de *Tools > Pool* dans [Outils](outils.html).

## Pour aller plus loin

- [Technique > Configuration → rendu](tech.html#config) : comment une valeur réglée dans un fichier finit par changer l'image (chargement, simulation, rendu).
