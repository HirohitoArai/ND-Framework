using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public static class BenchmarkRunner
{
    private const int NUMBER_OF_EXECUTIONS = 100;
    private const int NUMBER_OF_VERTICES = 1000;
    private const float GENERATION_RADIUS = 10.0f;

    [MenuItem("Tools/Benchmarks/Run 3DConvex Volume Hull Benchmark")]
    public static void RunBenchmark3D()
    {
        var results = new List<double>();
        var stopwatch = new Stopwatch();
        var convexBuilder = new BuildConvexMesh3D();
        var facetCounts = new List<int>();

        UnityEngine.Debug.Log($"--- Starting Benchmark ---");
        UnityEngine.Debug.Log($"Executions: {NUMBER_OF_EXECUTIONS}, Vertices per run: {NUMBER_OF_VERTICES}");

        var testDataSets = new List<Vector3[]>();
        for (int i = 0; i < NUMBER_OF_EXECUTIONS; i++)
        {
            testDataSets.Add(GenerateRandomVertices3D(NUMBER_OF_VERTICES, GENERATION_RADIUS));
        }

        convexBuilder.GenerateConvex(testDataSets[0]);

        for (int i = 0; i < NUMBER_OF_EXECUTIONS; i++)
        {
            var vertices = testDataSets[i];

            Profiler.BeginSample("GenerateConvex - Iteration");
        
            stopwatch.Reset();
            stopwatch.Start();
            var generatedFacets = convexBuilder.GenerateConvex(vertices);
            stopwatch.Stop();
        
            Profiler.EndSample();

            results.Add(stopwatch.Elapsed.TotalMilliseconds);
            facetCounts.Add(generatedFacets.Length);
        }
        Profiler.EndSample();

       PrintResults(results, facetCounts);
    }

    [MenuItem("Tools/Benchmarks/Run 4DConvex Volume Hull Benchmark")]
    public static void RunBenchmark4D()
    {
        var results = new List<double>();
        var stopwatch = new Stopwatch();
        var convexBuilder = new BuildConvexMesh4D();
        var facetCounts = new List<int>();

        UnityEngine.Debug.Log($"--- Starting Benchmark ---");
        UnityEngine.Debug.Log($"Executions: {NUMBER_OF_EXECUTIONS}, Vertices per run: {NUMBER_OF_VERTICES}");

        var testDataSets = new List<Vector4[]>();
        for (int i = 0; i < NUMBER_OF_EXECUTIONS; i++)
        {
            testDataSets.Add(GenerateRandomVertices4D(NUMBER_OF_VERTICES, GENERATION_RADIUS));
        }

        convexBuilder.GenerateConvex(testDataSets[0]);

        Profiler.BeginSample("Convex Hull Benchmark - Main Loop");
        for (int i = 0; i < NUMBER_OF_EXECUTIONS; i++)
        {
            var vertices = testDataSets[i];

            Profiler.BeginSample("GenerateConvex - Iteration");
        
            stopwatch.Reset();
            stopwatch.Start();
            var generatedFacets = convexBuilder.GenerateConvex(vertices);
            stopwatch.Stop();
        
            Profiler.EndSample();

            results.Add(stopwatch.Elapsed.TotalMilliseconds);
            facetCounts.Add(generatedFacets.Length);
        }
        Profiler.EndSample();

        PrintResults(results, facetCounts);
    }
    private static Vector3[] GenerateRandomVertices3D(int count, float radius)
    {
        var vertices = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = Random.insideUnitSphere * radius;
        }
        return vertices;
    }
    private static Vector4[] GenerateRandomVertices4D(int count, float radius)
    {
        var vertices = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            var v = new Vector4(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            );
            vertices[i] = v.normalized * radius * Random.value;
        }
        return vertices;
    }
    private static void PrintResults(List<double> results, List<int> facetCounts)
    {
        double averageTime = results.Average();
        double minTime = results.Min();
        double maxTime = results.Max();
        double sumOfSquares = results.Select(val => (val - averageTime) * (val - averageTime)).Sum();
        double stdDev = Mathf.Sqrt((float)(sumOfSquares / results.Count));
        
        UnityEngine.Debug.Log($"Total Executions: {results.Count}");
        UnityEngine.Debug.Log($"Average Time: {averageTime:F4} ms");
        //UnityEngine.Debug.Log($"Min Time: {minTime:F4} ms");
        //UnityEngine.Debug.Log($"Max Time: {maxTime:F4} ms");
        UnityEngine.Debug.Log($"Standard Deviation: {stdDev:F4} ms");

        if (facetCounts != null && facetCounts.Count > 0)
        {

            double averageFacetCount = facetCounts.Average();
            UnityEngine.Debug.Log($"Average Facet Count: {averageFacetCount:F2}");

            if (averageFacetCount > 0)
            {
                double timePerFacetMicroseconds = (averageTime * 1000) / averageFacetCount;
                UnityEngine.Debug.Log($"Time per Facet: {timePerFacetMicroseconds:F4} µs/facet");
            }
        }

        //UnityEngine.Debug.Log($"--- Internal Profiling ---");
        //UnityEngine.Debug.Log($"Avg Active Facet Count during ProcessPoint: {avgActiveFacetCount:F2}");
        //UnityEngine.Debug.Log($"Avg Time per ProcessPoint Call: {avgTimePerProcessPointCall:F6} ms");
        //UnityEngine.Debug.Log($"  - FindVisibleFaces: {avgTimePerFindVisibleCall:F6} ms ({findVisiblePercentage:F1}%)");
        //UnityEngine.Debug.Log($"  - FindHorizonRidges: {avgTimePerFindHorizonCall:F6} ms ({findHorizonPercentage:F1}%)");
        //UnityEngine.Debug.Log($"  - Other: {otherProcessPointTime:F6} ms ({otherPercentage:F1}%)");
    }
}