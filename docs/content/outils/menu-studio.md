# Menu Studio

> **Tools > Pool > Menu Studio** : l'éditeur graphique des écrans des menus (accueil, sous-menus, réglages, pause, chargement). Chaque écran est un fichier `Assets/Resources/Menus/<écran>.asset` ; la liste des écrans et les couleurs, polices et textes communs sont dans [Réglages](config.html#config).

## La fenêtre
- **En haut** :
  - la liste des écrans ;
  - les boutons pour ajouter une **bille**, une **carte**, un **texte**, une **image**, un **panneau** ou une **pilule** ;
  - Dupliquer, Supprimer, Derrière / Devant ;
  - *▶ Rejouer les effets* ;
  - la grille ;
  - *Réinitialiser l'écran*.
- **À gauche** : la liste des éléments, de celui dessiné derrière à celui dessiné devant, plus les réglages de l'écran (⚙). Un ▸ marque les éléments qui ont une action.
- **Au centre** : l'écran, dessiné comme dans le jeu.
  - **Cliquer** sur un élément le choisit ; le **glisser** le place.
  - Le **coin jaune** change sa taille.
  - **Flèches** : déplacer d'un demi-pourcent (Maj : 2 %). **Suppr** : supprimer. **Ctrl+D** : dupliquer.
  - La grille aimante au demi-pourcent ; Maj en glissant pour s'en passer.
- **À droite** : toutes les propriétés de l'élément choisi. Chaque changement se voit tout de suite et s'annule avec Ctrl+Z.

## Brouillon

On travaille toujours sur une copie de l'écran. Rien ne change dans le jeu tant qu'on n'a pas cliqué sur **Enregistrer**.
- Dès qu'il y a une modification, un **bandeau orange** l'indique, avec *Enregistrer* et *Abandonner*. Le titre de la fenêtre prend une étoile.
- **Changer d'écran, fermer la fenêtre ou lancer le Play** avec un brouillon en cours ouvre une demande : enregistrer, abandonner, ou rester / garder le brouillon.
- **Le brouillon survit** au rechargement des scripts et au passage en Play. Pendant le Play, le jeu montre la version enregistrée.
- ***Réinitialiser l'écran*** et les séquences Feel créées passent aussi par le brouillon ; il faut enregistrer pour les garder.

## Les propriétés d'un élément

| Groupe | Ce qu'on règle |
|---|---|
| Place | centre (en % de l'écran), taille (en pixels d'un écran 1080p), inclinaison, visible ou non |
| Apparence | couleur (bille, fond de carte ou de panneau, teinte d'image), bille rayée, numéro, image, bordure, arrondi |
| Texte | texte et petite ligne (balises de texte animé acceptées), tailles, couleur, police titre, ombre, lettres qui arrivent une à une |
| Action | ce que fait le choix : ouvrir un écran, retour, emplacement de sauvegarde, Just for fun (1 joueur), Local (2 joueurs), En ligne, C'est parti, lancer un type de partie (avec ses règles), lancer la Boogie Night, réglages, quitter, et pour la pause : reprendre, menu principal, confirmer ; grossissement quand on vise |
| Effets | arrivée (pop, chute, montée, glissé de gauche ou de droite, fondu, tour), son délai et sa durée ; mouvement permanent (flotte, pulse, se balance, gigote, tourne), force et vitesse |
| Feel | séquence jouée quand l'élément est **visé**, et quand il est **choisi**. Le bouton crée une séquence vide dans `Assets/Feel/Sequences/` et la relie ; on la remplit dans l'Inspector de Feel (son, vibration…) |

## Les réglages d'un écran (⚙)

- le fond : le bar dessiné (avec ou sans lumières, avec ou sans table), une couleur, une image, ou un voile (pause) ;
- la bille blanche et la queue, avec leur position et leur taille ;
- l'écran ouvert par Retour ;
- une séquence Feel jouée à l'ouverture.

## Les éléments que le jeu remplit

Ils gardent leur nom ; on peut les déplacer, les redimensionner et changer leurs couleurs, mais pas les renommer :

| Nom | Écran | Contenu |
|---|---|---|
| `context` | Level, GameChoice, Mode, Boogie, Loading | l'étiquette jaune (2 joueurs · Le bar…) |
| `rounds` | Boogie | le nombre de parties de la Boogie Night |
| `levels` | Level | la rangée de cartes des niveaux, centrée sur la zone |
| `rules` | Mode | la carte des règles du type de partie visé |
| `slot1`, `slot2` et `slot1-label`, `slot2-label` | Lobby | les ronds des joueurs et leur texte |
| `aim-hint` | Title, Mode… | le texte qui clignote |
| `panel` | Settings | le cadre de l'écran des touches (place, taille, couleurs, titre) |
| `who` | Pause | qui a mis en pause (sa couleur suit le joueur) |
| `postcard`, `tip`, `status`, `ready`, `gutter`, `go` | Loading | la carte postale, l'astuce, le message, les joueurs prêts, la gouttière, « C'est parti ! » |

## Ordre des choix

Sur les écrans avec la queue, on vise la bille la plus proche dans la direction poussée. Dans la pause, on passe d'un choix à l'autre dans l'ordre de la liste.
