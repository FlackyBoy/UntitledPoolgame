# Des fichiers de configuration au rendu final

> Comment une valeur réglée dans un fichier de configuration (ScriptableObject) finit par changer ce que le joueur voit à l'écran.

## Le principe

Un **ScriptableObject** est un fichier de données Unity (`.asset`) modifiable dans l'Inspector, indépendant des scènes. Le projet s'en sert pour deux choses :

1. **Les réglages partagés** — un seul fichier par thème, placé dans `Assets/Resources`, chargé au démarrage.
2. **Les pouvoirs** — un fichier par pouvoir (`Assets/Powers`), chacun avec ses propres valeurs.

Avantages : on règle sans toucher au code ni aux scènes, les mêmes valeurs servent partout, et un fichier manquant ne casse rien (valeurs par défaut).

## Chaîne complète

```mermaid
flowchart LR
  A["Fichier .asset<br/>(Assets/Resources)"] --> B["PoolSettingsLoader.LoadOrDefault<br/>(Resources.Load, sinon défauts)"]
  B --> C["Composants en jeu<br/>(PoolBall, PoolPocket, PoolMatchRules,<br/>LocalPoolAimController…)"]
  C --> D["Simulation<br/>physique (FixedUpdate 50 Hz)<br/>logique (Update)"]
  D --> E["Transforms, lumières,<br/>particules, caméra"]
  E --> F["Rendu URP Forward+<br/>(ombres, SSAO, post-process)"]
  F --> G["Image par joueur<br/>(écran partagé)"]
```

1. **Création** : **Tools > Pool > Ensure Config Assets Exist** crée les fichiers manquants (sans toucher aux existants).
2. **Chargement** : chaque composant appelle `PoolSettingsLoader.LoadOrDefault<T>("Nom")` dans son `Awake`. Si le fichier n'existe pas : avertissement dans la console et valeurs par défaut du code.
3. **Utilisation** : les valeurs sont lues pendant le jeu (pas de copie figée) — modifier l'asset en Play Mode se voit immédiatement, et la modification est conservée après l'arrêt (propriété des ScriptableObjects).
4. **Effet visible** : selon le réglage, il agit sur la physique, sur une lumière, sur des particules ou sur la caméra, puis passe par le rendu URP comme le reste de la scène.

## Les fichiers de configuration

### PoolPhysicsSettings — comportement des billes

| Champ | Défaut | Effet |
|---|---|---|
| `slideFriction` | 6 | Freinage tant que la bille glisse (avant de rouler) |
| `friction` | 2 | Résistance au roulement une fois qu'elle roule |
| `sleepVelocityThreshold` | 0,01 | Vitesse sous laquelle la bille est arrêtée net |
| `offTableDropThreshold` | 0,15 | Chute sous laquelle une bille sortie de table est considérée empochée |

Lu par `PoolBall.FixedUpdate` → modifie `Rigidbody.linearVelocity` / `angularVelocity` → la position rendue de la bille.

### PoolScreenJuiceSettings — sensations caméra et écran

Vision brouillée (couleur, clignotement, secousse), zoom et secousse de charge, secousse au tir, à l'empochage, à la faute (plus flash rouge), au ramassage de pouvoir (flash cyan).

Lu par `LocalPoolPowerEffectReceiver` (secousses, voiles plein écran) et `LocalPoolAimController` (zoom de charge) → position de la caméra et calque `OnGUI` par-dessus l'image.

### PoolPotEffectSettings — bille empochée et poches

Halo lumineux (couleur, intensité, portée, durée), aura qui monte (prefab Cartoon FX, durée, échelle, vitesse), surbrillance de la poche sélectionnée, visuel de poche fermée (prefab optionnel + échelle), ralenti (échelle de temps, durée).

Lu par `PoolPocket` (lumière ponctuelle + particules) et `PoolMatchRules` (`Time.timeScale` pour le ralenti).

### PoolPowerSpawnSettings — apparition des pouvoirs

Liste des pouvoirs disponibles, couleur par catégorie, nombre de caisses et délais de réapparition, prefab de caisse par catégorie, intervalle de rotation de la bille à pouvoir, intensité et portée de son halo, matériau de bille par catégorie.

Lu par `PoolPowerCrateManager`, `PoolPowerBallRotator`, `PoolPowerCrate`, `PowerBall` → objets instanciés, couleur des lumières, matériau affiché.

### Les pouvoirs (PoolPower)

Chaque pouvoir est son propre asset (`Create > Pool > Powers > …`) : nom, `Requires Own Turn`, et ses paramètres (ex. multiplicateur de puissance pour le tir boosté, multiplicateur de sensibilité pour la vision brouillée).

## Réglages qui ne sont pas (encore) des ScriptableObjects

- **Table** : dimensions dans `PoolTableAssetSettings` (ScriptableObject **d'éditeur**, utilisé seulement par `PoolTableBuilder`).
- **Visée, joueur, ragdoll** : champs sérialisés directement sur les composants du prefab joueur (distances de visée, vitesses, offsets de la queue, priorités de caméra…).
- **Matériaux physiques** (rebond et frottement des billes, bandes, tapis) : fichiers `PhysicsMaterial` générés par `PoolTableBuilder`.

Piste (voir *Pistes & suggestions*) : regrouper les réglages de visée et de joueur dans des ScriptableObjects, et décrire les dimensions et les niveaux de la même façon — ce serait la base de l'outil de niveau.
