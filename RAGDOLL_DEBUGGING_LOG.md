# Journal de debug — Ragdoll PuppetMaster (relevé après chute)

But de ce fichier : garder la trace de TOUT ce qui a été tenté sur le problème
du relevé du ragdoll, pour ne pas re-proposer une piste déjà testée et écartée.
À lire avant toute nouvelle tentative sur ce sujet.

## ✅ RÉSOLU — Vraie cause racine (trouvée par l'utilisateur)

Après tout ce qui suit dans ce journal (des dizaines de tentatives côté code,
toutes écartées une par une), la cause réelle n'était PAS dans le code de ce
projet du tout : dans l'Inspector de `Behaviour Puppet`, sous
**`Defaults` (Muscle Group Properties)**, les champs **`Regain Pin Speed`** et
**`Max Mapping Weight`** étaient tous les deux à **0** — à bien distinguer du
champ `Regain Pin Speed` GLOBAL, plus haut dans le même Inspector, qui lui
était correct à 1 (source de confusion : deux champs de même nom à deux
endroits différents du composant). Avec ces deux valeurs de `Defaults` à 0,
`pinWeightMlp`/`mappingWeightMlp` (voir `Muscle.State`) restaient bloqués à
0 pour toujours après un relevé, quel que soit le code écrit autour — la force
de pin ne remontait donc jamais réellement, PuppetMaster ne "tenait" jamais
la pose debout.

**Fix** : remis les deux champs à leur valeur par défaut (1) dans l'Inspector.
Confirmé par logs (`pinWeightMlp` stable à 1.00 sur plusieurs secondes après
relevé, alors qu'il était bloqué à 0.00 avant) et par test utilisateur ("Ok ça
a l'air d'etre bon ! Houa !").

**Conséquence** : plus besoin d'AUCUN `Teleport()` ni de repositionnement
manuel des muscles — une fois la force de pin réellement fonctionnelle,
PuppetMaster fait tout le travail nativement. L'architecture finale retenue
(voir CHANGELOG 2026-09-16) : `LocalCharacterModelFollow.cs` (suivi continu
racine↔enfant animé, désactivé pendant le ragdoll) + `OnRecovered()` qui lit
juste où PuppetMaster a déjà laissé le mesh pour repositionner la racine,
sans toucher aux muscles.

**Leçon pour la prochaine fois qu'un comportement PuppetMaster semble ne PAS
suivre son API (positions/poids qui ne bougent jamais malgré un code a priori
correct)** : vérifier D'ABORD les valeurs `Defaults` de `Behaviour Puppet`
dans l'Inspector (pas juste les champs globaux du même nom) avant de
soupçonner le code.

## Symptôme d'origine (avant la découverte ci-dessus — gardé pour mémoire, ne
plus reproposer ces pistes)

Après une chute + relevé, le squelette physique du ragdoll (les os/muscles
PuppetMaster) ne correspond pas à la position du mesh visible — il reste
visuellement décalé (souvent proche du sol / de l'endroit de la chute) alors
que le mesh animé, lui, a l'air debout. Voir dernier screenshot fourni par
l'utilisateur : gizmos du squelette physique clairement séparés du corps
visible après un cycle chute/relevé, malgré la dernière tentative de fix.

## Architecture du personnage concerné

- Prefab : `Assets/Prefabs/PlayerLocal/poolPlayerMixamo Root.prefab`
  (personnage Mixamo, différent du tout premier perso Rigify sur lequel le
  ragdoll avait été mis au point au début de ce chantier).
- Structure à DEUX objets (racine + enfant), confirmée correcte par
  comparaison directe avec la hiérarchie réelle de la démo RootMotion :
  - `poolPlayerMixamo Root` (racine) : `Character Controller`, `Player Input`,
    `Local Fps Player Controller`, `Local Character Animation Controller`,
    `Local Player Ragdoll Controller`, `Local Player Hand Controller`.
    - `Behaviours` (enfant) : `Puppet (with Fall)` (`BehaviourPuppet`), `Fall`
      (`BehaviourFall`).
    - `PuppetMaster` (enfant) : composant `PuppetMaster`, `targetRoot` = 
      `poolPlayerMixamo` (voir plus bas).
    - `poolPlayerMixamo` (enfant) : `Animator`, cible animée de PuppetMaster.
    - `CameraPivot` (enfant) : `Camera`, `CM_FPS` (Cinemachine).
    - `CM_Ragdoll` (enfant, au même niveau que les autres, PAS sous
      `CameraPivot`) : caméra ragdoll 3e personne.
- Animator Controller utilisé : `Humanoid Third Person Puppet 2.controller`
  (copie de démo RootMotion), paramètres `Forward`/`Right`/`Turn`/`IsStrafing`/
  `OnGround`/`Crouch`/`Jump`/`JumpLeg`/`ActionIndex`/`FallBlend`/`DoubleJump`.
  États pertinents : `Grounded Strafe` (actif en jeu, `IsStrafing=true`),
  `GetUpProne`, `GetUpSupine`.

## Scripts actuels impliqués

- `Assets/Scripts/Player/LocalPlayerRagdollController.cs` — sur la racine.
  Réagit à `BehaviourPuppet.onLoseBalance`/`onRegainBalance` (events Inspector,
  PAS de polling). `OnKnockedDown()` désactive les contrôleurs + le nouveau
  `LocalCharacterModelFollow`. `OnRegainBalance()` attend `getUpRecoveryDelay`
  (6s) puis `OnRecovered()` : calcule où PuppetMaster a DÉJÀ (nativement)
  positionné `modelTransform`, en déduit la position/rotation (yaw seul) que
  doit prendre la racine pour que `modelTransform` retrouve son offset local
  de repos, déplace la racine, puis `modelFollow.ResyncNow()` +
  `modelFollow.enabled = true`.
- `Assets/Scripts/Player/LocalCharacterModelFollow.cs` — NOUVEAU, sur l'enfant
  `poolPlayerMixamo`. Calque le mécanisme réel de la démo RootMotion
  (`CharacterAnimationBase.SmoothFollow`, lu directement dans
  `Assets/Plugins/RootMotion/Shared Demo Assets/Scripts/Character
  Controllers/CharacterAnimationBase.cs`) : recale en PERMANENCE (LateUpdate)
  l'enfant sur `parent.TransformPoint(offset local de repos)` avec lissage
  (Lerp/Slerp, vitesse `followSpeed=20`). Désactivé par
  `LocalPlayerRagdollController` pendant toute la durée du ragdoll.
- `Assets/Scripts/Player/RagdollHitRelay.cs` — un par os du ragdoll, appelle
  `LocalPlayerRagdollController.NotifyCollision()` sur un impact qualifiant.
- `LocalPlayerRagdollController.NotifyCollision()` appelle
  `behaviourPuppet.SetHasCollided(true)` (voir plus bas, fix confirmé
  fonctionnel pour le déclenchement).

## Tout ce qui a été tenté pour le repositionnement au relevé (dans l'ordre)

1. **Position du bassin + raycast vers le sol** (première version, sur
   l'ancien perso Rigify) — fonctionnait sur CE perso-là après avoir
   suffisamment augmenté `getUpRecoveryDelay` (6s) pour laisser le clip de
   relevé se terminer avant d'échantillonner. Confirmé fonctionnel par
   l'utilisateur À L'ÉPOQUE sur le perso Rigify.
   - Repris tel quel sur `poolPlayerMixamo` (perso Mixamo) → ne suffisait
     plus (voir tentatives suivantes), le contexte n'est pas strictement
     identique (rig différent, Animator Controller différent).

2. **`PuppetMaster.Teleport(pos, rot, moveToTarget:true)`** appliqué à
   `modelTransform`, calculé en position monde à partir de l'offset local
   de repos — testé pour synchroniser mesh ET muscles d'un coup (au lieu de
   laisser les muscles rattraper progressivement).
   - Logs de diagnostic : le Teleport lui-même fonctionne correctement
     (muscle0/bassin finit à la bonne hauteur debout, X/Z corrects).
   - MAIS : provoque une chute soudaine et nette du ragdoll juste après
     ("comme si rien ne le tenait", pas progressif) — hypothèse : les
     `ConfigurableJoint` se retrouvent sous tension après un snap rigide
     instantané de tout le squelette d'un coup.
   - **Écarté.**

3. **Reset simple `modelTransform.SetLocalPositionAndRotation(rest)`** (sans
   Teleport) — laisser PuppetMaster rattraper les muscles tout seul via sa
   force de pin normale (progressive).
   - Résultat : le ragdoll reste bloqué au sol INDÉFINIMENT. La force de pin
     de PuppetMaster est trop faible pour faire passer tout le corps d'une
     pose "allongée" à "debout" — elle est prévue pour de petits ajustements,
     pas une transition de pose complète.
   - **Écarté.**

4. **Dériver la position de la racine à partir de la position déjà (censément)
   correcte de `modelTransform`** (sans toucher aux muscles ni faire de
   Teleport) — lire où PuppetMaster a nativement laissé `modelTransform`
   (`MoveTarget`/`GroundTarget` internes à `BehaviourPuppet`, actifs pendant
   l'état `Unpinned`), en déduire la position racine par la même géométrie que
   la tentative actuelle (voir "État actuel" plus haut).
   - Testé UNE PREMIÈRE FOIS avec encore l'architecture à deux objets
     d'origine (avant la restructuration, tentative 6 ci-dessous) :
     utilisateur a répondu "toujours pas" avec un screenshot montrant le
     squelette physique décalé au sol par rapport au mesh debout — CE
     N'ÉTAIT DONC DÉJÀ PAS SUFFISANT tel quel, sans mécanisme de suivi
     continu en complément.
   - Refait ENSUITE avec le nouveau `LocalCharacterModelFollow` en
     complément (tentative actuelle, voir "État actuel") — toujours pas
     résolu au moment de ce journal (dernier screenshot).

5. **Hypothèse (fausse, écartée après vérification) : la démo RootMotion
   n'a AUCUNE séparation parent/enfant**, tout serait sur le même objet
   (conclusion tirée à tort de la lecture de `CharacterThirdPerson.cs`
   seul — `OnAnimatorMove()` faisait croire que le script de mouvement et
   l'Animator étaient forcément sur le même GameObject).
   - **Corrigée après avoir vu un screenshot de la vraie hiérarchie de la
     démo** : `ThirdPersonPuppet` (racine) → `Character Controller` (enfant)
     → `Animation Controller` → `Pilot` (Animator, encore plus bas) → os du
     ragdoll. La démo a BIEN une séparation parent/enfant, comme nous.
   - Ne jamais reproposer "tout mettre sur un seul objet" sans revérifier la
     vraie hiérarchie de référence.

6. **Restructuration complète du prefab** pour mettre `CharacterController`/
   `PlayerInput`/tous les scripts `Local*` sur le MÊME objet que l'Animator/
   `targetRoot` (suite à l'hypothèse fausse ci-dessus) — `poolPlayerMixamo`
   promu en racine du prefab, ancien wrapper supprimé.
   - Résultat : PIRE qu'avant. Deux nouveaux problèmes :
     a. Téléportation soudaine sur une grande distance PENDANT la chute
        elle-même (pas seulement au relevé) — parce que PuppetMaster fait
        alors suivre `targetRoot` (= toute la hiérarchie racine, caméra
        comprise) au bassin qui tumble/roule au sol EN CONTINU pendant tout
        l'état `Unpinned`, pas juste une fois au relevé.
     b. Câblage des events `On Lose Balance`/`On Regain Balance` sur
        `Behaviour Puppet` CASSÉ silencieusement (le champ Object pointait
        vers l'ancien GameObject racine, supprimé pendant la restructuration
        → référence Unity détruite, event devenu inopérant sans erreur
        visible).
   - **Entièrement annulée** : restauration depuis
     `poolPlayerMixamo Root Backup2.prefab` (backup fait juste avant cette
     tentative). L'ancien fichier restructuré a été gardé de côté sous
     `poolPlayerMixamo Root (broken).prefab` si besoin de le ré-inspecter.
   - **Leçon** : ne PAS refaire cette restructuration. La séparation à deux
     objets doit être conservée.

## Mise à jour (session suivante) — diagnostic par logs, avancée réelle

Suite au constat "on tourne en rond", des `Debug.Log` temporaires ont été
ajoutés (dans `LocalCharacterModelFollow` et `LocalPlayerRagdollController`)
pour objectiver plutôt que deviner. Résultats concrets :

- **`modelTransform`/la racine se synchronisent parfaitement** — logs
  confirmés stables sur plusieurs frames après `OnRecovered()` (root pos ==
  modelTransform pos en permanence, aucune dérive). Ce n'est PAS le problème.
- **Le vrai problème est ailleurs** : les os PHYSIQUES du ragdoll (muscles
  PuppetMaster, Rigidbody séparés) ne suivent pas `modelTransform` tout seuls
  (confirme l'attempt 3 du journal : force de pin native trop faible pour une
  transition complète allongé→debout).
- **Re-testé `PuppetMaster.Teleport(pos, rot, moveToTarget: true)`**, cette
  fois avec une position bien vérifiée par logs (donc plus le problème de
  "mauvaise position de départ" suspecté avant) : le muscle du bassin
  (`muscles[0]`) monte bien à hauteur debout (~Y=1.1) IMMÉDIATEMENT après le
  Teleport — la fonction marche techniquement. **MAIS** il redescend ensuite
  progressivement (PAS un crash instantané, un affaissement sur ~1.5-2
  secondes) jusqu'à se stabiliser à une hauteur basse (~Y=0.25-0.35, pose
  écrasée/accroupie), pendant que `behaviourPuppet.state` reste `Puppet` tout
  du long (donc ce n'est PAS un nouveau déclenchement officiel de chute —
  c'est un pur affaissement physique, PuppetMaster "pense" que tout va bien).
- **Hypothèse testée et ÉCARTÉE** : pin weight pas encore assez remonté après
  le relevé (`regainPinSpeed`/`pinWeightMlp`, voir `BehaviourPuppet.cs`).
  Testé en portant `Get Up Recovery Delay` de 6s à 10s (donc en laissant
  beaucoup plus de temps à la force de pin pour remonter avant d'appeler
  Teleport) — RÉSULTAT IDENTIQUE au trait près (même courbe de descente
  Y=1.11→~0.25 sur le même intervalle de temps, peu importe le délai
  précédent). **Ce n'est donc PAS un problème de timing/pin-ramp.**
- **Conclusion actuelle** : le problème vient de `moveToTarget: true`
  lui-même dans l'appel à `Teleport()` — cette option force CHAQUE muscle à
  se replacer individuellement et instantanément sur sa cible d'un coup,
  indépendamment du timing. Suspicion : ça mmet les `ConfigurableJoint` sous
  tension (cf. le commentaire `@todo` trouvé dans le code source de
  PuppetMaster : *"might it be that when Kinematic, joints are under stress
  for not having their anchors updated and will fly away when the puppet is
  activated?"*).
- **Testé `moveToTarget: false`** : toujours pas résolu, ET nouveau
  comportement surprenant — alors que la position cible passée à `Teleport()`
  est IDENTIQUE à la position actuelle de `modelTransform` (donc delta
  théorique nul, `moveToTarget: false` ne devrait rigoureusement rien faire
  bouger), le muscle du bassin saute quand même loin de sa position
  précédente dès que le Teleport différé est traité (log
  "OnRecovered START" juste avant = proche de `modelTransform`, log
  "+0,2s" juste après = décalé de ~1,9 unité). Donc soit la compréhension du
  fonctionnement DIFFÉRÉ de `Teleport()` (traité au `Read()` suivant de
  PuppetMaster, pas immédiatement) est incomplète, soit autre chose modifie
  `targetRoot`/les muscles entre l'appel et son traitement effectif.
- **Bilan à ce stade** : la synchronisation racine ↔ `modelTransform` est
  fiable et prouvée par logs (aucune dérive sur plusieurs secondes). Le
  déclenchement de la chute, la caméra, la coupure/reprise des contrôles
  fonctionnent tous correctement. Seul le repositionnement PRÉCIS des
  muscles physiques du ragdoll pour qu'ils collent au mesh juste après le
  relevé résiste — chaque variante de `Teleport()` testée introduit un
  problème différent (collapse progressif avec `moveToTarget:true`, saut
  inexpliqué avec `moveToTarget:false`) plutôt que de régler celui d'avant.
- **Option pragmatique à considérer** : arrêter d'essayer de forcer la
  synchronisation exacte des muscles via `Teleport()`, accepter que les
  colliders physiques du ragdoll restent visuellement un peu décalés du mesh
  juste après un relevé (défaut cosmétique), puisque tout le reste
  (déclenchement fiable, caméra, contrôles, position de la racine/
  `CharacterController` pour le gameplay) fonctionne correctement et est
  vérifié par logs. À rouvrir plus tard si besoin, pas urgent pour avancer
  sur autre chose.

## État actuel (dernière tentative en date, toujours pas résolue)

Architecture à deux objets restaurée (backup2) + nouveau
`LocalCharacterModelFollow.cs` ajouté sur l'enfant `poolPlayerMixamo`,
inspiré du VRAI mécanisme de la démo (`CharacterAnimationBase.SmoothFollow`) :
suivi continu de la racine par l'enfant, désactivé pendant tout le ragdoll,
réactivé + resynchronisé (`ResyncNow()`) après le calcul de repositionnement
de la racine dans `OnRecovered()`.

**Testé par l'utilisateur → toujours pas résolu.** Screenshot montre encore le
squelette physique du ragdoll décalé/couché près du sol alors que le mesh a
l'air débout à côté. Cause exacte pas encore identifiée à ce stade — la
prochaine session doit repartir d'ici, PAS refaire les tentatives 1 à 6
ci-dessus.

## Pistes non encore explorées (à essayer ensuite)

- Vérifier avec des logs (comme précédemment, `Debug.Log` temporaires
  acceptés et efficaces dans ce projet) si `LocalCharacterModelFollow.Awake()`
  capture bien le BON offset de repos (`localRestPosition`/`localRestRotation`)
  — si ce cache est capturé à un mauvais moment (avant que PuppetMaster/le
  rig ne soit complètement initialisé), toute la suite serait fausse.
- Vérifier que `modelFollow.enabled = false` dans `OnKnockedDown()` se produit
  bien AVANT que PuppetMaster ne commence à bouger `modelTransform` — sinon
  le `LateUpdate` de `LocalCharacterModelFollow` pourrait continuer à tirer
  l'enfant vers l'ancienne position racine pendant un ou plusieurs frames au
  tout début de la chute, avant d'être coupé.
- Vérifier l'ORDRE D'EXÉCUTION des scripts (Script Execution Order dans les
  Project Settings) entre `LocalCharacterModelFollow.LateUpdate()` et
  n'importe quel autre système touchant `modelTransform`/la racine la même
  frame — un ordre différent de celui attendu pourrait expliquer un résultat
  incohérent.
- Revérifier que `OnRecovered()` se déclenche bien APRÈS que PuppetMaster ait
  fini tout repositionnement natif (le délai `getUpRecoveryDelay` a été
  raccourci par l'utilisateur à un moment — vérifier sa valeur actuelle et si
  elle est encore assez longue).
- Envisager d'ajouter des `Debug.Log` similaires à ceux utilisés avec succès
  plus tôt dans ce chantier (position de `modelTransform`, position de la
  racine calculée, valeurs juste avant/après `ResyncNow()`) pour objectiver
  ce qui se passe réellement plutôt que de raisonner dans le vide.
- Vérifier si `PuppetMaster.mappingWeightMlp`/`pinWeightMlp` (rampe
  progressive after get-up, voir `BehaviourPuppet.cs` autour de la ligne 700)
  a réellement atteint 1 au moment où `OnRecovered()` échantillonne
  `modelTransform` — si ce n'est pas le cas, le mesh visible pourrait
  lui-même ne pas encore refléter la vraie pose finale à ce moment précis
  (indépendamment de tout ce qu'on fait dans notre propre code).

## Réglages/paramètres tunés en cours de route (contexte, pas des bugs)

- `Behaviour Puppet` → `Collision Layers` : couvre `TransparentFX` (layer où
  l'utilisateur place volontairement les objets de test à percuter) — déjà
  correct, ne pas re-suspecter cette piste.
- `Behaviour Puppet` → `Collision Threshold` : abaissé de 700 (bien trop
  élevé) à 0.
- `Behaviour Puppet.hasCollidedSinceGetUp` (champ privé, doc du plugin :
  *"Puppets do not get unpinned if they haven't collided with anything on the
  Collision Layers since the last time they got up"*) — se réarme
  automatiquement seulement si `puppetMaster.pinWeight < 1f`, trop peu fiable
  en pratique (le perso tombait une fois puis plus jamais après un relevé) →
  fix confirmé fonctionnel : `RagdollHitRelay` appelle désormais
  `LocalPlayerRagdollController.NotifyCollision()` →
  `behaviourPuppet.SetHasCollided(true)` sur chaque impact qualifiant. **Ce
  fix-là fonctionne, ne pas y retoucher.**
- `Behaviour Fall` (sur l'objet `Fall`) : `Can End` devait être coché, et son
  `On End` → `Switch To Behaviour` = `BehaviourPuppet` — sur `poolPlayerMixamo`
  ces deux champs étaient vides/décochés par défaut (contrairement à l'ancien
  perso Rigify) → corrigé, confirmé fonctionnel pour que le cycle
  Puppet→Fall→Puppet boucle correctement.
- `Get Up Recovery Delay` : 6s à l'origine (calé sur l'ancien perso Rigify),
  raccourci depuis par l'utilisateur pour accélérer le relevé sur
  `poolPlayerMixamo` — valeur actuelle à reconfirmer avant la prochaine
  session de debug.
