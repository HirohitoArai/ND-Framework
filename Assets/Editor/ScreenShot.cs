using UnityEditor;
using UnityEngine;
using System.IO;

public class ScreenShot
{
    [MenuItem("Tools/Capture Screenshot %#c")] // Ctrl+Shift+C
    static void Capture()
    {
        string customSavePath = "Assets/ScreenShot/";
        if (!Directory.Exists(customSavePath))
        Directory.CreateDirectory(customSavePath);

        string filename = $"capture_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(customSavePath, filename);
        // superSize‚ğã‚°‚é‚Æ‚‰ğ‘œ“x‚É‚È‚é (—á: 2=2”{, 4=4”{)
        ScreenCapture.CaptureScreenshot(fullPath, superSize: 4);

        Debug.Log($"Screenshot saved: {fullPath}");
    }
}

