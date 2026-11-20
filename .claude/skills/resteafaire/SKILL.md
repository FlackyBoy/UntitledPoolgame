---
name: resteafaire
description: Fait le point sur ce qui reste à faire, par chantier — features, évolutions, correctifs, tests en attente —, à partir de TODO.md, du GDD et des pistes. Sans argument, affiche tout de suite l'ensemble des chantiers et des pistes avec leur nombre d'entrées ; avec un chantier (ou « pistes »), le détaille. À utiliser quand l'utilisateur demande /resteafaire, « qu'est-ce qui reste à faire », « où en est tel chantier », ou veut choisir sur quoi travailler.
argument-hint: "[modes|pouvoirs|billard|joueur|camera|combat|menus|game-feel|level-maker|dimensions|online|equipe|pistes|tout|correctifs]"
---

# resteafaire — ce qui reste, chantier par chantier

`/resteafaire` affiche d'emblée tous les chantiers et toutes les pistes. `/resteafaire <catégorie>` détaille un chantier, `/resteafaire pistes` toutes les idées non décidées. Ce skill **ne modifie rien** : il lit et résume.

## Les catégories

C'est Claude qui propose les noms : l'utilisateur n'a pas à les connaître. Les noms ci-dessous sont ceux du script ; accepter aussi un nom approchant (« camera » ou « caméra », « level » pour `level-maker`, « ui » pour `menus`, « bagarre » pour `combat`…).

| Catégorie | Ce qu'elle couvre |
|---|---|
| `modes` | Modes de jeu et leurs lots (Play Anywhere, Hungry Hole, Power Wash, Catch the Hole, Shooter…), type de partie, Boogie Night |
| `pouvoirs` | Pouvoirs, caisses, jetons, répartition aléatoire, pouvoirs par mode |
| `billard` | Visée, tir, distance de jeu, règles de base, blanche, craie |
| `joueur` | Animations, IK, prise de la queue, objets tenus, ragdoll, manette |
| `camera` | Caméra de jeu, de placement, en écran partagé |
| `combat` | Bagarre, coups, jauge d'encaissement, PNJ |
| `menus` | Menus, HUD, interface, TextFx, borne d'arcade, réglages en jeu |
| `game-feel` | Juice, Feel, halos, secousses, combos |
| `level-maker` | Créateur de niveau, étages, kits, génération procédurale |
| `dimensions` | Système de dimensions, environnements, règles loufoques |
| `online` | Jeu en ligne, réseau |
| `equipe` | Skills, organisation du dépôt à deux |

Mots-clés spéciaux :
- `pistes` : toutes les idées de `docs/content/pistes.md`, par section (pistes de l'utilisateur, puis suggestions de Claude), présentées comme non décidées ;
- `tout` : la vue d'ensemble, comme sans argument, mais avec les 2 ou 3 tâches les plus importantes de chaque catégorie ;
- `correctifs` : seulement la section « Correctifs à faire » de la TODO, toutes catégories confondues ;
- `a-tester` : renvoyer vers `/a-tester`, qui fait déjà ce travail.

Si le nom ne correspond à rien, ne pas deviner en silence : proposer les deux catégories les plus proches.

## Sans argument : tous les chantiers et toutes les pistes, tout de suite

Ne rien demander à l'utilisateur : afficher directement la vue d'ensemble.

1. Lancer le script. Il compte les lignes ouvertes (⬜ et 🔄) de `TODO.md` par chantier (hors « Notes techniques » et « Fait »), puis les entrées de chaque section de `pistes.md` :

   ```bash
   bash .claude/skills/resteafaire/chantiers.sh
   ```

2. Présenter deux tableaux courts :
   - **Chantiers** : nom à taper, nombre de tâches ouvertes, et en quelques mots ce qui domine (« surtout des lots de modes en cours », « 5 points à tester »). Les compteurs se recoupent (une tâche peut être dans deux chantiers) : ne pas les additionner.
   - **Pistes (rien n'est décidé)** : chaque section de `pistes.md`, avec son nombre d'idées, en séparant les pistes de l'utilisateur et les suggestions de Claude.
3. Finir par une suggestion : le ou les chantiers où avancer en premier, et pourquoi (beaucoup de « compilé, non testé », un lot qui en bloque d'autres…), puis rappeler `/resteafaire <chantier>` et `/resteafaire pistes` pour le détail.

## `pistes` : toutes les idées non décidées

`bash .claude/skills/resteafaire/chantiers.sh pistes` donne chaque idée avec sa ligne dans `pistes.md`. Les présenter par section, en une ligne chacune, avec `pistes.md:<ligne>`. Signaler celles qui sont déjà implémentées (la ligne le dit souvent) ou déjà passées dans la TODO. Ne jamais en faire une tâche : si l'utilisateur en retient une, elle va dans la TODO.

## Avec une catégorie : détailler le chantier

1. **Extraire** les lignes de la catégorie, avec leur numéro dans la TODO :

   ```bash
   bash .claude/skills/resteafaire/chantiers.sh <categorie>
   ```

   Le tri par mots-clés n'est qu'un premier tri. **Relire chaque ligne dans `TODO.md`** (lire autour du numéro donné, pas le fichier entier) pour retirer ce qui n'a rien à voir et repérer ce qui manque : chercher aussi avec Grep les mots de la catégorie dans la TODO.

2. **Dédoublonner** : une même tâche apparaît souvent dans « À tester en jeu », dans un résumé (« Outils : reste à faire ») et dans sa ligne détaillée. Le même titre trahit le doublon : n'en garder qu'une, la plus détaillée.

3. **Compléter**, sans mélanger avec la TODO :
   - le GDD (`docs/content/gdd.md`) : les parties 📌 (prévu) ou 🔄 de la catégorie ;
   - les pistes (`docs/content/pistes.md`) : ce qui est seulement envisagé, à présenter à part, jamais comme une tâche décidée.

4. **Présenter**, en français, lisible par quelqu'un de non technique :

   ```
   ## Modes — 9 chantiers ouverts

   ### En cours
   - Play Anywhere (prototype) — reste : lot 5 pouvoirs, lot 6 trous au hasard + menu
   - Hungry Hole — lot 1 en cours ; reste : lot 2 tour par tour, lot 3 choix des versions

   ### À tester (codé, jamais essayé)
   - Pocket Size — TODO.md:102

   ### Pas commencé
   - Snake Pool : polish (à définir avec l'utilisateur)

   ### Envisagé seulement (pistes, non décidé)
   - Mode 4 joueurs en écran scindé

   Suggestion : finir Hungry Hole lot 1 avant d'ouvrir un nouveau mode.
   ```

   - Un chantier par ligne, avec ses lots restants dessous plutôt que chaque sous-ligne à plat.
   - Le statut vient de la TODO, pas d'une impression : « compilé, non testé » va dans « À tester », même si la ligne est 🔄.
   - Donner la référence `TODO.md:<ligne>` pour pouvoir y aller.
   - Signaler ce qui bloque (« attend le système de dimensions », « tâche de l'utilisateur »), et ce qui semble dépassé ou fait sans que la TODO le dise, sous forme de question.
   - Pas d'estimation de temps inventée : seulement celles écrites dans la TODO.

5. Finir par une suggestion de prochaine étape.

## Entretien

Un nouveau chantier qui ne tombe dans aucune catégorie apparaît dans `autre` : ajouter ses mots-clés à la catégorie qui lui va dans `chantiers.sh`, ou créer une catégorie si plusieurs tâches le justifient, et mettre à jour ce tableau et la section du site (`docs/content/outils/skills.md#resteafaire`).
