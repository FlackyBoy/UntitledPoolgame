using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, crate object: the destroyer. As soon as it's placed,
    // every placed object within Destroy Radius goes, the destroyer with
    // them: a way to clear a course that got blocked. Its ghost shows the
    // radius (PlayAnywhereProp).
    [AddComponentMenu("Pool/Play Anywhere/Destructeur")]
    public class PlayAnywhereDestroyer : MonoBehaviour, IPlayAnywherePlaced
    {
        public void OnPlaced(PlayAnywhereRuleSet rules, PlayAnywhereProp prop)
        {
            float radius = rules.Settings.destroyRadius;
            // Read first: removed with the others, it forgets its owner.
            int owner = prop.Owner;
            int count = rules.DestroyPlacedProps(transform.position, radius);
            if (rules.Settings.debugLogs)
                Debug.Log($"[PlayAnywhere] J{owner + 1} pose le destructeur : {Mathf.Max(0, count - 1)} objet(s) détruit(s) à moins de {radius:0.#} m.", this);
        }

        private void OnDrawGizmosSelected()
        {
            PlayAnywhereRuleSet rules = PlayAnywhereRuleSet.Current;
            float radius = rules != null ? rules.Settings.destroyRadius : 1.5f;
            Gizmos.color = new Color(1f, 0.3f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
