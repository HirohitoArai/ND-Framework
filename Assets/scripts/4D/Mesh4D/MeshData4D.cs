using Geometry4D;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "NewConvexData", menuName = "4D/Convex Mesh Data")]
public class MeshData4D : ScriptableObject
{
    public Vector4[] Vertices;
    public FacetInfo4D[] SurfaceFaces;
}
