# UntitledPoolGame — notes pour Claude

Jeu de billard « friend slop » en vue FPS, Unity 6 (6000.6), URP, 2 joueurs en écran partagé local (en ligne plus tard). L'utilisateur échange en français.

## Où trouver quoi

- `TODO.md` — tâches (⬜ à faire · 🔄 en cours · ✅ fait) + notes techniques détaillées. **La version locale fait foi.**
- `CHANGELOG.md` — historique, plus récent en haut, une section `## AAAA-MM-JJ` par jour.
- `docs/` — site GitHub Pages (https://flackyboy.github.io/UntitledPoolgame/).
  - `docs/content/gdd.md` — GDD (statuts ✅ / 🔄 / 📌 par partie).
  - `docs/content/pistes.md` — idées non décidées (utilisateur) et suggestions de Claude, séparées.
  - `docs/content/tech/*.md` — documentation technique (architecture, séquences, config, plugins, rendu, performances, arborescence, annexes). **À relire avant de toucher à une partie du code, et à mettre à jour quand l'architecture change.**
  - La TODO et le changelog du site sont lus depuis `TODO.md`/`CHANGELOG.md` sur master : ne pas les recopier dans `docs/`.
  - `docs/ui.html` — maquettes d'UI à comparer ; l'utilisateur colle son récapitulatif de choix dans la conversation.
  - Page à venir : outil de niveau.

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

## Git

- `git` n'est pas dans le PATH : utiliser celui de GitHub Desktop, `C:\Users\Shadow\AppData\Local\GitHubDesktop\app-3.6.5\resources\app\git\cmd\git.exe`.
- Les très gros envois échouent (HTTP 500) : découper en plusieurs commits/push.
