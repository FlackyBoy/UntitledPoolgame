# Historique des prompts consolidés

> Synthèse des demandes faites à Claude, regroupées par période et par sujet, à partir des sessions enregistrées localement du **3 au 29 septembre 2026** (970 messages, dont beaucoup de simples « ok » pendant des configurations pas à pas). Les sessions antérieures (24 août → 1er septembre) ne sont plus disponibles en local : le CHANGELOG couvre cette période. Les journaux collés dans les messages ne sont pas repris ; le jeton d'accès GitHub partagé le 28/09 a été retiré.

## Consignes permanentes données en cours de route

Ces demandes s'appliquent à tout le travail, pas à une tâche précise :

- **Tenir les fichiers Markdown à jour à chaque fois** (TODO, CHANGELOG) — « c'est super important » (16/09).
- **Ne pas supprimer ce qui appartient aux plugins** : corriger les erreurs de compilation sans supprimer de fichiers (08/09).
- **Donner les étapes de configuration d'un seul coup**, précisément, plutôt qu'au compte-gouttes (07/09, 11/09).
- **Utiliser ce que les plugins font nativement** avant d'écrire du code maison — « retire tout ce qui peut être fait nativement par PuppetMaster » (09/09).
- **Une modification à la fois, avec des logs** pour comparer avant/après, plutôt que des corrections à l'aveugle (21/09).
- **Retirer immédiatement ce qui est demandé** quand une piste ne marche pas (« retire ce que tu viens de faire », 23/09).
- **Préférer l'IK aux animations** pour la visée, le tir et les interactions avec la table (17/09).
- Le mode en ligne est à garder en tête mais **le focus est le mode local** ; les scripts online sont finalement conservés mais figés (28/09).

## 3 septembre — visée, fin de partie 8-ball

- Aligner le joueur sur la direction de visée en sortant du mode visée.
- **Condition de victoire** en 8-ball : blanche et noire empochées ensemble = défaite.
- **Annonce de poche pour la 8** en vue de dessus avec un sélecteur en surbrillance, navigation ZQSD relative à la caméra, un appui = une poche.
- Bug : la blanche placée dans une poche après une faute bloquait le coup suivant.
- Triche **C+W** pour passer directement en fin de partie 8-ball.
- Pouvoirs multiples : **un seul en stock, façon Mario Kart**, une caisse ramassée en trop disparaît sans remplacer le pouvoir.

## 4 septembre — pouvoirs, ramasser et lancer

- Nouveau pouvoir **Fermer une poche** (d'abord au hasard) ; il doit gêner le joueur suivant, pas celui qui l'active ; prefab personnalisable et taille réglable.
- **Ramasser et lancer des objets** (modes concernés à décider plus tard).
- **Pouvoirs hors tour** : seulement pertinent en écran partagé / en ligne ; case à cocher par pouvoir ; effet **immédiat** si c'est déjà le tour de l'adversaire.

## 5 → 7 septembre — personnage animé

- Intégrer un **personnage visible avec animations**, et un **ragdoll** qui dépend de la force et de l'endroit de l'impact, avec relevé animé.
- Rejouer une partie depuis l'écran de fin.
- Configuration de l'Animator pas à pas : Idle, marche, **strafe**, diagonales, vitesse d'animation calée sur le déplacement, rotation sur place animée.
- Liste d'animations à prévoir : strafe, course, visée, emote. Environnements à prévoir : **maison/prison hantée, base spatiale**.

## 7 → 14 septembre — ragdoll (MMRagdoller puis PuppetMaster)

- Premiers essais avec **MMRagdoller** (Feel) : relevés en boucle, personnage décalé ou sous le sol → passage à **PuppetMaster** (7/09).
- Longue mise au point du prefab : couches, colliders, échelle du rig (problème réel trouvé côté Blender : échelle ×100), mannequin de test, point d'apparition.
- **Caméra ragdoll à la 3ᵉ personne avec Cinemachine** (idée proposée par l'utilisateur, 8/09), puis corrections de décalage et de repositionnement après le relevé.
- Impact **localisé et proportionnel à la force** du lancer ; relever le personnage là où il a atterri.
- Demande d'un **fichier de suivi du chantier ragdoll** pour ne pas revenir en arrière (→ `RAGDOLL_DEBUGGING_LOG.md`, 14/09).
- Tenir un objet en main via PuppetMaster (props) — mis de côté.

## 11 → 16 septembre — contrôleur, écran partagé, animations avec la queue

- Contrôleur clavier/manette/souris et caméra sur le nouveau prefab `poolPlayerMixamo`.
- Écran partagé : caméras qui suivaient le mauvais joueur, entrées de P1 actives pour P2.
- Animator basé sur le contrôleur de démo *Humanoid Third Person Puppet* ; calques haut du corps / bas du corps ; poses **tenir la queue** distinctes de **tenir un autre objet**.
- Désactiver l'IK et lâcher la queue pendant une chute (ajouté à la TODO).

## 15 → 18 septembre — Final IK

- Installation de **Final IK** : Aim IK, Limb IK, Look At IK, Full Body Biped IK.
- Décision (17/09) : **la visée et le tir se feront par IK**, avec des cibles placées dans la scène, et non par des animations dédiées.
- Solution trouvée par l'utilisateur pour la posture : décocher **Rotate Once** sur l'Interaction Target (18/09).
- Liste des « animations » IK à faire : viser/tirer, tourner autour de la table, frapper, lancer, lâcher la queue en ragdoll, attraper un autre joueur.
- Ajout à la TODO : **craie sur la queue** (bonus / malus).
- Ramasser la queue avec E, seulement **de près et face à elle**.

## 18 → 23 septembre — ramassage de la queue (Interaction System)

- Longue série d'essais pour que la queue soit prise proprement quel que soit le côté d'approche : orientation automatique avant la prise, lévitation vers le joueur, suivi des mains, rotation forcée, `TwoHandedProp`, Limb IK, Aim IK. **Aucune n'a donné un résultat satisfaisant** ; chaque essai a été retiré à la demande.
- Hypothèses de l'utilisateur vérifiées en cours de route : le passage en *kinematic* au moment de l'appui, l'orientation du *hold point* qui ne suit pas le personnage.
- Solution retenue (23/09) : **Interaction Trigger** de Final IK pour la zone et l'angle d'approche, prise à deux mains, orientation fixe une fois tenue.
- Faire avancer/reculer la main pour que la queue glisse dynamiquement.

## 23 → 25 septembre — visée avec le personnage

- Caméra de visée, tir, retour de la vue de dessus pour la main libre.
- La queue doit être **dans l'axe de la bille blanche**, le corps **placé autour de la table sans y entrer**, la tête qui **regarde la bille en visée** et suit la caméra sinon.
- Si la bille est trop loin : **garder la queue au corps et interdire le tir** (idée de l'utilisateur, 24/09).
- **Couches physiques** : le corps et la queue ne doivent pas pousser les billes.
- **Bend goals** des bras : seulement déplacer ceux qui existent selon l'état, sans ajouter de système parallèle.
- **Le point de frappe déplace la queue** en visée.
- Idée : récupérer automatiquement les références assignées à la main au spawn du joueur.

## 28 septembre — dépôt git et revue du code

- Configuration pour que Claude puisse **pousser directement sur GitHub** (jeton limité à ce dépôt) ; envoi découpé en plusieurs morceaux après des erreurs HTTP 500.
- **Revue complète du code** : refacto et optimisation → correctifs appliqués « selon les recommandations » et validés en jeu.
- **Doublons online/local** : un seul jeu de scripts de référence (`Local*`), les scripts online **conservés mais figés**.
- Ajout à la TODO : **créateur de niveau** (outil simplifié).

## 29 septembre — site du projet

- Page HTML de la TODO, puis **site GitHub Pages** : TODO + GDD + pistes & suggestions, **documentation technique** (cette page), **propositions d'UI**, **outil de génération de niveau** (inspiré de *BMT Building Maker Toolset*).
- Laisser de côté le JSON et documenter les **fichiers de configuration en ScriptableObjects**.
- La version **locale** de la TODO et du CHANGELOG fait foi.
- Création de `CLAUDE.md` pour garder le contexte d'une session à l'autre.

## Enseignements

- Les chantiers les plus longs (ragdoll, ramassage de la queue) ont avancé quand la cause a été **observée** (logs, lecture du code du plugin, test dans une scène de démo) et piétiné quand les réglages étaient changés **sans diagnostic**.
- Plusieurs solutions décisives sont venues de l'utilisateur en testant dans l'éditeur (échelle du rig, *Rotate Once*, caméra Cinemachine séparée, garder la queue au corps quand la bille est trop loin).
- Les configurations d'éditeur (prefabs, Animator, PuppetMaster) sont un point de fragilité : faire une sauvegarde du prefab avant un chantier, et documenter la configuration attendue.
