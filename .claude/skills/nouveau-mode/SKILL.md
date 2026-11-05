---
name: nouveau-mode
description: Checklist complète pour ajouter un mode de jeu (règles, réglages, menu, roue de Boogie Night, HUD, doc). À utiliser quand l'utilisateur demande un nouveau mode, /nouveau-mode, ou quand on renomme ou retire un mode. Pour un pouvoir seul, voir nouveau-pouvoir.
---

# nouveau-mode — tout ce qu'un mode doit toucher

Un mode passe par une dizaine de fichiers existants, et le compilateur n'en signale presque aucun oubli. Dérouler toute la liste, puis vérifier avec un mode récent :

```bash
grep -rlE "BowlingPool|Bowling" Assets/Scripts docs/content --include=*.cs --include=*.md
```

**Modèle à copier selon le mode :**
- tour par tour : `BowlingPoolRuleSet` ;
- simultané, les joueurs sont les billes : `SumoPoolRuleSet` ;
- simultané, joueurs miniatures : `ShooterMiniRuleSet` ou `PocketSizeRuleSet`.

## Avant de coder

Décider, ou demander si le choix change le jeu :
- type de partie : tour par tour, Speed Pool, ou les deux ;
- pouvoirs : aucun, tous, ou seulement ceux du mode ;
- présence sur la roue de Boogie Night ;
- nom affiché et règles du menu, en une ou deux phrases drôles et claires.

Lancer `notion-todo start`. Notion est plein pour l'instant : si la création échoue, le noter dans la ligne de la TODO et continuer.

## Code

| Fichier | À ajouter |
|---|---|
| `Pool/PoolGameMode.cs` | La valeur **à la fin de l'enum** : elle est sérialisée en entier, ne jamais réordonner. Commentaire d'une ou deux lignes. |
| `Pool/<Mode>RuleSet.cs` | Les règles (`IPoolRuleSet` : `Setup`, `ResolveShot`, `DescribePlayer`). |
| `Pool/<Mode>Settings.cs` | Le ScriptableObject de réglages, chargé par `PoolSettingsLoader.LoadOrDefault<T>()`, avec un `debugLogs` et un tag `[Mode]`. |
| `Pool/PoolMatchRules.cs` | `CreateRuleSet`. `DrawModeButton` (menu de secours). Le `Tick` s'il faut un suivi par image. Le nettoyage de fin de mode si le mode crée des objets. |
| `Pool/PoolMatchSetup.cs` | `ModeName`. `Supports` si un type de partie est impossible. `HasPowers` si le mode n'a pas de pouvoirs. |
| `Pool/PoolModeSettings.cs` | Une ligne dans `Defaults()` (types permis, type par défaut, *Own Powers Only*). |
| `UI/MenuSettings.cs` | Le champ `MenuModeBall` (nom, numéro, couleur, règles) et la ligne de `ModeBall()`. |
| `UI/Menus/MenuDefaults.cs` | `ModeRow(...)` avec un id et un numéro de ligne libres. |
| `UI/BoogieNightSettings.cs` | Le mode dans la liste par défaut de la roue. |
| `UI/MatchHud.cs` | L'affichage propre au mode (score, manche, chrono), si `DescribePlayer` ne suffit pas. |
| `UI/SkipMainMenu.cs` | La mention du mode dans le tooltip de *Mode*. |
| `Editor/PoolTableBuilder.cs` | `Ensure<Mode>SettingsAsset()`, appelé avec les autres. |
| `Pool/LocalPoolAimController.cs` | Seulement si le mode bloque ou change le tir. |

Quand un mode s'ajoute à une chaîne de `else if` ou à une condition déjà longue, le signaler à l'utilisateur comme dette plutôt que de refactorer au passage.

## Fichiers de données : le piège

Modifier le code ne suffit pas, car ces fichiers existent déjà et gardent leurs valeurs :

| Fichier | Que faire |
|---|---|
| `Assets/Resources/Menus/Mode.asset` | **Ajouter la ligne aussi ici**, en copiant un élément existant : `MenuDefaults` ne sert que si le fichier manque ou pour *Réinitialiser l'écran*. Vérifier que le cadre et le panneau de règles ont encore la place : `MenuDefaults` et ce fichier doivent rester d'accord sur la disposition. |
| `Assets/Resources/BoogieNightSettings.asset` | `modes` est une suite d'entiers hexadécimaux sur 8 caractères (little-endian). Ajouter la valeur du mode à la fin : la valeur 17 s'écrit `11000000`. Sinon, le mode n'est jamais tiré. |
| `Assets/Resources/PoolModeSettings.asset` | Rien : un mode absent prend les valeurs de `Defaults()`. |
| `Assets/Resources/<Mode>Settings.asset` | Créé par l'utilisateur via **Tools > Pool > Ensure Config Assets Exist**. Le rappeler dans le message final. |

Ne jamais modifier une scène partagée sans que l'utilisateur l'ait annoncée comme prise.

## Doc

- `docs/content/gdd.md` : la table des modes et la liste de la Boogie Night.
- `docs/content/config.md` : la table `PoolModeSettings`, une section `<Mode>Settings`, la ligne *Roue*.
- `docs/content/controles.md` : si le mode a ses propres touches.
- `docs/content/tech/architecture.md` : le fonctionnement du mode et ce qu'il réutilise.
- `docs/content/tech/arborescence.md` : les nouveaux fichiers.
- `TODO.md` : la ligne du mode avec **Choix faits sans demande**, **Code** et **Comment tester**.
- `CHANGELOG.md` : une entrée lisible par un joueur, sous la date du jour.
- `docs/content/pistes.md` : ce qui est seulement envisagé.

## Vérifier

1. Lancer `unity-check`.
2. Relancer le `grep` du début avec le nom du nouveau mode et comparer avec Bowling : chaque fichier absent doit avoir une raison.
3. Donner à l'utilisateur le test le plus court : *Lancer sans le menu* avec *Mode* = le mode, puis le parcours Menu > Jouer > ligne du mode. Écrire « compilé, non testé » jusqu'à son retour.
