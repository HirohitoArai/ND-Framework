using System;
using UnityEngine;

namespace Geometry4D
{
    [System.Serializable]
    public struct FacetInfo4D
    {
        public FacetIdx4D FacetIdx; //Vertex index
        public Vector4 FacetNormal; //Facet Normals
        public Vector4 FacetCenter; //Facet centroid
        public FacetInfo4D(FacetIdx4D indexes, Vector4 facetnormal, Vector4 facetcenter)
        {
            FacetIdx = indexes;
            FacetNormal = facetnormal;
            FacetCenter = facetcenter;
        }
    }
    public class FacetData4D
    {
        public Vector4[] FacetVert; //Vertex coordinates
        public Vector4 FacetNormal; //Facet Normals
        public Vector4 FacetCenter; //Facet centroid

        public FacetData4D(Vector4[] vertices, Vector4 facetnormal)
        {
            FacetVert = vertices;
            FacetNormal = facetnormal;
            FacetCenter = CalculateCenter(vertices);
        }
        private static Vector4 CalculateCenter(Vector4[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return Vector4.zero; 
            var sum = Vector4.zero;
            foreach (var vert in vertices)
            {
                sum += vert;
            }
            return sum * 0.25f;
        }
    }
    public struct RidgeIdx4D
    {
        public int V0;
        public int V1;
        public int V2;

        public RidgeIdx4D(int v0, int v1, int v2)
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

    [System.Serializable]
    public struct FacetIdx4D
    {
        public int V0;
        public int V1;
        public int V2;
        public int V3;

        public FacetIdx4D(int v0, int v1, int v2, int v3)
        {
            Span<int> verts = stackalloc int[4] { v0, v1, v2, v3 };
            SortSmallStack(3, verts);
            V0 = verts[0];
            V1 = verts[1];
            V2 = verts[2];
            V3 = verts[3];
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
    public struct Simplex4D
    {
        public int V0;
        public int V1;
        public int V2;
        public int V3;
        public int V4;

        public Simplex4D(int v0, int v1, int v2, int v3, int v4)
        {
            V0 = v0; 
            V1 = v1; 
            V2 = v2; 
            V3 = v3; 
            V4 = v4;
        }
    }
    public class BooleanFacetInfo4D
    {
        public BooleanFacetIdx4D FacetIdx;
        public Vector4 FacetNormal;
        public Vector4 FacetCenter;
        public BooleanFacetInfo4D() { }
        public BooleanFacetInfo4D(BooleanFacetIdx4D indexes, Vector4 facetnormal, Vector4 facetcenter)
        {
            FacetIdx = indexes;
            FacetNormal = facetnormal;
            FacetCenter = facetcenter;
        }
    }
    public struct BooleanRidgeIdx4D
    {
        public int V0;
        public int V1;

        public BooleanRidgeIdx4D(int v0, int v1)
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
    public struct BooleanFacetIdx4D
    {
        public int V0;
        public int V1;
        public int V2;

        public BooleanFacetIdx4D(int v0, int v1, int v2)
        {
            int[] verts = { v0, v1, v2 };
            Array.Sort(verts);
            V0 = verts[0];
            V1 = verts[1];
            V2 = verts[2];
        }
    }
    public struct BooleanSimplex4D
    {
        public int V0;
        public int V1;
        public int V2;
        public int V3;

        public BooleanSimplex4D(int v0, int v1, int v2, int v3)
        {
            V0 = v0; 
            V1 = v1; 
            V2 = v2; 
            V3 = v3;
        }
    }
    [Serializable]
    struct Vector4Int : IEquatable<Vector4Int>
    {
        public int x;
        public int y;
        public int z;
        public int w;

        public Vector4Int(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        public override int GetHashCode(){return HashCode.Combine(x, y, z, w);}
        public override bool Equals(object obj){return obj is Vector4Int other && Equals(other);}
        public bool Equals(Vector4Int other){return x == other.x && y == other.y && z == other.z && w == other.w;}
        public static bool operator ==(Vector4Int lhs, Vector4Int rhs){return lhs.Equals(rhs);}
        public static bool operator !=(Vector4Int lhs, Vector4Int rhs){return !(lhs == rhs);}
    }
    public struct AABB4D
    {
        public Vector4 Max;
        public Vector4 Min;
        public AABB4D(Vector4 max, Vector4 min)
        { 
            Max = max; 
            Min = min;
        }
    }
    public struct Sphere4D
    {
        public Vector4 Center;
        public float Radius;
        public Sphere4D(Vector4 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }
    public struct Tetrahedron
    {
        public Vector4 V0;
        public Vector4 V1;
        public Vector4 V2;
        public Vector4 V3;

        public Vector4 Normal;

        public int Idx0;
        public int Idx1;
        public int Idx2;
        public int Idx3;

        public Tetrahedron(Vector4 v0, Vector4 v1, Vector4 v2, Vector4 v3, Vector4 normal,int idx0, int idx1, int idx2, int idx3)
        {
            V0 = v0; 
            V1 = v1; 
            V2 = v2; 
            V3 = v3;

            Normal = normal;

            Idx0 = idx0; 
            Idx1 = idx1; 
            Idx2 = idx2; 
            Idx3 = idx3;
        }
    }

    public class CollisionConstraint4D
    {
        public PhysicsBody4D Body;      // The body to which this constraint should be applied
        public int ParticleIndex;      // Which particle of that body
        public Vector4 Normal;          // The direction to be extruded
        public float Depth;             // Depth of the indentation
        public Tetrahedron Tetrahedron;
        public CollisionConstraint4D(PhysicsBody4D body, int idx, Vector4 n, float d,Tetrahedron tetrahedron)
        {
            Body = body; 
            ParticleIndex = idx; 
            Normal = n; 
            Depth = d;
            Tetrahedron = tetrahedron;
        }
    }
}