using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere's match: Courses holes of the scene, played in their
    // Order. When both players are done with one, the score card shows for
    // Scoreboard Time, then the balls go to the next hole's tees (the
    // placed objects are cleared). After the last one, the lowest total
    // wins: a tie goes to whoever won more holes, then to whoever holed out
    // first on the last one.
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
            foreach (PlayAnywhereScoreboard.Row r in results) total += player == 0 ? r.player1 : r.player2;
            return phase == Phase.Intermission ? total : total + strokes[player];
        }

        // Only the hole being played is seen (and solid); the others wait
        // their turn, those left out of the match stay hidden.
        private void ShowOnly(PlayAnywhereHole current)
        {
            foreach (PlayAnywhereHole h in Object.FindObjectsByType<PlayAnywhereHole>())
                h.SetShown(h == current);
        }

        // Another mode starts: no flag nor course left in the level.
        private static void HideAllHoles()
        {
            foreach (PlayAnywhereHole h in Object.FindObjectsByType<PlayAnywhereHole>())
                h.SetShown(false);
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

        // Both players are done with this hole: the score card, then the
        // next hole or the end of the match.
        private void EndCourse()
        {
            results.Add(new PlayAnywhereScoreboard.Row { course = holeIndex + 1, par = hole.Par, player1 = strokes[0], player2 = strokes[1] });
            phase = Phase.Intermission;
            bool last = holeIndex + 1 >= course.Count;
            int winner = -1;
            string footer;
            if (last)
            {
                winner = MatchWinner(out string why);
                footer = $"J{winner + 1} gagne la partie !{why}";
            }
            else footer = $"Parcours suivant : {course[holeIndex + 1].name}";

            string title = last ? "Score final" : $"Parcours {holeIndex + 1}/{course.Count} terminé";
            if (settings.debugLogs)
                Debug.Log($"[PlayAnywhere] {title} : J1 {strokes[0]} coups, J2 {strokes[1]} (par {hole.Par}) ; total J1 {TotalOf(0)}, J2 {TotalOf(1)}. {footer}");
            PlayAnywhereScoreboard.Show(title, results, footer);
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
            for (int p = 0; p < 2; p++)
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
            for (int p = 0; p < 2; p++)
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

        // Lowest total; tie: more holes won; tie again: first to hole out
        // on the last hole. why: the tie-break used, for the card.
        private int MatchWinner(out string why)
        {
            int total1 = 0, total2 = 0, won1 = 0, won2 = 0;
            foreach (PlayAnywhereScoreboard.Row r in results)
            {
                total1 += r.player1;
                total2 += r.player2;
                if (r.player1 < r.player2) won1++;
                else if (r.player2 < r.player1) won2++;
            }
            why = "";
            if (total1 != total2) return total1 < total2 ? 0 : 1;
            if (won1 != won2)
            {
                why = " (égalité de coups, plus de parcours gagnés)";
                return won1 > won2 ? 0 : 1;
            }
            why = " (égalité parfaite, a fini le dernier parcours le premier)";
            return Mathf.Max(0, firstFinisher);
        }

        private string CourseLabel() => course.Count > 1 ? $"Parcours {holeIndex + 1}/{course.Count} · " : "";
    }
}
