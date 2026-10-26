using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Unity side of the React bridge. In a WebGL build events go through Assets/Plugins/WebGL/SoccerVisionBridge.jslib
    /// to react-unity-webgl's window.dispatchReactUnityEvent (and a plain window CustomEvent). Commands arrive via
    /// sendMessage("SoccerVisionBridge", "OnCommand", json). In the editor everything is a no-op except logging.
    /// </summary>
    public sealed class WebBridge : MonoBehaviour
    {
        public const string GameObjectName = "SoccerVisionBridge";

        public event Action<BridgeCommand> CommandReceived;

        public bool logInEditor;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SV_Emit(string eventName, string json);
#endif

        public void Emit(string eventName, object payload)
        {
            string json = payload is string s ? s : JsonUtility.ToJson(payload);
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SV_Emit(eventName, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("SoccerVision bridge emit failed: " + ex.Message);
            }
#else
            if (logInEditor) Debug.Log("[bridge] " + eventName + " " + json);
#endif
        }

        /// <summary>Called by the host page (react-unity-webgl sendMessage).</summary>
        public void OnCommand(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            BridgeCommand cmd;
            try
            {
                cmd = JsonUtility.FromJson<BridgeCommand>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("SoccerVision bridge: bad command " + json + " (" + ex.Message + ")");
                return;
            }
            if (cmd != null) CommandReceived?.Invoke(cmd);
        }
    }
}
