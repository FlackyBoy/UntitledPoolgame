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
| **Ensure Config Assets Exist** | Crée les fichiers de réglages manquants (`Assets/Resources`, écrans des menus), sans toucher à ceux qui existent. Pour repartir des valeurs par défaut : supprimer le fichier, puis relancer |
| **Create Main Menu Scene** | Crée la scène `MainMenu` si elle manque, puis lance *Sync Build Scenes* |
| **Sync Build Scenes (menu + levels)** | Met la scène du menu en premier dans les scènes du build, suivie des niveaux de `GameFlowSettings`. Les niveaux *Coming Soon* et ceux sans scène sont ignorés |

## Table

| Outil | Ce qu'il fait |
|---|---|
| **Build Table** | Génère une table complète : tapis, bandes, poches, billes, queues et points d'apparition des pouvoirs |
| **Attach Physics To Custom Table** | Pour un modèle de table importé : sélectionner le modèle, puis lancer. Ajoute la physique (enfant `PoolPhysics`) sans les visuels générés. **Le relancer régénère tout `PoolPhysics`**, donc les poches réalignées à la main sont perdues |
| **Select Custom Table Settings** | Sélectionne `PoolTableAssetSettings` (dimensions du tapis, bandes, billes, poches). Pour changer la taille, régler ce fichier et régénérer plutôt que changer l'échelle de `PoolPhysics` |
| **Add Power Spawn System To Selected Table** | Ajoute seulement ce qui manque pour les pouvoirs (caisses, bille à pouvoir) à la table sélectionnée, sans rien régénérer. Sans risque, même relancé |

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
