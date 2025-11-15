using System;
using System.Collections.Generic;

// Enumeration for indicating data precision
public enum PlexPrecision
{
    Single, // Single Precision (float)
    Double  // double precision (double)
}

// A class that represents an N-dimensional vector
public class PlexVector
{
    // Always store as double and convert precision when saving
    public double[] Coordinates; 
    
    public PlexVector(int dimension) 
    {
        // Consider the unusual case where the dimension becomes negative, and make it safe.
        if (dimension < 0) dimension = 0;
        Coordinates = new double[dimension];
    }
}

// A class that represents one facet
public class PlexFacet
{
    // Changed to uint because the specification uses uint (4 bytes)
    public uint[] Indices; 
    
    public PlexVector Normal;
    public PlexVector Center; // Optional

    public PlexFacet(int dimension)
    {
        if (dimension < 0) dimension = 0;
        Indices = new uint[dimension];
    }
}

public class PlexData
{
    //Fields in the META chunk (fixed length 40 bytes)
    public int Dimension;
    public ulong VertexCount; // The total number of vertices to read from the META chunk
    public ulong CreationTime; // Unix Time Stamp
    public ulong ModifiedTime; // Unix Time Stamp
    public PlexPrecision Precision = PlexPrecision.Single; // Support for data type flags
    public string SoftwareName; // Variable length data following LSoftware

    // MeshData
    public List<PlexVector> Vertices = new List<PlexVector>();
    public List<PlexFacet> Facets = new List<PlexFacet>();

    public PlexData()
    {
        // Set initial value at creation time
        CreationTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ModifiedTime = CreationTime;
        SoftwareName = "PlexParser C# Generator";
    }
}
