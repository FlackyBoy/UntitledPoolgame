using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // In-game test menu: hold the left mouse button and press the right one
    // 4 times quickly to open it (same gesture, or its "Fermer" button, to
    // close it). From it:
    //   - give any power to player 1 or 2 without touching a crate (replaces
    //     what they hold), or empty their slot;
    //   - make the current player win or lose;
    //   - jump to the "pot the 8" phase (same as the C+W cheat);
    //   - trigger an error, choosing the condition: a plain foul by player
    //     1 or 2, the current player potting a ball of group 1 (solids) or
    //     2 (stripes), or their shot touching a ball of group 1 or 2 first.
    //     Shots are judged by the real rule set (PoolMatchRules.Debug*).
    // While it's open, the players' inputs are off and the cursor is free,
    // as in the pause menu. Provisional OnGUI drawing (a test tool, not part
    // of the game's UI). Created automatically in any scene that has a
    // PoolMatchRules.
    public class DebugMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (FindAnyObjectByType<PoolMatchRules>() == null || FindAnyObjectByType<DebugMenu>() != null) return;
            new GameObject("Debug Menu").AddComponent<DebugMenu>();
        }

        [Tooltip("Nombre de clics droits, clic gauche maintenu, pour ouvrir / fermer le menu.")]
        [SerializeField] private int rightClicksToToggle = 4;
        [Tooltip("Temps (secondes) pour faire tous les clics droits.")]
        [SerializeField] private float comboWindow = 1.5f;

        // Any id unique among this project's GUI windows (only one menu exists).
        private const int WindowId = 0x0DEB06;

        private enum ErrorKind { Foul, PotGroup, FirstContactGroup }
        private static readonly string[] ErrorKindLabels = { "Faute", "Empoche une bille", "1re bille touchée" };

        private bool open;
        private readonly List<float> rightClicks = new List<float>();
        private readonly List<PoolPower> powers = new List<PoolPower>();
        private ErrorKind errorKind;
        private string status = "";
        private Rect window = new Rect(20f, 20f, 460f, 620f);
        private Vector2 scroll;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // The right clicks only count while the left button is held, and
            // all within comboWindow.
            if (!mouse.leftButton.isPressed)
            {
                rightClicks.Clear();
                return;
            }
            if (!mouse.rightButton.wasPressedThisFrame) return;

            float now = Time.unscaledTime;
            rightClicks.Add(now);
            rightClicks.RemoveAll(t => now - t > comboWindow);
            if (rightClicks.Count < rightClicksToToggle) return;
            rightClicks.Clear();
            SetOpen(!open);
        }

        private void OnDisable()
        {
            if (open) SetOpen(false);
        }

        private void SetOpen(bool value)
        {
            if (value && PauseMenu.IsPaused) return;
            open = value;
            if (open)
            {
                CollectPowers();
                status = "";
                foreach (PlayerInput player in PlayerInput.all) player.DeactivateInput();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            foreach (PlayerInput player in PlayerInput.all) player.ActivateInput();
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules != null && rules.MatchStarted && !rules.GameOver && !PauseMenu.IsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Every power asset of the project in the editor (no need to add a
        // new power anywhere to test it); in a build, the ones handed out by
        // the crates (PoolPowerSpawnSettings).
        private void CollectPowers()
        {
            powers.Clear();
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:PoolPower"))
                AddPower(UnityEditor.AssetDatabase.LoadAssetAtPath<PoolPower>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
#endif
            PoolPowerSpawnSettings spawn = PoolSettingsLoader.LoadOrDefault<PoolPowerSpawnSettings>("PoolPowerSpawnSettings");
            if (spawn != null && spawn.availablePowers != null)
                foreach (PoolPower power in spawn.availablePowers) AddPower(power);
            powers.Sort((a, b) => string.CompareOrdinal(a.PowerName, b.PowerName));
        }

        private void AddPower(PoolPower power)
        {
            if (power != null && !powers.Contains(power)) powers.Add(power);
        }

        private void OnGUI()
        {
            if (!open) return;
            window = GUI.Window(WindowId, window, DrawWindow, "Menu debug");
        }

        private void DrawWindow(int id)
        {
            PoolMatchRules rules = PoolMatchRules.Instance;
            if (rules == null || !rules.MatchStarted)
            {
                GUILayout.Label("Aucune partie en cours.");
                if (GUILayout.Button("Fermer")) SetOpen(false);
                GUI.DragWindow();
                return;
            }

            int current = rules.CurrentPlayer;
            GUILayout.BeginHorizontal();
            GUILayout.Label(rules.GameOver ? $"Partie finie — J{rules.Winner + 1} a gagné" : $"Joueur actif : J{current + 1}");
            if (GUILayout.Button("Fermer", GUILayout.Width(70f))) SetOpen(false);
            GUILayout.EndHorizontal();
            if (!rules.GameOver && !rules.DebugCanAct)
                GUILayout.Label("Les billes roulent : attendre qu'elles s'arrêtent.");

            scroll = GUILayout.BeginScrollView(scroll);
            DrawPowers(rules);
            DrawMatch(rules, current);
            DrawErrors(rules, current);
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            GUI.DragWindow();
        }

        private void DrawPowers(PoolMatchRules rules)
        {
            Section("Pouvoirs");
            GUILayout.Label($"En main : J1 {HeldName(rules, 0)} · J2 {HeldName(rules, 1)}");
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !rules.GameOver;
            foreach (PoolPower power in powers)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{power.PowerName} ({power.Type})");
                // Level 1, then level 2 (power levels: PoolMatchRules.GetHeldPowerLevel).
                for (int level = 1; level <= 2; level++)
                    for (int p = 0; p < 2; p++)
                        if (GUILayout.Button(level == 1 ? $"J{p + 1}" : $"J{p + 1} N2", GUILayout.Width(level == 1 ? 34f : 48f)))
                        {
                            rules.DebugSetHeldPower(p, power, level);
                            status = $"{power.PowerName} niveau {level} donné à J{p + 1}.";
                        }
                GUILayout.EndHorizontal();
            }
            if (powers.Count == 0) GUILayout.Label("Aucun asset de pouvoir trouvé.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Vider la main");
            for (int p = 0; p < 2; p++)
                if (GUILayout.Button($"J{p + 1}", GUILayout.Width(40f)))
                {
                    rules.DebugSetHeldPower(p, null);
                    status = $"Main de J{p + 1} vidée.";
                }
            GUILayout.EndHorizontal();
            GUI.enabled = wasEnabled;
        }

        private static string HeldName(PoolMatchRules rules, int player)
        {
            PoolPower held = rules.GetHeldPower(player);
            if (held == null) return "—";
            int level = rules.GetHeldPowerLevel(player);
            return level > 1 ? $"{held.PowerName} (niv. {level})" : held.PowerName;
        }

        private void DrawMatch(PoolMatchRules rules, int current)
        {
            Section("Partie");
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !rules.GameOver;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Victoire (J{current + 1})")) { rules.Win(current); status = $"Victoire de J{current + 1}."; }
            if (GUILayout.Button($"Défaite (J{current + 1})")) { rules.Win(1 - current); status = $"Défaite de J{current + 1}."; }
            GUILayout.EndHorizontal();

            GUI.enabled = rules.DebugCanAct;
            if (GUILayout.Button("Passer le tour")) { rules.DebugSwitchTurn(); status = $"Au tour de J{rules.CurrentPlayer + 1}."; }

            Section("Phase de jeu");
            GUI.enabled = rules.DebugCanAct && rules.Mode != PoolGameMode.NineBall && rules.Mode != PoolGameMode.FourteenOne;
            if (GUILayout.Button($"Empocher la 8 (J{current + 1} a vidé son groupe)"))
            {
                rules.DebugForceEightBallEndgame();
                status = $"J{current + 1} doit maintenant annoncer et empocher la 8.";
            }
            GUI.enabled = wasEnabled;
        }

        private void DrawErrors(PoolMatchRules rules, int current)
        {
            Section("Erreurs");
            errorKind = (ErrorKind)GUILayout.Toolbar((int)errorKind, ErrorKindLabels);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = rules.DebugCanAct;

            if (errorKind == ErrorKind.Foul)
            {
                GUILayout.Label("Faute du joueur choisi : main libre pour l'autre.");
                GUILayout.BeginHorizontal();
                for (int p = 0; p < 2; p++)
                    if (GUILayout.Button($"Faute de J{p + 1}"))
                    {
                        rules.DebugFoul(p);
                        status = $"Faute de J{p + 1} : main libre pour J{2 - p}.";
                    }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(errorKind == ErrorKind.PotGroup
                    ? $"J{current + 1} empoche une bille du groupe choisi (sa 1re bille touchée reste une des siennes si possible)."
                    : $"La blanche de J{current + 1} touche d'abord une bille du groupe choisi, rien n'est empoché.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Groupe 1 (pleines)")) TriggerGroupError(rules, BallGroup.Solid, 1);
                if (GUILayout.Button("Groupe 2 (rayées)")) TriggerGroupError(rules, BallGroup.Stripe, 2);
                GUILayout.EndHorizontal();
            }
            GUI.enabled = wasEnabled;
        }

        private void TriggerGroupError(PoolMatchRules rules, BallGroup group, int groupNumber)
        {
            int shooter = rules.CurrentPlayer;
            PoolBall ball = FindBall(group, null);
            if (ball == null)
            {
                status = $"Plus aucune bille du groupe {groupNumber} sur la table.";
                return;
            }

            if (errorKind == ErrorKind.PotGroup)
            {
                // A legal first contact when there is one (one of the
                // shooter's own balls), so only the pot itself is tested.
                BallGroup own = rules.GetGroup(shooter);
                PoolBall contact = own == BallGroup.Solid || own == BallGroup.Stripe ? FindBall(own, ball) : null;
                rules.DebugResolveShot(ball, contact != null ? contact : ball);
                status = $"J{shooter + 1} a empoché la {ball.Number} (groupe {groupNumber}).";
            }
            else
            {
                rules.DebugResolveShot(null, ball);
                status = $"J{shooter + 1} a touché d'abord la {ball.Number} (groupe {groupNumber}).";
            }
            status += rules.GameOver ? $" Partie finie : J{rules.Winner + 1} gagne." : $" Au tour de J{rules.CurrentPlayer + 1}.";
        }

        private static PoolBall FindBall(BallGroup group, PoolBall except)
        {
            foreach (PoolBall ball in PoolBall.Active)
                if (ball != except && ball.Group == group) return ball;
            return null;
        }

        private GUIStyle sectionStyle;

        private void Section(string title)
        {
            if (sectionStyle == null) sectionStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Space(6f);
            GUILayout.Label(title, sectionStyle);
        }
    }
}
