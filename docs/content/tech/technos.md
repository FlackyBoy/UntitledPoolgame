# Briques technologiques et plugins

> Ce qui est utilisé, pourquoi, et ce qui a été essayé puis écarté. Versions relevées dans `Packages/manifest.json` et `ProjectSettings/ProjectVersion.txt` au 29/09/2026.

## Moteur et packages Unity

| Brique | Version | Utilisation dans le projet |
|---|---|---|
| **Unity 6** | 6000.6.0f1 | Moteur |
| **URP** (Universal Render Pipeline) | 17.6 | Pipeline de rendu, en mode **Forward+** (voir *Rendu*) |
| **Shader Graph** | 17.6 | Disponible pour les shaders maison (utilisé par certains packs VFX) |
| **Input System** | 1.20 | Toutes les commandes. `PlayerInput` par joueur + `PlayerInputManager` pour l'écran partagé. Asset : `InputSystem_Actions_Local` |
| **Cinemachine** | 6.6 | Caméra virtuelle FPS (elle recule elle-même pendant une chute en ragdoll) ; un canal (`OutputChannels`) par joueur en écran partagé |
| **Netcode for GameObjects** | 2.13 | Multijoueur en ligne (scripts online figés ; l'online sera reconstruit au-dessus des scripts `Local*`) |
| **Multiplayer Play Mode** | 3.0 | Tester plusieurs clients dans l'éditeur |
| **Test Framework** | 1.8 | Installé, pas encore de tests écrits (voir *Pistes & suggestions*) |
| **Post Processing** | 3.5 | Ancien système de post-process ; URP utilise ses propres *Volumes* (`DefaultVolumeProfile`) |
| **AI Navigation** | 2.0 | Installé pour les futurs PNJ |
| Splines, Timeline, Visual Scripting, 2D Animation | — | Installés, non utilisés par le code actuel |

## Plugins (Asset Store)

### RootMotion — Final IK ✅ utilisé

Cinématique inverse (IK) : calcule la position des membres pour qu'une main, un pied ou la tête atteigne une cible.

- **Full Body Biped IK (FBBIK)** : tout le corps suit quand les mains vont chercher la queue ; **bend goals** des bras déplacés en visée pour un placement des coudes naturel.
- **Interaction System** : ramassage de la queue (`InteractionObject` sur la queue, `InteractionTarget` pour chaque main, `InteractionTrigger` pour la zone et l'angle d'approche). `PickUpCue` reprend la logique de la démo `PickUp2Handed`.
- **Look At IK** : la tête suit la caméra, et regarde la bille blanche en visée.
- Essayés puis écartés pour la queue : **Aim IK**, **Limb IK**, `TwoHandedProp` (voir CHANGELOG du 15 au 23/09).

### RootMotion — PuppetMaster ✅ utilisé

Ragdoll « actif » : un double physique du squelette suit l'animation grâce à des muscles, et peut perdre l'équilibre quand il est percuté.

- `PuppetMaster` + `BehaviourPuppet` (chute, relevé, résistance aux collisions par couche).
- `LocalPlayerRagdollController` écoute ses événements pour couper/rendre le contrôle, lâcher l'objet tenu et basculer de caméra.
- `RagdollHitRelay` (un par os) ajoute une impulsion au point d'impact, proportionnelle à la quantité de mouvement de l'objet lancé.

### MoreMountains — Feel ✅ branché sur les moments de la partie

Retours visuels et sonores (secousses, flashs, ressorts…).

- **Pas utilisé sur la caméra** : trois systèmes écrivent déjà dans la caméra (FPS, visée, vue de dessus) ; un composant Feel indépendant entrerait en conflit. Les secousses et flashs sont faits maison dans `LocalPoolPowerEffectReceiver`.
- `MMRagdoller` essayé pour le ragdoll puis remplacé par PuppetMaster (relevés en boucle, décalages).
- Candidat pour des effets sur des objets isolés (pop des caisses de pouvoir).
- **Branché sur la partie depuis le 09/10** : `GameFeel` joue une séquence Feel par moment de la partie (`GameFeelSettings`), avec deux feedbacks maison (texte du HUD avec `TextFx`, secousse et flash par notre système de caméra). Les feedbacks UI Toolkit de Feel ne servent pas : ils visent un UIDocument posé dans la scène, alors que nos interfaces sont créées en code.

### Nappin — Physics Character Controller ❌ écarté pour le déplacement

Contrôleur basé sur un Rigidbody, évalué au début. Remplacé par un contrôleur FPS maison sur `CharacterController` (plus simple à synchroniser en ligne, strafe correct par construction). Des briques restent réutilisables.

### Effets visuels (VFX)

| Pack | Utilisation |
|---|---|
| **JMO — Cartoon FX Remaster** | ✅ Aura qui monte d'une poche quand une bille tombe (`CFXR3 Magic Aura A (Runic)`) |
| **Hovl Studio — Epic Toon VFX 3** | Disponible |
| **Piloto Studio** (Ultimate Loot VFX, Treasure Pack…) | Disponible, candidat pour les caisses de pouvoir |

### Autres

| Plugin | Utilisation |
|---|---|
| **RetopoStudios — Forest Bar** | Décor du bar (scène `BarSplitscreen`) |
| **FImpossible Creations — Eyes Animator** | Animation des yeux |
| **Adaptive Split Screen** | ❌ Essayé puis retiré : impose la même rotation à toutes les caméras, incompatible avec un FPS |

## Briques maison

| Brique | Rôle |
|---|---|
| Contrôleur FPS (`LocalFpsPlayerController`) | Corps tourné par le regard, déplacement relatif au corps, attente du début de partie |
| Physique des billes (`PoolBall`) | Modèle glissement / roulement ajouté à PhysX (voir *Sources*) |
| Règles (`IPoolRuleSet`) | 8-ball, 9-ball, 14.1, interchangeables |
| Pouvoirs (`PoolPower`) | ScriptableObjects, activation par `PoolMatchRules` |
| Réglages (`PoolSettingsLoader`) | Chargement des ScriptableObjects avec valeurs par défaut |
| Générateur de table (`PoolTableBuilder`) | Table, billes, poches, queues, points d'apparition, matériaux physiques — en un clic |
| Interface | `OnGUI` provisoire (écran de mode, scores, messages) — à refaire |
