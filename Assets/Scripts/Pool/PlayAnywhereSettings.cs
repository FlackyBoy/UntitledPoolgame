using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere (PlayAnywhereRuleSet, PlayAnywhereHole): golf in the
    // décor. The hole itself (tees, cup, par) is set on its scene object.
    // A hole is played in rounds: each player takes an object from the
    // crate and places it (PlayAnywhereCrate, PlayAnywhereProp), then plays
    // Strokes Per Round strokes.
    [CreateAssetMenu(fileName = "PlayAnywhereSettings", menuName = "Pool/Play Anywhere Settings")]
    public class PlayAnywhereSettings : ScriptableObject
    {
        [Header("Shot")]
        [Tooltip("Multiplie la force des tirs dans ce mode : la salle est bien plus grande qu'une table. 1 = force du billard.")]
        [Min(0.1f)] public float shotPowerMultiplier = 2f;

        [Header("Surfaces")]
        [Tooltip("Surface du sol et du décor qui n'ont pas de composant Play Anywhere Surface. Vide = frottement du tapis de billard. Le tapis de la table garde toujours le sien.")]
        public PlayAnywhereSurfaceType defaultSurface;

        [Header("Hole")]
        [Tooltip("Vitesse (m/s) au-dessous de laquelle une bille qui passe sur le trou tombe dedans. Plus vite, elle passe par-dessus, comme au golf.")]
        [Min(0.05f)] public float cupCatchSpeed = 2.2f;
        [Tooltip("Part de la vitesse perdue (0 à 1) quand une bille trop rapide passe sur le trou et tape la lèvre. Assez ralentie, elle tombe quand même.")]
        [Range(0f, 1f)] public float cupLipSlowdown = 0.4f;
        [Tooltip("Déviation maximale (degrés) d'une bille qui tape la lèvre du trou, vers l'extérieur. Nulle au centre, maximale au bord.")]
        [Range(0f, 90f)] public float cupLipDeflection = 30f;
        [Tooltip("Petit saut (m/s vers le haut) d'une bille qui tape la lèvre du trou.")]
        [Min(0f)] public float cupLipHop = 0.3f;
        [Tooltip("Durée (secondes) de la chute de la bille dans le trou.")]
        [Min(0.05f)] public float cupSinkTime = 0.4f;

        [Header("Match")]
        [Tooltip("Nombre de parcours de la partie : les trous de la scène (Play Anywhere - Trou), joués dans l'ordre de leur champ Order. 0 = tous ceux de la scène.")]
        [Min(0)] public int courses = 3;
        [Tooltip("Durée (secondes) du tableau des scores à la fin de chaque parcours, avant le suivant ou la fin de la partie.")]
        [Min(0.5f)] public float scoreboardTime = 6f;

        [Header("Rounds")]
        [Tooltip("Nombre de manches sur chaque parcours. Chaque manche : un objet à prendre dans la caisse et à poser, puis Strokes Per Round coups chacun. 0 = autant de manches qu'il faut pour que les deux aient rentré leur bille (comme au golf ; Max Strokes reste le plafond).")]
        [Min(0)] public int rounds = 0;
        [Tooltip("Coups de chaque joueur par manche, avant de repasser à la pose.")]
        [Min(1)] public int strokesPerRound = 2;
        [Tooltip("Coups ajoutés au score d'un joueur qui n'a pas rentré sa bille à la fin de la dernière manche.")]
        [Min(0)] public int unfinishedPenalty = 2;

        [Header("Strokes")]
        [Tooltip("Nombre de coups au plus sur le trou. Arrivé là sans avoir rentré sa bille, le joueur a fini avec ce score. 0 = pas de limite.")]
        [Min(0)] public int maxStrokes = 10;
        [Tooltip("Coups de pénalité quand la bille sort des limites (zone hors limites, poche de la table, chute sous le parcours). Elle revient là où elle s'était arrêtée en dernier.")]
        [Min(0)] public int outOfBoundsPenalty = 1;
        [Tooltip("Une bille qui tombe plus bas que ce nombre de mètres sous le départ du trou est hors limites (tombée derrière le décor).")]
        [Min(0.2f)] public float outOfBoundsDepth = 2f;

        [Header("Objects (crate and placement)")]
        [Tooltip("Objets que la caisse peut proposer (prefab, chance relative, échelle). Remplie par Ensure Config Assets Exist avec des objets du pack Forest Bar.")]
        public List<PlayAnywherePropChoice> props = new List<PlayAnywherePropChoice>();
        [Tooltip("Objets posés dans la caisse à chaque manche, tirés au hasard (un par joueur au moins ; celui que l'autre prend n'est plus là).")]
        [Min(1)] public int crateChoices = 3;
        [Tooltip("Pour tester : la caisse propose tous les objets de Props (une fois chacun) à chaque manche, au lieu de Crate Choices tirés au hasard. Plus rapide encore : glisser le prefab d'un objet à effet dans la scène, il marche sans passer par la caisse.")]
        public bool crateOffersAll;
        [Tooltip("Distance (mètres) jusqu'où l'on peut poser son objet, dans la direction du regard.")]
        [Min(0.5f)] public float placeRange = 5f;
        [Tooltip("Vitesse de rotation (degrés par seconde) de l'objet à poser, croix gauche / droite maintenue.")]
        [Min(1f)] public float rotateSpeed = 120f;
        [Tooltip("Rotation (degrés) de l'objet à poser par cran de molette de la souris.")]
        [Min(1f)] public float rotateStep = 15f;
        [Tooltip("Distance (mètres) autour du trou, des départs et des billes où l'on ne peut rien poser : le parcours n'est jamais bouché.")]
        [Min(0f)] public float noBuildRadius = 0.5f;
        [Tooltip("Pente maximale (degrés) du sol où l'on peut poser un objet.")]
        [Range(0f, 60f)] public float maxPlaceSlope = 30f;
        [Tooltip("Temps (secondes) pour prendre et poser son objet. Passé ce temps, un objet pas encore posé est retiré. 0 = pas de limite.")]
        [Min(0f)] public float buildTimeLimit = 45f;
        [Tooltip("Gros message à l'écran (comme les fautes et les pouvoirs) au début de la phase de pose, sur chaque moitié d'écran. Balises de Text FX Studio acceptées. Vide = pas de message.")]
        public string buildShout = "À la caisse !";
        [Tooltip("Secondes de pose restantes à partir desquelles Build Warn Shout prévient (avec Build Time Limit).")]
        [Min(0f)] public float buildWarnTime = 10f;
        [Tooltip("Gros message quand il reste Build Warn Time secondes de pose. Vide = pas de message.")]
        public string buildWarnShout = "Plus que 10 s !";
        [Tooltip("Gros message quand le temps de pose est écoulé (les objets pas posés sont retirés). Vide = pas de message.")]
        public string buildTimeUpShout = "Temps écoulé !";
        [Tooltip("Couleur de l'aperçu quand l'objet peut être posé là (la transparence compte).")]
        public Color ghostValidColor = new Color(0.3f, 1f, 0.4f, 0.45f);
        [Tooltip("Couleur de l'aperçu quand il ne peut pas (trop près du trou, d'un départ ou d'une bille, dans un autre objet, sol trop en pente).")]
        public Color ghostInvalidColor = new Color(1f, 0.25f, 0.2f, 0.45f);

        [Header("Gadgets (objets à effet)")]
        [Tooltip("Ventilateur : poussée (m/s²) sur une bille juste devant lui ; elle faiblit jusqu'à 0 au bout de sa zone (2 m). Une bille poussée moins fort que le frottement du sol s'arrête (parquet : 0,3).")]
        [Min(0f)] public float fanAcceleration = 3f;
        [Tooltip("Ressort : vitesse (m/s) à laquelle il renvoie une bille devant lui, au moins.")]
        [Min(0f)] public float springLaunchSpeed = 3.5f;
        [Tooltip("Ressort : une bille qui arrive vite repart à sa vitesse multipliée par ceci, quand c'est plus que Spring Launch Speed.")]
        [Min(1f)] public float springSpeedGain = 1.3f;
        [Tooltip("Tuyau : vitesse minimale (m/s) d'une bille à la sortie, pour qu'elle ne reste pas coincée dans la bouche.")]
        [Min(0f)] public float pipeMinExitSpeed = 1f;
        [Tooltip("Tuyau : distance maximale (mètres) entre le trou d'entrée et la sortie, à la pose.")]
        [Min(0.5f)] public float pipeMaxLength = 6f;
        [Tooltip("Destructeur : rayon (mètres) dans lequel il détruit les objets posés (lui compris) dès qu'il est posé.")]
        [Min(0.1f)] public float destroyRadius = 1.5f;

        [Header("Diagnostics")]
        [Tooltip("Écrit dans la console chaque coup, chaque bille rentrée ou hors limites et le score ([PlayAnywhere]).")]
        public bool debugLogs = true;
    }

    [System.Serializable]
    public class PlayAnywherePropChoice
    {
        [Tooltip("L'objet (un prefab). Ses colliders sont remplacés par une boîte à sa taille, sauf avec Use Own Colliders.")]
        public GameObject prefab;
        [Tooltip("Chance relative d'être tiré dans la caisse.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Échelle de l'objet posé (1 = taille du prefab).")]
        [Min(0.05f)] public float scale = 1f;
        [Tooltip("Garde les colliders du prefab (il en faut un sur l'objet racine) au lieu d'une boîte à sa taille : pour une rampe, un cône, une forme que la bille doit suivre.")]
        public bool useOwnColliders;
    }
}
