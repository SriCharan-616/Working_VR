using UnityEngine;
using System.IO;
using System;

public class AndroidVoiceInput : MonoBehaviour
{
    private bool isListening = false;
    private string filePath;
    private AndroidJavaObject speechRecognizer;
    private AndroidJavaObject unityActivity;

    void Start()
    {
        // Create file path with current date
        string fileName = "VoiceRecognition_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        Debug.Log("Voice recognition file: " + filePath);

        // Initialize Android Speech Recognition
        InitializeAndroidSpeechRecognition();

        // Start listening immediately
        StartListening();
    }

    private void InitializeAndroidSpeechRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            // Check if speech recognition is available
            AndroidJavaClass speechRecognizer = new AndroidJavaClass("android.speech.SpeechRecognizer");
            bool isAvailable = speechRecognizer.CallStatic<bool>("isRecognitionAvailable", unityActivity);
            
            if (isAvailable)
            {
                Debug.Log("Speech recognition is available on this device");
            }
            else
            {
                Debug.LogError("Speech recognition not available on this device");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to initialize Android Speech Recognition: " + e.Message);
        }
#endif
    }

    public void StartListening()
    {
        if (!isListening)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Call Android native speech recognition
            CallAndroidSpeechRecognition();
#else
            Debug.Log("Speech recognition only works on Android device");
#endif

            isListening = true;
            Debug.Log("Started listening...");
        }
    }

    private void CallAndroidSpeechRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
            AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", 
                "android.speech.action.RECOGNIZE_SPEECH");
            
            intent.Call<AndroidJavaObject>("putExtra", 
                "android.speech.extra.LANGUAGE_MODEL", "free_form");
            intent.Call<AndroidJavaObject>("putExtra", 
                "android.speech.extra.LANGUAGE", "en-US");
            intent.Call<AndroidJavaObject>("putExtra", 
                "android.speech.extra.PROMPT", "Speak now...");
            
            unityActivity.Call("startActivityForResult", intent, 1001);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to start speech recognition: " + e.Message);
            isListening = false;
        }
#endif
    }

    // This method will be called from Android native code
    public void OnSpeechResult(string result)
    {
        if (!string.IsNullOrEmpty(result))
        {
            SaveToFile(result);
            Debug.Log("Recognized and saved: " + result);
        }

        isListening = false;

        // Restart listening after a short delay
        Invoke("StartListening", 1f);
    }

    private void SaveToFile(string recognizedText)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] {recognizedText}\n";

            File.AppendAllText(filePath, entry);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save to file: " + e.Message);
        }
    }

    public void StopListening()
    {
        isListening = false;
        Debug.Log("Stopped listening.");
    }

    private void OnDestroy()
    {
        StopListening();
    }
}