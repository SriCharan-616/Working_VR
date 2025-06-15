using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Android;

public class VRVoiceChartManager : MonoBehaviour
{
    public Button startButton;

    #region --- Voice Recognition Variables ---
    private string lastSpokenLetter = "";
    private bool isListening = false;
    private string filePath;
    private AndroidJavaObject voiceBridge;
    private AndroidJavaObject unityActivity;
    private bool voiceSystemInitialized = false;
    #endregion

    #region --- VR Goggle Settings ---
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
    #endregion

    #region --- Internal VR Display State ---
    private Camera leftEyeCamera;
    private Camera rightEyeCamera;
    private List<GameObject> instantiatedLetters = new List<GameObject>();
    private List<PeripheralLetterData> peripheralLetters = new List<PeripheralLetterData>();
    private int currentIndex = 0;
    private bool isDisplaying = false;
    private Quaternion baseRotation = Quaternion.identity;
    private bool gyroEnabled = false;
    private string currentlyDisplayedLetter = "";
    #endregion

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
        Debug.Log("VRVoiceChartManager Start() ");

        SetupStereoCameras();
        SetupPeripheralChart();
        CreateFixationPoint();

        gyroEnabled = SystemInfo.supportsGyroscope;
        if (gyroEnabled)
        {
            Input.gyro.enabled = true;
            baseRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Initialize voice system
        InitializeVoiceSystem();

        // Set up button listener
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartTestSequence);
            Debug.Log("Button listener added");
        }
        else
        {
            Debug.LogError("Start button is not assigned!");
        }

        Debug.Log("VRVoiceChartManager Start() Complete ");
    }

    void InitializeVoiceSystem()
    {
        Debug.Log("INITIALIZING VOICE SYSTEM ");

#if UNITY_ANDROID && !UNITY_EDITOR
        InitializeVoiceBridge();
        CreateFilePath();
        voiceSystemInitialized = true;
        Debug.Log("Voice system initialized for Android");
#else
        Debug.Log("Voice system not initialized - not on Android platform");
        // For testing in editor, create a mock file path
        CreateFilePath();
        voiceSystemInitialized = true;
#endif

        Debug.Log("VOICE SYSTEM INIT COMPLETE ");
    }

    void Update()
    {
        if (gyroEnabled)
        {
            Quaternion deviceRotation = Input.gyro.attitude;
            Quaternion correctedRotation = baseRotation * new Quaternion(-deviceRotation.x, -deviceRotation.y, deviceRotation.z, deviceRotation.w);

            if (leftEyeCamera != null)
                leftEyeCamera.transform.localRotation = correctedRotation;
            if (rightEyeCamera != null)
                rightEyeCamera.transform.localRotation = correctedRotation;

            if (fixationPoint != null)
            {
                fixationPoint.transform.LookAt(leftEyeCamera.transform);
                fixationPoint.transform.Rotate(0, 180, 0);
            }
        }

        // Debug input for testing in editor
#if UNITY_EDITOR
        if (Input.inputString.Length > 0)
        {
            char inputChar = Input.inputString[0];
            if (char.IsLetter(inputChar))
            {
                string letter = inputChar.ToString().ToUpper();
                Debug.Log("Editor input detected: " + letter);
                OnSpeechResult(letter);
            }
        }
#endif
    }

    public void StartTestSequence()
    {
        if (!isDisplaying)
        {
            Debug.Log("STARTING TEST SEQUENCE ");

            if (!voiceSystemInitialized)
            {
                Debug.LogError("Voice system not initialized!");
                return;
            }

            // Update button state FIRST
            if (startButton != null)
            {
                startButton.interactable = false;
                // Try both Text and TextMeshProUGUI components
                Text buttonText = startButton.GetComponentInChildren<Text>();
                TextMeshProUGUI buttonTextTMP = startButton.GetComponentInChildren<TextMeshProUGUI>();

                if (buttonText != null)
                {
                    buttonText.text = "Running...";
                    Debug.Log("Updated Text component");
                }
                if (buttonTextTMP != null)
                {
                    buttonTextTMP.text = "Running...";
                    Debug.Log("Updated TextMeshProUGUI component");
                }

                // Also try hiding the button completely
                startButton.gameObject.SetActive(false);
                Debug.Log("Button hidden");
            }

            // Clear any existing letters first
            ClearExistingLetters();

            // Display the chart
            DisplayChart();

            // Reset index
            currentIndex = 0;

            // Start the letter sequence
            StartCoroutine(ShowLettersOneByOne());

            // Start voice recognition
            StartVoiceRecognition();

            Debug.Log("TEST SEQUENCE STARTED ");
        }
        else
        {
            Debug.Log("Test sequence already running");
        }
    }

    void StartVoiceRecognition()
    {
        Debug.Log("START VOICE RECOGNITION ");

#if UNITY_ANDROID && !UNITY_EDITOR
        StartListening();
#else
        Debug.Log("Voice recognition mock started (Editor mode - use keyboard input)");
#endif

        Debug.Log("VOICE RECOGNITION STARTED ");
    }

    void SetupStereoCameras()
    {
        Camera mainCamera = Camera.main ?? FindObjectOfType<Camera>() ?? new GameObject("Main Camera").AddComponent<Camera>();
        mainCamera.tag = "MainCamera";

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
        if (rightEyeCamera != null)
            ConfigureCamera(rightEyeCamera);
    }

    void ConfigureCamera(Camera cam)
    {
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 90f;
        cam.backgroundColor = Color.black;
    }

    void CreateFixationPoint()
    {
        if (fixationPoint != null)
        {
            Destroy(fixationPoint);
        }

        fixationPoint = new GameObject("FixationPoint");
        TextMeshPro textMesh = fixationPoint.AddComponent<TextMeshPro>();
        textMesh.text = fixationSymbol;
        textMesh.fontSize = fixationSize * 100;
        textMesh.color = letterColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;

        fixationPoint.transform.position = fixedPosition + new Vector3(0, 0, distanceFromPlayer);
        if (leftEyeCamera != null)
        {
            fixationPoint.transform.LookAt(leftEyeCamera.transform);
            fixationPoint.transform.Rotate(0, 180, 0);
        }

        Debug.Log($"Fixation point created at position: {fixationPoint.transform.position}");
    }

    void SetupPeripheralChart()
    {
        peripheralLetters.Clear();
        string[] chartLetters = { "E", "F", "P", "T", "O", "Z", "L", "D", "C" };
        CreatePeripheralRing(10f, 8, chartLetters);
        CreatePeripheralRing(15f, 12, chartLetters);
        CreatePeripheralRing(20f, 16, chartLetters);

        Debug.Log($"Created {peripheralLetters.Count} peripheral letters");
    }

    void CreatePeripheralRing(float eccentricity, int count, string[] letters)
    {
        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep;
            peripheralLetters.Add(new PeripheralLetterData
            {
                letter = letters[UnityEngine.Random.Range(0, letters.Length)],
                eccentricity = eccentricity,
                meridian = angle,
                fontSize = peripheralFontSize,
                isVisible = true
            });
        }
    }

    void DisplayChart()
    {
        ClearExistingLetters();

        Debug.Log($"About to display {peripheralLetters.Count} letters");

        foreach (var data in peripheralLetters)
        {
            if (data.isVisible)
            {
                GameObject letterObj = CreatePeripheralLetterObject(data);
                instantiatedLetters.Add(letterObj);
                Debug.Log($"Created letter {data.letter} at position {letterObj.transform.position}");
            }
        }

        Debug.Log($"Actually displayed {instantiatedLetters.Count} letters");

        // Verify letters are in scene
        foreach (var letter in instantiatedLetters)
        {
            if (letter != null)
            {
                Debug.Log($"Letter {letter.name} exists in scene, active: {letter.activeSelf}");
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

        // Calculate position using corrected formula
        Vector3 worldPos = PolarToWorldPosition(data.eccentricity, data.meridian);
        obj.transform.position = worldPos;

        Debug.Log($"Letter {data.letter}");

        if (leftEyeCamera != null)
        {
            obj.transform.LookAt(leftEyeCamera.transform);
            obj.transform.Rotate(0, 180, 0);
        }

        if (randomizeSize)
        {
            float randomScale = UnityEngine.Random.Range(minSize, maxSize);
            obj.transform.localScale = Vector3.one * randomScale;
        }

        // Start with letter VISIBLE for debugging
        obj.SetActive(true);

        return obj;
    }

    Vector3 PolarToWorldPosition(float ecc, float mer)
    {
        // Convert to radians
        float eccRad = ecc * Mathf.Deg2Rad;
        float merRad = mer * Mathf.Deg2Rad;

        // Calculate position in spherical coordinates
        // Using standard spherical coordinate system where:
        // - ecc is the polar angle (from positive Z axis)
        // - mer is the azimuthal angle (rotation around Y axis)

        float x = distanceFromPlayer * Mathf.Sin(eccRad) * Mathf.Cos(merRad);
        float y = distanceFromPlayer * Mathf.Sin(eccRad) * Mathf.Sin(merRad);
        float z = distanceFromPlayer * Mathf.Cos(eccRad);

        Vector3 relativePos = new Vector3(x, y, z);
        Vector3 worldPos = fixedPosition + relativePos;

        Debug.Log($"Polar ({ecc}°, {mer}°) -> Relative ({relativePos}) -> World ({worldPos})");

        return worldPos;
    }

    IEnumerator ShowLettersOneByOne()
    {
        isDisplaying = true;
        Debug.Log($"Starting ShowLettersOneByOne coroutine with {instantiatedLetters.Count} letters");

        if (instantiatedLetters.Count == 0)
        {
            Debug.LogError("No letters to display!");
            isDisplaying = false;
            yield break;
        }

        while (isDisplaying && instantiatedLetters.Count > 0)
        {
            // Hide all letters first
            foreach (var letter in instantiatedLetters)
            {
                if (letter != null)
                    letter.SetActive(false);
            }

            // Show current letter
            if (currentIndex < instantiatedLetters.Count && instantiatedLetters[currentIndex] != null)
            {
                currentlyDisplayedLetter = GetCurrentLetter();
                instantiatedLetters[currentIndex].SetActive(true);

                Debug.Log($"Displaying letter #{currentIndex}: {currentlyDisplayedLetter}");
                LogCurrentDisplayAndSpoken();
            }
            else
            {
                Debug.LogError($"Invalid currentIndex {currentIndex} or null letter object");
            }

            yield return new WaitForSeconds(displayInterval);

            // Move to next letter
            currentIndex = (currentIndex + 1) % instantiatedLetters.Count;

            yield return new WaitForSeconds(intervalBetweenLetters);
        }

        Debug.Log("ShowLettersOneByOne coroutine ended");
    }

    string GetCurrentLetter()
    {
        if (currentIndex < instantiatedLetters.Count && instantiatedLetters[currentIndex] != null)
        {
            string[] parts = instantiatedLetters[currentIndex].name.Split('_');
            return parts.Length >= 2 ? parts[1] : "";
        }
        return "";
    }

    void ClearExistingLetters()
    {
        Debug.Log($"Clearing {instantiatedLetters.Count} existing letters");
        foreach (var letter in instantiatedLetters)
            if (letter != null) Destroy(letter);
        instantiatedLetters.Clear();
    }

    public void StopTestSequence()
    {
        Debug.Log("STOPPING TEST SEQUENCE ");

        isDisplaying = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (voiceBridge != null && isListening)
        {
            try
            {
                voiceBridge.Call("stopListening");
                isListening = false;
                Debug.Log("Voice recognition stopped");
            }
            catch (Exception e)
            {
                Debug.LogError("Error stopping voice recognition: " + e.Message);
            }
        }
#endif

        // Show button again
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.interactable = true;

            Text buttonText = startButton.GetComponentInChildren<Text>();
            TextMeshProUGUI buttonTextTMP = startButton.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null) buttonText.text = "Start Test";
            if (buttonTextTMP != null) buttonTextTMP.text = "Start Test";
        }

        Debug.Log("TEST SEQUENCE STOPPED ");
    }

    #region --- Voice Recognition Methods ---

#if UNITY_ANDROID && !UNITY_EDITOR
    void InitializeVoiceBridge()
    {
        Debug.Log("INITIALIZING VOICE BRIDGE ");
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            voiceBridge = new AndroidJavaObject("com.unity3d.player.VoiceBridge", unityActivity, gameObject.name);
            Debug.Log("VoiceBridge initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError("VoiceBridge initialization failed: " + e.Message);
            Debug.LogError("Stack trace: " + e.StackTrace);
        }
        Debug.Log("VOICE BRIDGE INIT COMPLETE ");
    }

    void StartListening()
    {
        Debug.Log("START LISTENING CALLED ");
        
        // Check microphone permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("Requesting microphone permission...");
            Permission.RequestUserPermission(Permission.Microphone);
            
            // Wait for permission and try again
            StartCoroutine(WaitForPermissionAndStart());
            return;
        }

        if (!isListening && voiceBridge != null)
        {
            try
            {
                voiceBridge.Call("startListening");
                isListening = true;
                Debug.Log("Voice recognition listening started successfully");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to start listening: " + e.Message);
                Debug.LogError("Stack trace: " + e.StackTrace);
            }
        }
        else
        {
            if (isListening)
                Debug.LogWarning("Already listening");
            if (voiceBridge == null)
                Debug.LogError("VoiceBridge is null!");
        }
        
        Debug.Log("END START LISTENING ");
    }

    IEnumerator WaitForPermissionAndStart()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("Microphone permission granted, starting listening...");
            StartListening();
        }
        else
        {
            Debug.LogError("Microphone permission denied or timeout");
        }
    }
#endif

    void CreateFilePath()
    {
        try
        {
            string fileName = "VoiceRecognition_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            filePath = Path.Combine(Application.persistentDataPath, fileName);

            Debug.Log("FILE PATH CREATED ");
            Debug.Log("File name: " + fileName);
            Debug.Log("Full file path: " + filePath);
            Debug.Log("Persistent data path: " + Application.persistentDataPath);

            // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log("Created directory: " + directory);
            }

            // Test write to ensure file is accessible
            File.WriteAllText(filePath, "Voice Recognition Log Started at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + Environment.NewLine);
            Debug.Log("Test file write successful!");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create file path: " + e.Message);
            Debug.LogError("Stack trace: " + e.StackTrace);
        }
    }

    // This method is called by the Android voice recognition system
    [UnityEngine.Scripting.Preserve]
    public void OnSpeechResult(string result)
    {
        Debug.Log("SPEECH RESULT START ");
        Debug.Log("OnSpeechResult() called with: '" + result + "'");
        Debug.Log("Currently displayed letter: '" + currentlyDisplayedLetter + "'");

        if (string.IsNullOrEmpty(result))
        {
            Debug.LogWarning(" Speech result is empty or null");
            return;
        }

        lastSpokenLetter = result.Trim().ToUpper();
        LogCurrentDisplayAndSpoken();
        Debug.Log("Processed spoken letter: '" + lastSpokenLetter + "'");

        // Use coroutine for thread-safe file operations
        StartCoroutine(SaveToFileCoroutine(lastSpokenLetter, currentlyDisplayedLetter));

#if UNITY_ANDROID && !UNITY_EDITOR
        isListening = false;
        Debug.Log("Setting isListening to false, will restart in 1 second");
        
        // Restart listening after a brief delay
        Invoke(nameof(StartListening), 1f);
#endif

        Debug.Log("SPEECH RESULT END ");
    }

    IEnumerator SaveToFileCoroutine(string recognizedText, string displayedLetter)
    {
        yield return null; // Wait one frame to ensure we're on main thread

        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timestamp}] Spoken: '{recognizedText}' | Displayed: '{displayedLetter}'";

            bool isMatch = string.Equals(displayedLetter, recognizedText.Trim(), StringComparison.OrdinalIgnoreCase);
            entry += $" | Match: {isMatch}";

            // Write to file
            File.AppendAllText(filePath, entry + Environment.NewLine);

            Debug.Log("SPEECH LOG ");
            Debug.Log(entry);
            Debug.Log("File written to: " + filePath);
            Debug.Log("END LOG ");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save spoken text to file: " + e.Message);
            Debug.LogError("Stack trace: " + e.StackTrace);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    [UnityEngine.Scripting.Preserve]
    public void OnSpeechError(string error)
    {
        Debug.LogError("Speech error: " + error);
        
        isListening = false;
        
        // Try to restart listening after error
        Debug.Log("Attempting to restart listening after error...");
        Invoke(nameof(StartListening), 2f);
        
        Debug.Log("END SPEECH ERROR ");
    }
#endif

    void LogCurrentDisplayAndSpoken()
    {
        Debug.Log($"Displayed letter: '{currentlyDisplayedLetter}' | Last spoken letter: '{lastSpokenLetter}'");
    }

    void OnDestroy()
    {
        Debug.Log("VRVoiceChartManager OnDestroy ");

        isDisplaying = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (voiceBridge != null)
        {
            try
            {
                voiceBridge.Call("destroy");
                Debug.Log("VoiceBridge destroyed");
            }
            catch (Exception e)
            {
                Debug.LogError("Error destroying VoiceBridge: " + e.Message);
            }
        }
#endif

        Debug.Log("VRVoiceChartManager Destroyed ");
    }

    // Add this method for debugging
    [ContextMenu("Debug Letter Positions")]
    void DebugLetterPositions()
    {
        Debug.Log("=== DEBUG LETTER POSITIONS ===");
        Debug.Log($"Camera position: {(leftEyeCamera != null ? leftEyeCamera.transform.position.ToString() : "null")}");
        Debug.Log($"Fixed position: {fixedPosition}");
        Debug.Log($"Distance from player: {distanceFromPlayer}");

        for (int i = 0; i < instantiatedLetters.Count; i++)
        {
            if (instantiatedLetters[i] != null)
            {
                Vector3 pos = instantiatedLetters[i].transform.position;
                bool active = instantiatedLetters[i].activeSelf;
                Debug.Log($"Letter {i}: {instantiatedLetters[i].name} at {pos}, active: {active}");

                // Calculate distance from camera
                if (leftEyeCamera != null)
                {
                    float distance = Vector3.Distance(pos, leftEyeCamera.transform.position);
                    Debug.Log($"  Distance from camera: {distance}");
                }
            }
        }
        Debug.Log("=== END DEBUG ===");
    }

    #endregion
}