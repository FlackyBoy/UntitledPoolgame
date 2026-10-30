using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, crate object: the destroying portal. A ball that goes
    // through its opening is out of bounds (penalty for its own stroke, back
    // to where it last stood still), as if it had left the course.
    [AddComponentMenu("Pool/Play Anywhere/Portail destructeur")]
    public class PlayAnywherePortal : PlayAnywhereGadget
    {
        [Tooltip("Ouverture du portail (espace de l'objet) : une bille qui la traverse est détruite comme une sortie des limites.")]
        [SerializeField] private Vector3 openingCenter = new Vector3(0f, 0.275f, 0f);
        [SerializeField] private Vector3 openingSize = new Vector3(0.42f, 0.55f, 0.06f);

        protected override void Act(PlayAnywhereRuleSet rules, int player, PoolBall ball, Vector3 from, Vector3 to)
        {
            if (!Crosses(from, to, openingCenter, openingSize)) return;
            Log(rules, $"J{player + 1} passe dans le portail destructeur ({name}).", this);
            // The ball comes back near here: not caught again on the way.
            Cooldown(player, 0.5f);
            rules.SendOutOfBounds(player, "dans le portail destructeur");
        }

        protected override void DrawZones() => DrawZone(openingCenter, openingSize, new Color(0.7f, 0.3f, 1f));
    }
}
