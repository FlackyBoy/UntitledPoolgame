# Réglages combat

> Paramètres des composants de combat, dans l'ordre de l'Inspector. Un composant déjà placé garde ses valeurs quand les valeurs par défaut changent (*clic droit > Reset* pour les reprendre). Unpin = déséquilibre PuppetMaster (repère : 5 fait généralement tomber).

## Coup de queue — Local Cue Melee

### Références

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Action Map Name | Action map du joueur dans l'asset d'input | Player |
| Attack Action Name | Action qui charge (maintenir) et déclenche (relâcher) le coup | Attack |
| View Transform | Caméra du joueur, dont le coup suit le repère (vide = trouvée seule) | — |
| Full Body IK | IK du personnage (vide = trouvé seul ; sans lui, le buste ne suit pas) | — |

### Direction (rotation pendant la charge)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Turn Threshold | Rotation à faire pendant la charge pour choisir le coup : droite = balayage vers la gauche, gauche = vers la droite, haut = vertical ; en dessous = estoc | 12° |
| Thrust Stance Delay | Temps sans rotation avant que la queue passe en position de tir (estoc) | 0,2 s |
| Pose Blend Time | Fondu quand la pose change (position de tir → balayage) | 0,1 s |

### Retour de la vue à la frappe

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Return View On Strike | Retour de la vue vers la cible à la frappe : 1 = complet, 0 = la vue ne bouge pas | 1 |
| Max View Return | Rotation maximale imposée à la vue | 120° |
| Assist Angle | Demi-angle du cône dans lequel un adversaire attire la vue (0 = pas d'assistance) | 40° |
| Assist Range | Distance maximale de l'adversaire visé | 3 m |

### Charge et arcs

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Charge Time | Temps de maintien pour la charge maximale | 0,8 s |
| Min Windup | Part de l'armé déjà là sans charge | 0,45 |
| Shoulder Pivot | Centre de rotation de la queue : 1 = épaules (grands gestes), 0 = entre les mains | 1 |
| Sweep Windup | Balayages : angle d'armé à pleine charge | 100° |
| Sweep Follow Through | Balayages : angle atteint de l'autre côté | 110° |
| Overhead Windup | Vertical : angle d'armé vers le haut | 95° |
| Overhead Follow Through | Vertical : angle atteint vers le bas | 70° |

### Estoc (position de tir)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Thrust Pull Back | Recul de la queue le long de son axe à pleine charge | 0,25 m |
| Thrust Reach | Avancée de la queue à la frappe (portée de l'estoc) | 0,5 m |
| Stance Side | Position de tir : décalage des mains à droite des épaules | 0,12 m |
| Stance Height | Position de tir : hauteur des mains par rapport aux épaules | −0,25 m |
| Stance Forward | Position de tir : distance des mains devant les épaules | 0,15 m |
| Stance Blend Time | Temps pour passer en position de tir | 0,15 s |

### Corps

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Sweep Torso Twist | Part de l'angle du balayage reprise par le buste | 0,4 |
| Overhead Torso Bend | Part de l'angle du vertical reprise par le buste (il se plie) | 0,25 |
| Windup Lean Back | Recul du corps à pleine charge | 0,1 m |
| Strike Lean | Engagement du corps dans le coup | 0,18 m |

### Rythme

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Strike Time | Durée de la frappe | 0,13 s |
| Follow Through Hold | Pause en fin de geste | 0,08 s |
| Recover Time | Retour à la pose de port | 0,3 s |
| Cooldown | Délai minimal entre deux coups | 0,25 s |

### Touche et force

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Muscle Layers | Couches des muscles des ragdolls (vide = Ragdoll) | Ragdoll |
| Hit Radius | Épaisseur de la zone de touche le long de la queue | 0,2 m |
| Sub Steps | Tests de touche par image (évite de traverser un bras) | 4 |
| Min Unpin / Max Unpin | Déséquilibre sans charge / à pleine charge | 1 / 8 |
| Min Force / Max Force | Force sans charge / à pleine charge | 400 / 2500 N |
| Guaranteed Knockdown At | Charge à partir de laquelle la cible tombe à coup sûr | 0,9 |
| Upward Bias | Part de la force vers le haut | 0,15 |
| Hitstop Duration | Micro-ralenti à l'impact, pour les deux joueurs (0 = désactivé) | 0,06 s |
| Hitstop Time Scale | Vitesse du temps pendant ce ralenti | 0,05 |
| Show Charge Indicator | Jauge et flèche de direction provisoires | oui |
| Debug Logs | Chaque coup et chaque touche dans la console | oui |

## Poing et pied — Local Unarmed Melee

### Références

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Action Map Name | Action map du joueur | Player |
| Punch Action Name | Action du coup de poing | Punch |
| Kick Action Name | Action du coup de pied | Kick |
| View Transform | Caméra du joueur (vide = trouvée seule) | — |
| Full Body IK | IK du personnage, obligatoire (vide = trouvé seul) | — |
| Fps Camera | Caméra Cinemachine FPS pour le zoom du spartiate (vide = celle dont le nom contient « FPS ») | — |

### Taille des membres

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Reference Arm Length | Bras pour lequel les distances du poing sont écrites ; elles sont mises à l'échelle du bras réel (↑ = gestes plus petits) | 0,6 m |
| Reference Leg Length | Idem pour la jambe et le pied | 0,9 m |
| Max Limb Extension | Distance maximale d'une cible, en part de la longueur du membre (1 = tendu à fond) | 0,95 |

### Appui

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Tap Time | Pied : relâché avant ce délai = coup simple, au-delà = chargé | 0,18 s |
| Charge Time | Pied : temps de charge jusqu'au maximum | 0,7 s |
| Windup Time | Temps pour mettre le poing en garde / lever le genou ; le poing part ensuite tout seul | 0,08 s |

### Poing

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Start Forward | Départ (garde) : distance du poing devant l'épaule | 0,12 m |
| Start Height | Départ : hauteur du poing par rapport à l'épaule | 0,08 m |
| Start Outward | Départ : décalage vers l'extérieur (négatif = devant la poitrine) | 0,05 m |
| Mid Forward | Mi-course : avancée en part de la portée (↓ = crochet plus large) | 0,5 |
| Mid Outward | Mi-course : écart vers l'extérieur (trop grand = bras tendu sur le côté) | 0,2 m |
| Mid Height | Mi-course : hauteur par rapport à l'épaule | 0,06 m |
| Punch Reach | Arrivée : distance du poing devant les épaules | 0,6 m |
| End Height | Arrivée : hauteur par rapport à l'épaule | 0 m |
| Fist Spread | Arrivée : écart entre les deux poings en largeurs d'épaules (1 = bras parallèles, < 1 = vers le centre) | 1,1 |
| Punch Pitch Follow | Part du regard haut/bas reprise par la hauteur du coup | 0,3 |
| Elbow Out | Force qui écarte le coude sur le côté | 0,6 |
| Fist Align | Orientation du poing : 1 = jointures vers la cible, paume vers le bas ; 0 = celle de l'animation | 1 |
| Fist Roll | Rotation du poignet en plus (90 = poing vertical) | 0° |
| Jab Torso Twist | Rotation du buste dans le coup | 12° |
| Shoulder Reach | Avancée de l'épaule qui frappe au bout du coup (allonge) ; le poing va plus loin d'autant (0 = l'épaule ne bouge pas) | 0,1 m |
| Punch Lean | Engagement du corps | 0,12 m |

### Pied

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Kick Reach | Distance du pied devant la hanche, jambe tendue | 0,95 m |
| Kick Height | Hauteur du pied par rapport à la hanche, jambe tendue | 0,1 m |
| Kick Pitch Follow | Part du regard haut/bas reprise par la hauteur du coup | 0,4 |
| Chamber Forward | Genou levé : distance du pied devant la hanche | 0,12 m |
| Chamber Height | Genou levé : hauteur du pied par rapport à la hanche | −0,2 m |
| Chamber Raise | Genou levé : hauteur ajoutée à pleine charge | 0,12 m |
| Knee Forward | Force qui tire le genou vers l'avant | 0,6 |
| Foot Sole Forward | 1 = semelle face à la cible, orteils vers le haut ; 0 = orientation de l'animation | 1 |
| Kick Lean Back | Recul du buste pendant le coup de pied | 0,25 m |
| Kick Lunge | Pas en avant pendant un coup de pied simple | 0,2 m |
| Spartan Lunge | Pas en avant à pleine charge | 0,55 m |

### Rythme

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Jab Strike Time | Durée de la frappe du poing | 0,09 s |
| Kick Strike Time | Durée de la frappe d'un coup de pied simple | 0,12 s |
| Charged Kick Strike Time | Durée de la frappe d'un coup de pied chargé | 0,15 s |
| Hold Time | Pause bras tendu | 0,05 s |
| Kick Hold Time | Pause jambe tendue | 0,12 s |
| Recover Time | Retour à la pose normale (et relâchement du bras précédent quand on enchaîne) | 0,2 s |
| Cooldown | Délai après un coup terminé (l'enchaînement des poings n'attend pas) | 0,05 s |

### Touche

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Muscle Layers | Couches des muscles des ragdolls (vide = Ragdoll) | Ragdoll |
| Fist Radius | Zone de touche autour du poing | 0,15 m |
| Foot Radius | Zone de touche autour du pied | 0,2 m |
| Sub Steps | Tests de touche par image | 3 |

### Force

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Jab Unpin | Déséquilibre d'un coup de poing | 1,5 |
| Jab Force | Force d'un coup de poing | 500 N |
| Kick Unpin | Déséquilibre d'un coup de pied simple | 2,5 |
| Kick Force | Force d'un coup de pied simple | 900 N |
| Charged Kick Unpin | Déséquilibre d'un coup de pied à pleine charge | 8 |
| Charged Kick Force | Force d'un coup de pied à pleine charge | 2500 N |
| Upward Bias | Part de la force vers le haut | 0,15 |

### Coup de pied spartiate (pied chargé)

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Spartan Min Charge | Charge à partir de laquelle le coup de pied devient spartiate (chute garantie, projection, zoom) | 0,8 |
| Launch Speed | Vitesse de projection de tout le corps de la cible vers l'arrière | 9 m/s |
| Launch Lift | Vitesse vers le haut ajoutée à la projection | 2,5 m/s |
| Slow Mo Zoom | Champ de vision retiré à la caméra de l'attaquant au contact (0 = pas de zoom). Le nom date de l'ancien ralenti, retiré | 12° |
| Spartan Zoom Duration | Durée du zoom, retour compris | 0,5 s |

### Divers

| Paramètre | Ce qu'il fait | Défaut |
|---|---|---|
| Hitstop Duration | Micro-ralenti à l'impact de chaque coup, spartiate compris (0 = désactivé) | 0,05 s |
| Hitstop Time Scale | Vitesse du temps pendant ce ralenti | 0,05 |
| Show Charge Indicator | Jauge de charge provisoire du coup de pied | oui |
| Debug Logs | Coups, touches, longueurs de membres mesurées dans la console | oui |

> En jeu, joueur sélectionné, la vue *Scene* dessine le trajet du poing (jaune) et sa cible (rouge) pendant un coup.
