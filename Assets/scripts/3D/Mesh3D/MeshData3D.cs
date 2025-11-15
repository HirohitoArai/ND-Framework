using Geometry3D;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "NewConvexData3D", menuName = "3D/Convex Mesh Data")]
public class MeshData3D : ScriptableObject
{
    public Vector3[] Vertices;
    public FacetInfo3D[] SurfaceFaces;
}
