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

- **Maison / prison hantée**, **base spatiale** — gameplay associé à définir.
- Probablement le même système qui choisira le décor **et** les règles et pouvoirs actifs de chaque dimension.

### Outils et confort

- **Créateur de niveau** simplifié (page dédiée à venir).
- **Menu de réglages de sensibilité** en jeu, souris et manette, sauvegardé.

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
- **Corriger le conflit de boutons** sur manette : Attaque et pouvoir partagent X / □ (voir GDD, Contrôles).

### Technique

- **Git LFS pour les gros fichiers** (textures, modèles, audio). *Pourquoi :* les gros envois ont échoué sur GitHub (erreur HTTP 500) et ont dû être découpés à la main ; le dépôt reste lourd à cloner.
- **Tests automatiques des règles** (tests EditMode Unity sur `EightBallRuleSet`, `NineBallRuleSet`…). *Pourquoi :* deux bugs de règles (fausse faute sur la casse) ne sont apparus qu'en jeu ; un test par cas de faute les aurait repérés.
- **Tout réglage de gameplay dans des ScriptableObjects**, chargés comme les réglages existants (`PoolSettingsLoader`). *Pourquoi :* on règle sans toucher au code, et c'est la base de l'outil de niveau et des dimensions.
- **Réassignation des touches** avec l'outil de l'Input System d'Unity, sauvegardée par joueur. *Pourquoi :* se combine avec le menu de sensibilité prévu.
- **Build automatique** sur GitHub Actions (projet GameCI). *Pourquoi :* savoir à chaque push si le projet compile encore, sans ouvrir Unity.
- **En ligne : physique des billes calculée par le serveur**, les clients ne faisant qu'afficher. *Pourquoi :* la physique Unity ne donne pas exactement le même résultat d'une machine à l'autre, donc chaque client doit recevoir la vérité du serveur.
