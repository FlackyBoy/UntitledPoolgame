using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UntitledPoolGame.Interaction;

namespace UntitledPoolGame.Pool
{
    // Play Anywhere, for testing: C + A (AZERTY) opens a list of the crate's
    // objects (PlayAnywhereSettings.props); a click drops one on the ground
    // the keyboard player looks at, turned with their view, without the
    // crate or the rounds. Dropped this way (no PlayAnywhereProp), a gadget
    // works straight away (PlayAnywhereGadget). Made by
    // PlayAnywhereRuleSet.Setup in the editor and development builds only;
    // what it dropped goes on Cleanup or "Tout retirer".
    public class PlayAnywhereSpawnMenu : MonoBehaviour
    {
        private static PlayAnywhereSpawnMenu instance;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private bool open;
        private Rect window = new Rect(20f, 20f, 240f, 10f);
        private CursorLockMode lockBefore;
        private bool cursorVisibleBefore;
        private string last;

        // How far ahead an object can be dropped.
        private const float Range = 6f;
        // Only one such window: any fixed id unused by other IMGUI windows.
        private const int WindowId = 0x5041;

        public static void Ensure()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (instance != null) return;
            // A plain scene object: gone with the scene, and on leaving Play.
            instance = new GameObject("Play Anywhere - Menu de test (C+A)").AddComponent<PlayAnywhereSpawnMenu>();
        }

        public static void Clear()
        {
            if (instance == null) return;
            instance.RemoveAll();
            instance.SetOpen(false);
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || PlayAnywhereRuleSet.Current == null)
            {
                if (open) SetOpen(false);
                return;
            }
            // Keys are named by their QWERTY position: the A of an AZERTY
            // keyboard is reported as qKey.
            bool combo = k.cKey.isPressed && k.qKey.isPressed;
            if (combo && !comboWasActive) SetOpen(!open);
            else if (open && k.escapeKey.wasPressedThisFrame) SetOpen(false);
            comboWasActive = combo;
        }

        private bool comboWasActive;

        // While the list is open, the cursor is free and the keyboard
        // player's controls are off (a click on the list would shoot);
        // both put back after.
        private void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            if (open)
            {
                lockBefore = Cursor.lockState;
                cursorVisibleBefore = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                frozenInput = KeyboardPlayer();
                if (frozenInput != null) frozenInput.DeactivateInput();
            }
            else
            {
                Cursor.lockState = lockBefore;
                Cursor.visible = cursorVisibleBefore;
                if (frozenInput != null) frozenInput.ActivateInput();
                frozenInput = null;
            }
        }

        private PlayerInput frozenInput;

        // The player on the keyboard, if any.
        private static PlayerInput KeyboardPlayer()
        {
            foreach (PlayerInput input in PlayerInput.all)
                foreach (InputDevice d in input.devices)
                    if (d is Keyboard) return input;
            return null;
        }

        private void OnGUI()
        {
            if (!open || PlayAnywhereRuleSet.Current == null) return;
            window = GUILayout.Window(WindowId, window, DrawWindow, "Objets de test (C + A)");
        }

        private void DrawWindow(int id)
        {
            PlayAnywhereSettings settings = PlayAnywhereRuleSet.Current.Settings;
            GUILayout.Label("Clic : posé là où regarde le joueur au clavier.");
            foreach (PlayAnywherePropChoice choice in settings.props)
            {
                if (choice == null || choice.prefab == null) continue;
                if (GUILayout.Button(choice.prefab.name)) Spawn(choice);
            }
            if (settings.props.Count == 0) GUILayout.Label("Props est vide : Générer les objets de la caisse.");
            GUILayout.Space(6f);
            if (GUILayout.Button($"Tout retirer ({spawned.Count})")) RemoveAll();
            if (GUILayout.Button("Fermer (Échap)")) SetOpen(false);
            if (!string.IsNullOrEmpty(last)) GUILayout.Label(last);
            GUI.DragWindow();
        }

        private void Spawn(PlayAnywherePropChoice choice)
        {
            Transform view = KeyboardPlayerView();
            if (view == null)
            {
                last = "Pas de joueur avec une caméra.";
                return;
            }
            if (!GroundAhead(view, out Vector3 point))
            {
                last = $"Aucun sol à moins de {Range:0} m devant.";
                return;
            }
            Quaternion rotation = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
            GameObject go = Instantiate(choice.prefab, point, rotation);
            go.name = choice.prefab.name + " (test)";
            go.transform.localScale = choice.prefab.transform.localScale * choice.scale;
            spawned.Add(go);
            PlayAnywhereRuleSet.Current.IgnorePlayers(go.GetComponentsInChildren<Collider>(true));
            last = $"{choice.prefab.name} posé à {point}.";
            if (PlayAnywhereRuleSet.Current.Settings.debugLogs) Debug.Log($"[PlayAnywhere] Menu de test : {last}", go);
        }

        // The view of the player on the keyboard, else of the first player.
        private Transform KeyboardPlayerView()
        {
            Component player = frozenInput;
            if (player == null)
                foreach (LocalPlayerHandController h in PlayAnywhereRuleSet.Current.Hands)
                    if (h != null) { player = h; break; }
            if (player == null)
            {
                Camera main = Camera.main;
                return main != null ? main.transform : null;
            }
            Camera cam = player.GetComponentInChildren<Camera>();
            return cam != null ? cam.transform : player.transform;
        }

        // The first fixed ground along the view (bodies and players aren't
        // ground); missing that, straight down 2 m ahead.
        private bool GroundAhead(Transform view, out Vector3 point)
        {
            if (Nearest(new Ray(view.position, view.forward), Range, out point)) return true;
            Vector3 ahead = view.position + Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * 2f;
            return Nearest(new Ray(ahead + Vector3.up, Vector3.down), 4f, out point);
        }

        private bool Nearest(Ray ray, float range, out Vector3 point)
        {
            point = default;
            int count = Physics.RaycastNonAlloc(ray, hits, range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c.attachedRigidbody != null || c is CharacterController || hits[i].distance >= best) continue;
                best = hits[i].distance;
                point = hits[i].point;
            }
            return best < float.PositiveInfinity;
        }

        private void RemoveAll()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
            last = null;
        }

        private void OnDestroy()
        {
            SetOpen(false);
            if (instance == this) instance = null;
        }
    }
}
