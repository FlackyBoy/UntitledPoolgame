# Tous les outils

> Les outils d'éditeur du projet, tous dans le menu **Tools > Pool** d'Unity. Ils sont rangés par familles, séparées par un trait. Les gros éditeurs ont leur propre section dans le menu à gauche.

## Éditeurs

| Outil | À quoi il sert | Mode d'emploi |
|---|---|---|
| **Menu Studio** | Modifier les écrans des menus (accueil, sous-menus, réglages, pause, chargement) : éléments, positions, textes, effets, séquences Feel | [Menu Studio](outils.html#menu-studio) |
| **Text FX Studio** | Poser des effets d'animation sur les textes du jeu, les voir en direct, créer une séquence Feel avec un texte | [Text FX Studio](outils.html#text-fx-studio) |
| **Level Maker** | Monter un niveau jouable en sept étapes : niveau, salle, table, joueurs, pouvoirs, ambiance, vérification | [Level Maker](outils.html#level-maker) |

## Projet

| Outil | Ce qu'il fait |
|---|---|
| **Ensure Config Assets Exist** | Crée les fichiers de réglages manquants (`Assets/Resources`, écrans des menus, assets des pouvoirs dans `Assets/Powers`), sans toucher à ceux qui existent. Pour le pouvoir Trou, génère aussi le decal du contour brûlé (`Assets/Textures/Decals`, `Assets/Materials/Decals`) et prévient si le renderer URP n'a pas la fonction Decal. Pour repartir des valeurs par défaut : supprimer le fichier, puis relancer |
| **Create Main Menu Scene** | Crée la scène `MainMenu` si elle manque, puis lance *Sync Build Scenes* |
| **Sync Build Scenes (menu + levels)** | Met la scène du menu en premier dans les scènes du build, suivie des niveaux de `GameFlowSettings`. Les niveaux *Coming Soon* et ceux sans scène sont ignorés |

## Table

| Outil | Ce qu'il fait |
|---|---|
| **Build Table** | Génère une table complète : tapis, bandes, poches, billes, queues et points d'apparition des pouvoirs |
| **Attach Physics To Custom Table** | Pour un modèle de table importé : sélectionner le modèle, puis lancer. Ajoute la physique (enfant `PoolPhysics`) sans les visuels générés. **Le relancer régénère tout `PoolPhysics`**, donc les poches réalignées à la main sont perdues |
| **Select Custom Table Settings** | Sélectionne `PoolTableAssetSettings` (dimensions du tapis, bandes, billes, poches). Pour changer la taille, régler ce fichier et régénérer plutôt que changer l'échelle de `PoolPhysics` |
| **Générer les jetons de pouvoir** | Fabrique le jeton de chaque pouvoir posé sur la table : une forme 3D qui le représente (fusée, œil, cadenas, bombe…, « ? » sinon), faite de formes simples, avec ses propres couleurs, au-dessus d'un anneau à la couleur de sa catégorie (60 % opaque : l'alpha des matériaux `Token_Ring_<catégorie>` se règle à la main). Elle tourne et flotte. Prefabs dans `Assets/Prefabs/Powers`, matériaux dans `Assets/Materials/PowerTokens`. Il fabrique aussi l'**icône du pouvoir dans le HUD** (mêmes objets, blanche cernée de noir, dans `Assets/Textures/PowerHudIcons`). Il ne crée que les jetons et icônes **manquants** : les prefabs générés se retouchent à la main sans risque. **Régénérer tous les jetons de pouvoir (écrase les retouches)** les reconstruit tous, après confirmation. *Ensure Config Assets Exist* crée aussi ceux qui manquent |
| **Add Power Spawn System To Selected Table** | Ajoute seulement ce qui manque pour les pouvoirs (caisses, bille à pouvoir) à la table sélectionnée, sans rien régénérer. Sans risque, même relancé |

## Play Anywhere

| Outil | Ce qu'il fait |
|---|---|
| **Créer un trou** | Pose un parcours (trou) de Play Anywhere au sol, numéroté après ceux de la scène (*Play Anywhere - Trou 2*, *Order* = 2 : joué en 2e), sous le centre de la vue Scène : les deux départs (sous-bocks bleu et rouge), le trou 3 m devant (disque noir et drapeau), sans colliders. Déplacer les pièces, puis régler le par sur *Play Anywhere - Trou*. Matériaux dans `Assets/Materials/PlayAnywhere`. Annulable |
| **Créer la caisse** | Pose la caisse des objets (*Caisse*, une boîte en bois de 2,3 m) au sol, sous le centre de la vue Scène. À chaque manche, les objets de *Props* (`PlayAnywhereSettings`) y sont posés. Annulable |
| **Générer les objets de la caisse** | Crée dans `Assets/Prefabs/PlayAnywhere` les objets simples de la caisse, avec des collisions qui suivent leur forme : un bloc orange (50 × 25 × 50 cm), une rampe bleue (60 cm de large, 1 m de long, 20 cm de haut, au ras du sol à l'avant : la bille la monte et saute derrière), un cône rouge (30 cm de diamètre, 40 cm de haut), et les objets à effet : un portail destructeur violet (ouverture de 42 × 55 cm), un tuyau de raccourci vert en deux parties (un trou au sol, puis un bout de tuyau d'où la bille ressort ; dans le prefab, la sortie est 2 m devant), un destructeur (bloc noir à bande rouge), un ventilateur gris (souffle sur 2 m), un ressort à plaque rouge, une plaque de glue (1 m) et une de verglas (1,2 m). Une flèche sur le ventilateur et le ressort montre leur sens. Ne remplace pas ceux qui existent, sauf l'ancien tuyau d'un seul tenant, refait au même endroit (*Props* le garde). Crée aussi les surfaces *Glue* et *Verglas* et ajoute tous les objets à *Props*, comme *Ensure Config Assets Exist*. Se fait aussi tout seul en lançant le mode Play quand un de ces objets manque |
| **Ajouter la pose allongée au joueur** | Ajoute au contrôleur d'animation du joueur (`New.prefab`) un calque « Play Anywhere Allongé » qui joue `male_idle_on_floor` (EverydayMotionPack) sur tout le corps, à poids 0 : la visée l'active quand il faut s'allonger. À relancer pour le refaire |
| **Créer une zone hors limites** | Pose une boîte déclencheur (*Zone hors limites*) pour exclure un coin à l'intérieur du couloir d'un parcours. En dehors du couloir vert du trou, la bille est déjà hors limites. Annulable |
| **Tracer le parcours** (trou sélectionné, dans la Scene) | Sphères bleue et rouge : les départs ; jaune : le trou (le drapeau suit) ; vertes : les points du parcours. Tirées, elles se posent sur la surface sous la souris (sol, comptoir, étagère), le parcours prend donc la hauteur du décor. « + » entre deux points en ajoute un, le cube rouge au-dessus d'un point le retire, les points sur le bord du couloir règlent sa largeur. Boutons dans l'Inspector : *Ajouter un point avant le trou*, *Retirer le dernier point*. Annulable |
| **Surface de la sélection** > Parquet, Carrelage, Comptoir, Tapis, Moquette | Pour les exceptions : la plupart des objets sont reconnus par leur matériau (liste *Materials* du type de surface) ou leur nom. Donne aux objets sélectionnés un composant *Surface* de ce type (crée les surfaces si elles manquent). Annulable |

## R&D

| Outil | Ce qu'il fait |
|---|---|
| **Préparer le joueur de R&D** | Ajoute à la variante `New_PlayerVariant` le déplacement, les poings de boxe et la jauge d'encaissement, puis les calques d'animation des coups, en un passage |
| **Créer l'arène de test** | Crée la scène `Assets/Scenes/Sandbox/RnD/ArenaRnD.unity` (sol, rebords de hauteur croissante, rampe, marches, caisses, deux mannequins à frapper, sans table ni partie) et prépare d'abord le joueur de R&D. Les scènes partagées ne sont pas touchées |

## Créer des fichiers (menu Create)

Clic droit dans la fenêtre *Project* > **Create > Pool** :

| Entrée | Ce qu'elle crée |
|---|---|
| *Powers > …* | Un pouvoir (un fichier par pouvoir, dans `Assets/Powers`) |
| *… Settings*, *Menu Screen* | Un fichier de réglages ou un écran de menu. Inutile en temps normal : *Ensure Config Assets Exist* les crée au bon endroit |
| *Level Maker Palette* | Une palette de pièces de décor pour le Level Maker |
| *Level Maker Ambience* | Une ambiance (lumières, ciel, post-process, brouillard) pour le Level Maker |

Les réglages de ces outils (`PoolTableAssetSettings`, `LevelMakerSettings`, palettes, ambiances) sont décrits dans [Réglages > Réglages d'éditeur](config.html#config).
