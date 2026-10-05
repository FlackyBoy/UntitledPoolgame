# Configuration

> Tous les réglages du jeu : où ils se trouvent, comment les modifier et ce que fait chaque valeur. Les valeurs indiquées sont celles par défaut du code ; celles de ton projet peuvent différer si elles ont déjà été réglées.

## Vue d'ensemble

| Type de réglage | Où | Créé par | Pour quoi |
|---|---|---|---|
| **Fichiers de réglages partagés** | `Assets/Resources/*.asset` | *Tools > Pool > Ensure Config Assets Exist* | Physique des billes, sensations caméra, effets d'empochage, apparition des pouvoirs, HUD |
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

`Assets/InputManagers/InputSystem_Actions_Local.inputactions` : double-clic pour ouvrir l'éditeur d'*Input Actions*. Chaque action (*Attack*, *Interact*, *Next*…) a une liaison clavier / souris et une manette ; on peut en ajouter, en retirer ou en changer, puis *Save Asset*.

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
