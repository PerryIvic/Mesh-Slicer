using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mesh;
using UnityEngine.UIElements;
using static UnityEngine.Networking.UnityWebRequest;
using static UnityEngine.Random;
using System.Runtime.InteropServices.WindowsRuntime;

/*
 * 1. Find intersection Positions
 * 2. Find Center of Intersection
 * 3. Create Two Meshes
 * 4. Use Plane normal to determine which side to create
 * 5. Determine which vertices are on the same side as the plane normal
 * 6. Using intersection points, rebind all vertices indices while ignoring 
 * the vertices on the other half of the plane. 
 * 7.  Repeat process to other side.
 * 8. Instantiate two new gameobjects with each mesh and colliders.
 * 9. Delete current mesh.
 * 10. Slice Complete!
 */

// TODO create component that holds material that will be used for the new sliced surface

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

        for(int i = 0; i < hits.Length; ++i)
        {
            GameObject hitObj = hits[i].gameObject;

            Vector3 localPlaneNormal = hitObj.transform.InverseTransformDirection(transform.up);
            Vector3 localPlanePosition = hitObj.transform.InverseTransformPoint(transform.position);

            Plane cutPlane = new Plane();
            cutPlane.SetNormalAndPosition(localPlaneNormal, localPlanePosition);

            MeshData positiveMeshData = new MeshData();
            MeshData negativeMeshData = new MeshData();
            List<Vector3> intersectingPoints = new List<Vector3>();

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
        //if(intersectingVerts.Count <= 1)
        //{
        //    Debug.LogWarning("No intersection points registered");
        //    return;
        //}

        Vector3 center = Vector3.zero;
        foreach(Vector3 v in intersectingVerts)
        {
            center += v;
        }
        center /= intersectingVerts.Count;

        for (int i = 0; i < intersectingVerts.Count - 1; i += 2)
        {
            Vector3[] vertices = new Vector3[] { intersectingVerts[i], intersectingVerts[(i + 1) % intersectingVerts.Count], center };
            Vector3[] normals = new Vector3[] { -cutPlane.normal, -cutPlane.normal, -cutPlane.normal };
            Vector2[] uvs = new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero };

            TriangleData currentTriangle = new TriangleData(vertices, normals, uvs, originalMesh.subMeshCount);

            FlipInvertedTriangle(currentTriangle);
            positiveMeshData.AddTriangleData(currentTriangle);

            normals = new Vector3[] { cutPlane.normal, cutPlane.normal, cutPlane.normal };

            currentTriangle = new TriangleData(vertices, normals, uvs, originalMesh.subMeshCount);

            FlipInvertedTriangle(currentTriangle);
            negativeMeshData.AddTriangleData(currentTriangle);
        }
    }

    void SliceMesh(Mesh originalMesh, MeshData positiveMeshData, MeshData negativeMeshData, Plane cutPlane, List<Vector3> intersectingVerts)
    {
        for(int subIndex = 0; subIndex < originalMesh.subMeshCount; ++subIndex)
        {
            int[] subMeshtriangles = originalMesh.GetTriangles(subIndex);
            for(int i = 0; i < subMeshtriangles.Length; i += 3)
            {
                int triIndex1 = subMeshtriangles[i];
                int triIndex2 = subMeshtriangles[i + 1];
                int triIndex3 = subMeshtriangles[i + 2];

                TriangleData triangle = TriangleData.GetTriangleData(originalMesh, triIndex1, triIndex2, triIndex3, i);

                bool[] vertSides =
                {
                    cutPlane.GetSide(triangle.vertices[0]),
                    cutPlane.GetSide(triangle.vertices[1]),
                    cutPlane.GetSide(triangle.vertices[2])
                };

                switch(vertSides[0])
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

    void FillSlicedAreaDepricated(Mesh originalMesh, List<Vector3> intersectingVerts, Plane cutPlane, MeshData positiveMeshData, MeshData negativeMeshData)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> polygon = new List<Vector3>();

        for(int i = 0; i < intersectingVerts.Count; ++i)
        {
            if (!vertices.Contains(intersectingVerts[i]))
            {
                polygon.Clear();
                polygon.Add(intersectingVerts[i]);
                polygon.Add(intersectingVerts[i + 1]);

                vertices.Add(intersectingVerts[i]);
                vertices.Add(intersectingVerts[i + 1]);

                EvaluatePairsDeptricated(intersectingVerts, vertices, polygon);
                FillDeptricated(originalMesh, vertices, cutPlane, positiveMeshData, negativeMeshData);
            }
        }
    }

    void EvaluatePairsDeptricated(List<Vector3> intersectingVerts, List<Vector3> vertices, List<Vector3> polygon)
    {
        bool isDone = false;
        while (!isDone)
        {
            isDone = true;
            for (int i = 0; i < intersectingVerts.Count; i += 2)
            {
                if (intersectingVerts[i] == polygon[polygon.Count - 1] && !vertices.Contains(intersectingVerts[i + 1]))
                {
                    isDone = false;
                    polygon.Add(intersectingVerts[i + 1]);
                    vertices.Add(intersectingVerts[i + 1]);
                }
                else if (intersectingVerts[i + 1] == polygon[polygon.Count - 1] && !vertices.Contains(intersectingVerts[i]))
                {
                    isDone = false;
                    polygon.Add(intersectingVerts[i]);
                    vertices.Add(intersectingVerts[i]);
                }
            }
        }
    }

    void FillDeptricated(Mesh originalMesh, List<Vector3> polygon, Plane cutPlane, MeshData positiveMeshData, MeshData negativeMeshData)
    {
        Vector3 center = Vector3.zero;
        foreach (Vector3 v in polygon)
        {
            center += v;
        }
        center /= polygon.Count;

        Vector3 up = cutPlane.normal;
        Vector3 left = Vector3.Cross(cutPlane.normal, up);

        Vector3 displacement = Vector3.zero;
        Vector2 uv1 = Vector2.zero;
        Vector2 uv2 = Vector2.zero;

        for (int i = 0; i < polygon.Count; ++i)
        {
            displacement = polygon[i] - center;
            uv1 = new Vector2()
            {
                x = .5f + Vector3.Dot(displacement, left),
                y = .5f + Vector3.Dot(displacement, up)
            };

            displacement = polygon[(i + 1) % polygon.Count] - center;
            uv2 = new Vector2()
            {
                x = .5f + Vector3.Dot(displacement, left),
                y = .5f + Vector3.Dot(displacement, up)
            };

            Vector3[] vertices = { polygon[i], polygon[(i + 1) % polygon.Count], center };
            Vector3[] normals = { -cutPlane.normal, -cutPlane.normal, -cutPlane.normal };
            Vector2[] uvs = { uv1, uv2, new Vector2(0.5f, 0.5f) };

            TriangleData currentTriangle = new TriangleData(vertices, normals, uvs, originalMesh.subMeshCount + 1);

            if (Vector3.Dot(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]), normals[0]) < 0)
            {
                FlipTriangle(currentTriangle);
            }
            positiveMeshData.AddTriangleData(currentTriangle);

            normals = new Vector3[] { cutPlane.normal, cutPlane.normal, cutPlane.normal };
            currentTriangle = new TriangleData(vertices, normals, uvs, originalMesh.subMeshCount + 1);

            if (Vector3.Dot(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]), normals[0]) < 0)
            {
                FlipTriangle(currentTriangle);
            }
            negativeMeshData.AddTriangleData(currentTriangle);
        }
    }

    void SliceTriangle(Plane cutPlane, TriangleData targetTriangle, bool[] vertSides, MeshData positiveMeshData, MeshData negativeMeshData, List<Vector3> intersectingVertices)
    {
        TriangleData positiveTriangle = new TriangleData();
        TriangleData negativeTriangle = new TriangleData();

        SortVerticesIntoTrianglesClean(targetTriangle, vertSides, positiveTriangle, negativeTriangle);

        TriangleData largeTriangleHalf = (positiveTriangle.vertices.Count > negativeTriangle.vertices.Count) ? positiveTriangle : negativeTriangle;
        TriangleData smallTriangleHalf = (largeTriangleHalf == positiveTriangle) ? negativeTriangle : positiveTriangle;

        // Calculate intersecting vertice data
        float hitDistance = 0;
        Vector3 leftVert = GetVerticeRayCastPlane(cutPlane, largeTriangleHalf.vertices[0], smallTriangleHalf.vertices[0], out hitDistance);
        intersectingVertices.Add(leftVert);

        float percentage = hitDistance / (largeTriangleHalf.vertices[0] - smallTriangleHalf.vertices[0]).magnitude;

        Vector3 leftNormal = Vector3.Lerp(largeTriangleHalf.normals[0], smallTriangleHalf.normals[0], percentage);
        Vector2 leftUV = Vector2.Lerp(largeTriangleHalf.uvs[0], smallTriangleHalf.uvs[0], percentage);

        Vector3 rightVert = GetVerticeRayCastPlane(cutPlane, largeTriangleHalf.vertices[1], smallTriangleHalf.vertices[0], out hitDistance);
        intersectingVertices.Add(rightVert);

        percentage = hitDistance / (largeTriangleHalf.vertices[1] - smallTriangleHalf.vertices[0]).magnitude;

        Vector3 rightNormal = Vector3.Lerp(largeTriangleHalf.normals[1], smallTriangleHalf.normals[0], percentage);
        Vector2 rightUV = Vector2.Lerp(largeTriangleHalf.uvs[1], smallTriangleHalf.uvs[0], percentage);

        //Create three triangles then sort them to the proper mesh
        Vector3[] currentVertices = { largeTriangleHalf.vertices[0], leftVert, rightVert };
        Vector3[] currentnormals = { largeTriangleHalf.normals[0], leftNormal, rightNormal };
        Vector2[] currentUVs = { largeTriangleHalf.uvs[0], leftUV, rightUV };

        TriangleData currentTriangle = new TriangleData(currentVertices, currentnormals, currentUVs, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(currentTriangle);
        if (largeTriangleHalf == positiveTriangle) positiveMeshData.AddTriangleData(currentTriangle); else negativeMeshData.AddTriangleData(currentTriangle);

        currentVertices = new Vector3[] { largeTriangleHalf.vertices[0], largeTriangleHalf.vertices[1], rightVert };
        currentnormals = new Vector3[] { largeTriangleHalf.normals[0], largeTriangleHalf.normals[1], rightNormal };
        currentUVs = new Vector2[] { largeTriangleHalf.uvs[0], largeTriangleHalf.uvs[1], rightUV };

        currentTriangle = new TriangleData(currentVertices, currentnormals, currentUVs, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(currentTriangle);
        if (largeTriangleHalf == positiveTriangle) positiveMeshData.AddTriangleData(currentTriangle); else negativeMeshData.AddTriangleData(currentTriangle);

        currentVertices = new Vector3[] { smallTriangleHalf.vertices[0], leftVert, rightVert };
        currentnormals = new Vector3[] { smallTriangleHalf.normals[0], leftNormal, rightNormal };
        currentUVs = new Vector2[] { smallTriangleHalf.uvs[0], leftUV, rightUV };

        currentTriangle = new TriangleData(currentVertices, currentnormals, currentUVs, targetTriangle.subMeshIndex);
        FlipInvertedTriangle(currentTriangle);
        if (smallTriangleHalf == positiveTriangle) positiveMeshData.AddTriangleData(currentTriangle); else negativeMeshData.AddTriangleData(currentTriangle);

    }

    // Slices a triangle into three new triangles then sorts them to the proper mesh depending on what side of the plane the triangle is on
    void SliceTriangleDepricated(Plane cutPlane, TriangleData triangle, bool[] vertSides, MeshData positiveMeshData, MeshData negativeMeshData, List<Vector3> intersectingVertices)
    {
        TriangleData positiveTriangle = new TriangleData(new Vector3[2], new Vector3[2], new Vector2[2], triangle.subMeshIndex);
        TriangleData negativeTriangle = new TriangleData(new Vector3[2], new Vector3[2], new Vector2[2], triangle.subMeshIndex);

        SortVerticesIntoTriangles(triangle, vertSides, positiveTriangle, negativeTriangle);

        // Calculate left vertice intersecting triangle
        float hitDistance;
        Vector3 leftVert = GetVerticeRayCastPlane(cutPlane, positiveTriangle.vertices[0], negativeTriangle.vertices[0], out hitDistance);
        intersectingVertices.Add(leftVert);

        float percentage = hitDistance / (positiveTriangle.vertices[0] - negativeTriangle.vertices[0]).magnitude;
        Vector3 leftNormal = Vector3.Lerp(positiveTriangle.normals[0], negativeTriangle.normals[0], percentage);
        Vector2 leftUV = Vector2.Lerp(positiveTriangle.uvs[0], negativeTriangle.uvs[0], percentage);

        // Calculate right vertice intersecting triangle
        Vector3 rightVert = GetVerticeRayCastPlane(cutPlane, positiveTriangle.vertices[1], negativeTriangle.vertices[1], out hitDistance);
        intersectingVertices.Add(rightVert);

        percentage = hitDistance / (positiveTriangle.vertices[1] - negativeTriangle.vertices[1]).magnitude;
        Vector3 rightNormal = Vector3.Lerp(positiveTriangle.normals[1], negativeTriangle.normals[1], percentage);
        Vector2 rightUV = Vector2.Lerp(positiveTriangle.uvs[1], negativeTriangle.uvs[1], percentage);

        // Create then validate before adding first triangle
        Vector3[] currentVertices = { positiveTriangle.vertices[0], leftVert, rightVert };
        Vector3[] currentNormals = { positiveTriangle.normals[0], leftNormal, rightNormal };
        Vector2[] currentUVs = { positiveTriangle.uvs[0], leftUV, rightUV };

        TriangleData currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            positiveMeshData.AddTriangleData(currentTriangle);
        }

        // Create then validate before adding second triangle
        currentVertices = new Vector3[] { positiveTriangle.vertices[0], positiveTriangle.vertices[1], rightVert };
        currentNormals = new Vector3[] { positiveTriangle.normals[0], positiveTriangle.normals[1], rightNormal };
        currentUVs = new Vector2[] { positiveTriangle.uvs[0], positiveTriangle.uvs[1], rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            positiveMeshData.AddTriangleData(currentTriangle);
        }

        // Create then validate before adding third triangle
        currentVertices = new Vector3[] { negativeTriangle.vertices[0], leftVert, rightVert };
        currentNormals = new Vector3[] { negativeTriangle.normals[0], leftNormal, rightNormal };
        currentUVs = new Vector2[] { negativeTriangle.uvs[0], leftUV, rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            negativeMeshData.AddTriangleData(currentTriangle);
        }

        // Create then validate before adding fourth triangle
        currentVertices = new Vector3[] { negativeTriangle.vertices[0], negativeTriangle.vertices[1], rightVert };
        currentNormals = new Vector3[] { negativeTriangle.normals[0], negativeTriangle.normals[1], rightNormal };
        currentUVs = new Vector2[] { negativeTriangle.uvs[0], negativeTriangle.uvs[1], rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            negativeMeshData.AddTriangleData(currentTriangle);
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

    bool FlipValidTriangle(TriangleData triangle)
    {
        bool isValid = (triangle.vertices[0] != triangle.vertices[1] && triangle.vertices[0] != triangle.vertices[2]);
        if(isValid)
        {
            Vector3 ab = triangle.vertices[1] - triangle.vertices[0];
            Vector3 ac = triangle.vertices[2] - triangle.vertices[0];
            if(Vector3.Dot(Vector3.Cross(ab, ac), triangle.normals[0]) < 0)
            {
                FlipTriangle(triangle);
            }
        }

        return isValid;    
    }

    void FlipTriangle(TriangleData triangle)
    {
        Vector3 tempVertice = triangle.vertices[2];
        triangle.vertices[2] = triangle.vertices[0];
        triangle.vertices[0] = tempVertice;

        Vector3 tempNormal = triangle.normals[2];
        triangle.normals[2] = triangle.normals[0];
        triangle.normals[0] = tempNormal;

        Vector2 tempUV = triangle.uvs[2];
        triangle.uvs[2] = triangle.uvs[0];
        triangle.uvs[0] = tempUV;
    }

    Vector3 GetVerticeRayCastPlane(Plane cutPlane, Vector3 start, Vector3 end, out float hitDistance)
    {
        cutPlane.Raycast(new Ray(start, (end - start).normalized), out hitDistance);

        float percentage = hitDistance / (end - start).magnitude;
        return Vector3.Lerp(start, end, percentage);
    }

    void SortVerticesIntoTrianglesClean(TriangleData triangle, bool[] vertSides, TriangleData positiveTriangle, TriangleData negativeTriangle)
    {
        for(int i = 0; i < vertSides.Length; ++i)
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

    // Places vertices belonging to the same plane side into a seperate triangle
    void SortVerticesIntoTriangles(TriangleData triangle, bool[] vertSides, TriangleData positiveTriangle, TriangleData negativeTriangle)
    {
        bool isOnPositiveSide = false;
        bool isOnNegativeSide = false;
        for (int i = 0; i < 3; ++i)
        {
            if (vertSides[i])
            {
                if (!isOnPositiveSide)
                {
                    isOnPositiveSide = true;

                    positiveTriangle.vertices[0] = triangle.vertices[i];
                    positiveTriangle.vertices[1] = triangle.vertices[i];

                    positiveTriangle.normals[0] = triangle.normals[i];
                    positiveTriangle.normals[1] = triangle.normals[i];

                    positiveTriangle.uvs[0] = triangle.uvs[i];
                    positiveTriangle.uvs[1] = triangle.uvs[i];
                }
                else
                {
                    positiveTriangle.vertices[1] = triangle.vertices[i];
                    positiveTriangle.normals[1] = triangle.normals[i];
                    positiveTriangle.uvs[1] = triangle.uvs[i];
                }
            }
            else
            {
                if (!isOnNegativeSide)
                {
                    isOnNegativeSide = true;

                    negativeTriangle.vertices[0] = triangle.vertices[i];
                    negativeTriangle.vertices[1] = triangle.vertices[i];

                    negativeTriangle.normals[0] = triangle.normals[i];
                    negativeTriangle.normals[1] = triangle.normals[i];

                    negativeTriangle.uvs[0] = triangle.uvs[i];
                    negativeTriangle.uvs[1] = triangle.uvs[i];
                }
                else
                {
                    negativeTriangle.vertices[1] = triangle.vertices[i];
                    negativeTriangle.normals[1] = triangle.normals[i];
                    negativeTriangle.uvs[1] = triangle.uvs[i];
                }
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

        Material parentMaterial = parentObject.GetComponent<MeshRenderer>().material;
        Material[] mats = new Material[finishedMesh.subMeshCount];
        for (int i = 0; i < finishedMesh.subMeshCount; ++i)
        {
            mats[i] = parentMaterial;
        }

        MeshCollider meshColl = go.GetComponent<MeshCollider>();
        meshColl.sharedMesh = finishedMesh;
        meshColl.convex = true;

        go.GetComponent<MeshRenderer>().materials = mats;
        go.GetComponent<MeshFilter>().mesh = finishedMesh;
        //go.GetComponent<MeshRenderer>().material = parentMaterial;
        
        go.transform.SetParent(slicedObjectParent.transform);
    }
}
