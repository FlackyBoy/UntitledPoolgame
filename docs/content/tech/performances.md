# Performances

> Relevé fait en lisant le code et les réglages au 29/09/2026, **sans mesure au Profiler** : les points ci-dessous sont des risques ou des gains probables, à confirmer par une mesure avant d'optimiser.

## En jeu (runtime)

### Budget de référence

Deux joueurs en écran partagé, 16 billes, une salle. Cible raisonnable : 60 images par seconde sur PC, soit **16,6 ms par image** pour tout (logique, physique, rendu des deux caméras).

### Physique

| Réglage | Valeur | Commentaire |
|---|---|---|
| Pas fixe | 0,02 s (**50 Hz**) | La physique des billes avance 50 fois par seconde |
| Itérations du solveur | 6 (position) / 1 (vitesse) | Valeurs par défaut |
| Détection continue (CCD) | Billes et queue en *Continuous Dynamic* | Évite qu'une bille rapide traverse une bande fine |
| Interpolation | Billes interpolées | Mouvement fluide à l'écran malgré le pas de 50 Hz |
| Déterminisme renforcé | Désactivé | À activer si l'online recalcule la physique (voir *Pistes*) |

- Le modèle de frottement maison (`PoolBall.FixedUpdate`) coûte quelques opérations vectorielles par bille et par pas : négligeable pour 16 billes.
- **Piste** : tester un pas de 0,01 s (100 Hz). Chocs plus précis entre billes rapides ; coût faible avec si peu d'objets.
- Les couches `PlayerAnimated`, `Ragdoll` et `Cuestick` ignorent les billes (`PhysicsLayerSetup`) : moins de paires de collisions à tester, et le corps ne pousse plus les billes.

### Logique (scripts)

| Point | Où | Coût | Correction proposée |
|---|---|---|---|
| Recherche de la bille blanche **à chaque image, deux fois** par joueur hors visée | `LocalPoolAimController.FindNearbyCueBall` (appelé par `Update` et par `WantsInteractThisFrame`) | `Physics.OverlapSphere` crée un nouveau tableau à chaque appel → déchets mémoire (GC) en continu | `PoolBall.FindCueBall()` + test de distance (aucune requête physique, aucune allocation), une seule fois par image |
| `PoolPocket.Radius` | `GetComponent<SphereCollider>()` à chaque lecture | Faible | Mettre le rayon en cache dans `Awake` |
| Détection des caisses | `PoolPowerCrate.FixedUpdate` : `PoolBall.FindCueBall()` par caisse et par pas | Parcours de 16 billes × 3 caisses : négligeable | — |
| Fin de tir | `PoolBall.AnyMoving()` chaque image | Parcours de 16 billes : négligeable | — |
| Interface `OnGUI` | HUD de `PoolMatchRules`, visée, effets | `OnGUI` est appelé plusieurs fois par image et alloue des chaînes | Passer à une UI Canvas ou UI Toolkit (prévu avec la page Propositions UI) |
| Ralenti | `Time.timeScale` global | Aucun, mais ralentit **tout** (y compris l'autre joueur) | À garder en tête en écran partagé |

### Rendu

| Point | Commentaire | Piste |
|---|---|---|
| **Écran partagé** | Deux caméras : ombres, SSAO et post-process calculés deux fois | Principal poste de coût GPU ; à mesurer en premier |
| Ombres sur 50 m, 4 cascades | Largement plus que la taille d'une salle | 15–20 m, 2 cascades : ombres plus nettes et moins chères |
| Ombres des lumières additionnelles | Halos de poches et billes à pouvoir : lumières ponctuelles | Couper les ombres de ces petites lumières (effet visuel quasi nul) |
| SSAO | Passe plein écran par caméra | Réduire la résolution ou l'intensité si besoin |
| Depth + Opaque texture | Copies d'image supplémentaires | Couper l'opaque texture si aucun effet ne l'utilise |
| Éclairage entièrement temps réel | — | Précalculer (lightmaps) l'éclairage fixe de la salle |
| Personnage | Maillage skinné + FBBIK + Look At IK + PuppetMaster, ×2 joueurs | Les solveurs IK tournent chaque image ; désactiver ceux qui ne servent pas selon l'état (déjà fait pendant le ragdoll) |

## Hors jeu (éditeur, build, dépôt)

| Point | Constat | Piste |
|---|---|---|
| **Taille du dépôt** | Dossier `.git` ≈ **4,8 Go**, ~20 800 fichiers suivis ; les gros envois échouent (HTTP 500) et doivent être découpés | **Git LFS** pour les textures, modèles, audio, vidéos |
| Compilation des scripts | Pas d'*assembly definition* (`.asmdef`) dans `Assets/Scripts` : tout le code maison est dans `Assembly-CSharp` | Un `.asmdef` par dossier (Pool, Player, Interaction, Core, Editor) limite ce qui recompile à chaque modification |
| Démos des plugins | Démos Feel (dont HDRP), FinalIK, PuppetMaster compilées et importées | Supprimer les dossiers de démo inutiles (ils restent téléchargeables depuis l'Asset Store) |
| Entrée en Play Mode | Options d'entrée activées mais rechargement du domaine et de la scène conservés | Désactiver le rechargement du domaine accélère fortement le Play Mode, **mais** le code utilise des champs statiques (`Instance`, `Active`, événements) qu'il faudrait réinitialiser explicitement — à faire avant de l'activer |
| Assets en double / anciens | `Prefabs/PlayerLocal/Old`, `A trier`, `Assets/_Recovery`, `Assets/TEMP` | Faire le tri : réduit l'import, la taille du dépôt et les confusions |
| Variantes de shaders | Plusieurs packs VFX avec leurs shaders + URP | Surveiller le temps de build ; *Shader Variant Log Level* pour les repérer |

## Méthode

1. **Mesurer** avec le Profiler Unity (CPU, GPU, mémoire) en écran partagé, dans la scène du bar.
2. Corriger le poste le plus coûteux mesuré, pas le plus visible dans le code.
3. Revérifier après chaque changement.
