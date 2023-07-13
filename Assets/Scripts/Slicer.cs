using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mesh;
using UnityEngine.UIElements;
using static UnityEngine.Networking.UnityWebRequest;
using static UnityEngine.Random;

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

public class Slicer : MonoBehaviour
{
    public LayerMask collisionMask;

    [SerializeField]
    float rotationSpeed = 0;

    Vector3 previousMousePosition;

    MeshCollider planeCollider;

    // Start is called before the first frame update
    void Start()
    {
        planeCollider = GetComponent<MeshCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0)) // 0 is left mouse button
        {
            Slice();
        }

        RotatePlane();
    }

    private void OnDrawGizmos()
    {
        if (planeCollider == null)
            return;
        //
        //Gizmos.color = Color.blue;
        //
        //Gizmos.DrawMesh(planeCollider.sharedMesh, transform.position, transform.rotation);
    }

    private void RotatePlane()
    {
        Vector3 inversedDelta = (Input.mousePosition - previousMousePosition) * -1f;
        previousMousePosition = Input.mousePosition;

        transform.Rotate(0,0, rotationSpeed * inversedDelta.x * Time.deltaTime);
    }

    void Slice()
    {
        string name = LayerMask.LayerToName(collisionMask);
        Collider[] hits = Physics.OverlapBox(transform.position, planeCollider.bounds.extents, transform.rotation, collisionMask);

        for(int i = 0; i < hits.Length; ++i)
        {
            GameObject hitObj = hits[i].gameObject;

            Vector3 localPlaneNormal = hitObj.transform.InverseTransformDirection(transform.up);
            Vector3 localPlanePosition = hitObj.transform.InverseTransformPoint(transform.position);

            Plane cutPlane = new Plane();
            cutPlane.SetNormalAndPosition(localPlaneNormal, localPlanePosition);

            MeshData positiveMeshData = new MeshData();
            MeshData negativeMeshData = new MeshData();
            List<Vector3> intersectingVerts = new List<Vector3>();

            Mesh originalMesh = hitObj.GetComponent<MeshFilter>().mesh;

            SliceMesh(originalMesh, positiveMeshData, negativeMeshData, cutPlane, intersectingVerts);
            FillSlicedArea(originalMesh, intersectingVerts, cutPlane, positiveMeshData, negativeMeshData);

            CreateGameObjectUsingMeshData(positiveMeshData, hitObj.gameObject);
            CreateGameObjectUsingMeshData(negativeMeshData, hitObj.gameObject);

            Destroy(hitObj);
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

    void FillSlicedArea(Mesh originalMesh, List<Vector3> intersectingVerts, Plane cutPlane, MeshData positiveMeshData, MeshData negativeMeshData)
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

                EvaluatePairs(intersectingVerts, vertices, polygon);
                Fill(originalMesh, vertices, cutPlane, positiveMeshData, negativeMeshData);
            }
        }
    }

    void SliceTriangle(Plane cutPlane, TriangleData triangle, bool[] vertSides, MeshData positiveMeshData, MeshData negativeMeshData, List<Vector3> intersectingVertices)
    {
        TriangleData positiveTriangle = new TriangleData(new Vector3[2], new Vector3[2], new Vector2[2], triangle.subMeshIndex);
        TriangleData negativeTriangle = new TriangleData(new Vector3[2], new Vector3[2], new Vector2[2], triangle.subMeshIndex);

        SortVerticesIntoTriangles(triangle, vertSides, positiveTriangle, negativeTriangle);

        // Calculate points intersecting triangle
        float hitDistance;
        Vector3 leftVert = GetVerticeRayCastPlane(cutPlane, positiveTriangle.vertices[0], negativeTriangle.vertices[0], out hitDistance);
        intersectingVertices.Add(leftVert);

        float percentage = hitDistance / (positiveTriangle.vertices[0] - negativeTriangle.vertices[0]).magnitude;
        Vector3 leftNormal = Vector3.Lerp(positiveTriangle.normals[0], negativeTriangle.normals[0], percentage);
        Vector2 leftUV = Vector2.Lerp(positiveTriangle.uvs[0], negativeTriangle.uvs[0], percentage);

        Vector3 rightVert = GetVerticeRayCastPlane(cutPlane, positiveTriangle.vertices[1], negativeTriangle.vertices[1], out hitDistance);
        intersectingVertices.Add(rightVert);

        percentage = hitDistance / (positiveTriangle.vertices[1] - negativeTriangle.vertices[1]).magnitude;
        Vector3 rightNormal = Vector3.Lerp(positiveTriangle.normals[1], negativeTriangle.normals[1], percentage);
        Vector2 rightUV = Vector2.Lerp(positiveTriangle.uvs[1], negativeTriangle.uvs[1], percentage);


        Vector3[] currentVertices = { positiveTriangle.vertices[0], leftVert, rightVert };
        Vector3[] currentNormals = { positiveTriangle.normals[0], leftNormal, rightNormal };
        Vector2[] currentUVs = { positiveTriangle.uvs[0], leftUV, rightUV };

        TriangleData currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);

        if (FlipValidTriangle(currentTriangle))
        {
            positiveMeshData.AddTriangleData(currentTriangle);
        }

        currentVertices = new Vector3[] { positiveTriangle.vertices[0], positiveTriangle.vertices[1], rightVert };
        currentNormals = new Vector3[] { positiveTriangle.normals[0], positiveTriangle.normals[1], rightNormal };
        currentUVs = new Vector2[] { positiveTriangle.uvs[0], positiveTriangle.uvs[1], rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            positiveMeshData.AddTriangleData(currentTriangle);
        }

        currentVertices = new Vector3[] { negativeTriangle.vertices[0], leftVert, rightVert };
        currentNormals = new Vector3[] { negativeTriangle.normals[0], leftNormal, rightNormal };
        currentUVs = new Vector2[] { negativeTriangle.uvs[0], leftUV, rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            negativeMeshData.AddTriangleData(currentTriangle);
        }

        currentVertices = new Vector3[] { negativeTriangle.vertices[0], negativeTriangle.vertices[1], rightVert };
        currentNormals = new Vector3[] { negativeTriangle.normals[0], negativeTriangle.normals[1], rightNormal };
        currentUVs = new Vector2[] { negativeTriangle.uvs[0], negativeTriangle.uvs[1], rightUV };

        currentTriangle = new TriangleData(currentVertices, currentNormals, currentUVs, triangle.subMeshIndex);
        if (FlipValidTriangle(currentTriangle))
        {
            negativeMeshData.AddTriangleData(currentTriangle);
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

    void CreateGameObjectUsingMeshData(MeshData meshData, GameObject parentObject)
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
        //go.AddComponent<Rigidbody>();

        //Rigidbody rb = go.GetComponent<Rigidbody>();
        //rb.useGravity = true;
        //rb.isKinematic = false;

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
    }

    void EvaluatePairs( List<Vector3> intersectingVerts, List<Vector3> vertices, List<Vector3> polygons)
    {
        bool isDone = false;
        while(!isDone)
        {
            isDone = true;
            for(int i = 0; i < intersectingVerts.Count; i += 2)
            {
                if (intersectingVerts[i] == polygons[polygons.Count - 1] && !vertices.Contains(intersectingVerts[i + 1]))
                {
                    isDone = false;
                    polygons.Add(intersectingVerts[i + 1]);
                    vertices.Add(intersectingVerts[i + 1]);
                }
                else if(intersectingVerts[i + 1] == polygons[polygons.Count - 1] && !vertices.Contains(intersectingVerts[i]))
                {
                    isDone = false;
                    polygons.Add(intersectingVerts[i]);
                    vertices.Add(intersectingVerts[i]);
                }
            }
        }
    }

    void Fill(Mesh originalMesh, List<Vector3> polygon, Plane cutPlane, MeshData positiveMeshData, MeshData negativeMeshData)
    {
        Vector3 center = Vector3.zero;
        foreach(Vector3 v in polygon)
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
}
