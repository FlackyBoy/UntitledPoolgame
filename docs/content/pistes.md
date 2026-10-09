# Pistes & suggestions

> Rien sur cette page n'est décidé. La partie 1 rassemble les idées déjà notées dans la TODO. La partie 2 réunit des **propositions de Claude**, à valider ou écarter. Ce qui est décidé ou fait se trouve dans le GDD.

## 1. Pistes envisagées

### Pouvoirs candidats

| Idée | Catégorie pressentie |
|---|---|
| Aimant : la bille jouée est attirée vers la poche la plus proche | Effet |
| Ralentisseur (bille ou contrôles adverses, à préciser) | Attaque |
| Désactiver la visée adverse | Attaque |
| Faire exploser sa propre bille, comptée comme empochée | Effet |
| Bille destructrice : la première bille touchée par la blanche est détruite, comptée comme empochée (implémentée le 02/10, voir le GDD) | Effet |
| Mélanger toutes les billes sur la table | Attaque |
| Bille interdite : faute si l'adversaire la touche | Attaque (piège) |
| Trajectoire courbée pour l'adversaire | Attaque |
| Traverser les billes adverses sans faute | Effet |
| Faire disparaître pleines / rayées (plus moyen de distinguer son groupe) | Attaque |
| Vent qui dévie les billes | Attaque |
| Rendre une bille lourde | Attaque |
| Échanger la bille blanche avec la bille visée | Défense |
| Trajectoire affichée avec les rebonds sur bande | Défense |
| Faire apparaître un trou sur la table | À trancher |
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

- **Créateur de niveau** : fait en grande partie (page *Outil de niveau*) ; restent les chemins de décor, les dimensions et la génération procédurale ci-dessous.
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
- **Chrono de tir optionnel en Party** — *Pourquoi :* garde le rythme d'une soirée entre amis et donne de la valeur aux pouvoirs qui font perdre du temps (Vision brouillée, Commandes inversées).
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
