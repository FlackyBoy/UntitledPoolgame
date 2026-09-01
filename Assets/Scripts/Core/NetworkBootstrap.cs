using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UntitledPoolGame.Core
{
    // Temporary manual test harness: Host/Client/Server via on-screen buttons AND
    // keyboard shortcuts (H/C/S), since the FPS controller locks/hides the cursor
    // on Play, which makes the buttons unclickable.
    // Meant to be replaced by a real lobby/menu once the network setup is validated.
    public class NetworkBootstrap : MonoBehaviour
    {
        private void Update()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) NetworkManager.Singleton.StartHost();
            else if (keyboard.f2Key.wasPressedThisFrame) NetworkManager.Singleton.StartClient();
            else if (keyboard.f3Key.wasPressedThisFrame) NetworkManager.Singleton.StartServer();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) NetworkManager.Singleton.StartHost();
            else if (Input.GetKeyDown(KeyCode.F2)) NetworkManager.Singleton.StartClient();
            else if (Input.GetKeyDown(KeyCode.F3)) NetworkManager.Singleton.StartServer();
#endif
        }

        private void OnGUI()
        {
            if (NetworkManager.Singleton == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 150));

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label("Click OR press key:");
                if (GUILayout.Button("Host (F1)")) NetworkManager.Singleton.StartHost();
                if (GUILayout.Button("Client (F2)")) NetworkManager.Singleton.StartClient();
                if (GUILayout.Button("Server (F3)")) NetworkManager.Singleton.StartServer();
            }
            else
            {
                GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client")}");
                if (GUILayout.Button("Shutdown")) NetworkManager.Singleton.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}
