using System;
using UnityEngine;

namespace Geometry3D
{
    [System.Serializable]
    public struct FacetInfo3D
    {
        public FacetIdx3D FacetIdx; //Vertex index
        public Vector3 FacetNormal; //Facet Normals
        public Vector3 FacetCenter; //Facet centroid
        public FacetInfo3D(FacetIdx3D indexes, Vector3 facetnormal, Vector3 facetcenter)
        {
            FacetIdx = indexes;
            FacetNormal = facetnormal;
            FacetCenter = facetcenter;
        }
    }
    public class FacetData3D
    {
        public Vector3[] FacetVert; //Vertex coordinates
        public Vector3 FacetNormal; //Facet Normals
        public Vector3 FacetCenter; //Facet centroid

        public FacetData3D(Vector3[] vertices, Vector3 facetnormal)
        {
            FacetVert = vertices;
            FacetNormal = facetnormal;
            FacetCenter = CalculateCenter(vertices);
        }
        private static Vector3 CalculateCenter(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return Vector3.zero; 
            var sum = Vector3.zero;
            foreach (var vert in vertices)
            {
                sum += vert;
            }
            return sum * 0.3333333f;
        }
    }
    public struct RidgeIdx3D
    {
        public int V0;
        public int V1;

        public RidgeIdx3D(int v0, int v1)
        {
            if (v0 < v1)
            {
                V0 = v0;
                V1 = v1;
            }
            else
            {
                V0 = v1;
                V1 = v0;
            }
        }
    }

    [System.Serializable]
    public struct FacetIdx3D
    {
        public int V0;
        public int V1;
        public int V2;

        public FacetIdx3D(int v0, int v1, int v2)
        {
            Span<int> verts = stackalloc int[3] { v0, v1, v2 };
            SortSmallStack(3, verts);
            V0 = verts[0];
            V1 = verts[1];
            V2 = verts[2];
        }
        public static void SortSmallStack(int n, Span<int> data)
        {
            for (int i = 1; i < n; i++)
            {
                int key = data[i];
                int j = i - 1;
                while (j >= 0 && data[j] > key)
                {
                    data[j + 1] = data[j];
                    j--;
                }
                data[j + 1] = key;
            }
        }
    }

    public struct Simplex3D
    {
        public int V0;
        public int V1;
        public int V2;
        public int V3;

        public Simplex3D(int v0, int v1, int v2, int v3)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
        }
    }

    public struct AABB3D
    {
        public Vector3 Max;
        public Vector3 Min;
        public AABB3D(Vector3 max, Vector3 min)
        {
            Max = max;
            Min = min;
        }
    }
    public struct Sphere3D
    {
        public Vector3 Center;
        public float Radius;
        public Sphere3D(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }
    public struct Triangle
    {
        public Vector3 V0;
        public Vector3 V1;
        public Vector3 V2;

        public Vector3 Normal;

        public int Idx0;
        public int Idx1;
        public int Idx2;
        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2,Vector3 normal,int idx0, int idx1, int idx2)
        {
            V0 = v0; 
            V1 = v1; 
            V2 = v2;

            Normal = normal;

            Idx0 = idx0; 
            Idx1 = idx1; 
            Idx2 = idx2;
        }
    }
    public class CollisionConstraint3D
    {
        public PhysicsBody3D Body;      // The body to which this constraint should be applied
        public int ParticleIndex;      // Which particle of that body
        public Vector3 Normal;          // The direction to be extruded
        public float Depth;             // Depth of the indentation
        public Triangle Triangle;
        public CollisionConstraint3D(PhysicsBody3D body, int idx, Vector3 n, float d,Triangle triangle)
        {
            Body = body; 
            ParticleIndex = idx; 
            Normal = n; 
            Depth = d;
            Triangle = triangle;
        }
    }
}
