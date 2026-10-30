using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere's rounds. Each round: Build (the crate offers a few
    // objects, each player takes one, first come first served, and places
    // it, both at once), then Shoot (each player plays Strokes Per Round
    // strokes, in turns or in Speed Pool). Placed objects stay for the whole
    // hole. After the last round, a player who hasn't holed out scores their
    // strokes plus Unfinished Penalty.
    public partial class PlayAnywhereRuleSet
    {
        // Intermission: the score card between two holes.
        private enum Phase { Build, Shoot, Intermission }

        private PoolMatchRules match;
        private Phase phase;
        private int round;
        private readonly int[] roundStrokes = new int[2];
        private readonly bool[] propTaken = new bool[2];
        private readonly bool[] propPlaced = new bool[2];
        private float buildStartedAt;
        private bool buildWarned;
        // The round started with no player yet: its build starts when one
        // joins, if nobody has shot by then.
        private bool buildAwaitsPlayers;
        // Per player index, their hands (pickup); null = no such player.
        private readonly LocalPlayerHandController[] hands = new LocalPlayerHandController[2];
        // How many PlayerInputs there were when the hands were last looked
        // up: a player who joins after the round started is found then.
        private int handsFoundFor = -1;
        // Per player index, the colliders crate objects let through: the
        // capsule and the ragdoll, not what the player carries.
        private readonly Collider[][] playerColliders = new Collider[2][];
        // This round's offer, still in the crate or being carried.
        private readonly List<PlayAnywhereProp> crateProps = new List<PlayAnywhereProp>();
        private readonly HashSet<Rigidbody> placedBodies = new HashSet<Rigidbody>();
        // Every prop made, to clear them when the hole starts again or
        // another mode starts.
        private static readonly List<PlayAnywhereProp> spawnedProps = new List<PlayAnywhereProp>();

        public LocalPlayerHandController[] Hands
        {
            get
            {
                RefreshHands();
                return hands;
            }
        }
        public bool IsBuilding => phase == Phase.Build;

        // Shooting phase, not done with the hole nor with this round's strokes.
        public bool CanShoot(int player) =>
            phase == Phase.Shoot && player >= 0 && player < 2 && !finished[player] && roundStrokes[player] < settings.strokesPerRound;

        public bool CanTakeProp(int player)
        {
            if (phase != Phase.Build || player < 0 || player >= 2) return false;
            int p = PlacerFor(player);
            return !propTaken[p] && !propPlaced[p];
        }

        // Alone (a single PlayerInput plays both balls in turns), that
        // player places both objects: J1's, then J2's.
        private bool Solo => PlayerInput.all.Count <= 1;

        // Whose object a pickup by this player is.
        public int PlacerFor(int handIndex) => Solo ? (propTaken[0] || propPlaced[0] ? 1 : 0) : handIndex;

        public bool IsPlacedProp(Rigidbody body) => placedBodies.Contains(body);

        // The players walk through crate objects; only the balls hit them.
        // Called again when the colliders come back on (the pair state may
        // not survive a disable) and for a player who joins later.
        public void IgnorePlayers(Collider[] colliders)
        {
            RefreshHands();
            foreach (Collider[] player in playerColliders)
            {
                if (player == null) continue;
                foreach (Collider c in player)
                {
                    if (c == null) continue;
                    foreach (Collider prop in colliders)
                        if (prop != null) Physics.IgnoreCollision(c, prop);
                }
            }
        }

        // The destroyer: every placed object within radius goes, itself
        // included. Returns how many.
        public int DestroyPlacedProps(Vector3 center, float radius)
        {
            int count = 0;
            for (int i = spawnedProps.Count - 1; i >= 0; i--)
            {
                PlayAnywhereProp prop = spawnedProps[i];
                if (prop == null || !prop.IsPlaced || !prop.Near(center, radius)) continue;
                if (prop.TryGetComponent(out Rigidbody body)) placedBodies.Remove(body);
                spawnedProps.RemoveAt(i);
                if (settings.debugLogs) Debug.Log($"[PlayAnywhere] {prop.name} détruit.", prop);
                prop.Remove();
                count++;
            }
            return count;
        }

        // Far enough (horizontally) from the cup, the tees and both balls.
        public bool IsClearOfCourse(Vector3 point, float radius)
        {
            if (hole == null) return true;
            if (FlatDistance(point, hole.CupPosition) < radius) return false;
            for (int p = 0; p < 2; p++)
            {
                if (FlatDistance(point, hole.TeePosition(p, 0f)) < radius) return false;
                if (balls[p] != null && FlatDistance(point, balls[p].transform.position) < radius) return false;
            }
            return true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        private void StartRounds(PoolMatchRules match)
        {
            this.match = match;
            ClearProps();
            crateProps.Clear();
            placedBodies.Clear();
            round = 0;
            BeginRound();
        }

        private void BeginRound()
        {
            round++;
            buildAwaitsPlayers = false;
            for (int p = 0; p < 2; p++)
            {
                roundStrokes[p] = 0;
                propTaken[p] = false;
                propPlaced[p] = false;
            }

            PlayAnywhereCrate crate = hole.Crate != null ? hole.Crate : PlayAnywhereCrate.Current;
            FindHands();
            if (crate == null || !HasPropChoices() || (hands[0] == null && hands[1] == null))
            {
                // The match starts before the players join: the build waits
                // for them (TickAwaitingPlayers) instead of being skipped.
                buildAwaitsPlayers = crate != null && HasPropChoices();
                if (settings.debugLogs)
                    Debug.Log($"[PlayAnywhere] Manche {round}{RoundsLabel()} : pas de pose ({(crate == null ? "pas de caisse dans la scène : Tools > Pool > Play Anywhere > Créer la caisse" : !HasPropChoices() ? "pas d'objets dans Props de PlayAnywhereSettings" : "pas encore de joueur : elle commencera à son arrivée")}).");
                BeginShooting();
                return;
            }

            phase = Phase.Build;
            buildStartedAt = Time.time;
            buildWarned = false;
            UntitledPoolGame.Core.MatchHud.ShowShout(-1, settings.buildShout);
            List<PlayAnywherePropChoice> all = settings.crateOffersAll ? settings.props.FindAll(c => c != null && c.prefab != null) : null;
            int count = all != null ? all.Count : Mathf.Max(settings.crateChoices, Solo ? 2 : PlayersPresent());
            for (int i = 0; i < count; i++)
            {
                PlayAnywherePropChoice choice = all != null ? all[i] : PickChoice();
                if (choice == null) break;
                PlayAnywhereProp prop = PlayAnywhereProp.Spawn(this, settings, choice, crate.Slot(i, count), crate.SlotRotation);
                crateProps.Add(prop);
                spawnedProps.Add(prop);
            }
            if (settings.debugLogs)
            {
                Debug.Log($"[PlayAnywhere] Manche {round}{RoundsLabel()} : pose. La caisse '{crate.name}' à {crate.transform.position} propose {crateProps.Count} objets ; chacun en prend un (Interact) et le pose (Attack ou Interact, molette / croix pour tourner).", crate);
                foreach (PlayAnywhereProp prop in crateProps)
                    Debug.Log($"[PlayAnywhere]   {prop.Describe()}", prop);
                for (int p = 0; p < 2; p++)
                    if (hands[p] != null)
                        Debug.Log($"[PlayAnywhere]   J{p + 1} est à {hands[p].transform.position}, à {Vector3.Distance(hands[p].transform.position, crate.transform.position):0.0} m de la caisse.", hands[p]);
            }
        }

        // Every frame during Build (from Tick): the time limit, warned of
        // Build Warn Time before, shouted when it's up (players didn't see
        // it coming: the build just stopped).
        private void TickBuild()
        {
            if (settings.buildTimeLimit <= 0f) return;
            float left = settings.buildTimeLimit - (Time.time - buildStartedAt);
            if (!buildWarned && left <= settings.buildWarnTime && settings.buildTimeLimit > settings.buildWarnTime)
            {
                buildWarned = true;
                UntitledPoolGame.Core.MatchHud.ShowShout(-1, settings.buildWarnShout);
            }
            if (left > 0f) return;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Temps de pose écoulé ({settings.buildTimeLimit:0} s) : les objets pas posés sont retirés.");
            UntitledPoolGame.Core.MatchHud.ShowShout(-1, settings.buildTimeUpShout);
            EndBuild();
        }

        // Every frame while a round waits for its players (from Tick).
        private void TickAwaitingPlayers()
        {
            if (!buildAwaitsPlayers || phase != Phase.Shoot) return;
            if (roundStrokes[0] > 0 || roundStrokes[1] > 0)
            {
                buildAwaitsPlayers = false;
                return;
            }
            RefreshHands();
            if (hands[0] == null && hands[1] == null) return;
            buildAwaitsPlayers = false;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Manche {round}{RoundsLabel()} : un joueur est arrivé, la pose commence.");
            round--;
            BeginRound();
        }

        // Whole seconds left to build, -1 without a limit.
        private int BuildSecondsLeft => settings.buildTimeLimit > 0f
            ? Mathf.Max(0, Mathf.CeilToInt(settings.buildTimeLimit - (Time.time - buildStartedAt)))
            : -1;

        public void OnPropTaken(PlayAnywhereProp prop, int player) => propTaken[player] = true;

        public void OnPropReturned(PlayAnywhereProp prop, int player)
        {
            if (player >= 0) propTaken[player] = false;
        }

        public void OnPropPlaced(PlayAnywhereProp prop, int player)
        {
            propPlaced[player] = true;
            crateProps.Remove(prop);
            Rigidbody body = prop.GetComponent<Rigidbody>();
            if (body != null) placedBodies.Add(body);
            RefreshHands();
            for (int p = 0; p < 2; p++)
                if ((Solo ? balls[p] != null : hands[p] != null) && !propPlaced[p]) return;
            EndBuild();
        }

        // Debug menu: one of Props straight into this player's hands, to
        // place as usual, out of the round (PlayAnywhereProp.MarkFree).
        // Returns what happened, for the menu.
        public string GiveProp(PlayAnywherePropChoice choice, int player)
        {
            LocalPlayerHandController hand = player >= 0 && player < 2 ? Hands[player] : null;
            if (hand == null) return $"Pas de J{player + 1}.";
            if (hand.HeldObject != null || hand.HandsMoving)
                return $"J{player + 1} a déjà quelque chose en main (la queue ?) : qu'il le lâche d'abord.";

            // Just in front, at chest height: a short reach, no bending down.
            Transform body = hand.transform;
            Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            Vector3 slot = body.position + forward * 0.45f + Vector3.up * 0.9f;
            PlayAnywhereProp prop = PlayAnywhereProp.Spawn(this, settings, choice, slot, Quaternion.LookRotation(forward));
            prop.MarkFree();
            spawnedProps.Add(prop);
            hand.Give(prop.Grabbable);
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Menu debug : {prop.name} dans les mains de J{player + 1}.", prop);
            return $"{choice.prefab.name} dans les mains de J{player + 1} : le poser comme d'habitude.";
        }

        public void OnFreePropPlaced(PlayAnywhereProp prop)
        {
            if (prop.TryGetComponent(out Rigidbody body)) placedBodies.Add(body);
        }

        // Debug menu: every object it gave, placed or in hand.
        public int RemoveFreeProps()
        {
            int count = 0;
            for (int i = spawnedProps.Count - 1; i >= 0; i--)
            {
                PlayAnywhereProp prop = spawnedProps[i];
                if (prop == null || !prop.IsFree) continue;
                if (prop.TryGetComponent(out Rigidbody body)) placedBodies.Remove(body);
                spawnedProps.RemoveAt(i);
                prop.Remove();
                count++;
            }
            return count;
        }

        private void EndBuild()
        {
            foreach (PlayAnywhereProp prop in crateProps)
                if (prop != null) prop.Remove();
            crateProps.Clear();
            BeginShooting();
        }

        private void BeginShooting()
        {
            phase = Phase.Shoot;
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Manche {round}{RoundsLabel()} : tir, {settings.strokesPerRound} coup{(settings.strokesPerRound > 1 ? "s" : "")} chacun.");
            if (match.IsSimultaneous) return;
            int next = NextShooter();
            if (next < 0) EndRound();
            // Not from Setup (no crate): the match isn't started, player 1 begins.
            else if (next != match.CurrentPlayer && match.MatchStarted) match.SwitchTurn();
        }

        // Speed Pool, every frame during Shoot (from Tick): the round ends
        // once nobody has a stroke left and every ball has stopped.
        private void TickShootSimultaneous()
        {
            for (int p = 0; p < 2; p++)
            {
                if (balls[p] == null || finished[p]) continue;
                if (roundStrokes[p] < settings.strokesPerRound || rolling[p]) return;
            }
            if (PoolBall.AnyMoving()) return;
            EndRound();
        }

        // Golf order among those with strokes left this round: the farthest
        // from the cup.
        private int NextShooter()
        {
            int best = -1;
            float bestSqr = -1f;
            for (int p = 0; p < 2; p++)
            {
                if (!CanShoot(p) || balls[p] == null) continue;
                Vector3 offset = balls[p].transform.position - hole.CupPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude <= bestSqr) continue;
                bestSqr = offset.sqrMagnitude;
                best = p;
            }
            return best;
        }

        private void EndRound()
        {
            if (match.GameOver || phase == Phase.Intermission) return;
            if (settings.rounds <= 0 || round < settings.rounds)
            {
                BeginRound();
                return;
            }
            if (settings.debugLogs) Debug.Log($"[PlayAnywhere] Dernière manche jouée.");
            for (int p = 0; p < 2; p++)
            {
                if (finished[p] || balls[p] == null) continue;
                strokes[p] += settings.unfinishedPenalty;
                if (settings.debugLogs) Debug.Log($"[PlayAnywhere] J{p + 1} n'a pas rentré sa bille : +{settings.unfinishedPenalty} (total {strokes[p]}).", balls[p]);
                balls[p].BeginBallInHand();
                Finish(match, p);
            }
        }

        private void RefreshHands()
        {
            if (PlayerInput.all.Count != handsFoundFor) FindHands();
        }

        private void FindHands()
        {
            handsFoundFor = PlayerInput.all.Count;
            hands[0] = hands[1] = null;
            foreach (LocalPlayerHandController h in Object.FindObjectsByType<LocalPlayerHandController>())
            {
                PlayerInput input = h.GetComponent<PlayerInput>();
                int index = input != null ? input.playerIndex : -1;
                if (index >= 0 && index < 2) hands[index] = h;
            }
            for (int p = 0; p < 2; p++)
                playerColliders[p] = hands[p] != null ? OwnColliders(hands[p]) : null;
            foreach (PlayAnywhereProp prop in spawnedProps)
                if (prop != null) prop.IgnorePlayers();
        }

        // The player's capsule and ragdoll (PuppetMaster lives under the
        // player), not the cue or an object in hand.
        private static Collider[] OwnColliders(LocalPlayerHandController player)
        {
            var own = new List<Collider>();
            foreach (Collider c in player.GetComponentsInChildren<Collider>(true))
                if (c.GetComponentInParent<LocalGrabbable>() == null) own.Add(c);
            return own.ToArray();
        }

        private int PlayersPresent() => (hands[0] != null ? 1 : 0) + (hands[1] != null ? 1 : 0);

        private bool HasPropChoices()
        {
            foreach (PlayAnywherePropChoice c in settings.props)
                if (c != null && c.prefab != null && c.weight > 0f) return true;
            return false;
        }

        // Weighted draw among the props with a prefab.
        private PlayAnywherePropChoice PickChoice()
        {
            float total = 0f;
            foreach (PlayAnywherePropChoice c in settings.props)
                if (c != null && c.prefab != null) total += c.weight;
            if (total <= 0f) return null;
            float roll = Random.value * total;
            foreach (PlayAnywherePropChoice c in settings.props)
            {
                if (c == null || c.prefab == null) continue;
                roll -= c.weight;
                if (roll <= 0f) return c;
            }
            return null;
        }

        private string RoundsLabel() => settings.rounds > 0 ? $"/{settings.rounds}" : "";

        // After the first hole, the running total too.
        private string TotalLabel(int player) => holeIndex > 0 ? $" · total {TotalOf(player)}" : "";

        // Changes whenever DescribeRound would: the HUD rebuilds its plate
        // only then.
        public int RoundStateKey(int player)
        {
            int state = phase == Phase.Intermission ? 99
                : phase == Phase.Build ? (propPlaced[player] ? 2 : propTaken[player] ? 1 : 0)
                : 3 + (finished[player] ? 9 : roundStrokes[player]);
            // Building against the clock: every second too.
            int seconds = phase == Phase.Build ? Mathf.Min(BuildSecondsLeft + 1, 999) : 0;
            return ((holeIndex * 100 + round) * 100 + state) * 1000 + seconds;
        }

        // The round's line on the HUD.
        public string DescribeRound(int player)
        {
            string where = $"{CourseLabel()}Manche {round}{RoundsLabel()}";
            if (phase == Phase.Intermission) return $"{CourseLabel()}terminé{TotalLabel(player)}";
            if (phase == Phase.Build)
                return $"{where} · {(propPlaced[player] ? "objet posé" : propTaken[player] ? "pose ton objet" : "prends un objet")}{(BuildSecondsLeft >= 0 ? $" · {BuildSecondsLeft} s" : "")}{TotalLabel(player)}";
            if (finished[player]) return $"{where}{TotalLabel(player)}";
            return $"{where} · coup {Mathf.Min(roundStrokes[player] + 1, settings.strokesPerRound)}/{settings.strokesPerRound}{TotalLabel(player)}";
        }

        private static void ClearProps()
        {
            foreach (PlayAnywhereProp prop in spawnedProps)
                if (prop != null) Object.Destroy(prop.gameObject);
            spawnedProps.Clear();
        }
    }
}
