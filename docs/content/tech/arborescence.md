# Arborescence

> Relevée le 29/09/2026. Les dossiers générés par Unity (`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`) et les fichiers de projet `.csproj`/`.sln` sont ignorés par git.

## Racine du dépôt

```text
UntitledPoolgame/
├── Assets/                 contenu du jeu (voir ci-dessous)
├── Packages/               manifest des packages Unity
├── ProjectSettings/        réglages du projet (physique, qualité, input, couches…)
├── docs/                   site GitHub Pages (cette documentation)
│   ├── index.html, gdd.html, tech.html, ui.html, level.html   pages du site
│   ├── assets/             site.css, site.js (menu, Markdown, diagrammes)
│   └── content/            textes en Markdown (gdd.md, pistes.md, level.md, tech/*.md)
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
├── Powers/                 un asset par pouvoir (BoostedShot, VisionImpair, InvertedControls, ClosePocket)
├── Editor/                 PoolTableAssetSettings.asset (dimensions de la table pour le générateur)
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
│   ├── SplitScreenCheatSpawner.cs  C+P : faire rejoindre un 2e joueur
│   ├── EightBallEndgameCheat.cs    C+W : fin de partie 8-ball directe
│   ├── RagdollDummySpawner.cs      G : mannequin de test pour le ragdoll
│   ├── ScenePlayerSpawner.cs       point d'apparition du joueur dans la scène
│   ├── NetworkBootstrap.cs         F1/F2/F3 : Host / Client / Server (online)
│   └── LocalClientRig.cs           ancien câblage Nappin (online, figé)
├── Editor/                         outils d'éditeur (non inclus dans le jeu)
│   ├── PoolTableBuilder.cs         menus Tools > Pool (table, physique, config, pouvoirs)
│   ├── PoolTableAssetSettings.cs   dimensions de la table
│   └── PoolBallTextureGenerator.cs textures numérotées des billes
├── Interaction/
│   ├── LocalPlayerHandController.cs  ramasser / poser / lancer (Interagir, Attaque)
│   ├── LocalGrabbable.cs           objet ramassable
│   ├── Cue.cs                      marqueur « c'est une queue »
│   ├── LocalCuePickupTrigger.cs    ramassage de la queue via InteractionSystem
│   ├── PickUpCue.cs                attache / détache la queue (FinalIK)
│   ├── CueChargeSlide.cs           glissement du mesh de la queue (charge, allonge)
│   ├── LocalCueMelee.cs            coup de queue sur l'autre joueur (IK + PuppetMaster Hit)
│   ├── LocalUnarmedMelee.cs        poing / pied, coup de pied spartiate (IK + PuppetMaster Hit)
│   ├── MeleeHit.cs                 commun aux coups : cible touchée, hitstop
│   └── Grabbable.cs, PlayerHandController.cs   versions online (figées)
├── Player/
│   ├── LocalFpsPlayerController.cs     déplacement, regard, canaux Cinemachine
│   ├── LocalCharacterAnimationController.cs  paramètres de l'Animator
│   ├── LocalCharacterModelFollow.cs    le modèle suit la racine hors ragdoll
│   ├── LocalPlayerRagdollController.cs chute / relevé, caméras, IK
│   ├── RagdollHitRelay.cs              impact localisé sur chaque os
│   └── FpsPlayerController.cs, CharacterAnimationController.cs, NetworkPlayerRigLink.cs   online (figés)
└── Pool/
    ├── PoolBall.cs, PoolPocket.cs, PoolTableSurface.cs   billes, poches, zone de jeu
    ├── PoolMatchRules.cs           partie : tours, fautes, main libre, pouvoirs, HUD
    ├── IPoolRuleSet.cs + EightBall/NineBall/FourteenOneRuleSet.cs   règles par mode
    ├── PoolGameMode.cs, PoolPartyMode.cs   modes et sous-modes Party
    ├── LocalPoolAimController.cs   visée, tir, vue de dessus (main libre, poche de la 8)
    ├── LocalPoolPowerController.cs activation du pouvoir
    ├── LocalPoolPowerEffectReceiver.cs   effets d'écran : pouvoirs, secousses, flashs
    ├── PoolPower.cs, PowerType.cs + BoostedShot/VisionImpair/InvertedControls/ClosePocketPower.cs
    ├── PoolPowerCrate.cs, PoolPowerCrateManager.cs, PoolPowerSpawnPoint.cs   caisses
    ├── PowerBall.cs, PoolPowerBallRotator.cs   bille à pouvoir
    ├── PoolPhysicsSettings.cs, PoolScreenJuiceSettings.cs, PoolPotEffectSettings.cs, PoolPowerSpawnSettings.cs   config
    ├── PoolSettingsLoader.cs       chargement des config avec valeurs par défaut
    └── PoolAimController.cs, PoolPowerController.cs   online (figés)
```
