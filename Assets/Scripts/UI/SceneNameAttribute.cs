using UnityEngine;

namespace UntitledPoolGame.Core
{
    // On a string field holding a scene name: the Inspector shows a scene
    // picker (drag a scene, or pick it from the list) instead of a text box
    // where a typo silently breaks the loading. The game itself only keeps
    // the name, which is what SceneManager.LoadSceneAsync takes. Drawer:
    // Editor/SceneNameDrawer.
    public class SceneNameAttribute : PropertyAttribute { }
}
