using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mesh;
using UnityEngine.UIElements;
using static UnityEngine.Networking.UnityWebRequest;
using static UnityEngine.Random;
using System.Runtime.InteropServices.WindowsRuntime;

public class Slicer : MonoBehaviour
{
    public LayerMask collisionMask;

    [SerializeField]
    GameObject slicedObjectParent;

    [SerializeField]
    float sliceForce = 2;

    [SerializeField]
    float rotationSpeed = 0;

    MeshCollider planeCollider;

    Vector3 planeHalfExtents = Vector3.zero;

    bool isVisible = false;

    public delegate void SliceEvent();
    public SliceEvent onSlice;

    [SerializeField]
    Material slicedMaterial;

    // Start is called before the first frame update
    void Start()
    {
        planeCollider = GetComponent<MeshCollider>();

        planeHalfExtents = planeCollider.bounds.extents;

        planeCollider.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(isVisible)
        {
            if (Input.GetMouseButtonDown(0)) // 0 is left mouse button
            {
                Slice();
            }

            RotatePlane();
        }
    }

    private void OnDrawGizmos()
    {
        //if (planeCollider == null)
        //    return;
        //
        //Gizmos.color = Color.blue;
        //
        //Matrix4x4 oldMatrix = Gizmos.matrix;
        //
        //Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, planeHalfExtents * 2);
        //Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        //
        //Gizmos.matrix = oldMatrix;
    }

    public void SetVisibility(bool isVisible)
    {
        this.isVisible = isVisible;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.enabled = isVisible;
    }

    private void RotatePlane()
    {
        float inversedDeltaX = Input.GetAxis("Mouse X") * -1f;

        transform.Rotate(0, 0, rotationSpeed * inversedDeltaX * Time.deltaTime);
    }

    void Slice()
    {
        Collider[] hits = Physics.OverlapBox(transform.position, planeHalfExtents, transform.rotation, collisionMask);

        Plane cutPlane = new Plane();

        MeshData positiveMeshData = new MeshData();
        MeshData negativeMeshData = new MeshData();

        List<Vector3> intersectingPoints = new List<Vector3>();

        for (int i = 0; i < hits.Length; ++i)
        {
            positiveMeshData.Clear();
            negativeMeshData.Clear();
            intersectingPoints.Clear();

            GameObject hitObj = hits[i].gameObject;

            Vector3 localPlaneNormal = hitObj.transform.InverseTransformDirection(transform.up);
            Vector3 localPlanePosition = hitObj.transform.InverseTransformPoint(transform.position);
            
            cutPlane.SetNormalAndPosition(localPlaneNormal, localPlanePosition);

            Mesh originalMesh = hitObj.GetComponent<MeshFilter>().mesh;

            SliceMesh(originalMesh, positiveMeshData, negativeMeshData, cutPlane, intersectingPoints);
            FillSlicedArea(originalMesh, intersectingPoints, cutPlane, positiveMeshData, negativeMeshData);

            CreateGameObjectUsingMeshData(positiveMeshData, hitObj.gameObject, cutPlane.normal * sliceForce);
            CreateGameObjectUsingMeshData(negativeMeshData, hitObj.gameObject, -cutPlane.normal * sliceForce);

            Destroy(hitObj);
        }

        onSlice?.Invoke();
    }

    void FillSlicedArea(Mesh originalMesh, List<Vector3> intersectingVerts, Plane cutPlane, MeshData positiveMeshData, MeshData negativeMeshData)
    {
        Vector3 center = Vector3.zero;
        foreach(Vector3 v in intersectingVerts)
        {
            center += v;
        }
        center /= intersectingVerts.Count;

        for (int i = 0; i < intersectingVerts.Count - 1; i += 2)
        {
            Vector3[] vertices = new Vector3[] { intersectingVerts[i], intersectingVerts[(i + 1) % intersectingVerts.Count], center };
            Vector3[] flippedNormals = new Vector3[] { -cutPlane.normal, -cutPlane.normal, -cutPlane.normal };

            // Arbitrary uv coordinates since the material for the sliced area we plan to use is a solid color.
            Vector2[] uvs = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f) }; 

            TriangleData positiveTriangle = new TriangleData(vertices, flippedNormals, uvs, originalMesh.subMeshCount);

            FlipInvertedTriangle(positiveTriangle);
            positiveMeshData.AddTriangleData(positiveTriangle);

            Vector3[] normals = new Vector3[] { cutPlane.normal, cutPlane.normal, cutPlane.normal };

            TriangleData negativeTriangle = new TriangleData(vertices, normals, uvs, originalMesh.subMeshCount);

            FlipInvertedTriangle(negativeTriangle);
            negativeMeshData.AddTriangleData(negativeTriangle);
        }
    }

    void SliceMesh(Mesh originalMesh, MeshData positiveMeshData, MeshData negativeMeshData, Plane cutPlane, List<Vector3> intersectingVerts)
    {
        IList<bool> vertSides = null;
        for (int subIndex = 0; subIndex < originalMesh.subMeshCount; ++subIndex)
        {
            int[] subMeshtriangles = originalMesh.GetTriangles(subIndex);
            for(int i = 0; i < subMeshtriangles.Length; i += 3)
            {
                int triIndex1 = subMeshtriangles[i];
                int triIndex2 = subMeshtriangles[i + 1];
                int triIndex3 = subMeshtriangles[i + 2];

                TriangleData triangle = TriangleData.GetTriangleData(originalMesh, triIndex1, triIndex2, triIndex3, subIndex);

                vertSides = Array.AsReadOnly(new bool[]
                {
                    cutPlane.GetSide(triangle.vertices[0]),
                    cutPlane.GetSide(triangle.vertices[1]),
                    cutPlane.GetSide(triangle.vertices[2])
                });

                switch (vertSides[0])
                {
                    case true when vertSides[1] && vertSides[2]: // all vertices are on the positive side of the plane
                        {
                            positiveMeshData.AddTriangleData(triangle);
                            break;
                        }
                    case false when !vertSides[1] && !vertSides[2]: // all vertices are on the negative side of the plane
                        {
                            negativeMeshData.AddTriangleData(triangle);
                            break;
                        }
                    default:
                        {
                            SliceTriangle(cutPlane, triangle, vertSides, positiveMeshData, negativeMeshData, intersectingVerts);
                            break;
                        }
                }
            }
        }
    }

    void SliceTriangle(Plane cutPlane, TriangleData targetTriangle, IList<bool> vertSides, MeshData positiveMeshData, MeshData negativeMeshData, List<Vector3> intersectingVertices)
    {
        TriangleData positiveTriangle = new TriangleData();
        TriangleData negativeTriangle = new TriangleData();

        SortVerticesIntoTriangles(targetTriangle, vertSides, positiveTriangle, negativeTriangle);

        TriangleData largeTriangleHalf = (positiveTriangle.vertices.Count > negativeTriangle.vertices.Count) ? positiveTriangle : negativeTriangle;
        TriangleData smallTriangleHalf = (largeTriangleHalf == positiveTriangle) ? negativeTriangle : positiveTriangle;

        // Calculate intersecting vertice data

        Vector3 leftRayDir = (smallTriangleHalf.vertices[0] - largeTriangleHalf.vertices[0]);
        float leftHitDistance;
        cutPlane.Raycast(new Ray(largeTriangleHalf.vertices[0], leftRayDir.normalized), out leftHitDistance);

        float leftPercentage = leftHitDistance / leftRayDir.magnitude;

        Vector3 leftVert = Vector3.Lerp(largeTriangleHalf.vertices[0], smallTriangleHalf.vertices[0], leftPercentage);
        Vector3 leftNormal = Vector3.Lerp(largeTriangleHalf.normals[0], smallTriangleHalf.normals[0], leftPercentage);
        Vector2 leftUV = Vector2.Lerp(largeTriangleHalf.uvs[0], smallTriangleHalf.uvs[0], leftPercentage);
        intersectingVertices.Add(leftVert);

        float rightHitDistance;
        Vector3 rightRayDir = (smallTriangleHalf.vertices[0] - largeTriangleHalf.vertices[1]);
        cutPlane.Raycast(new Ray(largeTriangleHalf.vertices[1], rightRayDir.normalized), out rightHitDistance);

        float rightPercentage = rightHitDistance / rightRayDir.magnitude;

        Vector3 rightVert = Vector3.Lerp(largeTriangleHalf.vertices[1], smallTriangleHalf.vertices[0], rightPercentage);
        Vector3 rightNormal = Vector3.Lerp(largeTriangleHalf.normals[1], smallTriangleHalf.normals[0], rightPercentage);
        Vector2 rightUV = Vector2.Lerp(largeTriangleHalf.uvs[1], smallTriangleHalf.uvs[0], rightPercentage);
        intersectingVertices.Add(rightVert);

        //Create three triangles then sort them to the proper mesh
        Vector3[] currentVertices1 = { largeTriangleHalf.vertices[0], leftVert, rightVert };
        Vector3[] currentnormals1 = { largeTriangleHalf.normals[0], leftNormal, rightNormal };
        Vector2[] currentUVs1 = { largeTriangleHalf.uvs[0], leftUV, rightUV };

        TriangleData triangle1 = new TriangleData(currentVertices1, currentnormals1, currentUVs1, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(triangle1);

        Vector3[] currentVertices2 = new Vector3[] { largeTriangleHalf.vertices[0], largeTriangleHalf.vertices[1], rightVert };
        Vector3[] currentnormals2 = new Vector3[] { largeTriangleHalf.normals[0], largeTriangleHalf.normals[1], rightNormal };
        Vector2[] currentUVs2 = new Vector2[] { largeTriangleHalf.uvs[0], largeTriangleHalf.uvs[1], rightUV };

        TriangleData triangle2 = new TriangleData(currentVertices2, currentnormals2, currentUVs2, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(triangle2);

        Vector3[] currentVertices3 = new Vector3[] { smallTriangleHalf.vertices[0], leftVert, rightVert };
        Vector3[] currentnormals3 = new Vector3[] { smallTriangleHalf.normals[0], leftNormal, rightNormal };
        Vector2[] currentUVs3 = new Vector2[] { smallTriangleHalf.uvs[0], leftUV, rightUV };

        TriangleData triangle3 = new TriangleData(currentVertices3, currentnormals3, currentUVs3, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(triangle3);

        if(largeTriangleHalf == positiveTriangle)
        {
            positiveMeshData.AddTriangleData(triangle1);
            positiveMeshData.AddTriangleData(triangle2);
            negativeMeshData.AddTriangleData(triangle3);
        }
        else
        {
            negativeMeshData.AddTriangleData(triangle1);
            negativeMeshData.AddTriangleData(triangle2);
            positiveMeshData.AddTriangleData(triangle3);
        }
    }

    void FlipInvertedTriangle(TriangleData triangle)
    {
        Vector3 ab = triangle.vertices[1] - triangle.vertices[0];
        Vector3 ac = triangle.vertices[2] - triangle.vertices[0];
        if (Vector3.Dot(Vector3.Cross(ab, ac), triangle.normals[0]) < 0)
        {
            FlipTriangle(triangle);
        }
    }

    void FlipTriangle(TriangleData triangle)
    {
        Vector3 tempVertice = triangle.vertices[2];
        triangle.vertices[2] = triangle.vertices[1];
        triangle.vertices[1] = tempVertice;

        Vector3 tempNormal = triangle.normals[2];
        triangle.normals[2] = triangle.normals[1];
        triangle.normals[1] = tempNormal;

        Vector2 tempUV = triangle.uvs[2];
        triangle.uvs[2] = triangle.uvs[1];
        triangle.uvs[1] = tempUV;
    }

    void SortVerticesIntoTriangles(TriangleData triangle, IList<bool> vertSides, TriangleData positiveTriangle, TriangleData negativeTriangle)
    {
        for(int i = 0; i < vertSides.Count; ++i)
        {
            if (vertSides[i])
            {
                positiveTriangle.vertices.Add(triangle.vertices[i]);
                positiveTriangle.normals.Add(triangle.normals[i]);
                positiveTriangle.uvs.Add(triangle.uvs[i]);
            }
            else
            {
                negativeTriangle.vertices.Add(triangle.vertices[i]);
                negativeTriangle.normals.Add(triangle.normals[i]);
                negativeTriangle.uvs.Add(triangle.uvs[i]);
            }
        }
    }

    void CreateGameObjectUsingMeshData(MeshData meshData, GameObject parentObject, Vector3 startForce)
    {
        GameObject go = new GameObject();

        go.transform.position = parentObject.transform.position;
        go.transform.localScale = parentObject.transform.localScale;
        go.transform.rotation = parentObject.transform.rotation;
        go.tag = parentObject.tag;
        go.name = parentObject.name;

        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshCollider>();
        go.AddComponent<Rigidbody>();

        Rigidbody rb = go.GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        rb.AddForce(startForce, ForceMode.Impulse);

        Mesh finishedMesh = meshData.GetGeneratedMesh();

        Material[] parentMaterials = parentObject.GetComponent<MeshRenderer>().materials;
        Material[] mats = new Material[finishedMesh.subMeshCount];

        // Size of parentMaterials correspond to the parents current amount of submeshes.
        for (int i = 0; i < parentMaterials.Length; ++i)
        {
            mats[i] = parentMaterials[i];
        }

        // Adding material to sliced area.
        mats[finishedMesh.subMeshCount - 1] = slicedMaterial;

        MeshCollider meshColl = go.GetComponent<MeshCollider>();
        meshColl.sharedMesh = finishedMesh;
        meshColl.convex = true;

        go.GetComponent<MeshRenderer>().materials = mats;
        go.GetComponent<MeshFilter>().mesh = finishedMesh;
        
        go.transform.SetParent(slicedObjectParent.transform);
    }
}
