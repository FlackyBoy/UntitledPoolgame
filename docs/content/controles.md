# Contrôles

## Menu principal

Nouvelle partie · Charger · Just for fun (niveau puis mode, type et pouvoirs) · Multijoueur (local : les joueurs rejoignent, puis niveau, mode, type et pouvoirs ; en ligne : plus tard) · Réglages · Quitter.

| Clavier / souris | Manette | Action |
|---|---|---|
| Souris, flèches, Z Q S D | Stick gauche, croix | Viser une bille |
| Clic, Entrée, Espace | A / ✕ | Tirer (valider) |
| Échap, Retour arrière | B / ○ | Écran précédent |
| Tab | Y / △ | Bille de mode visée : chacun son tour ou Speed Pool (pas pour 9-ball ni 14.1) |
| P | X / □ | Avec ou sans pouvoirs (tous les modes) |
| Molette, - / + | LB / RB | 14.1 : changer le score cible |

## Écran de chargement

Quand le niveau est chargé, chaque joueur appuie sur **A / ✕** (manette) ou **Entrée / Espace / clic** (clavier) pour dire qu'il est prêt ; la partie démarre quand tout le monde l'est. Dans le menu, J1 est celui qui navigue ; en Multijoueur > Local, J2 rejoint en appuyant sur A / Start (ou Entrée si J1 joue à la manette).

## Manettes et clavier en partie

- **Seul à la manette** : prendre une manette libre (un stick ou un bouton autre que Start) la donne au premier joueur qui n'en a pas, donc J1. Le joueur seul reprend le clavier et la souris dès qu'on s'en sert (une touche ou un clic, pas un simple mouvement de souris).
- **Arrivée de J2 : Start** sur une manette libre, dans une scène lancée sans le menu (*Lancer sans le menu*, arène de R&D), même quand J1 joue au clavier. Quand tout le monde a déjà une manette, n'importe quel bouton d'une nouvelle manette fait aussi arriver un joueur. Jusqu'à *Max Player Count* du PlayerInputManager (2 s'il est illimité). Ce Start-là n'ouvre pas la pause.
- **Depuis le menu**, ce sont les appareils choisis dans le menu (J1 = celui qui navigue, J2 = celui qui a rejoint dans la salle d'attente) ; aucun joueur n'arrive plus par un bouton ensuite, une manette libre va au joueur qui n'en a pas. C'est aussi le cas d'un niveau lancé directement depuis l'éditeur, qui passe par le menu.
- **Clavier contre manette** : J1 au clavier, l'autre appuie sur Start ; ou choisir les deux appareils dans la salle d'attente du menu ; ou C + P en test.

## Pause (en partie)

| Clavier / souris | Manette | Action |
|---|---|---|
| Échap | Start | Ouvrir / fermer la pause (arrête les deux joueurs) |
| Flèches, Z Q S D, souris | Stick gauche, croix | Choisir Reprendre, Réglages ou Menu principal |
| Entrée, Espace, clic | A / ✕ | Valider |
| Échap, Retour arrière | B / ○ | Reprendre (ou revenir de la confirmation) |

## Réglages des touches

Menu principal > Réglages, ou Pause > Réglages. Entrée / A sur une case, puis appuyer sur la nouvelle touche (clavier ou souris dans la colonne de gauche, manette dans celle de droite) ; Échap annule. Une touche déjà prise par une autre action est échangée. Les choix sont gardés et valent pour les deux joueurs. Se déplacer, regarder et la pause ne se modifient pas ici.

## Fin de partie

| Clavier / souris | Manette | Action |
|---|---|---|
| Entrée, Espace, E, clic sur « Revanche » | A / ✕, Y / △ | Revanche (même mode) |

## Clavier / souris

| Touche | Action |
|---|---|
| Z Q S D / W A S D, flèches | Se déplacer · en visée : déplacer le point de frappe · vue de dessus (main libre) : déplacer la bille |
| Souris | Regarder · en visée : tourner autour de la bille · choix d'une poche (annonce de la 8, ou de la poche de la file en Snake Pool, pouvoir) : viser la poche avec le pointeur blanc |
| E | Ramasser / poser · entrer / sortir de la visée (près de la blanche) · valider la bille ou la poche visée |
| Clic gauche | Mains vides : **coup de poing** (spammer pour enchaîner ; en R&D : **poing gauche**) · objet en main : maintenir puis relâcher pour **lancer** · queue en main : maintenir puis relâcher pour un **coup de queue** · en visée : maintenir puis relâcher pour **tirer** |
| Entrée | Comme le clic gauche pour tirer, lancer et le coup de queue (pas le poing) |
| Clic droit | En R&D seulement : **poing droit** |
| F | **Coup de pied** · maintenir puis relâcher : coup de pied chargé (**spartiate** à 80 %) |
| Ctrl gauche (maintenir) | En R&D seulement : **garde** |
| Q | Utiliser le pouvoir (partie avec pouvoirs) · pouvoir qui vise un trou (Aimant, Poche fermée) : ouvre le choix du trou, Q à nouveau pour l'annuler, E pour valider le trou visé avec le pointeur blanc · pouvoir Trou : même chose, en visant un point de la table · au mauvais moment (pas son tour, billes qui roulent…), la pastille du pouvoir tremble et dit pourquoi |
| Molette de la souris | Avec deux pouvoirs en stock : choisir celui que Q lance |
| C + P (maintenir) | Test : faire rejoindre un joueur de plus (avec une manette libre s'il y en a une ; sinon il prend le clavier et la souris, et l'autre joueur attend sans bouger), dans toute scène avec un PlayerInputManager |
| Tab | Test, à plusieurs joueurs : passer le clavier et la souris au joueur suivant (celui qui les perd récupère la manette de l'autre, s'il en avait une). Aussi dans le menu debug, rubrique *Joueurs* |
| C + W | Test : aller directement à la fin d'une partie de 8-ball |
| Clic gauche maintenu + 4 clics droits rapides | Test : ouvrir / fermer le **menu debug** (donner un pouvoir, victoire / défaite, phase « empocher la 8 », fautes et erreurs de groupe) |

## Manette

| Bouton (Xbox / PlayStation) | Action |
|---|---|
| Stick gauche | Se déplacer · en visée : déplacer le point de frappe · vue de dessus (main libre) : déplacer la bille |
| Stick droit | Regarder · en visée : tourner autour de la bille · choix d'une poche (annonce de la 8, ou de la poche de la file en Snake Pool, pouvoir) : viser la poche avec le pointeur blanc |
| Y / △ | Ramasser / poser · entrer / sortir de la visée (près de la blanche) · valider la bille ou la poche visée |
| X / □ | Objet en main : maintenir puis relâcher pour **lancer** · queue en main : maintenir puis relâcher pour un **coup de queue** · en visée : maintenir puis relâcher pour **tirer** |
| B / ○ | Utiliser le pouvoir (partie avec pouvoirs) · pouvoir qui vise un trou (Aimant, Poche fermée) : ouvre le choix du trou, B / ○ à nouveau pour l'annuler, Y / △ pour valider le trou visé avec le pointeur blanc · pouvoir Trou : même chose, en visant un point de la table · au mauvais moment, la pastille du pouvoir tremble et dit pourquoi |
| D-pad gauche / droite | Avec deux pouvoirs en stock : choisir celui que B / ○ lance |
| LB / L1 | Mains vides : **coup de poing** (spammer pour enchaîner ; en R&D : **poing gauche**) |
| RB / R1 | En R&D seulement : **poing droit** |
| RT / R2 | **Coup de pied** · maintenir puis relâcher : coup de pied chargé (**spartiate** à 80 %) |
| LT / L2 (maintenir) | En R&D seulement : **garde** |

> A / ✕ est libre (prévu pour un futur saut). B / ○ porte encore l'action « Crouch », lue par aucun script.

## R&D déplacement (arène de test, variante du joueur)

Seulement avec le joueur de R&D (`New_PlayerVariant`, qui a le composant *Local Player Locomotion*), par exemple dans l'arène de test (*Tools > Pool > R&D > Créer l'arène de test*). Le joueur normal ne change pas.

| Clavier / souris | Manette | Action |
|---|---|---|
| Maj (maintenir) | L3 (maintenir) | Sprint, en avançant |
| Espace | A / ✕ | Sauter · face à un rebord entre 0,8 et 2,1 m : **l'escalader** |
| Clic gauche / clic droit | LB / RB | **Poing gauche / poing droit**, qui part à l'appui. L'autre main enchaîne pendant un coup. |
| F (maintenir) | RT | Coup de pied, spartiate chargé (inchangé) |
| Ctrl gauche (maintenir) | LT (maintenir) | **Garde** : bloque les coups de face à la tête et au corps. Pendant ce temps : plus lent, sans sprint, ni poing ni pied. Un coup de poing donné juste après un blocage est une **contre-attaque**, plus rapide et plus forte. Trop de coups bloqués : **garde brisée**, plus de garde pendant un moment et les coups comptent de nouveau normalement. Chaque coup coûte de l'**endurance** : vide, on ne peut plus frapper le temps qu'elle remonte. |
