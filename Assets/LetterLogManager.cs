using UnityEngine;
using System;
using System.IO;
using System.Text;

public class LetterLogManager : MonoBehaviour
{
    public static LetterLogManager Instance;
    private string logFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string fileName = "LetterLog_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            logFilePath = Path.Combine(Application.persistentDataPath, fileName);

            File.WriteAllText(logFilePath, "Timestamp,Displayed Letter,Spoken Letter\n");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LogDisplayedLetter(string letter)
    {
        string entry = $"{GetTimestamp()},{letter},\n";
        File.AppendAllText(logFilePath, entry);
    }

    public void LogSpokenLetter(string spokenLetter)
    {
        string[] lines = File.ReadAllLines(logFilePath);
        if (lines.Length <= 1)
            return;

        // Find the last line without a spoken letter
        for (int i = lines.Length - 1; i >= 1; i--)
        {
            string[] parts = lines[i].Split(',');
            if (parts.Length == 3 && string.IsNullOrEmpty(parts[2]))
            {
                lines[i] = $"{parts[0]},{parts[1]},{spokenLetter}";
                File.WriteAllLines(logFilePath, lines);
                return;
            }
        }
    }

    public string GetLogContent()
    {
        return File.ReadAllText(logFilePath);
    }

    public string GetLogFilePath()
    {
        return logFilePath;
    }

    public string GetTimestamp()
    {
        return DateTime.Now.ToString("HH:mm:ss.fff");
    }

    // Used by UnitySendMessage from Android
    public void OnSpeechRecognized(string spokenText)
    {
        LogSpokenLetter(spokenText);
        Debug.Log($"🗣️ Received spoken letter: {spokenText}");
    }

    public void OnRequestLog()
    {
        Debug.Log("📜 Log Content:\n" + GetLogContent());
    }
}
