using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Makes the project runnable from any scene: if the loaded scene has no MatchController, one is created.
    /// (Use the menu SoccerVision &gt; Create Match Scene to get a saved scene for builds.)
    /// </summary>
    public static class MatchBootstrap
    {
        /// <summary>Set to false from other scenes' code (e.g. a future main menu) to disable auto-spawn.</summary>
        public static bool AutoSpawn = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfMissing()
        {
            if (!AutoSpawn) return;
            if (Object.FindFirstObjectByType<MatchController>() != null) return;
            var go = new GameObject("SoccerVisionMatch");
            go.AddComponent<MatchController>();
        }
    }
}
