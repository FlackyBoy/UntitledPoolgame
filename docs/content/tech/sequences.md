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
