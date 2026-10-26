// Unity -> host page bridge for SoccerVision (see Runtime/Bridge/WebBridge.cs).
// react-unity-webgl exposes window.dispatchReactUnityEvent(name, ...args); we also fire a plain
// "soccervision:<name>" CustomEvent so the build can be hosted without React.
mergeInto(LibraryManager.library, {
  SV_Emit: function (eventNamePtr, jsonPtr) {
    var eventName = UTF8ToString(eventNamePtr);
    var json = UTF8ToString(jsonPtr);
    try {
      if (typeof window.dispatchReactUnityEvent === "function") {
        window.dispatchReactUnityEvent(eventName, json);
      }
    } catch (e) {
      console.warn("SoccerVision: dispatchReactUnityEvent failed", e);
    }
    try {
      window.dispatchEvent(new CustomEvent("soccervision:" + eventName, { detail: json }));
    } catch (e) {
      // CustomEvent unsupported: ignore
    }
  },
});
