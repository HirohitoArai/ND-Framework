using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class FpsCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;
    [Range(0.01f, 1)]
    public float FPS_Frequency = 0.2f;

    [Range(0, 10)]
    public float warmupTime;

    private int frameCount;
    private float elapsedTime;
    private float currentFps;

    private List<float> fpsHistory = new List<float>();
    private List<float> frameTimes = new List<float>();
    private float highestFps = 0f;
    private float lowestFps = float.MaxValue;
    private float averageFps = 0f;
    private bool isRecording = false;
    private float startTime;

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
                averageFps = total / fpsHistory.Count;
            }

            if (fpsText != null)
            {
                if (Time.unscaledTime - startTime <= warmupTime)
                {
                    fpsText.text = $"Warming up... ({warmupTime - (Time.unscaledTime - startTime):F1}s)";
                }
                else
                {
                    fpsText.text =
                        $"FPS:     {currentFps:F1}\n" +
                        $"Avg:     {averageFps:F1}\n"+
                        $"Highest: {highestFps:F1}\n"+
                        $"Lowest : {lowestFps:F1}";
                }
            }

            frameCount = 0;
            elapsedTime = 0;
        }

        if (isRecording)
        {
            frameTimes.Add(Time.unscaledDeltaTime * 1000);
        }
    }

}