# UntitledPoolGame — notes pour Claude

Jeu de billard « friend slop » en vue FPS, Unity 6 (6000.6), URP, 2 joueurs en écran partagé local (en ligne plus tard). L'utilisateur échange en français.

## Où trouver quoi

- `TODO.md` — tâches (⬜ à faire · 🔄 en cours · ✅ fait) + notes techniques détaillées. **La version locale fait foi.**
- `CHANGELOG.md` — historique, plus récent en haut, une section `## AAAA-MM-JJ` par jour.
- `docs/` — site GitHub Pages (https://flackyboy.github.io/UntitledPoolgame/).
  - `docs/content/gdd.md` — GDD (statuts ✅ / 🔄 / 📌 par partie).
  - `docs/content/controles.md` — récapitulatif des touches ; `docs/content/reglages-combat.md` — réglages de game design du combat (effet de chaque champ de l'Inspector). Onglets de la page TODO & GDD, **pas** dans la doc technique. À tenir à jour quand une touche ou un réglage change.
  - `docs/content/config.md` — page « Configuration » du site : tous les réglages (fichiers de Resources, pouvoirs, composants du joueur, touches, caméra, éditeur), où ils sont et comment les modifier. À tenir à jour quand un réglage est ajouté, renommé ou déplacé.
  - `docs/content/pistes.md` — idées non décidées (utilisateur) et suggestions de Claude, séparées.
  - `docs/content/tech/*.md` — documentation technique (architecture, séquences, config, plugins, rendu, performances, arborescence, annexes). **À relire avant de toucher à une partie du code, et à mettre à jour quand l'architecture change.**
  - La TODO et le changelog du site sont lus depuis `TODO.md`/`CHANGELOG.md` sur master : ne pas les recopier dans `docs/`.
  - `docs/ui.html` — galerie des concepts d'UI ; `docs/ui/*.html` = un prototype interactif par concept (moteur commun `docs/ui/proto.js` + `proto.css`). L'utilisateur veut une UI pop, fun, originale (pas une copie de ses références). Il colle son récapitulatif de choix dans la conversation.
  - Vérifier un rendu : Chrome sans interface (`chrome.exe --headless=new --screenshot=… "file:///…/docs/ui/x.html#/ecran"`) puis lire l'image.
  - `docs/content/level.md` — cahier des charges de l'outil de création de niveau (non implémenté, questions ouvertes en fin de page).

## Conventions de code

- **Un seul jeu de scripts joueur : les `Local*`** (`LocalFpsPlayerController`, `LocalPlayerHandController`, `LocalPoolAimController`, `LocalGrabbable`…). Les scripts online (`FpsPlayerController`, `PlayerHandController`, `Grabbable`, `PoolAimController`, `CharacterAnimationController`, `PoolPowerController`) sont **conservés mais figés** : ne rien y reporter. L'online viendra par-dessus les `Local*`.
- **Réglages de gameplay dans des ScriptableObjects** chargés depuis `Assets/Resources` via `PoolSettingsLoader.LoadOrDefault<T>()` (créés par **Tools > Pool > Ensure Config Assets Exist**).
- **Ne pas modifier les plugins tiers** (`Assets/Plugins/…`, FinalIK, PuppetMaster, Feel…) : adapter dans nos propres scripts (ex. `PickUpCue` reprend la logique de la démo `PickUp2Handed` au lieu d'en hériter).
- Namespaces `UntitledPoolGame.{Pool,Player,Interaction,Core}`. Commentaires en anglais, qui expliquent le pourquoi.
- Les noms de champs sérialisés conditionnent les valeurs Inspector : les garder lors d'un refacto.

## Façon de travailler

- Pas d'itération à l'aveugle sur des réglages visuels/IK : diagnostiquer (logs, lecture du code du plugin) avant de changer, et retirer tout de suite ce que l'utilisateur demande de retirer.
- Ne pas toucher à `Assets/Scenes/BarSplitscreen.unity` sans demande explicite (configuration de l'utilisateur) et ne pas le commiter.
- Je ne peux pas lancer Unity : le dire quand une modification n'est ni compilée ni testée.
- À chaque fonctionnalité ou correctif : mettre à jour `TODO.md` et `CHANGELOG.md` ; mettre à jour le GDD quand le statut d'une fonctionnalité change ; la doc technique quand une classe, un flux ou un réglage change ; ajouter aux pistes ce qui est seulement envisagé.
- Commit / push uniquement quand l'utilisateur le demande, en ne commitant que les fichiers concernés.
- Travail en équipe (organisation dans `docs/content/equipe.md`) :
  - branches, commits et Pull Requests : voir *Workflow git* ci-dessous ;
  - la relecture et l'accord de merge restent humains ;
  - ne pas modifier une scène partagée sans que l'utilisateur l'ait annoncée comme prise.

## Workflow git

La branche principale de ce dépôt est **`master`**. Chaque PR mergée dessus envoie un récap sur Discord (`.github/workflows/discord-pr-merged.yml`) : titre de la PR, sa description, puis chaque commit (titre en gras, description dessous). Ces textes doivent donc être lisibles.

- **Ne jamais committer directement sur `master`.** Toujours travailler sur une branche créée depuis `master` à jour, nommée `feature/<nom-court>`, `fix/<nom-court>` ou `chore/<nom-court>` (en minuscules, avec des tirets).
- **Chaque commit** :
  - un titre court (moins de 72 caractères), à l'impératif, en français : « Ajoute la pause en carte », « Corrige la caméra de chute » ;
  - une ligne vide ;
  - une description qui explique ce qui a changé et pourquoi.
- **Pas de commits « wip » ou « fix typo » isolés** : les regrouper avec le commit qu'ils corrigent avant d'ouvrir la PR.
- **Une PR par feature**, avec :
  - un titre clair, lisible par quelqu'un de non technique (c'est ce qui s'affiche dans Discord) ;
  - une description courte : ce que la feature apporte, en 2 à 5 puces.
- **Merger avec `gh pr merge --merge --delete-branch`** (pas de squash, pour garder le détail des commits), seulement après l'accord de l'utilisateur.

## Git

- Si `git` n'est pas dans le PATH, utiliser celui de GitHub Desktop : `%LOCALAPPDATA%\GitHubDesktop\app-<version>\resources\app\git\cmd\git.exe` (la version change avec les mises à jour ; sur l'ancien poste : `app-3.6.5`).
- Ne jamais afficher ni recopier un jeton d'accès GitHub (ni dans la doc, ni dans un commit).
- Ne pas pousser `Assets/Plugins/LeartesStudios` (pack de 4,7 Go avec des fichiers de plus de 100 Mo, refusés par GitHub, et contenu sous licence) : il se réimporte depuis le compte Asset Store / Fab.
- Les très gros envois échouent (HTTP 500) : découper en plusieurs commits/push.
