using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // The provisional OnGUI: mode select, status lines, game-over box.
    public partial class PoolMatchRules
    {
        private void OnGUI()
        {
            if (!MatchStarted)
            {
                if (!ExternalMenu) DrawModeSelectGUI();
                return;
            }

            if (ExternalHud) return;

            if (GameOver)
            {
                DrawGameOverGUI();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 320, 190));
            GUILayout.Label(IsSimultaneous ? "Speed Pool : tout le monde tire !" : $"Tour : Joueur {CurrentPlayer + 1}");
            GUILayout.Label($"Joueur 1 — {ruleSet.DescribePlayer(0)}");
            GUILayout.Label($"Joueur 2 — {ruleSet.DescribePlayer(1)}");
            GUILayout.Label($"Pouvoirs J1 : {DescribeStock(0)}");
            GUILayout.Label($"Pouvoirs J2 : {DescribeStock(1)}");
            if (BallInHand)
                GUILayout.Label($"Faute ! Joueur {CurrentPlayer + 1} a la main libre — regarde où placer la bille blanche et valide avec Interact.");

            // Status line only; the call itself is made in
            // LocalPoolAimController.HandleCallPocket.
            if (IsShootingForEightBall(CurrentPlayer))
            {
                GUILayout.Label(calledEightBallPocket != null
                    ? $"Poche appelée pour la bille 8 : {calledEightBallPocket.DescribeLocation()}"
                    : "Bille 8 : désigne une poche (vue du dessus) et valide avec Interact.");
            }
            GUILayout.EndArea();
        }

        // Big centered "match over" box.
        private void DrawGameOverGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 110f, 400f, 220f), GUI.skin.box);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            GUILayout.Label("Partie terminée", titleStyle, GUILayout.Height(40));
            GUILayout.Label($"Joueur {Winner + 1} gagne !", titleStyle, GUILayout.Height(40));
            GUILayout.Label($"{ruleSet.DescribePlayer(0)}  —  {ruleSet.DescribePlayer(1)}", subtitleStyle);

            if (GUILayout.Button("Rejouer", GUILayout.Height(35))) pendingRestart = true;

            GUILayout.EndArea();
        }

        private void DrawModeSelectGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 190f, 300f, 380f), GUI.skin.box);

            GUILayout.Label("Type de partie");
            GUILayout.BeginHorizontal();
            DrawTypeButton(PoolMatchType.TurnBased);
            DrawTypeButton(PoolMatchType.SpeedPool);
            GUILayout.EndHorizontal();

            GUILayout.Label("Mode");
            DrawModeButton(PoolGameMode.EightBall, "Classique (8-ball)");
            DrawModeButton(PoolGameMode.NineBall, "9-ball");
            DrawModeButton(PoolGameMode.FourteenOne, "14.1 (score cible)");
            DrawModeButton(PoolGameMode.CatchTheHole, "Catch the Hole (trous vivants)");
            DrawModeButton(PoolGameMode.HungryHole, "Hungry Hole (la blanche est un trou)");
            DrawModeButton(PoolGameMode.SnakePool, "Snake Pool (chaîne de billes)");
            DrawModeButton(PoolGameMode.PowerWashPool, "Power Wash Pool (laver le tapis)");
            DrawModeButton(PoolGameMode.PlayAnywhere, "Play Anywhere (golf dans le décor)");
            DrawModeButton(PoolGameMode.PocketSize, "Pocket Size (le grand chasse le petit)");
            DrawModeButton(PoolGameMode.ShooterMini, "Shooter miniature (on se tire dessus sur la table)");
            DrawModeButton(PoolGameMode.ShooterPool, "Shooter Billard (vider son groupe en se tirant dessus)");
            DrawModeButton(PoolGameMode.BowlingPool, "Bowling Pool (des quilles sur la table)");
            DrawModeButton(PoolGameMode.SumoPool, "Sumo Pool (se pousser dans les trous)");
            DrawModeButton(PoolGameMode.SumoBilliard, "Sumo Billard (vider son groupe en étant une bille)");
            DrawModeButton(PoolGameMode.HideAndSeek, "Cache-cache (se cacher dans une bille)");

            bool powers = GUILayout.Toggle(selected.powers, "Pouvoirs");
            if (powers != selected.powers) Select((ref PoolMatchSetup s) => s.powers = powers);

            if (selected.mode == PoolGameMode.FourteenOne)
            {
                GUILayout.Label("Score cible :");
                targetScoreInput = GUILayout.TextField(targetScoreInput);
            }

            if (GUILayout.Button("Commencer la partie"))
            {
                int targetScore = int.TryParse(targetScoreInput, out int parsed) && parsed > 0 ? parsed : 150;
                Select((ref PoolMatchSetup s) => s.targetScore = targetScore);
                pendingStart = true;
            }

            GUILayout.EndArea();
        }

        private delegate void SetupChange(ref PoolMatchSetup setup);

        // From the latest pending click, so two clicks in a frame both count.
        private void Select(SetupChange change)
        {
            PoolMatchSetup next = pendingSelection ?? selected;
            change(ref next);
            pendingSelection = next;
        }

        private void DrawTypeButton(PoolMatchType type)
        {
            GUI.enabled = PoolMatchSetup.Supports(selected.mode, type);
            GUI.color = selected.type == type ? Color.green : Color.white;
            if (GUILayout.Button(PoolMatchSetup.TypeName(type))) Select((ref PoolMatchSetup s) => s.type = type);
            GUI.color = Color.white;
            GUI.enabled = true;
        }

        // A new mode starts on its own type (PoolMatchSetup.NativeType).
        private void DrawModeButton(PoolGameMode mode, string label)
        {
            GUI.color = selected.mode == mode ? Color.green : Color.white;
            if (GUILayout.Button(label))
                Select((ref PoolMatchSetup s) => { s.mode = mode; s.type = PoolMatchSetup.NativeType(mode); });
            GUI.color = Color.white;
        }
    }
}
