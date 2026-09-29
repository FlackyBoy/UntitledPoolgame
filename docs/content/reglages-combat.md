# Combat : comportement et réglages

> Les coups entre joueurs : **coup de queue** (`LocalCueMelee`) et **mains nues** — poing et pied (`LocalUnarmedMelee`). Pour chaque coup : ce qui se passe, dans l'ordre, puis chaque réglage de l'Inspector avec son effet concret. Valeurs par défaut au 29/09/2026 ; le survol d'un réglage dans l'Inspector en donne aussi la description.
>
> ⚠️ Un composant déjà placé dans une scène ou un prefab **garde ses anciennes valeurs** quand on change les valeurs par défaut dans le code : *clic droit > Reset* sur le composant pour les reprendre.

Touches : voir l'onglet [Contrôles](gdd.html#controles).

## Principes communs

- **Tout est procédural** : aucune animation de coup. Juste avant que FinalIK résolve le corps (`solver.OnPreUpdate`, après l'Animator), le script déplace une cible (la queue, le poing ou le pied), tourne le buste et penche le corps ; l'IK fait suivre les bras et les jambes. Les cibles sont recalculées à chaque image depuis la caméra et le squelette animé : un coup suit le regard et la marche.
- **Appui bref / maintenu** : maintenir le bouton **charge** le coup (jauge provisoire dans la moitié d'écran du joueur), le relâcher **frappe**. La **charge** (0 → 1) fait monter la force.
- **Touche** : pendant la frappe, une capsule suit la queue, le poing ou le pied, testée plusieurs fois par image (*Sub Steps*) pour qu'un coup rapide ne traverse pas la cible. Seuls les muscles du ragdoll d'**un autre joueur** comptent (couche `Ragdoll`), une seule fois par coup.
- **Effet sur la cible** : `MuscleCollisionBroadcaster.Hit(unpin, force, point)` de PuppetMaster.
  - **Unpin** (déséquilibre) : à quel point le muscle touché et ses voisins sont « lâchés ». Repère : **5 suffit en général à faire tomber**.
  - **Force** : poussée en newtons sur le muscle touché, dans le sens du coup, un peu relevée (*Upward Bias*).
  - En dessous du seuil de chute, la cible **titube** puis se rééquilibre ; au-dessus, elle tombe en **ragdoll** puis se relève.
  - Certains coups font tomber **à coup sûr** (`BehaviourPuppet.SetState(Unpinned)`), quelle que soit la résistance de la cible.
- **Impact** : secousse d'écran de l'attaquant, **hitstop** (micro-ralenti **global** : les deux moitiés d'écran le ressentent).
- **Exclusions** : pas de coup en visée ou en placement de bille ; un seul coup à la fois (queue, poing et pied s'excluent) ; pas d'entrée en visée pendant un coup ; rien pendant qu'on est au sol.
- **Debug Logs** (coché par défaut) : chaque coup, chaque touche (muscle, unpin, force, chute forcée) et chaque raté dans la console.

## Coup de queue — « armer en tournant »

**Conditions** : queue en main (prise terminée), hors visée. Bouton **Attaque** (clic gauche / X □).

### Déroulé

1. **Appui** : le jeu retient l'orientation de la vue.
2. **Charge** (bouton maintenu) : la **première rotation nette** de la vue choisit le coup, qui ne change plus ensuite :

   | Rotation pendant la charge | La queue s'arme… | Coup |
   |---|---|---|
   | vers la droite | à droite | **balayage vers la gauche** |
   | vers la gauche | à gauche | **balayage vers la droite** |
   | vers le haut | en haut | **coup vertical** |
   | aucune (ou vers le bas) | en position de tir | **estoc de la pointe** |

   La queue s'arme de plus en plus loin avec la charge : ce qu'on voit est le coup qui va partir. Le corps se penche en arrière.
3. **Relâchement** : la **vue revient** vers là où on regardait à l'appui — ou vers l'**adversaire** le plus proche dans un cône autour de cette direction (assistance légère) — pendant que la queue part. Le coup retombe donc sur la cible.
4. **Frappe** : arc (ou estoc) qui accélère, buste qui tourne ou se plie avec le coup, corps jeté en avant ; courte **pause** en fin de geste, puis **retour** à la pose de port.

### Réglages

**Direction**

| Réglage | Défaut | Effet |
|---|---|---|
| Turn Threshold | 12° | Rotation qu'il faut faire pour choisir un balayage ou un vertical. ↑ = moins de coups choisis par erreur, mais il faut tourner plus ; ↓ = plus réactif, plus d'estocs ratés en balayage. |
| Thrust Stance Delay | 0,2 s | Temps sans rotation avant que la queue passe en position de tir. ↓ = l'estoc se prépare plus tôt (mais un balayage décidé tard « saute » plus). |
| Pose Blend Time | 0,1 s | Fondu quand la queue passe de la position de tir à un balayage. |

**Retour de la vue et assistance**

| Réglage | Défaut | Effet |
|---|---|---|
| Return View On Strike | 1 | 1 = la vue revient complètement vers la cible ; 0 = elle ne bouge pas (le coup part alors là où l'on regarde au relâchement). |
| Max View Return | 120° | Limite de rotation imposée à la vue pendant la frappe. ↓ si le retour donne le tournis. |
| Assist Angle | 40° | Demi-angle du cône dans lequel un adversaire attire la vue. 0 = pas d'assistance. |
| Assist Range | 3 m | Distance maximale de l'adversaire visé. |

**Charge et arcs**

| Réglage | Défaut | Effet |
|---|---|---|
| Charge Time | 0,8 s | Temps pour atteindre la charge maximale. |
| Min Windup | 0,45 | Part de l'armé déjà là sans charge : un coup bref arme quand même la queue à 45 %. |
| Shoulder Pivot | 1 | Centre de rotation de la queue : 1 = les épaules (grands gestes, les bras travaillent), 0 = entre les mains (seule la pointe bouge). |
| Sweep Windup / Follow Through | 100° / 110° | Balayages : armé à pleine charge / angle atteint de l'autre côté. ↑ = coups plus amples. |
| Overhead Windup / Follow Through | 95° / 70° | Coup vertical : armé vers le haut / angle atteint vers le bas. |

**Estoc (position de tir)**

| Réglage | Défaut | Effet |
|---|---|---|
| Thrust Pull Back | 0,25 m | Recul de la queue le long de son axe à pleine charge. |
| Thrust Reach | 0,5 m | Avancée de la queue à la frappe : la portée de l'estoc. |
| Stance Side / Height / Forward | 0,12 / −0,25 / 0,15 m | Place du point entre les mains par rapport aux épaules, dans le repère de la caméra (droite / hauteur / devant). C'est ce qui donne la « pose de tir ». |
| Stance Blend Time | 0,15 s | Temps pour passer de la pose de port à la position de tir. |

**Corps**

| Réglage | Défaut | Effet |
|---|---|---|
| Sweep Torso Twist | 0,4 | Part de l'angle du balayage reprise par le buste. ↑ = le buste tourne plus, coup plus « engagé ». |
| Overhead Torso Bend | 0,25 | Idem pour le coup vertical (le buste se plie). |
| Windup Lean Back | 0,1 m | Recul du corps à pleine charge. |
| Strike Lean | 0,18 m | Le corps se jette dans le coup à la frappe. |

**Rythme**

| Réglage | Défaut | Effet |
|---|---|---|
| Strike Time | 0,13 s | Durée de la frappe. ↓ = plus sec et plus rapide. |
| Follow Through Hold | 0,08 s | Pause en fin de geste : donne du poids au coup. |
| Recover Time | 0,3 s | Retour à la pose de port. |
| Cooldown | 0,25 s | Délai minimal avant le coup suivant. |

**Touche et force**

| Réglage | Défaut | Effet |
|---|---|---|
| Muscle Layers | Ragdoll | Couches des muscles des ragdolls. |
| Hit Radius | 0,2 m | Épaisseur de la zone de touche le long de la queue (volontairement large). |
| Sub Steps | 4 | Tests de touche par image. |
| Min / Max Unpin | 1 / 8 | Déséquilibre sans charge / à pleine charge (entre les deux selon la charge). |
| Min / Max Force | 400 / 2500 N | Poussée sans charge / à pleine charge. |
| Guaranteed Knockdown At | 0,9 | À partir de 90 % de charge, la cible tombe à coup sûr. |
| Upward Bias | 0,15 | Part de la force vers le haut (soulève la cible). |
| Hitstop Duration / Time Scale | 0,06 s / 0,05 | Micro-ralenti à l'impact (0 = désactivé). |

## Mains nues — poing et pied

**Conditions** : hors visée, pas pendant un coup de queue. **Poing** : clic gauche / RB, **mains vides** seulement (avec la queue, le clic gauche reste le coup de queue ; avec un objet, le lancer). **Pied** : clic droit / RT, à tout moment, même queue en main.

### Taille des membres

Les distances du poing et du pied sont écrites **pour un bras de 0,6 m et une jambe de 0,9 m**, puis **mises à l'échelle du personnage** : à chaque coup, le script mesure le bras (épaule → coude → poignet) ou la jambe (hanche → genou → cheville) sur le squelette. Un personnage aux bras deux fois plus courts a donc des distances deux fois plus courtes. Aucune cible n'est placée plus loin que *Max Limb Extension* × la longueur du membre, pour que le bras ou la jambe ne se verrouille jamais tendu (c'était le cas avec des distances fixes sur le personnage cartoon). Avec *Debug Logs*, la console affiche à chaque coup la longueur mesurée et le facteur appliqué.

| Réglage | Défaut | Effet |
|---|---|---|
| Reference Arm Length | 0,6 m | Bras pour lequel les distances du poing sont écrites. ↑ = tous les gestes du poing plus petits, ↓ = plus grands. |
| Reference Leg Length | 0,9 m | Idem pour le pied. |
| Max Limb Extension | 0,95 | Distance maximale d'une cible, en part de la longueur du membre (1 = membre tendu à fond). |

### Appui bref ou chargé

- **Poing** : pas de charge. Le poing part dès qu'il est en garde (**Windup Time**, 0,08 s), bouton maintenu ou non. *(Le crochet chargé a été retiré le 29/09 après test.)*
- **Pied** : relâché en moins de **Tap Time** (0,18 s) → **coup simple**, immédiat ; maintenu plus longtemps → la charge monte pendant **Charge Time** (0,7 s) → **coup chargé** au relâchement. **Windup Time** : temps pour lever le genou.

### Poing — crochet

1. **Départ (garde de boxe)** : le poing monte du bras pendant jusqu'à côté de la mâchoire.
2. **Frappe** : le poing passe **un peu sur le côté**, coude plié à l'horizontale, jusqu'au point de mi-course, puis **revient vers l'avant** (courbe de Bézier : départ → mi-course → arrivée). À l'arrivée, les deux poings sont écartés de **Fist Spread × la largeur d'épaules** : à 1, chaque poing est droit devant son épaule (bras parallèles). Le coude est tiré vers l'extérieur, le buste tourne dans le coup, le corps s'engage.
   - Le trajet est posé dans le **repère horizontal du corps** (avant = direction du regard à plat, haut = vertical) : regarder vers le bas ne fait pas plonger le coup. Le regard ne fait que remonter ou baisser un peu la hauteur (*Punch Pitch Follow*).
   - **Régler à l'œil** : en jeu, sélectionner le joueur et regarder la vue *Scene* pendant un coup : le trajet est dessiné en jaune (départ, mi-course, arrivée) et la cible du poing en rouge.
3. **Enchaînement (spam)** : un clic pendant un coup lance le suivant avec **l'autre main**, sans attendre le retour ; cliqué avant que le poing n'arrive, le suivant part dès qu'il touche. Le bras précédent se relâche tout seul. Les mains alternent toujours.
4. Un coup de poing fait **tituber**, il ne fait pas tomber à lui seul.

```text
        vue de dessus (les deux poings à l'arrivée)

   3. arrivée   ●<── Fist Spread × largeur d'épaules ──>●   (Punch Reach devant les épaules, End Height)
                 \
                  \   ← le poing revient vers l'avant
                   ● 2. mi-course : un peu sur le côté (Mid Outward), coude plié,
                   |                avancé de Mid Forward × Punch Reach (Mid Height)
   1. départ     ●
   (garde)       |  ← poing levé à côté de la mâchoire (Start Forward / Height / Outward)
          [épaule droite]           [épaule gauche]
```

Les points 1 et 2 sont mesurés depuis l'**épaule qui frappe**, le point 3 depuis le milieu des épaules, dans le repère horizontal du corps, en mètres pour un bras de référence (voir *Taille des membres*). Pendant l'armé, le poing monte du bras pendant jusqu'à la garde.

| Réglage | Défaut | Effet |
|---|---|---|
| Start Forward | 0,12 m | 1. Départ (garde) : distance devant l'épaule. |
| Start Height | 0,08 m | 1. Départ : hauteur par rapport à l'épaule (~0,1 = à hauteur de mâchoire). |
| Start Outward | 0,05 m | 1. Départ : décalage vers l'extérieur de l'épaule. **Négatif = vers le centre** : le poing part alors de devant la poitrine. |
| Mid Forward | 0,5 | 2. Mi-course : avancée, en part de la portée. ↓ = le poing reste sur le côté plus longtemps (crochet plus large) ; ↑ = coup plus direct. |
| Mid Outward | 0,2 m | 2. Mi-course : écart vers l'extérieur. **Trop grand = le bras se tend sur le côté** (élévation latérale) au lieu de rester plié. |
| Mid Height | 0,06 m | 2. Mi-course : hauteur par rapport à l'épaule. |
| Punch Reach | 0,6 m | 3. Arrivée : distance devant les épaules. |
| End Height | 0 m | 3. Arrivée : hauteur par rapport à l'épaule. |
| **Fist Spread** | 1,1 | 3. Arrivée : **écart entre les deux poings bras tendus**, en part de la largeur d'épaules (mesurée sur le squelette, pas mise à l'échelle). 1 = chaque poing droit devant son épaule, bras parallèles ; < 1 = les poings se rapprochent du centre (0 = les deux au milieu, bras en diagonale) ; > 1 = plus écartés que les épaules. La console (*Debug Logs*) affiche la largeur d'épaules mesurée et l'écart obtenu en mètres. |
| Punch Pitch Follow | 0,3 | Part du regard haut/bas reprise par la hauteur du coup. 0 = toujours à hauteur d'épaule ; 1 = suit complètement le regard (regarder un adversaire proche vers le bas fait alors frapper au ventre). |
| Elbow Out | 0,6 | Force avec laquelle le coude est tiré sur le côté, à hauteur d'épaule (bras plié à l'horizontale, comme un vrai crochet ; 0 = libre). |
| Fist Align | 1 | Orientation du poing : 1 = jointures vers la cible, dos de la main vers le haut (paume vers le bas) ; 0 = orientation de l'animation (la main part alors vers l'extérieur quand le bras se lève). L'orientation de la main est mesurée sur le squelette au début du coup (doigts dans le prolongement de l'avant-bras, paume vers la cuisse). |
| Fist Roll | 0° | Rotation du poignet en plus, autour de la direction du coup, en miroir pour les deux mains : 0 = paume vers le bas ; 90 = poing vertical, paume vers l'intérieur. À ajuster si le poing reste mal tourné. |
| Jab Torso Twist | 12° | Rotation du buste dans le coup. |
| Punch Lean | 0,12 m | Engagement du corps (40 % de cette valeur dans le coup). |
| Jab Strike Time | 0,09 s | Durée de la frappe. |
| Hold Time | 0,05 s | Pause bras tendu. |
| Recover Time | 0,2 s | Retour — aussi la durée du relâchement du bras précédent pendant un enchaînement. |
| Cooldown | 0,05 s | Délai après un coup terminé (l'enchaînement, lui, n'attend pas). |
| Fist Radius | 0,15 m | Zone de touche autour du poing. |
| Jab Unpin / Force | 1,5 / 500 N | Déséquilibre et force d'un coup de poing : titube. |

### Pied — coup de pied et coup de pied spartiate

1. **Genou levé** : le pied monte devant la hanche, d'autant plus haut que la charge monte ; le buste recule pour l'équilibre.
2. **Frappe** : la jambe se tend droit devant (dans l'axe horizontal du regard, la hauteur suit en partie le regard), **semelle face à la cible, orteils vers le haut**, genou vers l'avant, avec un **pas en avant** ; pause jambe tendue, puis retour.
3. **Coup simple** : une poussée (titube).
4. **Chargé à 80 % ou plus → coup de pied spartiate** (*300*, AC Odyssey ; jauge dorée « SPARTE ! ») :
   - la cible tombe à coup sûr ;
   - **tout son ragdoll** est projeté vers l'arrière (vitesse donnée à chaque muscle, un pas physique après la chute pour ne pas être freinée) ;
   - **ralenti** global ;
   - **zoom** de la caméra de l'attaquant pendant le ralenti.

| Réglage | Défaut | Effet |
|---|---|---|
| Kick Reach | 0,95 m | Distance du pied devant la hanche, jambe tendue. ↑ = plus loin (au-delà de la longueur de jambe, c'est le pas en avant qui compense). |
| Kick Height | 0,1 m | Hauteur du pied par rapport à la hanche, jambe tendue (positif = au-dessus). |
| Kick Pitch Follow | 0,4 | Part du regard haut/bas reprise par la hauteur du coup (0 = hauteur fixe) : regarder plus haut vise plus haut. |
| Chamber Forward / Height | 0,12 / −0,2 m | Genou levé : position du pied par rapport à la hanche. |
| Chamber Raise | 0,12 m | Hauteur ajoutée au pied à pleine charge (genou encore plus haut). |
| Knee Forward | 0,6 | Force avec laquelle le genou est tiré vers l'avant (0 = libre). |
| Foot Sole Forward | 1 | 1 = semelle face à la cible, orteils vers le haut ; 0 = orientation de l'animation. |
| Kick Lean Back | 0,25 m | Recul du buste pendant le coup de pied. |
| Kick Lunge / Spartan Lunge | 0,2 / 0,55 m | Pas en avant pendant la frappe, simple / pleine charge. |
| Kick Strike Time / Charged Kick Strike Time | 0,12 / 0,15 s | Durée de la frappe. |
| Kick Hold Time | 0,12 s | Pause jambe tendue. |
| Foot Radius | 0,2 m | Zone de touche autour du pied. |
| Kick Unpin / Force | 2,5 / 900 N | Coup simple. |
| Charged Kick Unpin / Force | 8 / 2500 N | Pleine charge. |
| Spartan Min Charge | 0,8 | Charge à partir de laquelle le coup devient spartiate. |
| Launch Speed / Launch Lift | 9 / 2,5 m/s | Vitesse de projection vers l'arrière / vers le haut. ↑ = la cible vole plus loin. |
| Slow Mo Duration / Time Scale | 0,45 s / 0,2 | Ralenti du spartiate (temps réel ; les deux joueurs). |
| Slow Mo Zoom | 12° | Champ de vision retiré à la caméra de l'attaquant pendant le ralenti. |
| Hitstop Duration / Time Scale | 0,05 s / 0,05 | Micro-ralenti des autres coups. |

## Mise en place dans l'éditeur

- **Local Cue Melee** et **Local Unarmed Melee** sur la racine du prefab joueur.
- Os du ragdoll sur la couche `Ragdoll` (sinon renseigner *Muscle Layers*).
- *View Transform*, *Full Body IK* et *Fps Camera* sont trouvés automatiquement ; les renseigner si ce n'est pas le bon objet (la caméra Cinemachine FPS est cherchée par son nom : « FPS »).

## Limites connues

- Tout est **non calibré** : valeurs par défaut à ajuster en jeu.
- L'`InteractionSystem` (prise de la queue et des objets) utilise aussi les effecteurs des mains : le poing est coupé si on ramasse quelque chose pendant le coup.
- Le pied d'appui n'est pas stabilisé (pas de Grounder) : léger glissement possible pendant le coup de pied.
- Hitstop et ralenti sont **globaux** (les deux joueurs).
- À venir : une **jauge d'encaissement** (les coups reçus la remplissent ; pleine, le joueur tombe) — voir la TODO.
