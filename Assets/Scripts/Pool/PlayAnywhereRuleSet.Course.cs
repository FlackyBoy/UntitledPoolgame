using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere's match: Courses holes of the scene, played in their
    // Order. When every player is done with one, the score card shows for
    // Scoreboard Time, then the balls go to the next hole's tees (the
    // placed objects are cleared). After the last one, the lowest total
    // wins: a tie goes to whoever won more holes (alone with the fewest
    // strokes on it), then to whoever finished first on the last one.
    public partial class PlayAnywhereRuleSet
    {
        private readonly List<PlayAnywhereHole> course = new List<PlayAnywhereHole>();
        private int holeIndex;
        private readonly List<PlayAnywhereScoreboard.Row> results = new List<PlayAnywhereScoreboard.Row>();

        public int CourseNumber => holeIndex + 1;
        public int CourseCount => course.Count;

        // Strokes on the holes already played, plus the current one.
        public int TotalOf(int player)
        {
            int total = 0;
            foreach (PlayAnywhereScoreboard.Row r in results) total += r.StrokesOf(player);
            return phase == Phase.Intermission ? total : total + StrokesOf(player);
        }

        // Only the hole being played and its crate are seen (and solid); the
        // others wait their turn, those left out of the match stay hidden.
        private void ShowOnly(PlayAnywhereHole current)
        {
            foreach (PlayAnywhereHole h in Object.FindObjectsByType<PlayAnywhereHole>())
                h.SetShown(h == current);
            PlayAnywhereCrate currentCrate = current != null ? current.Crate : null;
            foreach (PlayAnywhereCrate c in Object.FindObjectsByType<PlayAnywhereCrate>())
                c.SetShown(c == currentCrate);
        }

        // Another mode starts: no flag, course nor crate left in the level.
        private static void HideAllHoles()
        {
            foreach (PlayAnywhereHole h in Object.FindObjectsByType<PlayAnywhereHole>())
                h.SetShown(false);
            foreach (PlayAnywhereCrate c in Object.FindObjectsByType<PlayAnywhereCrate>())
                c.SetShown(false);
        }

        // The scene's holes in Order (then name), cut to Courses.
        private void BuildCourse()
        {
            course.Clear();
            results.Clear();
            holeIndex = 0;
            course.AddRange(Object.FindObjectsByType<PlayAnywhereHole>());
            course.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.name, b.name));
            int wanted = settings.courses;
            if (wanted > 0 && course.Count > wanted) course.RemoveRange(wanted, course.Count - wanted);
            if (settings.debugLogs && course.Count > 0)
                Debug.Log($"[PlayAnywhere] Partie de {course.Count} parcours{(wanted > course.Count ? $" (Courses demande {wanted}, la scène n'en a que {course.Count})" : "")} : {string.Join(", ", course.ConvertAll(h => h.name))}.");
        }

        // Every player is done with this hole: the score card, then the
        // next hole or the end of the match.
        private void EndCourse()
        {
            int[] played = new int[ballCount];
            System.Array.Copy(strokes, played, ballCount);
            results.Add(new PlayAnywhereScoreboard.Row { course = holeIndex + 1, par = hole.Par, strokes = played });
            phase = Phase.Intermission;
            bool last = holeIndex + 1 >= course.Count;
            int winner = -1;
            string footer;
            if (last)
            {
                winner = MatchWinner(out string why);
                footer = $"J{winner + 1} gagne la partie !{why}";
            }
            else footer = $"Parcours suivant : {holeIndex + 2} sur {course.Count}";

            string title = last ? "Score final" : $"Parcours {holeIndex + 1}/{course.Count} terminé";
            if (settings.debugLogs)
            {
                var parts = new string[ballCount];
                for (int p = 0; p < ballCount; p++) parts[p] = $"J{p + 1} {strokes[p]} coups (total {TotalOf(p)})";
                Debug.Log($"[PlayAnywhere] {title} (par {hole.Par}) : {string.Join(", ", parts)}. {footer}");
            }
            PlayAnywhereScoreboard.Show(title, results, course, footer, winner, settings.scoreboardTime);
            match.StartCoroutine(AfterScoreboard(match.MatchGeneration, last, winner));
        }

        private IEnumerator AfterScoreboard(int generation, bool last, int winner)
        {
            // Real time: unaffected by a slow-motion or a pause.
            yield return new WaitForSecondsRealtime(settings.scoreboardTime);
            PlayAnywhereScoreboard.Hide();
            if (match == null || generation != match.MatchGeneration || match.GameOver) yield break;
            if (last) match.Win(winner);
            else LoadCourse(holeIndex + 1);
        }

        private void LoadCourse(int index)
        {
            holeIndex = index;
            hole = course[index];
            ShowOnly(hole);
            for (int p = 0; p < ballCount; p++)
                if (balls[p] != null) balls[p].ReturnFromTableHole(hole.TeePosition(p, balls[p].Radius) + Vector3.up * TeeLift);
            ResetHoleState();
            CheckTees();
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] Parcours {holeIndex + 1}/{course.Count} : '{hole.name}', par {hole.Par}.", hole);
            StartRounds(match);
        }

        // A hair above the tee: a ball set exactly on (or into) the floor can
        // be pushed through it.
        private const float TeeLift = 0.005f;

        // A tee with no ground right under it: the ball falls from it.
        private void CheckTees()
        {
            for (int p = 0; p < ballCount; p++)
            {
                if (balls[p] == null) continue;
                Vector3 tee = hole.TeePosition(p, balls[p].Radius);
                if (!hole.InPlayArea(tee))
                    Debug.LogWarning($"[PlayAnywhere] Parcours '{hole.name}' : le départ de J{p + 1} est hors du parcours (couloir vert). Élargis le couloir au départ (Path Width) ou rapproche le départ.", hole);
                if (!TeeGround(tee + Vector3.up * 0.3f, 0.3f + balls[p].Radius + GroundProbe, out RaycastHit hit))
                    Debug.LogWarning($"[PlayAnywhere] Parcours '{hole.name}' : pas de sol sous le départ de J{p + 1} ({tee}). Rapproche le disque du sol, ou le sol a un trou à cet endroit.", hole);
                else if (hit.point.y > tee.y - balls[p].Radius + 0.01f)
                    Debug.LogWarning($"[PlayAnywhere] Parcours '{hole.name}' : le départ de J{p + 1} est sous la surface de '{hit.collider.name}' ({(hit.point.y - (tee.y - balls[p].Radius)) * 100f:0} cm) : la bille risque de passer au travers. Remonte le disque au niveau du sol.", hole);
            }
        }

        private static readonly RaycastHit[] teeHits = new RaycastHit[8];

        // The nearest ground below: not a body (the ball sitting on the tee
        // was taken for the floor).
        private static bool TeeGround(Vector3 from, float length, out RaycastHit ground)
        {
            ground = default;
            bool found = false;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, teeHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = teeHits[i];
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                if (found && hit.distance >= ground.distance) continue;
                ground = hit;
                found = true;
            }
            return found;
        }

        // Lowest total; tie: more holes won (alone with the fewest strokes);
        // tie again: first to finish the last hole. why: the tie-break
        // used, for the card.
        private int MatchWinner(out string why)
        {
            var total = new int[ballCount];
            var won = new int[ballCount];
            foreach (PlayAnywhereScoreboard.Row r in results)
            {
                int best = int.MaxValue, bestPlayer = -1;
                for (int p = 0; p < ballCount; p++)
                {
                    int s = r.StrokesOf(p);
                    total[p] += s;
                    if (s < best) { best = s; bestPlayer = p; }
                    else if (s == best) bestPlayer = -1;
                }
                if (bestPlayer >= 0) won[bestPlayer]++;
            }
            int lowest = int.MaxValue;
            for (int p = 0; p < ballCount; p++) lowest = Mathf.Min(lowest, total[p]);
            var tied = new List<int>();
            for (int p = 0; p < ballCount; p++)
                if (total[p] == lowest) tied.Add(p);
            why = "";
            if (tied.Count == 1) return tied[0];

            int most = -1;
            foreach (int p in tied) most = Mathf.Max(most, won[p]);
            tied.RemoveAll(p => won[p] < most);
            if (tied.Count == 1)
            {
                why = " (égalité de coups, plus de parcours gagnés)";
                return tied[0];
            }
            why = " (égalité parfaite, a fini le dernier parcours le premier)";
            foreach (int p in finishOrder)
                if (tied.Contains(p)) return p;
            return tied[0];
        }

        private string CourseLabel() => course.Count > 1 ? $"Parcours {holeIndex + 1}/{course.Count} · " : "";
    }
}
