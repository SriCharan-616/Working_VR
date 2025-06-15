using UnityEngine;

public class VoiceReceiver : MonoBehaviour
{
    public void OnSpeechResult(string text)
    {
        Debug.Log("Speech Result: " + text);
    }

    public void OnSpeechError(string error)
    {
        Debug.LogError("Speech Error: " + error);
    }
}
