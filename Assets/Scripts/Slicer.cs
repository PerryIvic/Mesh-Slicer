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
    private class MeshData
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();

        public List<Vector2> uvs = new List<Vector2>();

        public List<int> indices = new List<int>();
    }
    private struct Vertex
    {
        public Vector3 v;
        public Vector3 n;
        public Vector2 uv;
        public int index;

        public Vertex(Vector3 vertice, Vector3 normal, Vector2 uvCoords, int triangleIndex = -1)
        {
            v = vertice;
            n = normal;
            uv = uvCoords;
            index = triangleIndex;
        }
    }

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

        if(hits.Length == 1)
        {
            GameObject hitObj = hits[0].gameObject;

            Plane plane = new Plane();

            Vector3 localPlaneNormal = hitObj.transform.InverseTransformDirection(transform.up);
            Vector3 localPlanePosition = hitObj.transform.InverseTransformPoint(transform.position);

            plane.SetNormalAndPosition(localPlaneNormal, localPlanePosition); 

            MeshData positiveMeshData = new MeshData();
            MeshData negativeMeshData = new MeshData();

            List<Vertex> intersectingVerts = new List<Vertex>();

            Mesh mesh = hits[0].GetComponent<MeshFilter>().mesh;
            for(int i = 0; i < mesh.triangles.Length; i += 3)
            {
                int triIndex1 = mesh.triangles[i];
                int triIndex2 = mesh.triangles[i + 1];
                int triIndex3 = mesh.triangles[i + 2];

                Vector3 v1 = mesh.vertices[triIndex1];
                Vector3 v2 = mesh.vertices[triIndex2];
                Vector3 v3 = mesh.vertices[triIndex3];

                Vector3 n1 = mesh.normals[triIndex1];
                Vector3 n2 = mesh.normals[triIndex2];
                Vector3 n3 = mesh.normals[triIndex3];

                Vector2 uv1 = mesh.uv[triIndex1];
                Vector2 uv2 = mesh.uv[triIndex2];
                Vector2 uv3 = mesh.uv[triIndex3];

                Vertex vert1 = new Vertex(v1, n1, uv1);
                Vertex vert2 = new Vertex(v2, n2, uv2);
                Vertex vert3 = new Vertex(v3, n3, uv3);

                bool vSide1 = plane.GetSide(v1);
                bool vSide2 = plane.GetSide(v2);
                bool vSide3 = plane.GetSide(v3);

                if (vSide1 == vSide2 && vSide1 == vSide3) // all vertices are on the same side
                {
                    MeshData selectedData = (vSide1) ? positiveMeshData : negativeMeshData;
                    AddTriangleToMeshData(ref selectedData, vert1, vert2, vert3);
                }
                else
                {
                    Vertex intersection1;
                    Vertex intersection2;

                    MeshData selectedMesh1 = (vSide1) ? positiveMeshData : negativeMeshData;
                    MeshData selectedMesh2 = (vSide1) ? negativeMeshData : positiveMeshData;

                    if (vSide1 == vSide2) // vert1 and vert2 are on the same side
                    {
                        intersection1 = CreateVertexIntersectingPlane(plane, vert2, vert3);
                        intersection2 = CreateVertexIntersectingPlane(plane, vert1, vert3);

                        AddTriangleToMeshData(ref selectedMesh1, vert1, vert2, intersection1);
                        AddTriangleToMeshData(ref selectedMesh1, vert1, intersection1, intersection2);
                        AddTriangleToMeshData(ref selectedMesh2, intersection1, vert3, intersection2);
                    }
                    else if(vSide1 == vSide3) // vert1 and vert3 are on the same side
                    {
                        intersection1 = CreateVertexIntersectingPlane(plane, vert1, vert2);
                        intersection2 = CreateVertexIntersectingPlane(plane, vert3, vert2);

                        AddTriangleToMeshData(ref selectedMesh1, vert1, intersection1, vert3);
                        AddTriangleToMeshData(ref selectedMesh1, intersection1, intersection2, vert3);
                        AddTriangleToMeshData(ref selectedMesh2, intersection1, vert2, intersection2);
                    }
                    else // vert1 is alone
                    {
                        intersection1 = CreateVertexIntersectingPlane(plane, vert2, vert1);
                        intersection2 = CreateVertexIntersectingPlane(plane, vert3, vert1);

                        AddTriangleToMeshData(ref selectedMesh1, vert1, intersection1, intersection2);
                        AddTriangleToMeshData(ref selectedMesh2, intersection1, vert2, vert3);
                        AddTriangleToMeshData(ref selectedMesh2, intersection1, vert3, intersection2);
                    }

                    intersectingVerts.Add(intersection1);
                    intersectingVerts.Add(intersection2);
                }
            }

            //CoverSlicedArea(intersectingVerts, positiveMeshData, negativeMeshData, plane);

            CreateGameObjectUsingMeshData(positiveMeshData, hits[0].gameObject);
            CreateGameObjectUsingMeshData(negativeMeshData, hits[0].gameObject);

            Destroy(hitObj);
        }
        else
        {
            Debug.Log("Unhandled Slice hits: " + hits.Length.ToString());
        }
    }

    Vector3 GetIntersectionPointRaycast(Plane plane, Vector3 position, Vector3 direction) 
    {
        Ray ray = new Ray(position, direction);

        float distance = 0;
        plane.Raycast(ray, out distance);

        return ray.GetPoint(distance);
    }

    Vector3 GetIntersectionPoint(Plane plane, Vector3 start, Vector3 end)
    {
        Vector3 result;

        Vector3 startToEnd = start - end;
        float t = (plane.distance - Vector3.Dot(plane.normal, start)) / Vector3.Dot(plane.normal, startToEnd);

        if (t >= -float.Epsilon && t <= (1 + float.Epsilon))
        {
            result = start + t * startToEnd;
        }
        else
        {
            result = Vector3.zero;
        }

        return result;
    }

    // Adds vertice, normal and uv to vertex. TriangleIndex not included!
    Vertex CreateVertexIntersectingPlane(Plane plane, Vertex start, Vertex end)
    {
        Vector3 direction = (end.v - start.v).normalized;
        Vector3 intersectionPoint = GetIntersectionPointRaycast(plane, start.v, direction);
        //Vector3 intersectionPoint = GetIntersectionPoint(plane, start.v, end.v);

        float distance = (end.v - start.v).magnitude;
        float partialDistance = (intersectionPoint - start.v).magnitude;
        float percentage = partialDistance / distance;

        Vertex result = new Vertex();

        result.v = intersectionPoint;

        // TODO normals need to be calculated correctly
        Quaternion _cw90 = Quaternion.AngleAxis(90f, Vector3.forward);
        result.n = _cw90 * CalculateNormalFromTriangle(intersectionPoint, start.v, end.v);
        result.uv = /*Vector2.Lerp(start.uv, end.uv, percentage)*/ Vector2.zero;

        return result;
    }

    void AddTriangleToMeshData(ref MeshData meshData, Vertex vert1, Vertex vert2, Vertex vert3, bool shouldBePlaceFirst = false)
    {
        if(shouldBePlaceFirst)
        {
            ShiftVertexesForward(ref meshData);
        }

        AddVertexToMeshData(ref meshData, vert1, shouldBePlaceFirst);
        AddVertexToMeshData(ref meshData, vert2, shouldBePlaceFirst);
        AddVertexToMeshData(ref meshData, vert3, shouldBePlaceFirst);
    }

    void AddVertexToMeshData(ref MeshData meshData, Vertex vert, bool shouldUseIndices)
    {
        int triIndex = meshData.vertices.IndexOf(vert.v);

        // if vertex already exists just add a reference to an existing vertex
        if (triIndex > -1)
        {
            meshData.indices.Add(triIndex);
        }
        else
        {
            if(shouldUseIndices)
            {
                int index = vert.index;
                meshData.vertices.Insert(index, vert.v);
                meshData.normals.Insert(index, vert.n);
                meshData.uvs.Insert(index, vert.uv);
                meshData.indices.Insert(index, index); 
            }
            else
            {
                meshData.vertices.Add(vert.v);
                meshData.normals.Add(vert.n);
                meshData.uvs.Add(vert.uv);

                int index = meshData.vertices.IndexOf(vert.v);
                meshData.indices.Add(index);
            }
            
        }
    }

    void CoverSlicedArea(List<Vertex> intersectingVerts, MeshData positiveMesh, MeshData negativeMesh, Plane plane)
    {
        // find middle point of intersection
        Vertex center = new Vertex();
        center.v = Vector3.zero;
        foreach(Vertex vert in intersectingVerts)
        {
            center.v += vert.v;
        }

        center.v /= intersectingVerts.Count;
        center.n = new Vector3(0, 1, 0);
        center.uv = Vector2.zero;
        center.index = 0;

        //Create hull for sliced area
        for(int i = 0; i < intersectingVerts.Count; i += 2)
        {
            Vertex vert1 = intersectingVerts[i];
            Vertex vert2 = intersectingVerts[i + 1];

            vert1.index = 1;
            vert2.index = 2;

            // TODO handle triangle wind order depending on where plane sliced the mesh
            AddTriangleToMeshData(ref positiveMesh, center, vert1, vert2, true);
            AddTriangleToMeshData(ref negativeMesh, center, vert2, vert1, true);
        }
    }

    void CreateGameObjectUsingMeshData(MeshData meshData, GameObject parentObject)
    {
        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        //go.AddComponent<Rigidbody>();
        //
        //Rigidbody rb = go.GetComponent<Rigidbody>();
        //rb.useGravity = true;
        //rb.isKinematic = false;

        go.GetComponent<MeshRenderer>().material = parentObject.GetComponent<MeshRenderer>().material;

        Mesh mesh = go.GetComponent<MeshFilter>().mesh;
        mesh.vertices = meshData.vertices.ToArray();
        mesh.normals = meshData.normals.ToArray();
        mesh.uv = meshData.uvs.ToArray();
        mesh.triangles = meshData.indices.ToArray();

        mesh.RecalculateNormals();

        MeshCollider meshColl = go.GetComponent<MeshCollider>();
        meshColl.sharedMesh = mesh;
        meshColl.convex = true;

        go.transform.position = parentObject.transform.position;
        go.transform.localScale = parentObject.transform.localScale;
        go.transform.rotation = parentObject.transform.rotation;
        go.tag = parentObject.tag;
        go.name = parentObject.name;
    }

    void ShiftVertexesForward(ref MeshData meshData)
    {
        for(int i = 0; i < meshData.indices.Count; i += 3)
        {
            meshData.indices[i] += 3;
            meshData.indices[i + 1] += 3;
            meshData.indices[i + 2] += 3;
        }
    }

    // Calculates normal using the points of a triangle
    Vector3 CalculateNormalFromTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 sideAB = b - a;
        Vector3 sideAC = c - a;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }
}
