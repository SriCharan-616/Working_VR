using UnityEngine;
using System.IO;

public class FileLocationChecker : MonoBehaviour
{
    void Start()
    {
        // Show file paths for debugging
        Debug.Log("=== FILE LOCATIONS ===");
        Debug.Log("persistentDataPath: " + Application.persistentDataPath);
        Debug.Log("dataPath: " + Application.dataPath);
        Debug.Log("streamingAssetsPath: " + Application.streamingAssetsPath);
        Debug.Log("temporaryCachePath: " + Application.temporaryCachePath);

        // Create a test file to verify location
        string testFile = Path.Combine(Application.persistentDataPath, "test.txt");
        File.WriteAllText(testFile, "Test file created at: " + System.DateTime.Now);
        Debug.Log("Test file created at: " + testFile);

        // Check if file exists
        if (File.Exists(testFile))
        {
            Debug.Log("✅ File successfully created and accessible");
            Debug.Log("File content: " + File.ReadAllText(testFile));
        }
        else
        {
            Debug.Log("❌ File creation failed");
        }
    }
}