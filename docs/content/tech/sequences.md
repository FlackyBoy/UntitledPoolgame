# Diagrammes de séquence

> Les échanges entre composants pour les moments clés d'une partie. Noms de méthodes tels qu'ils sont dans le code.

## Ramasser et lâcher la queue

```mermaid
sequenceDiagram
  actor J as Joueur
  participant H as LocalPlayerHandController
  participant T as LocalCuePickupTrigger
  participant IS as InteractionSystem (FinalIK)
  participant P as PickUpCue
  participant G as LocalGrabbable (queue)

  J->>H: Interagir (E)
  H->>T: TryStartPickup()
  T->>P: IsBusy ? (non)
  T->>IS: GetClosestTriggerIndex()
  T->>IS: TriggerInteraction(index)
  IS-->>P: OnInteractionStart
  P->>P: oriente le holdPoint (rotation fixe)
  T->>G: MarkExternallyHeld(true)
  Note over G: kinematic, collider solide coupé
  T->>H: NotifyExternallyHeld(queue)
  Note over IS: les mains vont vers les cibles de la queue
  IS-->>P: OnInteractionPause (mains arrivées)
  P->>P: attache la queue au personnage
  loop jusqu'à la pose tenue
    P->>P: LateUpdate : interpole vers holdPoint
  end

  J->>H: Interagir (E) à nouveau
  H->>T: TryReleaseIfCue(queue)
  T->>P: ReleaseCue()
  alt queue tenue
    P->>IS: ResumeAll()
    IS-->>P: OnInteractionResume
    P->>P: détache, physique réactivée
  else mains encore en route
    P->>IS: StopAll()
  end
  T->>G: MarkExternallyHeld(false)
```

Le cas « mains encore en route » est le correctif du 28/09 : avant, un second appui pendant le geste laissait la queue dans la main sans que le jeu le sache.

## Viser et tirer

```mermaid
sequenceDiagram
  actor J as Joueur
  participant A as LocalPoolAimController
  participant F as LocalFpsPlayerController
  participant B as PoolBall (blanche)
  participant M as PoolMatchRules
  participant R as IPoolRuleSet

  J->>A: Interagir près de la blanche (queue en main, son tour)
  A->>F: enabled = false (et CinemachineBrain)
  A->>M: ConsumePendingVisionImpair / InvertedControls
  A->>A: EnterAim : mesure la ligne de la queue, place le corps
  loop chaque image
    J->>A: Regarder (angle), Déplacer (effet), Attaque (charge)
    A->>A: ComputeAimBodyPose, ApplyHandShift, caméra en orbite
    A->>A: UpdatePreview (SphereCast)
  end
  J->>A: relâche Attaque
  A->>M: ConsumeShotPowerMultiplier()
  A->>B: ArmContactTracking()
  A->>M: NotifyShotFired()
  A->>B: AddForce + AddTorque (effet)
  A->>A: LeaveAim (rend la main au joueur)
  B-->>M: CueBallFirstContact (premier contact)
  B-->>M: Pocketed (chaque bille empochée)
  Note over M: attend que toutes les billes soient arrêtées
  M->>R: ResolveShot(empochées, blanche empochée, premier contact)
  R->>M: SwitchTurn / RegisterFoul / Win
```

Le tir n'est reconnu **que** par `NotifyShotFired()` : le simple mouvement des billes au démarrage ne compte plus comme un tir (bug de fausse faute corrigé).

## Faute et main libre

```mermaid
sequenceDiagram
  participant R as IPoolRuleSet
  participant M as PoolMatchRules
  participant B as PoolBall (blanche)
  participant A as LocalPoolAimController (joueur suivant)
  actor J as Joueur suivant

  R->>M: RegisterFoul()
  M-->>M: Fouled (secousse, flash rouge)
  M->>M: SwitchTurn(), BallInHand = true
  M->>B: BeginBallInHand() (kinematic, sans collision)
  A->>A: HandleBallInHand : vue de dessus
  loop tant que non validé
    J->>A: Déplacer
    A->>B: PlaceAt(position bornée à la table, hors des poches)
  end
  J->>A: Interagir
  A->>B: EndBallInHand()
  A->>M: ConfirmBallPlaced()
```

L'annonce de poche de la bille 8 suit le même schéma (vue de dessus, sélection avec Déplacer, validation avec Interagir → `CallEightBallPocket`).

## Pouvoirs : ramasser et activer

```mermaid
sequenceDiagram
  participant C as PoolPowerCrate
  participant CM as PoolPowerCrateManager
  participant PB as PowerBall
  participant M as PoolMatchRules
  participant PC as LocalPoolPowerController
  participant P as PoolPower (asset)
  participant FX as LocalPoolPowerEffectReceiver

  Note over C: FixedUpdate : distance à la bille blanche
  C->>M: GrantPower(joueur courant, pouvoir)
  C->>CM: NotifyCollected() (réapparition plus tard)
  PB-->>M: via PoolBall.Pocketed : GrantPower si la bille porte un pouvoir
  M-->>FX: PowerGranted (secousse, flash)

  PC->>M: GetHeldPower(joueur)
  alt RequiresOwnTurn et pas son tour
    PC-->>PC: refusé
  else autorisé
    PC->>M: TryActivatePower(joueur)
    M->>P: Activate(match, joueur)
    P->>M: ex. QueueVisionImpair(adversaire)
    Note over M: appliqué tout de suite si c'est déjà le tour de la cible, sinon à sa prochaine visée
  end
  FX->>M: IsVisionImpaired / IsControlsInverted (chaque image)
  FX->>FX: voile à l'écran, sensibilité, inversion
```

## Coup de queue sur l'autre joueur

```mermaid
sequenceDiagram
  actor J as Joueur (attaquant)
  participant M as LocalCueMelee
  participant Q as Queue tenue
  participant B as MuscleCollisionBroadcaster (muscle adverse)
  participant BP as BehaviourPuppet (adversaire)
  participant RC as LocalPlayerRagdollController (adversaire)

  J->>M: appuie sur Attaque (queue en main, hors visée)
  M->>M: mémorise l'orientation de la vue
  loop tant qu'Attaque est maintenue
    J->>M: tourne (Regarder)
    M->>M: 1re rotation nette fixe le coup (droite → balayage vers la gauche, gauche → vers la droite, haut → vertical ; sinon estoc)
    M->>Q: armé de plus en plus loin (charge 0 → 1), dans le repère de la caméra
    Note over Q: FBBIK : les mains suivent
  end
  J->>M: relâche Attaque
  M->>M: cible = vue de l'appui, ou adversaire dans le cône d'assistance
  loop frappe puis retour
    M->>M: LocalFpsPlayerController.AddLook : la vue revient vers la cible
    M->>Q: arc vers la fin du geste (estoc : pointe en avant, position de tir)
    M->>M: capsule mains → pointe (OverlapCapsule, sous-pas)
  end
  M->>B: Hit(unPin, force selon la charge, point)
  opt charge ≥ 90 %
    M->>BP: SetState(Unpinned) : chute garantie
  end
  B->>BP: OnMuscleHit : dépose le muscle et ses voisins, applique la force
  alt coup léger
    BP-->>BP: titube puis se rééquilibre
  else coup avec élan
    BP-->>RC: perte d'équilibre (OnKnockedDown)
    Note over RC: suite identique à la chute ci-dessous
  end
```

## Poing, pied et coup de pied spartiate

```mermaid
sequenceDiagram
  actor J as Joueur (attaquant)
  participant U as LocalUnarmedMelee
  participant IK as FBBIK (effecteur main / pied)
  participant B as MuscleCollisionBroadcaster (muscle adverse)
  participant BP as BehaviourPuppet (adversaire)
  participant CAM as Caméra Cinemachine FPS (attaquant)

  J->>U: appuie sur Poing (mains vides) ou Pied
  loop tant que le bouton est maintenu
    U->>IK: OnPreUpdate : garde / genou levé, buste armé
    Note over U: < 0,18 s = coup simple, au-delà la charge monte
  end
  J->>U: relâche
  loop frappe
    U->>IK: pose tendue (épaule/hanche + regard)
    U->>U: capsule balayée autour du poing / pied
  end
  U->>B: Hit(unPin, force selon la charge, point)
  alt pied chargé ≥ 80 % (spartiate)
    U->>BP: SetState(Unpinned)
    U->>BP: pas physique suivant : vitesse vers l'arrière sur tous les muscles
    U->>U: ralenti global 0,45 s
    U->>CAM: zoom (champ de vision réduit) puis retour
  else poing chargé ≥ 95 %
    U->>BP: SetState(Unpinned)
  else coup simple
    Note over BP: titube (dépend de la résistance)
  end
```

## Chute en ragdoll et relevé

```mermaid
sequenceDiagram
  participant O as Objet lancé
  participant HR as RagdollHitRelay (os touché)
  participant PM as PuppetMaster / BehaviourPuppet
  participant RC as LocalPlayerRagdollController
  participant H as LocalPlayerHandController
  participant CAM as Caméras Cinemachine

  O->>HR: OnCollisionEnter (objet non tenu, assez rapide)
  HR->>RC: NotifyCollision()
  HR->>HR: AddForceAtPosition (quantité de mouvement × multiplicateur)
  PM-->>RC: perte d'équilibre (OnKnockedDown)
  RC->>H: ForceDrop()
  RC->>RC: coupe CharacterController, déplacement, visée, animation, IK
  RC->>CAM: priorité à la caméra ragdoll (3e personne)
  Note over PM: chute physique puis animation de relevé
  PM-->>RC: OnRegainBalance / relevé terminé
  RC->>RC: replace le corps là où il a atterri
  RC->>RC: réactive les contrôleurs et l'IK
  RC->>CAM: priorité à la caméra FPS
```

`LocalPoolAimController.OnDisable` sort proprement de la visée ou du placement si la chute arrive pendant ces modes (caméra et Cinemachine rétablis).
