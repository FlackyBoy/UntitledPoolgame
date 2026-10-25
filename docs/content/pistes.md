# Pistes & suggestions

> Rien sur cette page n'est décidé. La partie 1 rassemble les idées déjà notées dans la TODO. La partie 2 réunit des **propositions de Claude**, à valider ou écarter. Ce qui est décidé ou fait se trouve dans le GDD.

## 1. Pistes envisagées

### Pouvoirs candidats

| Idée | Catégorie pressentie |
|---|---|
| Aimant : la bille jouée est attirée vers une poche (implémenté le 13/10 vers un trou choisi, voir le GDD) | Effet |
| Ralentisseur (bille ou contrôles adverses, à préciser) | Attaque |
| Désactiver la visée adverse | Attaque |
| Faire exploser sa propre bille, comptée comme empochée | Effet |
| Bille destructrice : la première bille touchée par la blanche est détruite, comptée comme empochée (implémentée le 02/10, voir le GDD) | Effet |
| Mélanger toutes les billes sur la table | Attaque |
| Bille interdite : faute si l'adversaire la touche | Attaque (piège) |
| Trajectoire courbée pour l'adversaire | Attaque |
| Traverser les billes adverses sans faute | Effet |
| Faire disparaître pleines / rayées (plus moyen de distinguer son groupe) (implémenté le 13/10 : « Groupe identique », voir le GDD) | Attaque |
| Vent qui dévie les billes | Attaque |
| Rendre une bille lourde | Attaque |
| Échanger la bille blanche avec la bille visée | Défense |
| Trajectoire affichée avec les rebonds sur bande | Défense |
| Faire apparaître un trou sur la table (implémenté le 15/10 : pouvoir « Trou », voir le GDD) | Attaque |
| Dédoubler la bille blanche | À trancher |
| Invoquer des minions | À explorer |
| Earthquake : la table se brise en morceaux | Pouvoir ou mode |

Autres idées autour des pouvoirs :

- **Choisir la poche à fermer** au lieu d'un tirage au hasard, en réutilisant le sélecteur de poche de la bille 8.
- **Ramasser des pouvoirs en explorant la salle**, pas seulement sur la table.
- **Pouvoirs liés à des billes précises** : empocher une bille donnée déclenche un effet.

### Modes de jeu

- **Bille « tic-tac »** qui explose après un délai.
- **Pistolet** : la queue tire des billes blanches sur des billes en bulles ou mobiles.
- **Wack-a-Ball** : toucher le plus vite possible des cibles qui sortent des trous.
- **Façon Rocket League** : on pilote directement la bille blanche.
- **Façon What The Golf** : un mode absurde qui change à chaque trou.
- **Pool Ragdoll.**
- **Combat au billard** avec barre de vie : on gagne au billard **ou** au combat.
- **Earthquake** (voir aussi les pouvoirs).
- **Billards face à face** (idée du 20/10) : deux tables l'une en face de l'autre, chaque joueur sur la sienne. Des **tremplins** sur la table font décoller les billes, qu'on envoie sur l'autre table. On vise une **cible**, un **trou**, ou **le joueur adverse** directement. Il reste à choisir l'objectif : marquer des points, défendre sa table ou les deux. Il faut aussi trancher si on joue chacun son tour ou en même temps.
- **Speed Pool** (retenu au brainstorm du 20/10 ; **prototype en cours le 21/10**, voir la TODO et le GDD) : les deux joueurs tirent **en même temps** sur la même table, chacun avec sa blanche (bleue et rouge). Le premier qui vide son groupe gagne. Il n'y a plus de temps mort. Encore à décider : garder ou non la 8, groupes fixes ou attribués à la première bille rentrée, un chrono.
- **Façon hole.io / Donut County** (idée du 23/10, à côté de Catch the Hole). Piloter le trou au stick ne serait plus du billard : c'est le coup de queue qui le fait bouger.
  - **Version retenue pour l'instant : la blanche est un trou.** Chaque joueur a sa blanche-trou (bleue / rouge). On la tire comme une blanche ; elle avale sur son passage les billes **plus petites qu'elle** et **grandit** à chaque bille avalée.
    - **Billes de tailles différentes** : une bille trop grosse pour le trou n'est pas avalée, elle est percutée comme au billard (obstacle, ou bille à pousser sur les autres). Idée : la 8 est la plus grosse, il faut avoir assez grandi pour la prendre.
    - **Deux façons de jouer à deux** (23/10) :
      - **Un seul trou, au tour par tour** : les joueurs tirent chacun leur tour la même blanche-trou. Bille adverse avalée : elle compte pour l'adversaire et le tour passe, comme en 8-ball.
      - **Un trou chacun, en même temps (façon Speed Pool)** : bille adverse avalée, elle compte pour l'adversaire **et** ton trou rétrécit (pas de tour à passer). Option : le plus gros trou avale celui de l'autre.
  - **Variante asymétrique** : le tireur joue au billard pendant que l'adversaire déplace un trou pour avaler ses billes en route ; les rôles tournent à chaque tir. Proche du pouvoir *Pilote* de Catch the Hole.

### Structure « party game » (idée du 20/10)

L'utilisateur envisage un jeu construit comme *Mario Party*. Une **soirée** enchaîne plusieurs modes courts. Ceux qui ne veulent pas de soirée choisissent un mode à part. Les billards face à face seraient l'un de ces modes.

- **Deux façons de jouer** dans le menu :
  - **La Soirée** : une suite de manches dans des modes différents, avec un classement général ;
  - **Partie libre** : un seul mode, choisi dans la liste (c'est ce que fait aujourd'hui *Just for fun*).
- **Une soirée** :
  - 5 manches environ, chacune de 2 à 4 minutes ;
  - entre deux manches, l'**ardoise** du bar montre le classement ;
  - le mode suivant est choisi par une roulette, par le perdant de la manche, ou en tirant sur une bille comme dans le menu ;
  - le gagnant de la soirée est celui qui a le plus de points (ou d'étoiles), avec une **manche finale** qui rapporte plus.
- **Garder la soirée ouverte jusqu'au bout** : un bonus au joueur mené, des étoiles bonus à la fin (« le plus de fautes », « le plus long tir »), comme les étoiles bonus de *Mario Party*.
- **Modes candidats** : 8-ball court, 9-ball, Speed Pool, billards face à face, trick shots, Wack-a-Ball, Pistolet, la borne d'arcade…
- **Conséquence** : chaque mode doit tenir en quelques minutes et partager le même cadre (début, chrono, score, fin, résultat transmis à la soirée).
- **À renommer** : le mode actuel « Party — Classique » (8-ball + pouvoirs) deviendrait « Pouvoirs », pour laisser le mot « Party » à la soirée.

### Rythme de partie (brainstorm du 20/10)

Constat de départ : le jeu manque de rythme. Le joueur qui attend n'a rien à faire, le cycle d'un tir est long, et une partie ne monte pas en intensité. Idées retenues par l'utilisateur :

- **Pronostic** : pendant que l'adversaire vise, celui qui attend parie en un bouton (« il rate », « il rentre », « il fait faute »). S'il a raison, il gagne un pouvoir.
- **Chrono de tir** : un compte à rebours visible pour chaque tir, qui accélère en fin de partie.
- **Événements du bar** : tous les 3 ou 4 tours, un événement est annoncé comme un sponsor, par exemple « Panne de courant ! », « La table penche ! » ou « Happy hour : pouvoirs x2 ».
- **Mort subite** : quand il reste une ou deux billes, la musique change, la lumière vire au rouge et des caisses de pouvoirs forts apparaissent.
- **Parties plus courtes, en plusieurs manches** : par exemple 5 billes chacun, en deux manches gagnantes. Chaque manche finit sur un pic d'intensité.

Proposées sans retour pour l'instant : frapper le tireur pendant sa charge fait dévier son tir, mais il peut répliquer et un K.O. lui donne la main libre ; raccourcir le cycle d'un tir (queue en main au début du tour, arrêt plus tôt des billes presque immobiles), en chronométrant d'abord un tour type ; un public de PNJ dans le bar qui réagit.

### Gameplay

- **Craie** : en mettre avant de tirer donne un bonus ; ne pas en mettre use la queue et peut provoquer un malus (fausse queue).
- **Combo** : un multiplicateur qui monte tant qu'on enchaîne les billes, avec un rendu très « juicy ». Cible (score, effet, cosmétique) et condition de remise à zéro à définir.
- **Objets à ramasser et lancer** : liste et comportements à définir, et dans quels modes ou dimensions les autoriser.
- **Queue comme arme de mêlée.**

### Environnements

- **Prison hantée** : bascule dans l'horreur (lumières qui s'éteignent, jump scares, billes qui bougent seules) et un **Némésis** qui traque le joueur pour l'empêcher de jouer — géré par le jeu, ou **incarné par l'autre joueur** pendant le tour adverse ?
- **Base spatiale** : billes en apesanteur, gravité qui change, sas de dépressurisation.
- Probablement le même système qui choisira le décor **et** les règles et pouvoirs actifs de chaque dimension.

### Outils et confort

- **Créateur de niveau** : fait en grande partie ([Level Maker](outils.html#level-maker)) ; restent les chemins de décor, les dimensions et la génération procédurale ci-dessous.
- **Menu de réglages de sensibilité** en jeu, souris et manette, sauvegardé.
- **Génération procédurale des salles**, à refaire plus tard (une première version maison a été retirée le 01/10). Références retenues :

  | | [RoomGen](https://assetstore.unity.com/packages/tools/level-design/roomgen-procedural-generator-215804) (Angular Fox Dev) | [Procedural Generation Grid](https://assetstore.unity.com/packages/tools/utilities/procedural-generation-grid-beta-195535) (FImpossible Creations) |
  |---|---|---|
  | Ce que ça fait | **Une pièce à la fois**, rectangulaire, entièrement décorée : sol, murs, coins, portes, fenêtres, toit, étages | **Grille de cellules + règles par cellule** (« Field Setup » : paquets de modificateurs qui posent ou retirent des objets selon les cellules voisines), plus un **Build Planner** (graphe de nœuds) qui génère le plan : pièces et couloirs |
  | Décor | Préréglages : chaque objet a une probabilité, un espacement minimal, décalage, rotation et échelle aléatoires, objets par étage | Règles ordonnées : murs d'abord, puis meubles alignés sous les murs (ils lisent où sont les murs), tampons, conditions sur les voisins |
  | Limites | Pas de liaison entre pièces ni de plan, angles à 90° seulement, pas d'escaliers | Plus complexe à prendre en main (grand nombre de modificateurs), en bêta |
  | Compatibilité | Built-in, URP, HDRP ; Unity 2022.3+ ; éditeur et exécution | Built-in, URP, HDRP ; Unity 2019.4+ ; éditeur et exécution |
  | Prix | ≈ 37 € | ≈ 42 € |

  **Faisabilité** :
  - **Les deux sont faisables avec le projet.** Ils sont compatibles URP et Unity 6, et on achète déjà chez FImpossible Creations (Eyes Animator est dans `Assets/Plugins`).
  - **Ce qui manquait à notre version** est justement ce qu'ils font bien. Le décor y est piloté par des règles qui lisent l'architecture déjà posée (murs, coins, portes) au lieu de jeter des objets dans un rectangle. Et les préréglages se règlent à la main, objet par objet.
  - **Piste la plus simple** : PGG pour le plan (pièces et couloirs reliés) et l'habillage par règles de voisinage, en gardant notre Level Maker pour la table, les joueurs, les pouvoirs et la vérification.
  - **Alternative** : RoomGen seul pour décorer une pièce tracée à la main avec nos outils, puisqu'il ne fait pas de plan.
  - **Contrainte** : ces outils seraient des plugins, à ne pas modifier, donc à adapter depuis nos propres scripts.
  - **À décider** : acheter l'un des deux, ou s'inspirer de leurs méthodes (règles de cellule ordonnées, préréglages par objet) pour une version maison.

## 2. Suggestions de Claude

> Propositions faites en relisant la TODO et le code. Chacune indique pourquoi elle est proposée.

### Game design

- **Pouvoirs de rattrapage** — le joueur mené au score tire plus souvent des pouvoirs forts (comme les objets de Mario Kart, déjà la référence du slot unique). *Pourquoi :* les party games restent drôles quand la partie reste ouverte jusqu'au bout.
- **Premiers pouvoirs de Défense** — la catégorie est vide. Pistes naturelles : « Bouclier » (annule la prochaine attaque reçue) et « Renvoi » (retourne l'attaque à l'envoyeur). *Pourquoi :* sans défense, les attaques n'ont pas de contre, et on réagit en jouant au lieu de subir.
- **Dimensions = pile de modificateurs** — plutôt qu'un jeu de règles complet par dimension, une dimension combine plusieurs petites règles réutilisables (gravité basse, poches mobiles, billes lourdes…), chacune dans son fichier de configuration. *Pourquoi :* on crée une nouvelle dimension en assemblant des briques existantes, et les pouvoirs peuvent réutiliser les mêmes règles.
- **Ralenti ou revoir le dernier coup** sur les beaux tirs et les fautes spectaculaires. *Pourquoi :* ce sont les moments qu'on partage dans un « friend slop ».
- **Emotes et provocations** (déjà prévues dans les animations) liées aux événements : faute adverse, bille empochée. *Pourquoi :* peu de travail et beaucoup de réactions entre joueurs.
- **Défis de trick-shots en solo** — positions de billes imposées, un seul tir. *Pourquoi :* donne du contenu à jouer seul et sert de tutoriel pour la visée et l'effet.

### Expérience joueur

- **Tutoriel intégré** à la première partie : ramasser la queue, entrer en visée, effet, charge. Le jeu repose sur beaucoup d'interactions différentes sur un même bouton.
- **HUD clair** : à qui le tour, pouvoir en stock (avec sa catégorie), groupe attribué, poche annoncée. Aujourd'hui ces informations sont dispersées ou absentes.
- **Accessibilité daltonisme** : distinguer pleines, rayées et catégories de pouvoir autrement que par la couleur (motifs, icônes).
- **Place du combat en partie** — à cadrer ; pistes : combat seulement en Party (ou réglable par mode), frapper celui qui vise lui fait rater son coup, trêve pendant la visée, un K.O. fait perdre le tour ou donne la bille en main, pouvoirs de combat (bouclier, coup renforcé, contre). *Pourquoi :* aujourd'hui on peut se battre à tout moment sans conséquence sur la partie ; ces règles donnent un enjeu au combat sans casser le billard.
- **Passer les effets codés en dur sur Feel** (secousses et flashs de faute, interjections du HUD) maintenant que `GameFeelSettings` existe. *Pourquoi :* tout le ressenti d'un moment se réglerait au même endroit, sans doublon entre le code et Feel.
- **Le menu en vraie 3D** — la version 2D du menu Synthèse permet de valider le parcours ; la version prévue se joue sur la table du bar avec la vraie queue. *Pourquoi :* c'est le moment où le joueur apprend le geste de visée avant même la première partie.

### Technique

- **Git LFS pour les gros fichiers** (textures, modèles, audio). *Pourquoi :* les gros envois ont échoué sur GitHub (erreur HTTP 500) et ont dû être découpés à la main ; le dépôt reste lourd à cloner.
- **Tests automatiques des règles** (tests EditMode Unity sur `EightBallRuleSet`, `NineBallRuleSet`…). *Pourquoi :* deux bugs de règles (fausse faute sur la casse) ne sont apparus qu'en jeu ; un test par cas de faute les aurait repérés.
- **Tout réglage de gameplay dans des ScriptableObjects**, chargés comme les réglages existants (`PoolSettingsLoader`). *Pourquoi :* on règle sans toucher au code, et c'est la base de l'outil de niveau et des dimensions.
- **Réassignation des touches** avec l'outil de l'Input System d'Unity, sauvegardée par joueur. *Pourquoi :* se combine avec le menu de sensibilité prévu.
- **Level Maker plus sûr** — salles enregistrées par rapport à elles-mêmes (pour pouvoir les déplacer et les dupliquer), retouche du tracé avec des poignées, un seul Ctrl+Z par action, vérifications de niveau (salles qui se chevauchent, escalier sans issue, point d'apparition dans un mur). *Pourquoi :* aujourd'hui déplacer une salle ou annuler une construction peut faire perdre du travail.
- **Level Maker plus léger en jeu** — un collider simple par mur et un seul par sol de salle au lieu d'un MeshCollider par pièce ; liste des salles et mesures des prefabs gardées en mémoire dans l'éditeur. *Pourquoi :* une salle de 10 × 10 m fait vite une centaine de colliders, et l'outil ralentit dans une grande scène.
- **Gabarits de pièce et pièces d'angle** dans les palettes. *Pourquoi :* poser d'un clic des pièces répétées (toilettes, cages), et des angles propres sur les kits qui en ont.
- **Prefab joueur réparé une fois pour toutes** — lui ajouter les composants qu'il reçoit aujourd'hui au lancement (pouvoirs, prise de queue, objets). *Pourquoi :* le prefab actuel avait perdu les composants des pouvoirs sans que rien ne le signale.
- **Retirer l'ancien système de prise de queue** (`LocalCuePickupTrigger`, `PickUpCue` sauf la pose de port). *Pourquoi :* la nouvelle prise est validée, l'ancienne n'est plus qu'une source de confusion.
- **Build automatique** sur GitHub Actions (projet GameCI). *Pourquoi :* savoir à chaque push si le projet compile encore, sans ouvrir Unity.
- **En ligne : physique des billes calculée par le serveur**, les clients ne faisant qu'afficher. *Pourquoi :* la physique Unity ne donne pas exactement le même résultat d'une machine à l'autre, donc chaque client doit recevoir la vérité du serveur.
