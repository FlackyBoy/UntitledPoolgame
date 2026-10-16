---
name: notion-todo
description: Synchronise TODO.md avec la base Notion « Tâches » de UntitledPoolGame — créer les tickets (descriptif + sous-tâches/lots + estimation Claude), démarrer une tâche (chrono), rattacher une PR, clôturer avec le temps réellement passé. À utiliser quand l'utilisateur demande /notion-todo, quand on commence une fonctionnalité de la TODO, à la création d'une PR et à son merge.
---

# notion-todo — TODO.md ↔ Notion

`TODO.md` **fait foi** (le site GitHub Pages le lit). Notion en est le miroir enrichi : descriptif, sous-tâches, estimation, temps passé, lien de PR. On n'édite jamais Notion sans refléter le changement dans `TODO.md`, et inversement.

Outils : le serveur MCP Notion (`notion-search`, `notion-fetch`, `notion-create-pages`, `notion-update-page`, `notion-create-database`…). S'ils ne sont pas chargés, le dire à l'utilisateur (reconnecter Notion ou ouvrir une nouvelle session) et s'arrêter : ne rien inventer.

La configuration (lien de la base, data source, page Pistes) est dans [config.md](config.md). Si elle est vide, faire d'abord `setup`.

## Règles permanentes (sans attendre qu'on lance le skill)

- **Toute ligne ajoutée à la TODO crée sa page Notion** dans la foulée (descriptif, détails techniques, « Comment tester », statut, rubrique, estimation ; sous-tâche reliée par `Parent` si c'est un lot), puis reçoit son commentaire `<!-- notion:… -->`.
- **Tout changement de statut dans la TODO est reporté dans Notion** au même moment, et inversement : ⬜ → 🔄 (`En cours`), PR ouverte (`En revue`), mergé non testé (`À tester`), validé en jeu (`Fait`). Un parent passe à `Fait` quand tous ses lots le sont.
- **Tâches et pistes sont séparées** : la base « Tâches » ne contient que ce qui est décidé et à faire. Une idée non décidée va dans `docs/content/pistes.md` (qui fait foi) et dans la page Notion **Pistes** (miroir). Une piste retenue devient une tâche et sort de la page Pistes.

## Schéma de la base « Tâches »

| Propriété | Type | Contenu |
|---|---|---|
| Nom | titre | Nom court de la tâche, comme dans la TODO |
| Statut | statut | `À faire` · `En cours` · `En revue` · `À tester` · `Fait` |
| Rubrique | sélection | Pouvoirs, Règles/HUD, Combat, Caméra, Level Maker, Menus, Game feel, Joueur, Online, Outils, Doc, Dimensions |
| Sous-tâches | relation (même base) | Synchronisée avec Parent, ne pas la remplir à la main |
| Parent | relation (même base) | La fonctionnalité dont c'est un lot ; activer les sous-éléments Notion |
| Estimation Claude (h) | nombre | Voir le barème plus bas |
| Temps Claude (h) | nombre | Mesuré par les hooks, jamais estimé de tête |
| PR | URL | Lien de la Pull Request GitHub |
| Branche | texte | `feature/…`, `fix/…`, `chore/…` |

Correspondance avec la TODO : ⬜ = `À faire` · 🔄 = `En cours` (ou `En revue` si une PR est ouverte) · ligne dans « À tester en jeu » = `À tester` · ✅ = `Fait`.

**Lien TODO → Notion** : en fin de ligne de la TODO, un commentaire HTML invisible sur le site : `<!-- notion:<page-id> -->`. C'est la seule clé fiable (le texte des lignes change souvent). Toujours le chercher avant de créer un ticket, pour ne pas faire de doublon.

## Corps d'une page

1. **Descriptif** : ce que la fonctionnalité apporte au joueur et pourquoi, 2 à 5 phrases, sans jargon (relu par des non-techniciens).
2. **Détails techniques** : classes, réglages, fichiers concernés — repris des notes de la TODO et de `docs/content/tech/`.
3. **Comment tester** : ce que l'utilisateur doit vérifier dans Unity, en cases à cocher (`- [ ]`). Obligatoire sur chaque page, lots compris ; pour une tâche encore à cadrer, écrire « À définir une fois … décidé ».

Une tâche `À tester` (déjà faite, temps non mesuré) n'a pas d'estimation.

Les **sous-tâches** sont des pages enfants reliées par `Parent`, une par lot cohérent (ex. Level Maker → Lot 1 … Lot 6), chacune avec son propre descriptif, statut et estimation. Ne pas descendre plus bas qu'un lot livrable en une PR.

## Barème d'estimation (temps de travail de Claude)

Temps de Claude seul : lecture du code, écriture, compile-check, doc, TODO/CHANGELOG. **Hors** tests dans Unity (Claude ne peut pas lancer Unity) et hors attente des réponses de l'utilisateur.

| Taille | Heures | Exemple |
|---|---|---|
| XS | 0,25 | Réglage, texte, petite correction ciblée |
| S | 0,5 | Correctif dans un seul script, une option d'Inspector |
| M | 1,5 | Fonctionnalité sur 2-4 scripts + doc |
| L | 4 | Nouveau système (pouvoir complet, outil d'éditeur) |
| XL | > 6 | **À découper en sous-tâches**, chacune estimée |

Le total d'un parent = somme de ses sous-tâches. Quand le temps réel dépasse l'estimation de plus de 50 %, le signaler à l'utilisateur à la clôture (et ce qui a coûté), pour caler les estimations suivantes.

## Actions

### `setup` (une fois)
1. Demander à l'utilisateur sous quelle page Notion créer la base (ou l'URL d'une base existante).
2. Créer la base avec le schéma ci-dessus, puis écrire son URL et son data source ID dans [config.md](config.md).

### `sync` — TODO → Notion
1. Lire `TODO.md`. Ne prendre que les lignes ⬜ / 🔄 et la section « À tester en jeu » (pas l'historique ✅, sauf demande).
2. Regrouper : une fonctionnalité = une page ; ses lots / sous-points = sous-tâches.
3. Pour chaque ligne sans `<!-- notion:… -->` : créer la page (descriptif, statut, rubrique, estimation), puis ajouter le commentaire dans la TODO.
4. Pour les lignes déjà liées : mettre à jour le statut s'il a changé.
5. Montrer à l'utilisateur la liste de ce qui sera créé **avant** de créer en masse (plus de 10 pages).
6. Les lignes qui sont des idées (pas des tâches décidées) sont liées à la page Pistes (`<!-- notion:<id de la page Pistes> -->`) ; mettre la page Pistes à jour depuis `docs/content/pistes.md` si celui-ci a changé.

### `start <tâche>`
1. Trouver la page (commentaire dans la TODO, sinon recherche Notion ; la créer si elle n'existe pas).
2. Statut → `En cours`, renseigner la Branche. Passer la ligne de la TODO à 🔄.
3. Démarrer le chrono : `bash .claude/hooks/time-task.sh <page-id>`. Il note l'heure du changement de tâche dans `.claude/time/log` ; les hooks `UserPromptSubmit`/`Stop` y notent le début et la fin de chaque tour. Le temps d'un tour va à la tâche active à chaque instant : une tâche démarrée en plein tour compte à partir de là. Ne pas écrire `current-task` à la main (le changement ne serait pas daté).

### `pr`
À la création de la PR (`gh pr create`) :
1. Ajouter dans la description de la PR une ligne `Notion : <url de la page>`.
2. Sur la page : `PR` = URL de la PR, Statut → `En revue`.

### `done`
Au merge de la PR (`gh pr merge`), ou quand l'utilisateur le demande :
1. Temps : `bash .claude/hooks/time-total.sh <page-id>` → écrire dans `Temps Claude (h)` (ajouter à la valeur existante si la tâche a été reprise dans plusieurs sessions et que le log a été vidé).
2. Statut → `À tester` si la fonctionnalité n'a pas encore été essayée dans Unity (cas général), `Fait` si l'utilisateur l'a validée en jeu. Refléter dans la TODO (section « À tester en jeu » ou ✅).
3. Si toutes les sous-tâches d'un parent sont `Fait`, passer le parent à `Fait`.
4. Arrêter le chrono : `bash .claude/hooks/time-task.sh` (sans argument).
5. Résumer à l'utilisateur : estimation vs temps réel.

Plusieurs tâches en parallèle : `current-task` n'en suit qu'une ; changer de tâche = refaire `start` (le chrono de la précédente s'arrête de lui-même).
