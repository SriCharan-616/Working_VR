using UnityEngine;

public class VoiceBridgeHandler : MonoBehaviour
{
    private AndroidJavaObject voiceBridge;

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                voiceBridge = new AndroidJavaObject("com.unity3d.player.VoiceBridge", activity);
            }
        }
    }

    public void StartListening()
    {
        if (voiceBridge != null)
        {
            voiceBridge.Call("StartListening");
        }
    }

    public void StopListening()
    {
        voiceBridge?.Call("StopListening");
    }

    private void OnDestroy()
    {
        voiceBridge?.Call("Destroy");
    }
}
