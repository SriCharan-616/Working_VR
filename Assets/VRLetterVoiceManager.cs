using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using UnityEngine.Android;

public class VRLetterVoiceManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject startButton;

    [Header("Fixation Point")]
    public string fixationSymbol = "+";
    public float fixationSize = 0.05f;
    private GameObject fixationPoint;

    [Header("VR Goggles Settings")]
    public bool enableStereoView = true;
    public float eyeSeparation = 0.064f;
    public Vector3 fixedPosition = new Vector3(0, 1.6f, 0);

    [Header("Chart Display Settings")]
    public float distanceFromPlayer = 0.6f;
    public float peripheralFontSize = 0.04f;
    public Color letterColor = Color.white;

    [Header("Letter Display")]
    public float displayInterval = 2f;
    public float intervalBetweenLetters = 0.5f;
    public float minSize = 0.03f;
    public float maxSize = 0.08f;
    public bool randomizeSize = true;

    private Camera leftEyeCamera;
    private Camera rightEyeCamera;
    private List<GameObject> instantiatedLetters = new List<GameObject>();
    private List<PeripheralLetterData> peripheralLetters = new List<PeripheralLetterData>();
    private int currentIndex = 0;
    private bool isDisplaying = false;
    private bool gyroEnabled = false;
    private Quaternion baseRotation = Quaternion.identity;

    private string recognizedText = "";
    private string filePath;

    private AndroidJavaObject unityActivity;

    [System.Serializable]
    public class PeripheralLetterData
    {
        public string letter;
        public float eccentricity;
        public float meridian;
        public float fontSize = 0.04f;
        public bool isVisible = true;
    }

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "VRVoiceLog_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
        SetupStereoCameras();
        SetupPeripheralChart();
        CreateFixationPoint();

        // Gyroscope
        gyroEnabled = SystemInfo.supportsGyroscope;
        if (gyroEnabled)
        {
            Input.gyro.enabled = true;
            baseRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
#endif
    }

    void Update()
    {
        if (gyroEnabled)
        {
            Quaternion deviceRotation = Input.gyro.attitude;
            Quaternion correctedRotation = baseRotation * new Quaternion(-deviceRotation.x, -deviceRotation.y, deviceRotation.z, deviceRotation.w);
            if (leftEyeCamera) leftEyeCamera.transform.localRotation = correctedRotation;
            if (rightEyeCamera) rightEyeCamera.transform.localRotation = correctedRotation;
            if (fixationPoint)
            {
                fixationPoint.transform.LookAt(leftEyeCamera.transform);
                fixationPoint.transform.Rotate(0, 180, 0);
            }
        }
    }

    public void StartExperiment()
    {
        if (startButton != null)
            startButton.SetActive(false);

        DisplayChart();              
        currentIndex = 0;            
        StartCoroutine(ShowLettersAndRecognize());
    }

    IEnumerator ShowLettersAndRecognize()
    {
        if (instantiatedLetters.Count == 0)
        {
            Debug.LogError("No letters were instantiated. Did you forget to call DisplayChart()?");
            yield break;
        }

        isDisplaying = true;
        while (isDisplaying)
        {
            foreach (var letter in instantiatedLetters)
                letter.SetActive(false);

            if (currentIndex >= instantiatedLetters.Count)
                currentIndex = 0;

            var currentLetterObj = instantiatedLetters[currentIndex];
            currentLetterObj.SetActive(true);

            string letterDisplayed = GetCurrentLetter();
            Debug.Log("Displaying letter: " + letterDisplayed);

            yield return new WaitForSeconds(displayInterval);

            currentLetterObj.SetActive(false);
            yield return StartCoroutine(StartListening(letterDisplayed));

            currentIndex++;
            yield return new WaitForSeconds(intervalBetweenLetters);
        }
    }

    IEnumerator StartListening(string displayedLetter)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        recognizedText = "";

        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaObject voiceBridge = new AndroidJavaObject("com.unity3d.player.VoiceBridge", currentActivity);
            voiceBridge.Call("startSpeechRecognition");
        }
        catch (Exception e)
        {
            Debug.LogError("Error starting speech recognition: " + e.Message);
        }

        float timeout = 5f;
        float timer = 0f;
        while (recognizedText == "" && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        string spoken = string.IsNullOrEmpty(recognizedText) ? "NoResult" : recognizedText;
        SaveToFile(displayedLetter, spoken);
#else
        recognizedText = UnityEngine.Random.value > 0.5f ? displayedLetter : "WrongLetter";
        yield return new WaitForSeconds(2f);
        SaveToFile(displayedLetter, recognizedText);
#endif
    }

    void SaveToFile(string displayedLetter, string saidLetter)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] Displayed: {displayedLetter}, Spoken: {saidLetter}\n";
        File.AppendAllText(filePath, entry);
        Debug.Log("Saved: " + entry);
    }

    public string GetCurrentLetter()
    {
        if (currentIndex < instantiatedLetters.Count)
        {
            string[] parts = instantiatedLetters[currentIndex].name.Split('_');
            if (parts.Length >= 2) return parts[1];
        }
        return "";
    }

    void SetupStereoCameras()
    {
        Camera mainCamera = Camera.main ?? FindObjectOfType<Camera>();
        if (!mainCamera)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCamera = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        if (enableStereoView)
        {
            GameObject leftEyeObj = new GameObject("Left Eye Camera");
            leftEyeCamera = leftEyeObj.AddComponent<Camera>();
            leftEyeCamera.CopyFrom(mainCamera);
            leftEyeCamera.rect = new Rect(0, 0, 0.5f, 1);
            leftEyeObj.transform.position = fixedPosition + Vector3.left * (eyeSeparation / 2);

            GameObject rightEyeObj = new GameObject("Right Eye Camera");
            rightEyeCamera = rightEyeObj.AddComponent<Camera>();
            rightEyeCamera.CopyFrom(mainCamera);
            rightEyeCamera.rect = new Rect(0.5f, 0, 0.5f, 1);
            rightEyeObj.transform.position = fixedPosition + Vector3.right * (eyeSeparation / 2);

            mainCamera.enabled = false;
        }
        else
        {
            leftEyeCamera = mainCamera;
            leftEyeCamera.transform.position = fixedPosition;
        }

        ConfigureCamera(leftEyeCamera);
        if (rightEyeCamera != null) ConfigureCamera(rightEyeCamera);
    }

    void ConfigureCamera(Camera cam)
    {
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 90f;
        cam.backgroundColor = Color.black;
    }

    void SetupPeripheralChart()
    {
        peripheralLetters.Clear();
        string[] chartLetters = { "E", "F", "P", "T", "O", "Z", "L", "D", "C" };
        CreatePeripheralRing(10f, 8, chartLetters);
        CreatePeripheralRing(15f, 12, chartLetters);
    }

    void CreatePeripheralRing(float eccentricity, int letterCount, string[] letters)
    {
        float angleStep = 360f / letterCount;
        for (int i = 0; i < letterCount; i++)
        {
            float meridian = i * angleStep;
            string letter = letters[UnityEngine.Random.Range(0, letters.Length)];

            PeripheralLetterData data = new PeripheralLetterData
            {
                letter = letter,
                eccentricity = eccentricity,
                meridian = meridian,
                fontSize = peripheralFontSize,
                isVisible = true
            };
            peripheralLetters.Add(data);
        }
    }

    void DisplayChart()
    {
        ClearExistingLetters();
        foreach (var letterData in peripheralLetters)
        {
            if (letterData.isVisible)
            {
                GameObject obj = CreatePeripheralLetterObject(letterData);
                instantiatedLetters.Add(obj);
            }
        }
    }

    GameObject CreatePeripheralLetterObject(PeripheralLetterData data)
    {
        GameObject obj = new GameObject($"Letter_{data.letter}_{data.eccentricity}deg");
        TextMeshPro textMesh = obj.AddComponent<TextMeshPro>();

        textMesh.text = data.letter;
        textMesh.fontSize = data.fontSize * 100;
        textMesh.color = letterColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;

        obj.transform.position = PolarToWorldPosition(data.eccentricity, data.meridian);
        obj.transform.LookAt(leftEyeCamera.transform);
        obj.transform.Rotate(0, 180, 0);

        if (randomizeSize)
        {
            float randomSize = UnityEngine.Random.Range(minSize, maxSize);
            obj.transform.localScale = Vector3.one * randomSize;
        }

        obj.SetActive(false);
        return obj;
    }

    Vector3 PolarToWorldPosition(float eccentricity, float meridian)
    {
        float er = eccentricity * Mathf.Deg2Rad;
        float mr = meridian * Mathf.Deg2Rad;
        float x = distanceFromPlayer * Mathf.Sin(er) * Mathf.Cos(mr);
        float y = distanceFromPlayer * Mathf.Sin(er) * Mathf.Sin(mr);
        float z = distanceFromPlayer * Mathf.Cos(er);
        return fixedPosition + new Vector3(x, y, z);
    }

    void ClearExistingLetters()
    {
        foreach (GameObject letter in instantiatedLetters)
            if (letter != null)
                Destroy(letter);
        instantiatedLetters.Clear();
    }

    void CreateFixationPoint()
    {
        fixationPoint = new GameObject("FixationPoint");
        TextMeshPro textMesh = fixationPoint.AddComponent<TextMeshPro>();
        textMesh.text = fixationSymbol;
        textMesh.fontSize = fixationSize * 100;
        textMesh.color = letterColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        fixationPoint.transform.position = fixedPosition + new Vector3(0, 0, distanceFromPlayer);
        fixationPoint.transform.LookAt(leftEyeCamera.transform);
        fixationPoint.transform.Rotate(0, 180, 0);
    }

    public void ReceiveRecognizedText(string result)
    {
        recognizedText = result;
    }
}
