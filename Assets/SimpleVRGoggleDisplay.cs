using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimpleVRGoggleDisplay : MonoBehaviour
{
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

    private Quaternion baseRotation = Quaternion.identity;
    private bool gyroEnabled = false;

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
        SetupStereoCameras();
        SetupPeripheralChart();
        DisplayChart();
        StartCoroutine(InitializeAndStartSequence());

        // Enable gyro
        gyroEnabled = SystemInfo.supportsGyroscope;
        if (gyroEnabled)
        {
            Input.gyro.enabled = true;
            baseRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    void Update()
    {
        if (gyroEnabled)
        {
            Quaternion deviceRotation = Input.gyro.attitude;
            // Convert to Unity coordinate system
            Quaternion correctedRotation = baseRotation * new Quaternion(-deviceRotation.x, -deviceRotation.y, deviceRotation.z, deviceRotation.w);

            // Apply to cameras
            if (leftEyeCamera != null)
                leftEyeCamera.transform.localRotation = correctedRotation;

            if (rightEyeCamera != null)
                rightEyeCamera.transform.localRotation = correctedRotation;
        }
    }


    void SetupStereoCameras()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
        if (mainCamera == null)
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

    void SetupPeripheralChart()
    {
        peripheralLetters.Clear();
        string[] chartLetters = { "E", "F", "P", "T", "O", "Z", "L", "D", "C" };
        CreatePeripheralRing(10f, 8, chartLetters);
        CreatePeripheralRing(15f, 12, chartLetters);
        CreatePeripheralRing(20f, 16, chartLetters);
    }

    void CreatePeripheralRing(float eccentricity, int letterCount, string[] letters)
    {
        float angleStep = 360f / letterCount;
        for (int i = 0; i < letterCount; i++)
        {
            float meridian = i * angleStep;
            string letter = letters[Random.Range(0, letters.Length)];

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
            float randomSize = Random.Range(minSize, maxSize);
            obj.transform.localScale = Vector3.one * randomSize;
        }

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

    IEnumerator InitializeAndStartSequence()
    {
        yield return new WaitForSeconds(0.5f);
        if (instantiatedLetters.Count == 0)
        {
            Debug.LogWarning("No letters found.");
            yield break;
        }
        StartCoroutine(ShowLettersOneByOne());
    }

    IEnumerator ShowLettersOneByOne()
    {
        isDisplaying = true;
        while (isDisplaying)
        {
            foreach (var letter in instantiatedLetters)
                letter.SetActive(false);

            if (currentIndex < instantiatedLetters.Count)
            {
                instantiatedLetters[currentIndex].SetActive(true);
                Debug.Log($"🔠 Showing letter: {GetCurrentLetter()}");
            }

            yield return new WaitForSeconds(displayInterval);
            currentIndex = (currentIndex + 1) % instantiatedLetters.Count;
            yield return new WaitForSeconds(intervalBetweenLetters);
        }
    }

    void ClearExistingLetters()
    {
        foreach (GameObject letter in instantiatedLetters)
            if (letter != null)
                Destroy(letter);
        instantiatedLetters.Clear();
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
}
