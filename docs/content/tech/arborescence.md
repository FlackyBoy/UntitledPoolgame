# Arborescence

> Relevée le 29/09/2026. Les dossiers générés par Unity (`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`) et les fichiers de projet `.csproj`/`.sln` sont ignorés par git.

## Racine du dépôt

```text
UntitledPoolgame/
├── Assets/                 contenu du jeu (voir ci-dessous)
├── Packages/               manifest des packages Unity
├── ProjectSettings/        réglages du projet (physique, qualité, input, couches…)
├── docs/                   site GitHub Pages (cette documentation)
│   ├── index.html          accueil
│   ├── suivi.html, gdd.html, config.html, outils.html, tech.html, equipe.html   une page par rubrique (menu latéral)
│   ├── ui.html, ui/        galerie et prototypes d'interface
│   ├── level.html, TODO.html   redirections vers les anciennes adresses
│   ├── assets/             site.css, site.js (menu, rubriques, Markdown, diagrammes)
│   └── content/            textes en Markdown (gdd.md, pistes.md, config.md, prompts.md, outils/*.md, tech/*.md…)
├── TODO.md                 tâches + notes techniques détaillées
├── CHANGELOG.md            historique, plus récent en haut
├── CLAUDE.md               carte du projet et conventions pour Claude
├── RAGDOLL_DEBUGGING_LOG.md  journal du chantier ragdoll (septembre)
├── .claude/, .vscode/      réglages d'outils
└── My project/, My project (1)/   ⚠️ projets Unity vides en trop — à supprimer après vérification
```

## Assets

```text
Assets/
├── Scripts/                code du jeu (détail plus bas)
├── Scenes/
│   ├── BarSplitscreen.unity    scène principale : bar + table + écran partagé
│   ├── SplitScreen.unity       scène d'écran partagé de base
│   ├── Online.unity            scène en ligne (scripts figés)
│   ├── SceneTest.unity, TestIKTrigger.unity, R&D.unity   scènes d'essai
├── Prefabs/
│   ├── PlayerLocal/            joueur (+ Dummy, Cue ; sous-dossiers Old/ et A trier/ à trier)
│   ├── PlayerOnline/           joueur en ligne (figé)
│   ├── Cue/, PoolTables/, PoolRack/, Test Envronnement/
├── Resources/              réglages chargés au lancement (4 ScriptableObjects)
│   ├── PoolPhysicsSettings.asset, PoolScreenJuiceSettings.asset
│   └── PoolPotEffectSettings.asset, PoolPowerSpawnSettings.asset
├── Powers/                 un asset par pouvoir (BoostedShot, VisionImpair, InvertedControls, ClosePocket, BallBlast, Magnet, IdenticalGroups, TableHole)
├── Editor/                 PoolTableAssetSettings.asset (dimensions de la table), LevelMakerSettings.asset (outil de niveau)
├── InputManagers/          InputSystem_Actions.inputactions (online) et _Local (écran partagé)
├── Settings/               URP : PC/Mobile RPAsset + Renderer, Volume profiles
├── Animations/             animations Mixamo, packs de mouvements
├── Materials/, Textures/   matériaux, textures des billes générées, matériaux physiques
├── ScriptableObjects/Dimensions/   prévu pour les dimensions (vide)
├── Plugins/                plugins tiers (ne pas modifier)
│   ├── RootMotion/         Final IK, PuppetMaster
│   ├── OverAll/Feel/       Feel (MoreMountains)
│   ├── nappin/             Physics Character Controller (écarté)
│   ├── VFX/                Cartoon FX, Hovl Studio, Piloto Studio
│   ├── RetopoStudios/      décor Forest Bar
│   └── FImpossible Creations/   Eyes Animator
├── TEMP/, _Recovery/       ⚠️ fichiers temporaires et scènes de récupération — à trier
├── Art/, Audio/            vides pour l'instant
└── TutorialInfo/           restes du modèle de projet Unity
```

## Scripts

```text
Assets/Scripts/
├── Core/                           outils transverses
│   ├── PhysicsLayerSetup.cs        couches ignorées par les billes (au lancement)
│   ├── SplitScreenCheatSpawner.cs  C+P : faire rejoindre un joueur ; Tab : passer le clavier au joueur suivant (créé tout seul)
│   ├── EightBallEndgameCheat.cs    C+W : fin de partie 8-ball directe
│   ├── DebugMenu.cs                menu debug en jeu (clic gauche maintenu + 4 clics droits)
│   ├── RagdollDummySpawner.cs      G : mannequin de test pour le ragdoll
│   ├── ScenePlayerSpawner.cs       point d'apparition du joueur dans la scène
│   ├── Level/                      données du Level Maker posées dans les scènes
│   │   ├── PrefabPalette.cs        kit de construction (mur, porte, fenêtre, dalles, props)
│   │   ├── RoomOutline.cs          tracé d'une salle et ses ouvertures
│   │   ├── RoomModule.cs           marqueur des pièces posées
│   │   ├── AmbiencePreset.cs       ambiance : ciel, lumières, post-process, brouillard, teinte du décor
│   │   ├── AmbienceController.cs   applique une ambiance (éditeur et jeu) et son scénario d'éclairage APV
│   │   └── AmbienceLight.cs        couleur et intensité d'origine d'une lampe du décor
│   ├── NetworkBootstrap.cs         F1/F2/F3 : Host / Client / Server (online)
│   └── LocalClientRig.cs           ancien câblage Nappin (online, figé)
├── Editor/                         outils d'éditeur (non inclus dans le jeu)
│   ├── GameFlowTools.cs            Tools > Pool > Create Main Menu Scene, Sync Build Scenes ; réglages du menu et du chargement
│   ├── SceneNameDrawer.cs          sélecteur de scène dans l'Inspector, avertissement si la scène n'est pas dans le build
│   ├── MenuStudio.cs               Tools > Pool > Menu Studio : éditeur graphique des écrans de menu
│   ├── RnDArenaTools.cs            Tools > Pool > R&D > Préparer le joueur de R&D · Créer l'arène de test
│   ├── RnDCombatAnimationTools.cs  calques Animator de la R&D (sonné, coups, garde, coup reçu), appelé par Préparer le joueur de R&D
│   ├── TextFxStudio.cs             Tools > Pool > Text FX Studio : écrire et prévisualiser les textes animés, créer une séquence Feel
│   ├── PoolTableBuilder.cs         menus Tools > Pool (table, physique, config, pouvoirs)
│   ├── PoolTableAssetSettings.cs   dimensions de la table
│   ├── PoolBallTextureGenerator.cs textures numérotées des billes
│   ├── TableHoleScorchGenerator.cs decal du contour brûlé des trous (texture, matériau, vérification URP)
│   └── LevelMaker/                 Tools > Pool > Level Maker (outil de niveau)
│       ├── LevelMakerWindow.cs     fenêtre en 6 étapes + outils de la vue Scène
│       ├── LevelMakerWindow.Room.cs  étape Salle : tracé des murs, ouvertures, props
│       ├── LevelMakerWindow.Ambience.cs  étape Ambiance : ambiances et précalcul
│       ├── PrefabPaletteEditor.cs  inspecteur de palette : cases de dépôt par type (plusieurs prefabs, dossiers)
│       ├── AmbienceTools.cs        APV, préparation de la scène, ambiances de départ, précalcul enchaîné
│       ├── LevelBuilder.cs         requêtes et opérations sur la scène (Undo)
│       ├── RoomBuilder.cs          construction des salles à partir d'un tracé et d'une palette
│       ├── KitGenerator.cs         palettes greybox (générée) et Forest Bar, remplissage depuis un dossier
│       ├── LevelValidator.cs       vérifications et corrections
│       ├── PrefabGhost.cs / .shader  fantôme d'un prefab dans la vue Scène (outil Props), dessiné sans rien instancier
│       └── LevelMakerSettings.cs   prefabs et options de l'outil
├── UI/
│   ├── GameFlowSettings.cs         scène du menu et niveaux (nom, scène, image, twist), asset dans Resources
│   ├── GameSession.cs              ce que le menu a décidé (niveau, mode, appareils des joueurs), garde le cap d'une scène à l'autre
│   ├── HudSettings.cs              réglages du HUD (textes, couleurs, tailles, durées), asset dans Resources
│   ├── LevelLoader.cs              écran de chargement + chargement du niveau, arrivée des joueurs, départ de la partie
│   ├── LevelPictures.cs            image d'un niveau (ou dessin de remplacement)
│   ├── LoadingScreenSettings.cs    textes, astuces et messages de l'écran de chargement, asset dans Resources
│   ├── MainMenuScene.cs            marqueur de la scène du menu
│   ├── MenuSettings.cs             textes, billes, couleurs, polices et fond du menu, de la pause et de l'écran des touches, asset dans Resources
│   ├── KeyBindings.cs              touches choisies par le joueur (PlayerPrefs) + KeyBindingsApplier
│   ├── KeyBindingsPanel.cs         écran « Réglages > Touches » (menu et pause)
│   ├── PauseMenu.cs                pause en partie (Échap / Start), créée toute seule
│   ├── Menus/                      écrans des menus en données (Menu Studio)
│   │   ├── MenuScreen.cs           un écran : fond, queue, retour, éléments (MenuElement), asset dans Resources/Menus
│   │   ├── MenuRenderer.cs         construit un écran (MenuView, MenuNode), effets (MenuFx), Feel (MenuFeel), chargement (MenuLayouts)
│   │   └── MenuDefaults.cs         disposition d'origine de chaque écran
│   ├── SceneNameAttribute.cs       champ « scène » choisi dans une liste (dessiné par Editor/SceneNameDrawer.cs)
│   ├── TextFx.cs                   texte animé lettre par lettre (balises <wave>, <shake>…, apparitions, machine à écrire)
│   ├── TextFxSettings.cs           force et vitesse des effets de texte, apparition, machine à écrire, asset dans Resources
│   ├── UiKit.cs                    palette et polices (lues dans MenuSettings), constructeurs d'éléments partagés par les menus
│   ├── MatchHud.cs                 UI en jeu (UI Toolkit, direction Synthèse) : HUD par joueur, interjections, fin de partie ; créé tout seul
│   ├── SkipMainMenu.cs             « Lancer sans le menu » : scène de test lancée directement, partie démarrée seule
│   └── SyntheseMenu.cs             menu principal et ses pages (UI Toolkit, direction Synthèse), créé tout seul
├── Feel/                           pont avec le plugin Feel (sans le modifier)
│   ├── GameFeel.cs                 écoute la partie et joue la séquence Feel de chaque moment
│   ├── GameFeelSettings.cs         moment de la partie → séquence Feel (MMF_Player), asset dans Resources
│   ├── MMF_PoolHudText.cs          feedback Feel « Texte du HUD (TextFx) »
│   └── MMF_PoolScreenJuice.cs      feedback Feel « Secousse et flash écran » (par LocalPoolPowerEffectReceiver)
├── Interaction/
│   ├── LocalPlayerHandController.cs  ramasser / poser / lancer (Interagir, Attaque)
│   ├── LocalGrabbable.cs           objet ramassable
│   ├── Cue.cs                      marqueur « c'est une queue »
│   ├── LocalCuePickupTrigger.cs    ramassage de la queue via InteractionSystem
│   ├── PickUpCue.cs                attache / détache la queue (FinalIK)
│   ├── CueChargeSlide.cs           glissement du mesh de la queue (charge, allonge)
│   ├── LocalObjectHands.cs         objets tenus en main et lancés avec un geste (léger à une main, lourd à deux)
│   ├── LocalCueHolder.cs           prise procédurale de la queue (en test, remplace LocalCuePickupTrigger + PickUpCue quand actif)
│   ├── LocalCueMelee.cs            coup de queue sur l'autre joueur (IK + PuppetMaster Hit)
│   ├── LocalUnarmedMelee.cs        poing / pied, coup de pied spartiate (IK + PuppetMaster Hit)
│   ├── LocalUnarmedMelee.Animated.cs  R&D : coups et garde joués par des animations (calque « R&D Combat »)
│   ├── LocalUnarmedMelee.Block.cs  R&D : garde maintenue (blocage, jauge de garde, garde brisée, contre-attaque)
│   ├── LocalUnarmedMelee.Stamina.cs  R&D : endurance de celui qui frappe (anti-spam, coups fatigués)
│   ├── LocalHitGauge.cs            R&D : jauge d'encaissement de la cible (titube → sonné → K.O. → réveil)
│   ├── LocalHitGauge.Reactions.cs  R&D : zones touchées, animations de coup reçu et de sonné, chute du K.O.
│   ├── MeleeHit.cs                 commun aux coups : cible touchée, hitstop
│   ├── CombatSettings.cs           R&D : coups animés, garde, nervosité et force des poings de boxe, asset dans Resources
│   └── Grabbable.cs, PlayerHandController.cs   versions online (figées)
├── Player/
│   ├── LocalFpsPlayerController.cs     déplacement, regard, canaux Cinemachine
│   ├── LocalCharacterAnimationController.cs  paramètres de l'Animator
│   ├── LocalCharacterModelFollow.cs    le modèle suit la racine hors ragdoll
│   ├── LocalPlayerRagdollController.cs chute / relevé, caméras, IK
│   ├── LocalPlayerLocomotion.cs        R&D : élan, sprint, saut, escalade, corps mou (variante du joueur)
│   ├── MovementSettings.cs             réglages de LocalPlayerLocomotion, asset dans Resources
│   ├── RagdollHitRelay.cs              impact localisé sur chaque os
│   └── FpsPlayerController.cs, CharacterAnimationController.cs, NetworkPlayerRigLink.cs   online (figés)
└── Pool/
    ├── PoolBall.cs, PoolPocket.cs, PoolTableSurface.cs   billes, poches, zone de jeu
    ├── PoolMatchRules.cs           partie : tours, fautes, main libre, pouvoirs, HUD
    ├── IPoolRuleSet.cs + EightBall/NineBall/FourteenOneRuleSet.cs   règles par mode
    ├── PoolGameMode.cs, PoolPartyMode.cs   modes et sous-modes Party
    ├── LocalPoolAimController.cs   visée, tir, vue de dessus (main libre), annonce de la 8
    ├── LocalPocketPointer.cs       choix d'une poche ou d'un point de la table en le visant (pouvoirs, annonce de la 8)
    ├── RingLine.cs                 cercle tracé au LineRenderer (poche visée, aperçu sur la table, bord d'un trou)
    ├── LocalPoolPowerController.cs activation du pouvoir, choix du trou (pointeur blanc)
    ├── LocalPoolPowerEffectReceiver.cs   effets d'écran : pouvoirs, secousses, flashs
    ├── PoolPower.cs, PowerType.cs + BoostedShot/VisionImpair/InvertedControls/BallBlast/IdenticalGroupsPower.cs
    ├── PocketTargetPower.cs + ClosePocket/MagnetPower.cs   pouvoirs qui visent un trou
    ├── BallDisguise.cs             aspect commun des billes (Groupe identique), restauré ensuite
    ├── TablePointPower.cs + TableHolePower.cs, TableHole.cs, TableHoleMeteor.cs   pouvoir Trou (météore, trou, billes tombées)
    ├── PoolPowerCrate.cs, PoolPowerCrateManager.cs, PoolPowerSpawnPoint.cs   caisses
    ├── PowerBall.cs, PoolPowerBallRotator.cs   bille à pouvoir
    ├── PoolPhysicsSettings.cs, PoolScreenJuiceSettings.cs, PoolPotEffectSettings.cs, PoolPowerSpawnSettings.cs   config
    ├── PoolSettingsLoader.cs       chargement des config avec valeurs par défaut
    └── PoolAimController.cs, PoolPowerController.cs   online (figés)
```
