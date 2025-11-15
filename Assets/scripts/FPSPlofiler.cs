using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

public class FpsPlofiler : MonoBehaviour
{
    [Range(0.01f, 1)]
    public float FPS_Frequency = 0.2f;

    [Range(1, 60)]
    public float recordDuration;
    public float warmupTime;

    private int frameCount;
    private float elapsedTime;
    private float currentFps;

    private List<float> fpsHistory = new List<float>();
    private List<float> frameTimes = new List<float>();
    private float highestFps = 0f;
    private float lowestFps = float.MaxValue;
    private bool isRecording = false;
    private float startTime;

    void Start()
    {
        if (recordDuration <= 0)
        {
            Debug.LogWarning("Recording will not occur because Record Duration is set to 0 or less.");
            return;
        }
        StartCoroutine(RecordingSequence());
    }

    void Update()
    {
        frameCount++;
        elapsedTime += Time.unscaledDeltaTime;

        if (elapsedTime >= FPS_Frequency)
        {
            currentFps = frameCount / elapsedTime;
            if (Time.unscaledTime - startTime > warmupTime)
            {
                if (currentFps > highestFps)
                    highestFps = currentFps;
                if (currentFps < lowestFps)
                    lowestFps = currentFps;

                fpsHistory.Add(currentFps);
                float total = 0f;
                foreach (float f in fpsHistory)
                    total += f;
            }

            frameCount = 0;
            elapsedTime = 0;
        }

        if (isRecording)
        {
            frameTimes.Add(Time.unscaledDeltaTime * 1000);
        }
    }

    private IEnumerator RecordingSequence()
    {
        Debug.Log($"Starting the warm-up...({warmupTime}s)");
        yield return new WaitForSeconds(warmupTime);

        Debug.Log($"Starting recording...({recordDuration}s)");
        isRecording = true;
        
        highestFps = 0f;
        lowestFps = float.MaxValue;
        fpsHistory.Clear();
        frameTimes.Clear();

        yield return new WaitForSeconds(recordDuration);

        isRecording = false;
        Debug.Log("End recording and save the results...");
        SaveResults();
    }

    private void SaveResults()
    {
        if (frameTimes.Count == 0) return;
        float avgFps = 1000 / frameTimes.Average();
        float minFps = 1000 / frameTimes.Max();
        float maxFps = 1000 / frameTimes.Min();

        frameTimes.Sort();
        int ninetyNinePercentIndex = Mathf.FloorToInt(frameTimes.Count * 0.99f);
        float ninetyNinePercentileFrameTime = frameTimes[ninetyNinePercentIndex];

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- Performance Measurement Results ---");
        sb.AppendLine($"Timestamp: {System.DateTime.Now}");
        sb.AppendLine($"Duration: {recordDuration:F1}s");
        sb.AppendLine($"Total Frames Recorded: {frameTimes.Count}");
        sb.AppendLine($"Average FPS: {avgFps:F2}");
        sb.AppendLine($"Minimum FPS: {minFps:F2}"); 
        sb.AppendLine($"99th Percentile Frame Time: {ninetyNinePercentileFrameTime:F2} ms");
        sb.AppendLine("---------------------------------");

        string customSavePath = "Assets/FPS_Logs/";
        if (!Directory.Exists(customSavePath))
        Directory.CreateDirectory(customSavePath);

        string path = Path.Combine(customSavePath, $"FPS_Log_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Results saved to: {path}");
    }
}
