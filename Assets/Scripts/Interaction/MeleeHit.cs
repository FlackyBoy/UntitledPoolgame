using System.Collections;
using System.Collections.Generic;
using RootMotion.Dynamics;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Interaction
{
    // Shared by the melee attacks (LocalCueMelee, LocalUnarmedMelee):
    // telling whose ragdoll muscle a collider is, and the global hitstop.
    public static class MeleeHit
    {
        // The mode or a power lets this player hit. No match (the R&D arena):
        // nothing to forbid it.
        public static bool Allowed(PlayerInput input)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            return rules == null || rules.CanHit(rules.GetEffectivePlayerIndex(input.playerIndex));
        }

        // True when the collider is a ragdoll muscle of another player that
        // this attack hasn't hit yet (the puppet is then added to alreadyHit).
        public static bool TryGetTarget(Collider hit, Transform attacker, PuppetMaster ownPuppet, HashSet<PuppetMaster> alreadyHit,
            out MuscleCollisionBroadcaster broadcaster, out PuppetMaster target)
        {
            broadcaster = null;
            target = null;
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out broadcaster)) return false;
            target = broadcaster.puppetMaster;
            if (target == null || target == ownPuppet || target.transform.IsChildOf(attacker) || alreadyHit.Contains(target)) return false;
            alreadyHit.Add(target);
            // Teammates (2 v 2) don't hit each other at all, not even a push.
            return !Teammates(attacker, target);
        }

        // Once per body hit, not per frame: two lookups up the hierarchy.
        private static bool Teammates(Transform attacker, PuppetMaster target)
        {
            if (!PlayerBallGroups.Teams) return false;
            PlayerInput own = attacker.GetComponentInParent<PlayerInput>();
            PlayerInput other = target.GetComponentInParent<PlayerInput>();
            return own != null && other != null && PlayerBallGroups.TeammateOf(own.playerIndex) == other.playerIndex;
        }

        public static BehaviourPuppet FindPuppetBehaviour(PuppetMaster puppetMaster)
        {
            foreach (BehaviourBase behaviour in puppetMaster.behaviours)
                if (behaviour is BehaviourPuppet puppet) return puppet;
            return null;
        }

        // Global slow-down for a moment, in real time. Only restores the
        // time scale if nothing else changed it meanwhile (PoolMatchRules'
        // pot slow-motion also drives Time.timeScale).
        public static IEnumerator Hitstop(float duration, float timeScale)
        {
            float previous = Time.timeScale;
            Time.timeScale = timeScale;
            yield return new WaitForSecondsRealtime(duration);
            if (Mathf.Approximately(Time.timeScale, timeScale)) Time.timeScale = previous;
        }
    }
}
