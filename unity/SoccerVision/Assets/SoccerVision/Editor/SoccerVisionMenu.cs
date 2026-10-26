using System.IO;
using System.Linq;
using SoccerVision.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerVision.EditorTools
{
    /// <summary>Editor menu: create the match scene and build the WebGL player for the React shell.</summary>
    public static class SoccerVisionMenu
    {
        private const string ScenePath = "Assets/Scenes/Match.unity";

        [MenuItem("SoccerVision/Create Match Scene")]
        public static void CreateMatchScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            cam.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0f, 0f, -20f);
            new GameObject("SoccerVisionMatch").AddComponent<MatchController>();
            new GameObject(WebBridge.GameObjectName).AddComponent<WebBridge>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            Debug.Log("SoccerVision: created " + ScenePath + " - press Play to watch the demo match.");
        }

        [MenuItem("SoccerVision/Build WebGL for React")]
        public static void BuildWebGL()
        {
            if (!File.Exists(ScenePath)) CreateMatchScene();
            AddSceneToBuild();
            // Output: <repo>/web/public/unity/SoccerVision/Build/SoccerVision.{loader.js,data,framework.js,wasm}
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../web/public/unity/SoccerVision"));
            PlayerSettings.productName = "SoccerVision";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            // Uncompressed builds work on any dev server; for production prefer Brotli with correct headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("SoccerVision WebGL build: " + report.summary.result + " -> " + output);
        }

        private static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
