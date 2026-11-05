---
name: nouveau-pouvoir
description: Checklist complète pour ajouter un pouvoir (classe, fichier de réglages, jeton 3D, icône du HUD, doc). À utiliser quand l'utilisateur demande un nouveau pouvoir, /nouveau-pouvoir, ou quand on renomme ou retire un pouvoir.
---

# nouveau-pouvoir — tout ce qu'un pouvoir doit toucher

Un pouvoir est **une classe et un fichier** (`Assets/Powers/<Nom>Power.asset`). Le nom de la classe sert de clé au générateur de jetons : le choisir avant de commencer.

**Modèle à copier :**
- effet immédiat : hériter de `PoolPower` ;
- le joueur vise un point du tapis : hériter de `TablePointPower` (exemple : `MudBucketPower`) ;
- pouvoir du Shooter : `ShooterGrapplePower`, et sa logique dans `ShooterMiniRuleSet.Powers.cs`.

## Avant de coder

Décider, ou demander si le choix change le jeu :
- catégorie (`Attack`, `Defense`, `Effect`) : elle décide qui voit l'annonce ;
- modes où il sort (*Only In Modes*, vide = tous) ;
- ce que change le niveau 2 (pouvoir ramassé deux fois), s'il en a un ;
- utilisable hors de son tour (*Requires Own Turn*, *Allow During Own Turn*) ou pendant que les billes roulent (*Allow While Balls Moving*).

## Code

| Fichier | À ajouter |
|---|---|
| `Pool/<Nom>Power.cs` | La classe, avec `[CreateAssetMenu]`, `Type`, `Activate`, et les réglages sérialisés avec un `[Tooltip]` en français. Sans effet hors de son mode : un `Debug.Log` avec le tag du mode, pas d'exception. |
| `Editor/PoolTableBuilder.cs` | `EnsurePowerAsset<T>(fichier, nom affiché, configure)`, avec les pouvoirs de sa famille. Il crée le fichier et l'ajoute à `PoolPowerSpawnSettings.availablePowers`. |
| `Editor/PowerTokenGenerator.cs` | **Deux** `case "<Nom>"` (classe sans `Power`) : `DrawHudIcon` pour l'icône, l'autre `switch (key)` pour le jeton 3D. Le même objet dans les deux, un dessin simple et lisible. |
| `Pool/PoolMatchRules.cs`, `UI/MatchHud.cs` | Seulement si le pouvoir a besoin d'un état de partie ou d'un affichage à lui. |

La triche **C + X** et le menu debug listent les pouvoirs tout seuls : rien à y ajouter.

## À lancer dans Unity (le rappeler à l'utilisateur)

1. **Tools > Pool > Ensure Config Assets Exist** : crée le fichier du pouvoir.
2. **Tools > Pool > Générer les jetons de pouvoir** : crée le prefab et l'icône, puis les branche sur le fichier.

Committer ensuite le `.asset`, le prefab de `Assets/Prefabs/Powers`, l'icône de `Assets/Textures/PowerHudIcons`, les matériaux de `Assets/Materials/PowerTokens`, et les `.meta` de chacun.

## Doc

- `docs/content/gdd.md` : la table des pouvoirs (effet pour le joueur, catégorie).
- `docs/content/config.md` : une ligne dans *Pouvoirs (un fichier par pouvoir)*, avec chaque réglage et sa valeur par défaut.
- `docs/content/tech/architecture.md` : le diagramme de classes et le texte, si le pouvoir ajoute un mécanisme.
- `docs/content/tech/arborescence.md` : le nouveau fichier.
- `TODO.md` et `CHANGELOG.md` : comme pour toute fonctionnalité, avec **Comment tester**.

## Vérifier

1. Lancer `unity-check`.
2. Comparer avec un pouvoir récent : `grep -rl "MudBucket" Assets/Scripts docs/content`.
3. Donner le test le plus court : **C + X** pour se donner le pouvoir, au niveau 1 puis 2, à J1 puis J2, dans son mode, et vérifier qu'il ne sort pas ailleurs. Écrire « compilé, non testé » jusqu'au retour de l'utilisateur.
