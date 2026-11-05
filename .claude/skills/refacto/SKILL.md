---
name: refacto
description: Chantier de refacto du code sans changer le comportement — audit classé par gain et par risque, puis une seule refacto par PR, avec les pièges de sérialisation d'Unity. À utiliser quand l'utilisateur demande /refacto, un audit du code, ou de découper ou simplifier un système. Pour nettoyer seulement un diff, utiliser /simplify.
---

# refacto — simplifier sans rien casser

`/refacto audit [zone]` analyse sans rien modifier. `/refacto <cible de l'audit>` réalise une refacto.

Le projet n'a pas de tests automatisés : **une régression ne se verra qu'en jouant**. Toute la méthode vient de là : petits pas, comportement identique, et rien sur du code dont le comportement de référence est inconnu.

## `audit`

Ne modifier aucun fichier. Mesurer plutôt que deviner :

```bash
find Assets/Scripts -name '*.cs' | xargs wc -l | sort -rn | head -n 20
grep -rn "ruleSet is \|\.Mode == PoolGameMode\." Assets/Scripts --include=*.cs | cut -d: -f1 | sort | uniq -c | sort -rn
```

Chercher, hors scripts online figés et plugins :
- ce qu'il faut retoucher à chaque nouveau mode ou pouvoir (chaînes de `else if`, `switch`, conditions à rallonge) ;
- les doublons entre `RuleSet`, entre pouvoirs ;
- les fichiers qui font plusieurs métiers ;
- le code mort ;
- les `GetComponent`, `Find` et allocations dans les boucles par image.

Présenter un tableau, classé du meilleur au moins bon rapport gain / risque :

| Cible | Problème mesuré | Gain | Risque | Taille | Testé en jeu ? |
|---|---|---|---|---|---|

**Testé en jeu ?** se lit dans `TODO.md`. Ne rien lancer sans le choix de l'utilisateur. Les pistes écartées pour l'instant vont dans `docs/content/pistes.md`.

## Avant de commencer

1. **Comportement de référence.** Si la zone touche des lignes « non testé » de la TODO, **s'arrêter et le dire** : on ne saurait pas distinguer un bug d'avant d'une régression. Proposer `/a-tester` sur cette zone d'abord. On ne continue que si l'utilisateur le demande explicitement.
2. **Conflits.** Plusieurs chantiers avancent en parallèle : `gh pr list --state open --json number,title,files`, et regarder aussi les branches et worktrees locaux (`git worktree list`). Si un autre chantier touche les mêmes gros fichiers, le dire : le merge sera douloureux.
3. **Branche dédiée.** `chore/refacto-<zone>`, une refacto par PR, jamais mêlée à une fonctionnalité ou à un correctif. Un bug trouvé en route est noté dans la TODO et corrigé à part.

## Pièges d'Unity

Le compilateur ne voit aucun de ces cas, et `unity-check` passera quand même :

| Changement | Ce qui casse | Parade |
|---|---|---|
| Renommer un champ sérialisé | Les valeurs de l'Inspector sont perdues | `[FormerlySerializedAs("ancien")]`, ou ne pas renommer |
| Déplacer un champ dans une classe imbriquée ou une base | Les valeurs sont perdues | Le garder en place, ou demander comment migrer |
| Renommer ou déplacer un script de `MonoBehaviour` ou `ScriptableObject` | La référence dans les scènes et assets si le `.meta` ne suit pas | `git mv` du `.cs` **et** de son `.meta` ; nom de classe = nom de fichier |
| Changer le type sérialisé d'une classe | Scènes et prefabs | `grep` du GUID du `.meta` dans `Assets`, demander |
| Réordonner une enum sérialisée | Les valeurs changent de sens sans erreur | Toujours ajouter à la fin |
| Renommer un champ lu par son nom | Erreur seulement dans l'éditeur (`FindProperty("requiresOwnTurn")`) | `grep -rn "FindProperty\|nameof" Assets/Scripts/Editor` |
| Renommer une méthode branchée en `UnityEvent` ou `SendMessage` | Silencieux dans les scènes et prefabs | `grep` du nom dans les `.unity`, `.prefab` et `.cs` |
| Changer l'ordre d'initialisation (`Awake`, `Start`, `OnEnable`, statiques) | Nulls selon l'ordre, écran partagé | Garder les appels au même endroit |
| Changer l'ordre dans `Update` ou `FixedUpdate` | Physique, caméra et IK décalés d'une image | Ne pas y toucher dans une refacto de structure |
| Déplacer un fichier chargé par `Resources.Load` | Le chemin en texte ne compile pas contre le fichier | Ne pas déplacer, ou `grep` du chemin |
| Changer un état partagé en écran partagé | J1 et J2 échangés ou un seul servi | Relire chaque index de joueur |

Règles du projet : scripts online **figés** (hors du périmètre), plugins tiers **jamais modifiés**, tags de log (`[Bowling]`…) conservés, car `/a-tester` les lit.

Les classes `partial` (comme `ShooterMiniRuleSet.Powers.cs`) sont le moyen le plus sûr de découper un gros fichier : aucun champ ni GUID ne bouge, seuls les nouveaux fichiers ont besoin de leur `.meta`, que Unity crée à l'ouverture.

## Réaliser

1. **Annoncer le plan** en quelques lignes : les étapes, ce qui reste identique, et ce qui serait à vérifier.
2. **Petites étapes**, chacune avec son commit : d'abord déplacer tel quel, ensuite seulement simplifier. Ne jamais faire les deux dans le même commit, sinon le diff devient illisible.
3. **`unity-check` après chaque étape.**
4. **Relire le diff** : `git diff --color-moved=dimmed-zebra`. Ce qui est présenté comme déplacé ne doit rien avoir changé, y compris l'ordre des appels et les conditions.
5. **Arrêter** si l'étape demande de changer un comportement, même une amélioration : la sortir de la refacto et demander.

## Livrer

- Mettre à jour `docs/content/tech/` (classes, flux, arborescence) et la checklist de `nouveau-mode` ou `nouveau-pouvoir` si la refacto change les fichiers à toucher.
- `CHANGELOG.md` et `TODO.md` : dire que le joueur ne voit aucune différence.
- Donner à l'utilisateur une **courte liste de tests ciblés** sur ce qui peut régresser, et écrire « compilé, non testé » jusqu'à son retour.
- PR via `/github` : préciser dans le titre que c'est une réorganisation sans changement de jeu, et ne pas merger avant son test.
