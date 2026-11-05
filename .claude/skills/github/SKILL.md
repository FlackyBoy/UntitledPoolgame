---
name: github
description: Tout le parcours Git et GitHub du projet — état du dépôt, branche, intégration de master, commit (avec la passe de validation et la doc), Pull Request, merge. À utiliser dès qu'il faut committer, pousser, ouvrir ou merger une PR, ou quand l'utilisateur demande /github, sans jamais agir sans sa demande.
---

# github — de la branche au merge

`/github <etat|branche|sync|commit|pr|merge>`. Le Discord de l'équipe reçoit le titre et la description des PR et des commits : ils doivent être lisibles par tous.

## Règles qui valent pour chaque action

- **Chaque étape a besoin de son accord.** Une demande de commit ne vaut pas accord pour pousser, une PR ouverte ne vaut pas accord pour merger.
- **Jamais de `git add -A` ni de `git add .`** : ajouter les fichiers un par un.
- **Jamais sur `master`** : ni commit, ni push direct.
- Regarder la branche réelle avant d'agir (`git branch --show-current`) : l'utilisateur en change en parallèle dans GitHub Desktop.
- Ne jamais résoudre soi-même un conflit sur un `.unity`, un `.prefab` ou un `.asset` : montrer les deux versions et demander.
- Ne jamais afficher de jeton d'accès.
- Si `git` manque du PATH : `%LOCALAPPDATA%\GitHubDesktop\app-<version>\resources\app\git\cmd\git.exe`.

## `etat`

```bash
bash .claude/skills/github/triage.sh          # --list pour voir chaque fichier
gh pr list --state open
```

Le script ne modifie rien et classe les fichiers :

| Groupe | Contenu | Traitement |
|---|---|---|
| **JAMAIS** | `BarSplitscreen.unity`, `LeartesStudios`, csproj | Ne jamais committer, même sur demande générale. `BarSplitscreen` seulement si l'utilisateur le nomme. |
| **BRUIT** | `.meta` de `.unitypackage` supprimés, `Assets/_Recovery` | Ignorer. Les paquets ne sont pas versionnés (`.gitignore`) : Unity semble retirer ces `.meta` orphelins. |
| **À CONFIRMER** | Plugins et packs, `ProjectSettings`, `Packages`, scènes | Demander, sauf si la tâche les a clairement modifiés. Vérifier qu'une scène partagée a été annoncée comme prise. |
| **À TRIER** | Le reste | Rattacher chaque fichier à une tâche. Plusieurs tâches = plusieurs commits. |

Il signale aussi une `.meta` sans son fichier (ou l'inverse), un fichier de plus de 100 Mo, et un asset modifié qui utilise un clip PROTOFACTOR exclu du `.gitignore`. Les clips nommés seulement dans un script lui échappent. Un nouveau clip utilisé s'ajoute à la liste du `.gitignore`.

## `branche`

1. Lancer `etat`. Si des fichiers sont modifiés, ne rien écraser : les laisser suivre seulement si l'utilisateur est d'accord, sinon lui demander quoi en faire.
2. `git fetch origin`, puis `git switch -c <type>/<nom-court> origin/master`, avec `feature/`, `fix/` ou `chore/`, en minuscules et avec des tirets.
3. Si l'arbre contient un autre chantier, travailler à côté sans y toucher : `git worktree add ../UPG-<nom-court> -b <type>/<nom-court> origin/master`. Ce dossier n'a ni `Library` ni csproj : `unity-check` n'y fonctionne qu'après une ouverture dans Unity.

## `sync`

Intégrer `origin/master` par un merge, jamais par un rebase de ce qui est déjà poussé. Lancer `unity-check` après. En cas de conflit de texte (TODO, CHANGELOG, code), résoudre en gardant les deux côtés puis relire le résultat. Pour les conflits d'assets, voir les règles plus haut.

## `commit`

1. **Tri** : lancer `etat` et arrêter la liste exacte des fichiers. Une modification sans rapport reste hors du commit, avec `git add -p` si besoin dans un fichier de texte.
2. **Passe sur le diff** :
   - validation : bugs, cas oubliés, deux joueurs et manettes, modes concernés ; lancer `unity-check` si un `.cs` a changé ;
   - refacto : code mort, doublons, style du code autour ;
   - optimisation : pas d'allocation ni de `GetComponent` ou de `Find` dans les boucles par image ;
   - règles du projet : scripts online figés, plugins tiers intacts, noms de champs sérialisés conservés (sinon `[FormerlySerializedAs]`), valeurs d'enum sérialisées jamais réordonnées, réglages dans des ScriptableObjects.
3. **Suivi** : vérifier selon ce qui a changé `TODO.md`, `CHANGELOG.md`, le GDD, `config.md`, `controles.md`, `outils/`, `tech/`.
4. **Annonce** : dire à l'utilisateur ce que la passe a changé, et ce qui reste compilé mais non testé.
5. **Message** : titre de moins de 72 caractères, à l'impératif, en français (« Ajoute la pause en carte ») ; ligne vide ; ce qui a changé et pourquoi, sans jargon inutile. Pas de « wip » ni de « fix typo » isolés : les regrouper avant la PR. Terminer par les lignes d'attribution indiquées dans la conversation.

## `pr`

1. Sur l'accord de l'utilisateur, pousser avec `git push -u origin <branche>`. Si l'envoi est très gros (HTTP 500), le découper en plusieurs commits et plusieurs push.
2. `gh pr create --base master` avec :
   - un titre clair pour quelqu'un de non technique ;
   - 2 à 5 puces sur ce que la feature apporte ;
   - ce qui reste à tester, s'il y en a ;
   - les lignes d'attribution indiquées dans la conversation.
3. Lancer `notion-todo pr`. Si Notion échoue (espace plein), le signaler sans bloquer la PR.
4. Donner le lien de la PR.

Une PR par feature. Si la branche en contient plusieurs, le dire avant d'ouvrir.

## `merge`

1. Vérifier `gh pr view <n> --json mergeable,reviewDecision,statusCheckRollup`, et que `master` n'a pas divergé au point de faire un `sync` d'abord.
2. **Demander l'accord de l'utilisateur pour cette PR**, en rappelant ce qui n'a pas été testé.
3. `gh pr merge <n> --merge --delete-branch` : pas de squash, le Discord montre le détail des commits.
4. Lancer `notion-todo done`. Une fonctionnalité non essayée passe dans « À tester en jeu », pas en ✅.
5. Revenir sur `master` à jour seulement si l'arbre de travail le permet sans rien perdre.
