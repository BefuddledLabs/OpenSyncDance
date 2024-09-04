using UnityEditor;
using UnityEngine.SceneManagement;

namespace BefuddledLabs.OpenSyncDance
{
    [InitializeOnLoad]
    public static class PlayModeGenerate
    {
        static PlayModeGenerate()
        {
            EditorApplication.playModeStateChanged += ModeStateChanged;
        }

        private static void ModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            
            foreach (var root in roots)
            {
                var syncDance = root.GetComponentsInChildren<OpenSyncDance>(false);
                if (syncDance.Length == 0) continue;

                foreach (var syncDanceComponent in syncDance)
                {
                    if (syncDanceComponent.gameObject.activeInHierarchy)
                        syncDanceComponent.GenerateAll();
                }
            }
        }
    }
}