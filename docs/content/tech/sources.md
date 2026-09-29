# Annexe — sources

> Les références derrière les notions algorithmiques utilisées dans le code et derrière les chantiers techniques envisagés.

## Physique du billard (`PoolBall`, `PoolTableBuilder`)

| Notion | Où dans le code | Sources |
|---|---|---|
| Glissement puis roulement sans glissement : le frottement agit sur la vitesse du point de contact tant que la bille glisse | `PoolBall.ApplySlideFriction` | [Rolling (en)](https://en.wikipedia.org/wiki/Rolling) · [Dr. Dave Alciatore — physique du billard](https://billiards.colostate.edu/) |
| Moment d'inertie d'une sphère pleine : I = 2/5·m·r² (facteur 0,4, d'où le facteur 3,5 sur le glissement) | `SphereInertiaFactor`, `SlipResponseFactor` | [Moment d'inertie](https://fr.wikipedia.org/wiki/Moment_d%27inertie) |
| Résistance au roulement : ralentir vitesse et rotation ensemble (v = ω × r conservé) | `PoolBall.ApplyRollingResistance` | [Résistance au roulement](https://fr.wikipedia.org/wiki/R%C3%A9sistance_au_roulement) |
| Frottement de Coulomb | Matériaux physiques tapis / bandes / billes | [Frottement](https://fr.wikipedia.org/wiki/Frottement) |
| Chocs quasi élastiques entre billes, coefficient de restitution ≈ 0,92 | `BallPhysics` (bounciness 0,92) | [Choc élastique](https://fr.wikipedia.org/wiki/Choc_%C3%A9lastique) · [Coefficient de restitution](https://fr.wikipedia.org/wiki/Coefficient_de_restitution) |
| Combinaison des matériaux physiques (Average, Minimum, Maximum) | Commentaires de `PoolTableBuilder` | [Unity — Physic Material](https://docs.unity3d.com/Manual/class-PhysicsMaterial.html) |
| Effet : couple = décalage du point de frappe × impulsion | `LocalPoolAimController.Shoot` | [Moment d'une force](https://fr.wikipedia.org/wiki/Moment_de_force) · [Produit vectoriel](https://fr.wikipedia.org/wiki/Produit_vectoriel) |
| Direction de la bille touchée = ligne des centres au contact (masses égales) | `UpdatePreview` | [Choc élastique](https://fr.wikipedia.org/wiki/Choc_%C3%A9lastique) |
| Pas de temps fixe de la physique | Réglage 50 Hz | [Glenn Fiedler — Fix Your Timestep!](https://gafferongames.com/post/fix_your_timestep/) |

## Règles du billard (`IPoolRuleSet`)

| Mode | Sources |
|---|---|
| 8-ball | [Wikipédia (en)](https://en.wikipedia.org/wiki/Eight-ball) · [Règles officielles WPA](https://wpapool.com/rules-of-play/) |
| 9-ball | [Wikipédia (en)](https://en.wikipedia.org/wiki/Nine-ball) |
| 14.1 continu | [Wikipédia (en)](https://en.wikipedia.org/wiki/Straight_pool) |

Les règles du jeu sont des versions décontractées (écarts listés dans le GDD).

## Géométrie et mouvements

| Notion | Où dans le code | Sources |
|---|---|---|
| Produit scalaire : alignement d'une poche avec la direction choisie (score = alignement / distance) | `StepHighlightedPocketTowards` | [Produit scalaire](https://fr.wikipedia.org/wiki/Produit_scalaire) |
| Sortie d'un rayon d'un rectangle (distance pour se placer hors de la table) | `PoolTableSurface.DistanceToClearPlayArea` | [Slab method (en)](https://en.wikipedia.org/wiki/Slab_method) |
| Balayage d'une sphère pour la trajectoire | `UpdatePreview` (`Physics.SphereCast`) | [Unity — SphereCast](https://docs.unity3d.com/ScriptReference/Physics.SphereCast.html) |
| Rotations par quaternions, interpolation sphérique | Inclinaison de la queue, corps en visée | [Quaternions et rotation](https://fr.wikipedia.org/wiki/Quaternions_et_rotation_dans_l%27espace) · [Slerp (en)](https://en.wikipedia.org/wiki/Slerp) |
| Lissage exponentiel indépendant du framerate : 1 − e^(−k·dt) | `ApplyHandShift` (inclinaison) | [Lissage exponentiel](https://fr.wikipedia.org/wiki/Lissage_exponentiel) |
| Ressort amorti (SmoothDamp) | `PickUpCue`, animations | [Unity — SmoothDamp](https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html) |
| Hauteur de caméra selon le rapport largeur/hauteur de l'écran | `PlacementHeightForCurrentViewport` | [Champ de vision (en)](https://en.wikipedia.org/wiki/Field_of_view_in_video_games) |

## Personnage

| Notion | Sources |
|---|---|
| Cinématique inverse | [Wikipédia](https://fr.wikipedia.org/wiki/Cin%C3%A9matique_inverse) · [Final IK](http://www.root-motion.com/finalikdox/html/index.html) |
| Ragdoll actif (PuppetMaster) | [Ragdoll physics (en)](https://en.wikipedia.org/wiki/Ragdoll_physics) · [PuppetMaster](http://root-motion.com/puppetmasterdox/html/pages.html) |
| Blend trees et calques d'animation | [Unity — Blend Trees](https://docs.unity3d.com/Manual/class-BlendTree.html) · [Animation Layers](https://docs.unity3d.com/Manual/AnimationLayers.html) |

## Architecture du code

| Notion | Où | Sources |
|---|---|---|
| Stratégie | `IPoolRuleSet` | [Wikipédia](https://fr.wikipedia.org/wiki/Strat%C3%A9gie_(patron_de_conception)) |
| Singleton | `PoolMatchRules.Instance` | [Wikipédia](https://fr.wikipedia.org/wiki/Singleton_(patron_de_conception)) |
| Observateur (événements) | `PoolBall.Pocketed`, `PoolMatchRules.Fouled` | [Wikipédia](https://fr.wikipedia.org/wiki/Observateur_(patron_de_conception)) |
| Données séparées du code | ScriptableObjects | [Unity — ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html) |

## Rendu

| Notion | Sources |
|---|---|
| Shaders, pipeline graphique | [Shader](https://fr.wikipedia.org/wiki/Shader) · [Pipeline graphique (en)](https://en.wikipedia.org/wiki/Graphics_pipeline) |
| Ombres | [Shadow mapping (en)](https://en.wikipedia.org/wiki/Shadow_mapping) · [Unity — cascades d'ombres](https://docs.unity3d.com/Manual/shadow-cascades.html) |
| Rendu différé et Forward+ | [Deferred shading (en)](https://en.wikipedia.org/wiki/Deferred_shading) · [Unity — chemins de rendu URP](https://docs.unity3d.com/Manual/urp/rendering-paths-comparison.html) |
| Occlusion ambiante | [SSAO (en)](https://en.wikipedia.org/wiki/Screen_space_ambient_occlusion) |
| Rendu physique | [PBR (en)](https://en.wikipedia.org/wiki/Physically_based_rendering) |

## Chantiers envisagés

| Chantier | Sources |
|---|---|
| En ligne : serveur qui fait autorité, prédiction côté client | [Gabriel Gambetta — Client-Server Game Architecture](https://www.gabrielgambetta.com/client-server-game-architecture.html) · [Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/) |
| Déterminisme de la physique | [Glenn Fiedler — Floating Point Determinism](https://gafferongames.com/post/floating_point_determinism/) |
| Game feel / « juice » | [Jonasson & Purho — Juice it or lose it (vidéo)](https://www.youtube.com/watch?v=Fy0aCDmgnxg) |
| Pouvoirs de rattrapage | [Équilibrage dynamique de la difficulté (en)](https://en.wikipedia.org/wiki/Dynamic_game_difficulty_balancing) |
| Gros fichiers dans git | [Git LFS](https://git-lfs.com/) |
| Build automatique Unity | [GameCI](https://game.ci/) |
| Tests des règles | [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) |
| Réassignation des touches | [Input System — rebinding](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/ActionBindings.html) |

## Outils de ce site

[Mermaid](https://mermaid.js.org/) (diagrammes) · [marked](https://marked.js.org/) (Markdown) · [DOMPurify](https://github.com/cure53/DOMPurify) (sécurité) · [GitHub Pages](https://docs.github.com/fr/pages).
