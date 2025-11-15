using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Force.Crc32; 

public static class PlexParser
{
    // Data Type Size Definitions
    private const int SIZE_FLOAT = 4;
    private const int SIZE_DOUBLE = 8;
    private const int SIZE_UINT = 4;
    private const int SIZE_ULONG = 8;

    #region Write メソッド (書き込み)

    public static void Write(string filePath, PlexData data)
    {
        // The PLEX specification uses little endian as the standard
        using (var stream = File.Open(filePath, FileMode.Create))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, false))
        {
            // Global header (8 bytes)
            writer.Write(Encoding.ASCII.GetBytes("PLEX")); 
            
            // Version Major=1, Minor=0 (uint16_t x 2)
            ushort majorVersion = 1;
            ushort minorVersion = 0;
            writer.Write(majorVersion); 
            writer.Write(minorVersion); 

            // --- Chunk Write Processing ---
            // Chunk data must be temporarily stored in memory and CRC-32 calculated.
            
            //META chunk
            WriteMetaChunk(writer, data);

            //FACE chunk
            WriteFaceChunk(writer, data);

            //VERT/NORM chunks (depending on precision)
            if (data.Precision == PlexPrecision.Double)
            {
                WriteVectorChunk(writer, "VERD", data.Vertices, data.Dimension, SIZE_DOUBLE);
                WriteVectorChunk(writer, "NORMD", data.Facets.ConvertAll(f => f.Normal), data.Dimension, SIZE_DOUBLE);
                WriteVectorChunk(writer, "CENTD", data.Facets.ConvertAll(f => f.Center), data.Dimension, SIZE_DOUBLE, true);
            }
            else
            {
                WriteVectorChunk(writer, "VERT", data.Vertices, data.Dimension, SIZE_FLOAT);
                WriteVectorChunk(writer, "NORM", data.Facets.ConvertAll(f => f.Normal), data.Dimension, SIZE_FLOAT);
                WriteVectorChunk(writer, "CENT", data.Facets.ConvertAll(f => f.Center), data.Dimension, SIZE_FLOAT, true);
            }
        }
    }

    //Writing META chunks (40 bytes fixed + variable length)
    private static void WriteMetaChunk(BinaryWriter writer, PlexData data)
    {
        // Prepare a byte array of the software name
        byte[] softwareNameBytes = Encoding.UTF8.GetBytes(data.SoftwareName ?? "");
        uint lSoftware = (uint)softwareNameBytes.Length;

        // LChunk = 40 (fixed part) + LSoftware (variable part)
        ulong lChunk = 40 + lSoftware;

        // Chunk Header
        writer.Write(Encoding.ASCII.GetBytes("META")); // Chunk Type
        writer.Write(lChunk);                          // Data length (ulong, 8 bytes)

        // Write the fixed data portion (40 bytes) to temporary memory
        using (var ms = new MemoryStream(40 + softwareNameBytes.Length))
        using (var bw = new BinaryWriter(ms))
        {
            //Number of dimensions (N) (4B)
            bw.Write(data.Dimension);
            //Padding 1 (4B)
            bw.Write((uint)0);
            //Number of vertices (V) (8B)
            bw.Write((ulong)data.Vertices.Count);
            //Creation date (8B)
            bw.Write(data.CreationTime);
            //Last updated date and time (8B)
            bw.Write(data.ModifiedTime);
            //Data Type Flags (1B)
            bw.Write((byte)(data.Precision == PlexPrecision.Double ? 1 : 0));
            //Padding 2 (3B)
            bw.Write((byte)0); bw.Write((ushort)0);
            //Creation software manager (4B)
            bw.Write(lSoftware);

            // Variable data part
            bw.Write(softwareNameBytes);

            // CRC-32 calculation and export
            byte[] chunkData = ms.ToArray();
            uint crc32 = Crc32Algorithm.Compute(chunkData); //Calculate CRC with the library
            writer.Write(chunkData); // Write the data body to a file
            writer.Write(crc32);
        }
    }

    //Exporting FACE chunks
    private static void WriteFaceChunk(BinaryWriter writer, PlexData data)
    {
        ulong facetCount = (ulong)data.Facets.Count;
        int N = data.Dimension;

        // LChunk = 8 (F) + (F * N * 4 (uint))
        ulong lChunk = SIZE_ULONG + (facetCount * (ulong)N * SIZE_UINT);

        // Chunk Header
        writer.Write(Encoding.ASCII.GetBytes("FACE"));
        writer.Write(lChunk); // データ長 (ulong, 8バイト)

        // Write the data body to temporary memory
        using (var ms = new MemoryStream((int)lChunk))
        using (var bw = new BinaryWriter(ms))
        {
            // Number of Facets (F) (8B)
            bw.Write(facetCount);
            
            //Index List
            foreach (var facet in data.Facets)
            {
                foreach (var index in facet.Indices)
                {
                    bw.Write((uint)index); // PLEX specificationsではuint
                }
            }

            // CRC-32 calculation and export
            byte[] chunkData = ms.ToArray();
            uint crc32 = Crc32Algorithm.Compute(chunkData);
            writer.Write(chunkData);
            writer.Write(crc32);
        }
    }

    //VERT/NORMExporting Chunks
    private static void WriteVectorChunk(BinaryWriter writer, string chunkType, List<PlexVector> vectors, int dimension, int elementSize, bool isOptional = false)
    {
        if (isOptional && (vectors == null || vectors.Count == 0))
        {
            return;
        }

        // LChunk = V * N * Size(DataType)
        ulong lChunk = (ulong)vectors.Count * (ulong)dimension * (ulong)elementSize;

        // Chunk Header
        writer.Write(Encoding.ASCII.GetBytes(chunkType));
        writer.Write(lChunk); // データ長 (ulong, 8バイト)

        // Write the data body to temporary memory
        using (var ms = new MemoryStream((int)lChunk))
        using (var bw = new BinaryWriter(ms))
        {
            // Coordinate/Normal List
            bool isDouble = elementSize == SIZE_DOUBLE;
            foreach (var vec in vectors)
            {
                for (int i = 0; i < dimension; i++)
                {
                    if (isDouble)
                        bw.Write((double)vec.Coordinates[i]);
                    else
                        bw.Write((float)vec.Coordinates[i]);
                }
            }

            // CRC-32 calculation and export
            byte[] chunkData = ms.ToArray();
            uint crc32 = Crc32Algorithm.Compute(chunkData);
            writer.Write(chunkData);
            writer.Write(crc32);
        }
    }

    #endregion

    #region Read (読み込み)

    public static PlexData Read(string filePath)
    {
        var data = new PlexData();
    
        using (var stream = File.Open(filePath, FileMode.Open))
        using (var reader = new BinaryReader(stream, Encoding.ASCII, false))
        {
            //Global Header Validation
            if (new string(reader.ReadChars(4)) != "PLEX") 
                throw new Exception("Invalid PLEX file format.");
        
            ushort majorVersion = reader.ReadUInt16();
            if (majorVersion != 1) 
                throw new Exception($"Unsupported PLEX version: {majorVersion}");
        
            reader.ReadUInt16(); // Skip Minor Version

            //Continue reading chunks until end of file
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                //Safely read the entire chunk, including the chunk header and CRC
                if (reader.BaseStream.Length - reader.BaseStream.Position < 12) break; // If it is less than the header size, end

                string chunkType = new string(reader.ReadChars(4));
                ulong chunkLength = reader.ReadUInt64();

                // Check if the data exceeds the file size
                if (reader.BaseStream.Length - reader.BaseStream.Position < (long)chunkLength + 4)
                    throw new Exception($"Incomplete chunk '{chunkType}' found.");

                //Data loading and CRC verification
                byte[] chunkData = reader.ReadBytes((int)chunkLength);
                uint fileCrc = reader.ReadUInt32();

                uint calculatedCrc = Crc32Algorithm.Compute(chunkData);
                if (fileCrc != calculatedCrc)
                {
                    // If the CRC does not match, choose whether to issue a warning or a strict error.
                    Console.WriteLine($"Warning: CRC mismatch for chunk '{chunkType}'. Data may be corrupt.");
                    // throw new Exception($"CRC mismatch for chunk '{chunkType}'."); 
                }

                // Parsing Validated Data
                ParseChunk(data, chunkType, chunkData);
            }
        }
        return data;
    }

    //The main chunk data parser to be newly added
    private static void ParseChunk(PlexData data, string chunkType, byte[] chunkData)
    {
        using (var ms = new MemoryStream(chunkData))
        using (var reader = new BinaryReader(ms))
        {
            switch (chunkType)
            {
                case "META":
                    data.Dimension = reader.ReadInt32();
                    reader.ReadInt32(); // Padding
                    data.VertexCount = reader.ReadUInt64();
                    data.CreationTime = reader.ReadUInt64();
                    data.ModifiedTime = reader.ReadUInt64();
                    data.Precision = (reader.ReadByte() == 1) ? PlexPrecision.Double : PlexPrecision.Single;
                    reader.ReadBytes(3); // Padding
                    uint lSoftware = reader.ReadUInt32();
                    data.SoftwareName = Encoding.UTF8.GetString(reader.ReadBytes((int)lSoftware));

                    // Prepare a saucer
                    for (ulong i = 0; i < data.VertexCount; i++) 
                        data.Vertices.Add(new PlexVector(data.Dimension));
                    break;
            
                case "FACE":
                    ulong facetCount = reader.ReadUInt64();
                    // Prepare a saucer
                    for (ulong i = 0; i < facetCount; i++) 
                        data.Facets.Add(new PlexFacet(data.Dimension));
                
                    for (ulong i = 0; i < facetCount; i++)
                        for (int j = 0; j < data.Dimension; j++)
                            data.Facets[(int)i].Indices[j] = reader.ReadUInt32();
                    break;

                case "VERT":
                case "VERD":
                    bool isDoubleV = chunkType.EndsWith("D");

                    int vertexSizeV = data.Dimension * (isDoubleV ? 8 : 4);
                    if (vertexSizeV == 0) break; // If the dimension is unknown, do nothing.
                    int countV = chunkData.Length / vertexSizeV;
                    while (data.Vertices.Count < countV)
                    {
                        data.Vertices.Add(new PlexVector(data.Dimension));
                    }

                    foreach (var vertex in data.Vertices)
                        for (int j = 0; j < data.Dimension; j++)
                            vertex.Coordinates[j] = isDoubleV ? reader.ReadDouble() : reader.ReadSingle();
                    break;

                case "NORM":
                case "NORMD":
                    bool isDoubleN = chunkType.EndsWith("D");

                    int facetSizeN = data.Dimension * (isDoubleN ? 8 : 4);
                    if (facetSizeN == 0) break;
                    int countN = chunkData.Length / facetSizeN;
                    while (data.Facets.Count < countN)
                    {
                        data.Facets.Add(new PlexFacet(data.Dimension));
                    }

                    foreach (var facet in data.Facets)
                    { 
                        if (facet.Normal == null) facet.Normal = new PlexVector(data.Dimension);
                        for (int j = 0; j < data.Dimension; j++)
                            facet.Normal.Coordinates[j] = isDoubleN ? reader.ReadDouble() : reader.ReadSingle();
                    }
                    break;
            
                case "CENT":
                case "CENTD":
                    bool isDoubleC = chunkType.EndsWith("D");

                    int facetSizeC = data.Dimension * (isDoubleC ? 8 : 4);
                    if (facetSizeC == 0) break;
                
                    int countC = chunkData.Length / facetSizeC;
                    while (data.Facets.Count < countC)
                    {
                        data.Facets.Add(new PlexFacet(data.Dimension));
                    }

                    foreach (var facet in data.Facets)
                    {
                        // Center may be optional, so add a null check
                        if (facet.Center == null) facet.Center = new PlexVector(data.Dimension);
                        for (int j = 0; j < data.Dimension; j++)
                            facet.Center.Coordinates[j] = isDoubleC ? reader.ReadDouble() : reader.ReadSingle();
                    }
                    break;

                default:
                    // If you receive it, it's a logic error.
                    break;
            }
        }
    }

    #endregion
}