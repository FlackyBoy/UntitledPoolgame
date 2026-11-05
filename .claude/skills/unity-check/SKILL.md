---
name: unity-check
description: Vérifie que les scripts C# du jeu et de l'éditeur compilent, sans ouvrir Unity. À utiliser après chaque modification d'un .cs, avant de dire « compilé », avant un commit, et quand l'utilisateur demande /unity-check ou si « ça compile ».
---

# unity-check — compiler sans Unity

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .claude/skills/unity-check/check.ps1
```

Options : `-ShowWarnings` liste les avertissements, `-Clean` repart d'un dossier de travail vide.

Le script compile des copies des csproj générés par Unity, dans `%TEMP%\untitledpool-unity-check`. Il ne modifie rien dans le dépôt. Il ajoute les scripts créés depuis la dernière génération par Unity et retire ceux qui ont été supprimés.

## Lire le résultat

| Code | Sens | Que faire |
|---|---|---|
| 0 | Tout compile | Écrire « compilé, non testé » |
| 1 | Erreurs de compilation, listées avec fichier et ligne | Les corriger puis relancer |
| 2 | Vérification impossible (csproj ou dotnet absent) | Le dire : le code reste « non compilé » |

- Le jeu est compilé d'abord. S'il échoue, les scripts d'éditeur ne sont pas vérifiés.
- Des erreurs dans un fichier qu'on n'a pas touché peuvent venir d'un paquet ajouté depuis la dernière génération des csproj : demander à l'utilisateur d'ouvrir Unity une fois, puis relancer.

## Limites à annoncer

- **Compilé ne veut pas dire testé.** Seul un essai dans Unity le confirme.
- Seuls `Assembly-CSharp` et `Assembly-CSharp-Editor` sont recompilés. Les autres assemblies viennent de `Library\ScriptAssemblies`, donc Unity doit avoir été ouvert au moins une fois sur ce poste.
- Les nouveaux scripts ne sont ajoutés que sous `Assets/Scripts`. Ailleurs, il faut que Unity ait régénéré les csproj.
- Les références cassées dans une scène ou un prefab, et les erreurs d'exécution, ne sont pas détectées.
