using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Android;

public class VRVoiceChartManager : MonoBehaviour
{
    public Button startButton;

    // Eye settings
    public Camera leftEyeCamera;
    public Camera rightEyeCamera;
    public GameObject peripheralLetterPrefab;
    public float displayDistance = 2f;
    public float displayInterval = 2f;
    public float intervalBetweenLetters = 1f;

    // Gyroscope
    private Quaternion baseRotation = Quaternion.identity;
    private bool gyroEnabled = false;

    // Voice Recognition
    private AndroidJavaObject voiceBridge, unityActivity;
    private bool voiceSystemInitialized = false;
    private bool isListening = false;
    private string lastSpokenLetter = "";

    // Display State
    private bool isDisplaying = false;
    private int currentIndex = 0;
    private string currentlyDisplayedLetter = "";

    private List<GameObject> instantiatedLetters = new List<GameObject>();
    private List<PeripheralLetterData> peripheralLetters = new List<PeripheralLetterData>();

    // Logging
    private string filePath;

    [System.Serializable]
    public class PeripheralLetterData
    {
        public string letter;
        public float eccentricity;
        public float meridian;
        public float fontSize;
        public bool isVisible = true;
    }

    void Start()
    {
        SetupVRChart();
        EnableGyroscope();
        InitializeVoiceSystem();
        SetupButton();
    }

    void SetupVRChart()
    {
        if (leftEyeCamera == null) leftEyeCamera = Camera.main;
        if (rightEyeCamera == null) rightEyeCamera = Camera.main;

        baseRotation = Quaternion.identity;

        // Sample letter data
        peripheralLetters.Add(new PeripheralLetterData { letter = "E", eccentricity = 5f, meridian = 0f, fontSize = 80f });
        peripheralLetters.Add(new PeripheralLetterData { letter = "F", eccentricity = 5f, meridian = 90f, fontSize = 80f });
        peripheralLetters.Add(new PeripheralLetterData { letter = "P", eccentricity = 5f, meridian = 180f, fontSize = 80f });
        peripheralLetters.Add(new PeripheralLetterData { letter = "T", eccentricity = 5f, meridian = 270f, fontSize = 80f });
    }

    void SetupButton()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartTestSequence);
        }
    }

    void EnableGyroscope()
    {
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
            gyroEnabled = true;
        }
        else
        {
            Debug.LogWarning("Gyroscope not supported on this device.");
        }
    }

    public void StartTestSequence()
    {
        if (isDisplaying) return;

        ClearExistingLetters();
        DisplayChart();

        currentIndex = 0;
        isDisplaying = true;

        filePath = CreateFilePath("VRVoiceChartResults");

        StartCoroutine(ShowLettersOneByOne());
        StartVoiceRecognition();

        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.GetComponentInChildren<Text>().text = "Running...";
        }
    }

    void DisplayChart()
    {
        foreach (var data in peripheralLetters)
        {
            if (data.isVisible)
            {
                GameObject letterObject = CreatePeripheralLetterObject(data);
                instantiatedLetters.Add(letterObject);
            }
        }
        Debug.Log("Peripheral chart displayed with " + instantiatedLetters.Count + " letters.");
    }

    void ClearExistingLetters()
    {
        foreach (var letter in instantiatedLetters)
        {
            Destroy(letter);
        }
        instantiatedLetters.Clear();
    }

    GameObject CreatePeripheralLetterObject(PeripheralLetterData data)
    {
        float angleRadians = data.meridian * Mathf.Deg2Rad;
        Vector3 position = Quaternion.Euler(0, data.meridian, 0) * Vector3.forward * displayDistance;

        GameObject letterObject = Instantiate(peripheralLetterPrefab, position, Quaternion.identity);
        letterObject.transform.localScale = Vector3.one * (data.fontSize / 100f);
        TextMesh textMesh = letterObject.GetComponentInChildren<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = data.letter;
        }

        letterObject.SetActive(false);
        return letterObject;
    }

    IEnumerator ShowLettersOneByOne()
    {
        while (isDisplaying && instantiatedLetters.Count > 0)
        {
            // Hide all letters
            foreach (var letterObj in instantiatedLetters)
            {
                letterObj.SetActive(false);
            }

            // Show current letter
            var currentLetterObj = instantiatedLetters[currentIndex];
            currentlyDisplayedLetter = GetCurrentLetter();
            currentLetterObj.SetActive(true);

            Debug.Log($"[Prompt] Say: {currentlyDisplayedLetter}");

            yield return new WaitForSeconds(displayInterval);

            currentIndex = (currentIndex + 1) % instantiatedLetters.Count;
            yield return new WaitForSeconds(intervalBetweenLetters);
        }
    }

    string GetCurrentLetter()
    {
        if (currentIndex >= 0 && currentIndex < peripheralLetters.Count)
        {
            return peripheralLetters[currentIndex].letter;
        }
        return "";
    }

    void InitializeVoiceSystem()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!voiceSystemInitialized)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
            using (AndroidJavaClass bridgeClass = new AndroidJavaClass("com.vrchart.voiceplugin.VoiceBridge"))
            {
                voiceBridge = bridgeClass.CallStatic<AndroidJavaObject>("getInstance");
                voiceBridge.Call("setUnityActivity", unityActivity);
            }
            voiceSystemInitialized = true;
        }
#endif
    }

    void StartVoiceRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartListening();
#endif
    }

    void StartListening()
    {
        if (!isListening)
        {
            voiceBridge.Call("startListening");
            isListening = true;
        }
    }

    public void OnSpeechResult(string result)
    {
        lastSpokenLetter = result.Trim().ToUpper();
        Debug.Log($"[Speech] You said: {lastSpokenLetter}");

        bool isMatch = string.Equals(lastSpokenLetter, currentlyDisplayedLetter, StringComparison.OrdinalIgnoreCase);
        Debug.Log(isMatch ? "[✅ Match]" : "[❌ No Match]");

        StartCoroutine(SaveToFileCoroutine(lastSpokenLetter, currentlyDisplayedLetter));

#if UNITY_ANDROID && !UNITY_EDITOR
        isListening = false;
        Invoke("StartListening", 1f);
#endif
    }

    IEnumerator SaveToFileCoroutine(string spoken, string expected)
    {
        string entry = $"{DateTime.Now:HH:mm:ss}, Expected: {expected}, Heard: {spoken}, Result: {(spoken == expected ? "Correct" : "Incorrect")}";
        File.AppendAllText(filePath, entry + Environment.NewLine);
        yield return null;
    }

    string CreateFilePath(string fileNamePrefix)
    {
        string directory = Application.persistentDataPath;
        string fileName = $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        return Path.Combine(directory, fileName);
    }

    public void Stop()
    {
        isDisplaying = false;
        StopAllCoroutines();

        if (startButton != null)
        {
            startButton.interactable = true;
            startButton.GetComponentInChildren<Text>().text = "Start";
        }
    }
}
