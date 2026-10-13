# Architecture et diagrammes de classes

> État du code au 29/09/2026 (`Assets/Scripts`). Seuls les scripts `Local*` sont maintenus ; les scripts online (`FpsPlayerController`, `PlayerHandController`, `Grabbable`, `PoolAimController`, `CharacterAnimationController`, `PoolPowerController`) sont conservés mais figés et n'apparaissent pas ici.

## Vue d'ensemble

Le code est découpé en quatre espaces de noms (namespaces) :

| Namespace | Rôle | Scripts principaux |
|---|---|---|
| `UntitledPoolGame.Player` | Déplacement, caméra, animation, ragdoll du joueur | `LocalFpsPlayerController`, `LocalCharacterAnimationController`, `LocalPlayerRagdollController`, `RagdollHitRelay`, `LocalCharacterModelFollow` |
| `UntitledPoolGame.Interaction` | Ramasser, porter, lancer ; la queue ; les coups (queue, poing, pied) | `LocalPlayerHandController`, `LocalGrabbable`, `Cue`, `LocalCuePickupTrigger`, `PickUpCue`, `CueChargeSlide`, `LocalCueMelee`, `LocalUnarmedMelee`, `MeleeHit` |
| `UntitledPoolGame.Pool` | Billard : billes, table, règles, visée, pouvoirs, réglages | `PoolBall`, `PoolPocket`, `PoolTableSurface`, `PoolMatchRules`, `IPoolRuleSet` et ses règles, `LocalPoolAimController`, `PoolPower` et ses pouvoirs, config ScriptableObjects |
| `UntitledPoolGame.Core` | Outils transverses et triches de test | `PhysicsLayerSetup`, `SplitScreenCheatSpawner`, `EightBallEndgameCheat`, `RagdollDummySpawner`, `ScenePlayerSpawner`, `NetworkBootstrap` |

Un outil d'éditeur, `PoolTableBuilder` (namespace `UntitledPoolGame.PoolEditor`), génère la table, les billes, les poches, les queues et les points d'apparition des pouvoirs. Le **Level Maker** (`LevelMakerWindow`, même namespace, Tools > Pool > Level Maker) monte un niveau jouable par étapes en s'appuyant dessus (`AttachPhysicsTo`, `AddPowerSpawnSystemTo`) et vérifie la scène (`LevelValidator`) ; voir la page [Level Maker](outils.html#level-maker).

### Principes

- **Un composant = une responsabilité** : le joueur est un assemblage de composants (`LocalFpsPlayerController` pour bouger, `LocalPlayerHandController` pour tenir, `LocalPoolAimController` pour viser…) qui se désactivent entre eux selon l'état (visée, ragdoll).
- **Singletons de scène** pour ce qui est unique : `PoolMatchRules.Instance`, `PoolTableSurface.Instance`, `PoolPowerCrateManager.Instance`.
- **Registres statiques** plutôt que des recherches coûteuses : `PoolBall.Active`, `PoolPocket.Active`.
- **Événements statiques** pour découpler : `PoolBall.Pocketed`, `PoolBall.CueBallFirstContact`, `PoolMatchRules.Fouled`, `PoolMatchRules.PowerGranted`.
- **Données dans des ScriptableObjects** : réglages (physique, effets, pouvoirs) et chaque pouvoir est un asset (voir *Configuration → rendu*).
- **Stratégie pour les règles** : `PoolMatchRules` délègue la résolution d'un tir à une implémentation de `IPoolRuleSet` choisie au lancement de la partie.

## Joueur

```mermaid
classDiagram
  direction LR
  class LocalFpsPlayerController {
    +float SensitivityMultiplier
    +bool InvertLook
    -HandleLook()
    -HandleMove()
  }
  class LocalPlayerHandController {
    +LocalGrabbable HeldObject
    +CanUseCueNow() bool
    +ForceDrop()
    +NotifyExternallyHeld(LocalGrabbable)
    -TryPickUp()
    -Drop()
    -Throw()
  }
  class LocalPoolAimController {
    +bool IsAiming
    +bool IsPlacementViewActive
    +float ChargeFraction
    +float AimReachExtra
    +bool IsOutOfReach
    +WantsInteractThisFrame(LocalGrabbable) bool
  }
  class LocalCuePickupTrigger {
    +TryStartPickup() bool
    +TryReleaseIfCue(LocalGrabbable) bool
  }
  class LocalCharacterAnimationController
  class LocalPlayerRagdollController {
    +OnKnockedDown()
    +OnRegainBalance()
    +NotifyCollision()
  }
  class LocalPoolPowerController
  class LocalPoolPowerEffectReceiver {
    +PlayShotFeedback()
  }
  class PlayerInput
  class CharacterController

  LocalPlayerHandController --> LocalPoolAimController : lui cède Interact
  LocalPlayerHandController --> LocalCuePickupTrigger : ramassage de la queue
  LocalPoolAimController --> LocalPlayerHandController : queue tenue
  LocalPoolAimController --> LocalFpsPlayerController : désactive en visée
  LocalCharacterAnimationController --> LocalPlayerHandController : lit HeldObject
  LocalCharacterAnimationController --> LocalPoolAimController : lit IsAiming
  LocalPlayerRagdollController --> LocalPlayerHandController : ForceDrop
  LocalPlayerRagdollController --> LocalFpsPlayerController : désactive
  LocalPlayerRagdollController --> LocalPoolAimController : désactive
  LocalPoolPowerEffectReceiver --> LocalFpsPlayerController : sensibilité, inversion
  LocalFpsPlayerController --> CharacterController
  LocalFpsPlayerController --> PlayerInput
```

**R&D déplacement** (branche `feature/deplacements-combat`) : `LocalPlayerLocomotion`, présent seulement sur la variante `New_PlayerVariant`, prend le déplacement à `LocalFpsPlayerController`, qui ne garde alors que le regard.
- **Mouvement** : élan, sprint, saut, escalade des rebords (détection par rayons : mur devant, dessus plat à bonne hauteur, place pour se tenir debout), avec les paramètres d'animation `Jump` et `JumpLeg`.
- **Corps mou** : il règle les `props.muscleWeight` / `pinWeight` des muscles PuppetMaster des bras, des mains et du buste quand les mains sont libres, et revient aux valeurs d'origine sinon. Il ajoute aussi un décalage du bassin FBBIK (`bodyEffector.positionOffset`) contre l'accélération, et pose les mains sur le rebord pendant l'escalade.
- **Vue** : décalage et roulis du pivot de caméra, ajoutés en `LateUpdate` puis retirés. Réglages : `MovementSettings`.

Tous ces composants sont sur la racine du prefab joueur. `PlayerInput` (Input System) fournit à chacun les actions de **ce** joueur : c'est ce qui permet deux joueurs sur le même PC.

## Queue et objets tenus

```mermaid
classDiagram
  direction LR
  class LocalGrabbable {
    +bool IsHeld
    +PickUp(Transform)
    +Drop()
    +Throw(Vector3)
    +MarkExternallyHeld(bool)
  }
  class Cue
  class PickUpCue {
    +InteractionSystem InteractionSystem
    +bool IsBusy
    +ReleaseCue()
  }
  class CueChargeSlide {
    +Vector3 TipAxis
    +float TipDistance
  }
  class InteractionSystem {
    <<FinalIK>>
  }
  class InteractionObject {
    <<FinalIK>>
  }
  class LocalCuePickupTrigger
  class LocalCueMelee {
    +bool IsSwinging
    -BeginSwing()
    -DetectHits()
  }
  class LocalUnarmedMelee {
    +bool IsAttacking
    -Begin(Punch | Kick)
    -ApplyBody()
    -Launch()
  }
  class MeleeHit {
    <<static>>
    +TryGetTarget()
    +Hitstop()
  }
  class MuscleCollisionBroadcaster {
    <<PuppetMaster>>
    +Hit(unPin, force, position)
  }

  LocalCueMelee --> PickUpCue : IsSettled
  LocalCueMelee ..> MeleeHit
  LocalUnarmedMelee ..> MeleeHit
  MeleeHit ..> MuscleCollisionBroadcaster : muscle d'un autre joueur
  Cue ..> LocalGrabbable : marque « c'est la queue »
  LocalCuePickupTrigger --> PickUpCue
  LocalCuePickupTrigger --> LocalGrabbable : MarkExternallyHeld
  PickUpCue --> InteractionSystem : écoute Start / Pause / Resume
  PickUpCue --> InteractionObject : la queue
  CueChargeSlide --> LocalPoolAimController : lit ChargeFraction, AimMeshReach
```

- Les objets ordinaires passent par `LocalObjectHands` (ajouté automatiquement au joueur) : la main droite va chercher l'objet, un objet léger est attaché à la main droite et lancé par-dessus l'épaule, un objet lourd est tenu à deux mains et lancé depuis au-dessus de la tête ; l'objet quitte la main en cours de geste (`LocalGrabbable.ReleaseFromHand`). Composant désactivé : ancien chemin `LocalGrabbable.PickUp/Drop/Throw` (objet collé devant le joueur).
- **Nouvelle prise procédurale (en test)** : `LocalCueHolder`, sur le joueur, prend la main sur la queue tant qu'il est présent et actif (le décocher = retour à l'ancienne prise ci-dessous, intacte). Les queues n'ont pas de propriétaire : chaque `Cue` s'inscrit dans `Cue.All` tant qu'elle est active (placée dans le niveau ou apparue en cours de partie) et expose ce qu'il faut pour la prendre (objet ramassable, glissement, prises des deux mains). On peut ramasser à tout moment, seul le tir dépend du tour. Il cherche la queue libre à portée que le joueur regarde, calcule deux prises autour du point de la queue le plus proche d'un point confortable devant la poitrine (main droite à droite), oriente les mains à partir des cibles de port de la queue (ses `InteractionTarget`), plie les genoux (pieds épinglés, effecteur du corps abaissé), puis amène la queue en pose de port (`PickUpCue.HoldPoint` / `CarryRotation`) en faisant glisser les mains jusqu'aux prises de port. `LocalPlayerHandController.CueBusy` / `CueSettled` exposent l'état de la prise, quel que soit le système. `FrontHandRelease` (0 → 1) fait lâcher la queue à la main avant (côté pointe). `LocalPoolAimController` s'en sert quand la bille est hors de portée : le corps se redresse, puis la main reprend la queue dès que le coup redevient possible.
- **Portée en visée** (`LocalPoolAimController`) :
  - Le corps se place sur la ligne de tir, juste hors de la table (*Table Clearance Margin* depuis le bord du tapis).
  - La distance restante est couverte successivement par les bras (*Max Hand Stretch*), le buste penché (*Max Body Lean*), la queue qui glisse dans les mains, puis le coup allongé (*Max Stretch Reach*).
  - Dans le coup allongé, le bassin monte sur la bande, le buste se couche, le pied d'appui est tenu par l'IK et l'autre jambe se lève. Le bassin est retenu au bord du tapis, gardé au-dessus de la table, et l'écart est compensé par la flexion du buste. La visée tremble un peu.
  - Au-delà : « trop loin », et la main avant lâche la queue.
- L'ancienne prise passe par **FinalIK InteractionSystem** : les mains vont chercher des cibles placées sur la queue, puis `PickUpCue` l'attache au personnage. `PickUpCue` règle aussi le poids des bend goals des bras : 0 sans la queue, fondu jusqu'à 1 quand elle est tenue (écrit seulement pendant le fondu). `LocalGrabbable` ne sert alors qu'à tenir l'état « tenu » à jour.
- `LocalCueMelee` gère le coup de queue « armer en tournant » : charge tant qu'Attaque est maintenue, direction donnée par la rotation faite pendant la charge, arc posé dans le repère de la caméra, vue ramenée vers la cible à la frappe (`LocalFpsPlayerController.AddLook`, assistance vers l'adversaire le plus proche dans un cône) ; la queue tenue pivote autour du point entre les deux prises (pour l'estoc, elle s'aligne sur le regard en position de tir puis avance le long de son axe : la pointe frappe) et les muscles des autres ragdolls touchés reçoivent `MuscleCollisionBroadcaster.Hit`.
- `LocalUnarmedMelee` gère le poing (mains vides, part au clic, sans charge) et le pied (clic bref = coup simple, maintenu = coup chargé). Les distances sont mises à l'échelle de la longueur réelle du bras / de la jambe, et la main comme le pied sont orientés (jointures, semelle vers la cible). Juste avant la résolution FBBIK, il tire l'effecteur de la main ou du pied droit vers la garde puis le long de la frappe (cibles recalculées depuis l'épaule ou la hanche animée et la caméra) : crochet en courbe de Bézier pour le poing avec le coude écarté, pied tendu semelle face à la cible avec le genou vers l'avant (bend goals FBBIK créés à l'exécution, réglages d'origine des chaînes remis ensuite). Il tourne aussi le buste, penche le corps et, pour le poing, avance l'épaule qui frappe (effecteur d'épaule). Les poings s'enchaînent : le bras précédent se relâche seul pendant que l'autre frappe. Le pied chargé (coup de pied spartiate) met la cible en ragdoll, donne une vitesse à tous ses muscles et zoome la caméra Cinemachine FPS de l'attaquant (plus de ralenti depuis le 30/09). `MeleeHit` regroupe ce qui est commun aux coups (identifier le muscle et le joueur touchés, hitstop).
  - **R&D *Boxing Punches*** (case cochée sur la variante) : un bouton par main (`Punch`, `PunchRight`), et les durées, la forme, le corps et la force du poing pris dans `CombatSettings`.
    - **Trajectoire** : le point de contrôle de la courbe de Bézier est ramené vers le milieu de la ligne garde → arrivée (*Hook Amount*), et le coude vers le bas.
    - **Courbe** : la frappe suit une courbe en cube (l'essentiel du trajet dans le premier tiers).
    - **Vue** : au départ du poing, `LocalPlayerLocomotion.KickView` fait rouler et avancer brièvement le pivot de caméra.
    - **Bras** : `LocalPlayerLocomotion` remet les muscles des bras à leur rigidité d'origine dès qu'un coup est en cours ou que la garde est levée (`IsGuarding`), sans transition, pour que PuppetMaster ne montre pas un bras en retard sur l'IK.
    - **Garde** : avant la résolution FBBIK, chaque main qui ne frappe pas est tenue près du menton par son effecteur, avec un bend goal de coude vers le bas. La main qui finit un coup est confiée à la garde (`HandToGuard`) au lieu d'être relâchée, et le retour se fait le long de la trajectoire jusqu'à la garde.
    - **Poignet** : la rotation du poing ajoute *Corkscrew* × (1 − avancée).
    - **Doigts** : après la résolution (`solver.OnPostUpdate`), les phalanges des mains fermées tournent vers la paume, autour de l'axe os × normale de la paume (la normale est calculée depuis les os de l'index, du majeur et de l'auriculaire de l'Animator humanoïde).
    - **Coups animés** (`LocalUnarmedMelee.Animated.cs`, classe partielle ; `CombatSettings.useAnimations` et calque présent dans le contrôleur) : pas d'IK pour le poing. Le code pilote un calque Animator « R&D Combat » (masque haut du corps, override, poids 0 par défaut, états `Empty` / `Guard` / `PunchL` / `PunchR` sans transitions, vitesse par le paramètre `PunchSpeed`).
      - **Pilotage** : `CrossFadeInFixedTime` et `SetLayerWeight` (1 pendant un coup, poids de la garde sinon). `phase` reste `Idle` ; c'est `animPunching` qui compte dans `IsAttacking`.
      - **Touches** : balayage `DetectHits` entre deux positions successives de l'os de la main (`GetBoneTransform`), quand le temps normalisé de l'état est dans *Hit Window*.
      - **Enchaînement** : l'autre main à partir de *Chain From* ; retour vers `Guard` à *Return At*.
      - **Désactivé dans ce mode** : `ApplyGuard` et `CurlFingers`.
      - **Direction** : les clips gardent l'orientation de leur garde de profil, et le jab partait de biais.
        - **Mesure** : à chaque coup, la direction horizontale de la main, de son entrée dans la fenêtre de touche jusqu'à son point le plus loin de l'épaule, est comparée à la vue. La console donne l'angle et la valeur à mettre.
        - **Correction** : `ApplyPunchAim` tourne la colonne (avant FBBIK) de *Punch Aim Left/Right*, en fondu de 0,08 s.
      - **Calques** : construits par `RnDCombatAnimationTools.BuildLayers`, appelé par *Tools > Pool > R&D > Préparer le joueur de R&D* (`RnDArenaTools`). Ils sont supprimés puis recréés dans le contrôleur de la variante.
      - **Outils** :
        - `RnDArenaTools.PreparePlayer` ajoute ce qui manque sur la variante (`LocalPlayerLocomotion`, `LocalHitGauge`, *Boxing Punches*), puis construit les calques. *Créer l'arène de test* l'appelle.
        - Les mannequins de l'arène et ceux de la touche G sont rendus inertes par `RagdollDummySpawner.MakeInert` (une seule liste : la touche G ne coupait pas `LocalPlayerLocomotion`).
  - **Jauge d'encaissement** (R&D, `CombatSettings.useHitGauge`, coups d'un joueur qui a *Boxing Punches*) : `TryHit` appelle `LocalHitGauge.For(cible).TakeHit(dégâts, spartiate)` **avant** `MuscleCollisionBroadcaster.Hit`. `LocalHitGauge` est sur la racine de la cible (à côté de `LocalPlayerRagdollController`), ajouté à l'exécution s'il manque.
    - **Titube** : pendant *Stagger Time*, les propriétés de muscles de `BehaviourPuppet` (`defaults` et chaque `groupOverrides`) reçoivent au moins *Stagger Resistance* (`collisionResistance`) et *Stagger Drift*² (`knockOutDistance`). Le coup ne lâche qu'en partie le muscle touché et ne fait pas perdre l'équilibre. Les valeurs du prefab reviennent quand ce temps est passé **et** que tous les muscles ont repris leur pin (`pinWeightMlp ≥ 0,95`), au plus 3 s plus tard.
      - **Pourquoi** :
        - un coup enlève `unpin / (props.collisionResistance × Collision Resistance)` au pin du muscle touché ;
        - la chute se déclenche quand un muscle au pin ≤ *Pin Weight Threshold* s'écarte de plus de `props.knockOutDistance × √global` (écart au carré) ;
        - dans le prefab du joueur (`New`, `Dummy`), ces deux propriétés par défaut sont à **0**, sans réglage par groupe. Tout coup lâche donc le muscle entièrement, et le moindre écart fait tomber. C'est pour ça que tout faisait tomber au premier contact ;
        - les deux premières versions multipliaient ces valeurs, qui restaient donc à 0.
      - **Bras et mains ignorés pendant le titube** : un groupe `Arm` + `Hand`, avec un `knockOutDistance` hors d'atteinte, est placé en tête d'un nouveau tableau `groupOverrides` (`GetProps` prend le premier groupe qui correspond). Le tableau du prefab est remis tel quel à la fin.
        - Diagnostic du 12/10 : un joueur tombait au 1ᵉʳ poing, à cause de sa main gauche molle (pin 0,44, sous le seuil de 0,5) qui avait ballotté à 0,61 m de sa pose.
      - **Fin du titube** : `SetHasCollided(false)` ferme la fenêtre de 3 s pendant laquelle `BehaviourPuppet` peut faire tomber après un contact.
        - Sans ça, un **joueur** tombait environ 1 s après un poing : ses bras mous (pin réduit par `LocalPlayerLocomotion`, sous *Pin Weight Threshold*) s'écartent toujours un peu, et la distance de déséquilibre du prefab, revenue à 0, déclenchait la chute.
        - Les mannequins n'avaient pas le problème : leur `LocalPlayerLocomotion` est désactivé.
    - **K.O.** : jauge pleine. Il passe `getUpDelay` à *Knock Out Time* et appelle `SetState(Unpinned)`. La chute et le relevé suivent ensuite le chemin habituel de `LocalPlayerRagdollController` (onLoseBalance / onRegainBalance).
    - **Réveil** : il est détecté au retour à l'état `Puppet`. Il remet la jauge à 0, rétablit les valeurs d'origine de `BehaviourPuppet` et donne la protection *Wake Protection*.
    - **Sonné** : `LocalPlayerLocomotion` lit `Dazed` (0..1) pour ralentir le joueur, le faire dériver et faire tanguer la vue.
    - **Zones, animations, chute** (`LocalHitGauge.Reactions.cs`) :
      - **Zone** : `TryHit` la tire du groupe du muscle touché (`Head` → tête, `Leg`/`Foot` → jambes, le reste → corps) et la passe à `TakeHit` avec la direction de la force. La zone multiplie le remplissage.
      - **Coup reçu** : `CrossFade` vers `Hit{Light|Heavy}{Front|Back|Left|Right}` sur le calque « R&D Coup reçu ». Le côté est déduit de la direction de la force dans le repère de la victime. La version lourde sert pour la tête ou quelqu'un de sonné. Le poids est à 1 pendant le clip, puis il revient à 0 en fondu, vers 85 % du clip.
      - **Sonné** : `Stunned` sur « R&D Sonné (haut) », avec un poids égal à `Dazed`. « R&D Sonné (jambes) » a ce même poids, multiplié par (1 − vitesse / 1,5 m/s).
      - **K.O.** : avant `SetState(Unpinned)`, `unpinnedMuscleWeightMlp` passe à *Knock Out Limp* (rétabli au réveil). Un pas de physique plus tard, des vitesses sont données par groupe de muscles selon la zone :
        - tête : le haut part avec le coup ;
        - corps : le bassin part en arrière, le haut vers l'avant et le bas ;
        - jambes : les jambes partent avec le coup, le haut à l'opposé.
      - Ces animations jouent aussi sur les mannequins : leur Animator tourne.
    - **Événement** : `LocalHitGauge.KnockedOut` (victime, point), joué par `GameFeel` comme le moment « K.O. ».
  - **Garde** (`LocalUnarmedMelee.Block.cs`, action `Block`) :
    - **État** : `UpdateBlock` calcule `IsBlocking` en premier dans `Update`. Il faut l'action maintenue, être au repos (`phase == Idle`, pas de coup animé) et ne pas avoir la garde brisée. Tant que la garde est levée, aucun coup ne part. `LocalPlayerLocomotion` ralentit le joueur et coupe le sprint.
    - **Animation** : en mode animé, le calque « R&D Combat » joue `Block` (IdleBlockHigh), puis `Blocked` (BlockedHigh) pendant 0,25 s après un coup bloqué. Le choix de l'état passe par `layerState` (Empty / Guard / Block / Blocked).
    - **Blocage** : dans `TryHit`, l'attaquant appelle `victime.TryBlock(direction, zone, pied, spartiate, out garde brisée)`. Le coup est bloqué s'il ne vise pas les jambes et vient de moins de *Block Angle* de la vue de la victime.
      - S'il est bloqué, la jauge, le déséquilibre et la force sont réduits. `TakeHit(…, blocked: true)` ne joue pas de réaction de coup reçu.
      - Le coup de pied spartiate brise la garde. Avec *Spartan Breaks Guard Only*, il est traité comme un coup bloqué : `spartanKnockOut` passe à faux, donc ni K.O. forcé, ni `Launch`.
      - **Levée de la garde** : `LocalHitGauge.CancelHitReaction` coupe une réaction encore en cours, dont le calque, au-dessus du combat, cachait la pose. 0,3 s plus tard, `DiagnoseBlockPose` vérifie l'état et le poids des calques (*Debug Logs*).
    - **Jauge de garde** (`blockMeter`) : chaque coup bloqué la fait baisser ; à 0, `BreakGuard`.
      - Le coup qui la vide reste bloqué. La garde est ensuite interdite pendant *Guard Break Time*.
      - `LocalHitGauge.PlayGuardBreakReaction` joue une réaction lourde, sans toucher la jauge d'encaissement. Les coups suivants y comptent normalement.
      - L'événement `GuardBroken` est émis (moment Feel « garde brisée »).
      - La première version passait aussi la jauge à 70 % (`ForceDazed`), ce qui faisait du coup suivant un K.O. Retiré le 12/10.
    - **Contre-attaque** : `counterUntil` est armé à chaque blocage. `StartCounterIfAny` marque le prochain poing (`counterPunch`) : vitesse ×*Counter Speed* via `PunchSpeed`, jauge et force ×*Counter Damage*.
  - **Endurance** (`LocalUnarmedMelee.Stamina.cs`) :
    - **Contrôle** : `CanStart` refuse un coup si `stamina` est inférieure à son coût (`HasStamina`).
    - **Paiement** : `SpendStamina` paie au départ (`Begin`, `StartAnimatedPunch`) et marque `tiredPunch` sous *Tired Below*. Un coup fatigué a sa vitesse multipliée par *Tired Speed* via `PunchSpeed`, sa jauge et sa force par *Tired Damage* dans `TryHit`. Le spartiate paie un supplément quand il part.
    - **Récupération** : l'endurance remonte après *Stamina Regen Delay*.
  - **Événement** : chaque coup qui touche émet `LocalUnarmedMelee.PlayerHit` (attaquant, victime, point, force 0..1), que `GameFeel` joue comme le moment « coup reçu ».
- `CueChargeSlide` ne déplace que le **mesh** de la queue (recul pendant la charge, allongement quand le joueur est loin de la bille), jamais les points de prise — sinon les mains, et le corps avec, suivraient.

## Billard et règles

```mermaid
classDiagram
  direction TB
  class PoolMatchRules {
    +Instance$
    +int CurrentPlayer
    +PoolGameMode Mode
    +bool MatchStarted
    +bool BallInHand
    +bool GameOver
    +NotifyShotFired()
    +SwitchTurn()
    +RegisterFoul()
    +Win(int)
    +CanPlayerShoot(int) bool
    +GetEffectivePlayerIndex(int) int
    +GrantPower(int, PoolPower)
    +TryActivatePower(int) bool
    +event Fouled$
    +event PowerGranted$
  }
  class IPoolRuleSet {
    <<interface>>
    +Setup(PoolMatchRules)
    +ResolveShot(match, pocketed, cueBallPocketed, firstContact)
    +DescribePlayer(int) string
  }
  class EightBallRuleSet
  class NineBallRuleSet
  class FourteenOneRuleSet
  class PoolBall {
    +Active$
    +bool IsCueBall
    +int Number
    +float Radius
    +Rigidbody Rigidbody
    +AnyMoving()$ bool
    +FindCueBall()$ PoolBall
    +ArmContactTracking()
    +BeginBallInHand()
    +PlaceAt(Vector3)
    +EndBallInHand()
    +event Pocketed$
    +event CueBallFirstContact$
  }
  class PoolPocket {
    +Active$
    +bool IsClosed
    +SetClosed(bool)
    +SetSelectionHighlight(bool)
    +AvoidAllPockets(Vector3, float)$ Vector3
  }
  class PoolTableSurface {
    +Instance$
    +ClampToPlayArea(Vector3, float) Vector3
    +DistanceToClearPlayArea(origin, dir, margin) float
  }
  class LocalPoolAimController

  IPoolRuleSet <|.. EightBallRuleSet
  IPoolRuleSet <|.. NineBallRuleSet
  IPoolRuleSet <|.. FourteenOneRuleSet
  PoolMatchRules o-- IPoolRuleSet : règle active
  PoolMatchRules ..> PoolBall : écoute Pocketed, CueBallFirstContact
  PoolPocket ..> PoolBall : OnPocketed
  LocalPoolAimController ..> PoolBall : vise, frappe
  LocalPoolAimController ..> PoolMatchRules : NotifyShotFired, tour
  LocalPoolAimController ..> PoolTableSurface : placement du corps
  LocalPoolAimController ..> PoolPocket : sélection de poche
```

Le mode **Party** réutilise `EightBallRuleSet` ; ce sont les gestionnaires de pouvoirs qui ne s'activent qu'en Party.

## Pouvoirs

```mermaid
classDiagram
  direction LR
  class PoolPower {
    <<abstract ScriptableObject>>
    +string PowerName
    +PowerType Type*
    +bool RequiresOwnTurn
    +Activate(PoolMatchRules, int)*
  }
  class BoostedShotPower
  class VisionImpairPower
  class InvertedControlsPower
  class PocketTargetPower {
    <<abstract>>
    +Activate(PoolMatchRules, int, PoolPocket)*
    +CanTarget(PoolPocket)$
  }
  class ClosePocketPower
  class IdenticalGroupsPower {
    +BuildLook()
  }
  class BallDisguise {
    <<static>>
    +Apply(looks)$
    +Restore()$
    +SetBaseMaterial(Renderer, Material)$
  }
  class MagnetPower {
    +FindCapture(PoolMatchRules, int, PoolPocket)
    +Pull(PoolBall, PoolPocket)
  }
  class BallBlastPower {
    +CanBlast(PoolBall)
    +PlayEffects(PoolBall)
  }
  class PowerType {
    <<enum>>
    Attack
    Defense
    Effect
  }
  class PoolPowerCrate {
    +Initialize(PoolPower, GameObject, Color)
  }
  class PoolPowerCrateManager {
    +Instance$
    +NotifyCollected(PoolPowerCrate)
  }
  class PoolPowerBallRotator
  class PowerBall {
    +PoolPower Power
    +SetGlow(...)
    +ClearGlow()
  }
  class PoolPowerSpawnPoint
  class PoolPowerSpawnSettings {
    <<ScriptableObject>>
  }
  class LocalPoolPowerController
  class LocalPocketPointer {
    +Point() PoolPocket
    +Release()
  }

  PoolPower <|-- BoostedShotPower
  PoolPower <|-- VisionImpairPower
  PoolPower <|-- InvertedControlsPower
  PoolPower <|-- PocketTargetPower
  PocketTargetPower <|-- ClosePocketPower
  PocketTargetPower <|-- MagnetPower
  PoolPower <|-- BallBlastPower
  MagnetPower ..> PoolMatchRules : ArmMagnet, attire une bille au prochain tir
  LocalPoolPowerController --> LocalPocketPointer : choix du trou
  LocalPocketPointer ..> PoolPocket : SetTargeted
  PoolPower <|-- IdenticalGroupsPower
  IdenticalGroupsPower ..> PoolMatchRules : QueueIdenticalGroups (tour de l'adversaire)
  PoolMatchRules ..> BallDisguise : Apply / Restore
  PowerBall ..> BallDisguise : SetBaseMaterial
  BallBlastPower ..> PoolMatchRules : ArmBallBlast, détruit au premier contact
  PoolPower --> PowerType
  PoolPowerCrateManager --> PoolPowerCrate : fait apparaître
  PoolPowerCrateManager --> PoolPowerSpawnPoint : emplacements
  PoolPowerCrateManager --> PoolPowerSpawnSettings
  PoolPowerBallRotator --> PowerBall : active / éteint
  PoolPowerBallRotator --> PoolPowerSpawnSettings
  PoolPowerCrate ..> PoolMatchRules : GrantPower
  PowerBall ..> PoolMatchRules : GrantPower à l'empochage
  LocalPoolPowerController ..> PoolMatchRules : TryActivatePower
  PoolPower ..> PoolMatchRules : applique son effet
```

Ajouter un pouvoir = une nouvelle classe dérivée de `PoolPower` + un asset créé via **Create > Pool > Powers**, ajouté à `Available Powers` dans `PoolPowerSpawnSettings`.

**Pouvoirs qui visent un trou** : dériver de `PocketTargetPower` plutôt que de `PoolPower`. `LocalPoolPowerController` ouvre alors un choix du trou à la place d'une activation directe.
- **Pointage** (`LocalPocketPointer`, partagé avec l'annonce de la 8 de `LocalPoolAimController`) : pointeur blanc au centre de la vue ; le trou retenu est le plus proche en angle du centre de la caméra du joueur (pas de raycast, un trou est trop petit). Chaque utilisateur appelle `Point()` à chaque image de son choix et `Release()` à la fin, et lit lui-même sa validation. Le choix d'un pouvoir attend que l'annonce de la 8 soit faite, et inversement.
- **Repère** : `PoolPocket.SetTargeted` affiche le cercle et le halo. Il compte les joueurs, pour deux joueurs qui visent le même trou.
- **Activation** : `PoolMatchRules.TryActivatePower(player, pocket)`.
- **Touche Interact** : pendant le choix, `IsChoosingPocket` (vrai aussi pendant l'image où le choix se termine) écarte `LocalPoolAimController` et, par `WantsInteractThisFrame`, `LocalPlayerHandController`.
- **Sans trou choisi** (contrôleur en ligne figé) : un trou ouvert au hasard.

L'**aimant** suit le cycle de la bille destructrice : `ArmMagnet`, consommé par `NotifyShotFired`. Pendant le tir, `PoolMatchRules.FixedUpdate` capte une bille du joueur (`IsOwnBall`) puis appelle `MagnetPower.Pull` à chaque pas de physique, jusqu'à l'empochage, la fin du coup ou *Max Pull Duration*.

## Interface (menus et HUD)

Deux composants UI Toolkit (`UntitledPoolGame.Core`, `Assets/Scripts/UI/`) suivent la direction « Synthèse » (`docs/ui/synthese.html`). Ils sont construits en code et créés tout seuls (`RuntimeInitializeOnLoadMethod`) dans toute scène qui contient un `PoolMatchRules`. Polices : `Resources/UI/Fonts` (Titan One, Bowlby One).

- **`UiKit`** : palette, polices, `PanelSettings` et petits constructeurs d'éléments communs aux menus (cartes « pop », billes, dégradés radiaux). La palette et les polices sont lues dans **`MenuSettings`** (Resources), qui porte aussi tous les textes et les billes du menu, de la pause et de l'écran des touches, l'image de fond et le logo. `SyntheseMenu`, `PauseMenu` et `KeyBindingsPanel` le lisent en se construisant ; seuls la mise en page et les animations restent dans le code.
- **`TextFx`** : texte animé lettre par lettre, version maison de *Text Animator* (Febucci).
  - **Balises** : les textes des réglages portent leurs effets (`<wave>`, `<shake>`, `<pop>`…). `TextFx` les retire du texte, garde les balises de texte enrichi d'Unity, et note pour chaque lettre ses effets et son heure d'apparition.
  - **Rendu** : après la mise en forme du texte par Unity, `TextElement.PostProcessTextVertices` donne les quatre sommets de chaque lettre ; `TextFx` les déplace, les tourne, les met à l'échelle et les teinte autour du centre de la lettre. Un label reste un seul élément, quelle que soit la longueur du texte.
  - **Rafraîchissement** : une tâche planifiée du label (`schedule.Execute(...).Every(16)`) le redessine tant qu'un effet bouge, puis s'endort.
  - **Entrées** : `Set` (texte fixe, ne fait rien si le texte n'a pas changé), `Reveal` (lettres une à une), `Type` (machine à écrire, avec pauses de ponctuation), `Hide`, `Skip`.
  - **Branchements** : `UiKit.Text` et `MatchHud.Text` passent tous leurs textes par `TextFx.Set`. Les interjections, les messages et les textes qui arrivent utilisent `Reveal` ou `Type`. Réglages : `TextFxSettings`.
  - **Sécurité** : si le rappel d'Unity lève une erreur (bug signalé en 6.5), l'animation du label est coupée et le texte reste lisible.
  - **Text FX Studio** (`Editor/TextFxStudio.cs`) : fenêtre d'éditeur qui prévisualise `TextFx` hors Play Mode (horloge en temps réel, `Time.realtimeSinceStartup`). Elle lit et écrit les textes des assets de réglages par `SerializedObject`, et crée les séquences Feel.
- **Feel** (`Assets/Scripts/Feel/`, plugin non modifié) :
  - **`GameFeel`** (créé tout seul avec `PoolMatchRules`) écoute `PoolBall.Pocketed`, `PoolMatchRules.Fouled` / `PowerGranted` / `BallBlasted` / `TurnChanged`, `LocalPoolAimController.ShotTaken` et la fin de partie.
  - Il joue les `MMF_Player` que `GameFeelSettings` associe à chaque moment ; chaque prefab est instancié une fois par scène. `GameFeel.EventPlayer` indique à qui appartient le moment.
  - **Feedbacks maison** :
    - `MMF_PoolHudText` passe par `MatchHud.ShowShout` (texte avec balises sur la bonne moitié d'écran) ;
    - `MMF_PoolScreenJuice` passe par `LocalPoolPowerEffectReceiver.PlayImpact`, pour que la caméra n'ait qu'un seul système qui l'écrit.
- **Écrans de menu en données** (`Assets/Scripts/UI/Menus/`) :
  - **`MenuScreen`** (un asset par écran dans `Resources/Menus`) : fond, queue, écran de retour, séquence Feel d'ouverture, et une liste de **`MenuElement`**. Chaque élément porte son type (bille, carte, texte, image, panneau, pilule, zone du jeu), sa place en % et sa taille en px 1080p, son apparence, ses textes, son action (`MenuAction`), ses effets (`MenuEntrance`, `MenuIdle`) et ses séquences Feel (visé, choisi).
  - **`MenuRenderer`** construit un `MenuView` à partir d'un `MenuScreen`, pour le jeu comme pour l'éditeur. Chaque élément est un `MenuNode` sur trois couches : holder (placé), fx (arrivée et mouvement permanent, `MenuFx`), visual (visée et tir du menu).
  - **`MenuFeel`** joue les séquences Feel des menus. **`MenuLayouts.Get`** charge un écran, ou sa disposition d'origine (**`MenuDefaults`**) s'il manque.
  - **Utilisation** : `SyntheseMenu`, `PauseMenu`, `KeyBindingsPanel` (cadre « panel » de l'écran Settings) et `LevelLoader` construisent leurs écrans ainsi. Ils retrouvent par leur nom les éléments qu'ils remplissent (`context`, `levels`, `rules`, `slot1`, `who`, `postcard`…). Les éléments avec une action sont les choix.
  - **`MenuStudio`** (*Tools > Pool > Menu Studio*) : aperçu identique au jeu à l'échelle de la fenêtre, sélection et déplacement à la souris, poignée de taille, propriétés par `PropertyField` liés au `SerializedObject` (annulation), création des séquences Feel.
- **`SyntheseMenu`** : menu principal (variante A, billes sur la table) et ses pages.
  - **Pages** :
    - titre ;
    - Nouvelle partie et Charger (emplacements ; pas encore de sauvegarde, Nouvelle partie lance une partie classique) ;
    - niveau (cartes postales ; le bar, puis prison et station « bientôt ») ;
    - type de partie (Classique, Pouvoirs, 9-ball, 14.1) ;
    - Multijoueur (Local → arrivée des joueurs ; En ligne « plus tard ») ;
    - Réglages.
  - **Navigation** : chaque page connaît sa page de retour (`Page.back`). Sur les pages-table, on vise et on tire ; sur les pages-cartes, le choix s'applique tout de suite.
  - **Lancement** : la partie démarre par `PoolMatchRules.RequestStart`, et le menu OnGUI provisoire est coupé (`PoolMatchRules.ExternalMenu`).
- **Du menu au niveau** :
  - La scène **`MainMenu`** (marqueur `MainMenuScene`) n'a que le menu. `SyntheseMenu` s'affiche dans cette scène, ou dans un niveau lancé directement depuis l'éditeur (`PoolMatchRules` présent, pas de session en cours).
  - **Joueurs** : J1 est l'appareil qui navigue dans le menu. En Local, J2 rejoint dans la salle d'attente.
  - **Lancement** : choisir le niveau puis le mode enregistre la session dans **`GameSession`** (niveau, mode, appareils), une classe statique qui survit au changement de scène.
  - **`LevelLoader`** (gardé d'une scène à l'autre le temps du chargement) affiche l'écran de chargement. Il charge la scène en arrière-plan (`LoadSceneAsync`, activation retenue jusqu'à la durée minimale), coupe l'arrivée des joueurs par bouton (`PlayerInputManager.DisableJoining`) et fait entrer les joueurs avec les appareils de la session (`JoinPlayer`). Il attend ensuite que chacun appuie, puis lance `PoolMatchRules.RequestStart`.
  - **Réglages** : niveaux dans `GameFlowSettings`, images comprises (`LevelPictures` dessine un remplacement s'il n'y en a pas) ; textes dans `LoadingScreenSettings`.
  - **Retour** : depuis la pause, « Menu principal » appelle `GameSession.LoadMainMenu`.
- **`KeyBindingsPanel`** : écran des touches, partagé par le menu et la pause. Sans joueur (scène du menu), il modifie une copie des actions de `GameFlowSettings.playerActions`.
  - **Réaffectation** : l'Input System écoute la prochaine touche (`PerformInteractiveRebinding`) sur les actions du premier joueur, et une touche déjà prise est échangée.
  - **`KeyBindings`** sauvegarde les surcharges en JSON dans les `PlayerPrefs`. `KeyBindingsApplier`, créé tout seul et gardé d'une scène à l'autre, les applique à chaque `PlayerInput` qui apparaît.
- **`PauseMenu`** : Échap ou Start pendant une partie.
  - **Pause** : `Time.timeScale` à 0, maintenu tant que la pause est ouverte. Les entrées de tous les joueurs sont coupées (`DeactivateInput`) et le curseur est libéré. Le menu indique qui a mis en pause.
  - **Menu principal** : après confirmation, la scène est rechargée, ce qui remet le menu.
  - **Superposition** : il s'affiche au-dessus du HUD (90) et du menu (100), à l'ordre d'affichage 110.
- **`MatchHud`** : UI en jeu. Textes, couleurs, tailles et durées dans `HudSettings` (`Resources`, voir *Configuration*).
  - **Mise en page** : une partie d'écran par `PlayerInput`, calée sur le `Camera.rect` de son joueur, avec un contenu dessiné pour une moitié de 960 × 1080 puis mis à l'échelle.
  - **Ce qu'il lit à chaque image** :
    - `PoolMatchRules` : tour, `GetGroup`, `TryGetScore`, pouvoir en stock, bille 8 ;
    - `PoolBall.Active` : billes restantes ;
    - le `LocalPoolAimController` du joueur : `IsAiming`, `ChargeFraction`, `ContactOffset`, `IsOutOfReach`, vues du dessus.
  - **Événements** : `PoolBall.Pocketed`, `PoolMatchRules.Fouled` / `PowerGranted` / `TurnChanged`, `LocalPoolAimController.ShotTaken`.
  - **Fin de partie** : « Revanche » passe par `PoolMatchRules.RequestRestart`.
  - **Affichage provisoire** : `PoolMatchRules.ExternalHud` coupe les `OnGUI` provisoires de `PoolMatchRules` et `LocalPoolAimController`. Les voiles des pouvoirs (`LocalPoolPowerEffectReceiver`) restent en OnGUI.

## Dépendances à respecter

- `PoolMatchRules` ne connaît **aucun** script joueur : tout passe par des index de joueur (0/1). C'est ce qui lui permet d'être partagé par tous les modes et, plus tard, par l'online.
- Les scripts joueur lisent `PoolMatchRules.Instance` et tolèrent son absence (scène sans table).
- `GetEffectivePlayerIndex` traduit l'index physique d'un `PlayerInput` en joueur de la partie : identique en écran partagé, ramené au joueur dont c'est le tour quand un seul joueur joue les deux côtés.
