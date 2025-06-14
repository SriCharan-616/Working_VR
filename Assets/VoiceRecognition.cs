using UnityEngine;

public class VoiceRecognition : MonoBehaviour
{
    public void OnSpeechResult(string result)
    {
        Debug.Log("🎤 Recognized: " + result);
        // Optionally, send result back to your manager
        FindObjectOfType<VRLetterVoiceManager>().ReceiveRecognizedText(result);
    }
}
