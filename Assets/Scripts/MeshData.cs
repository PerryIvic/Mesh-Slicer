using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();

    public List<Vector2> uvs = new List<Vector2>();

    public List<List<int>> subMeshIndices = new List<List<int>>();

    public void AddTriangleData(TriangleData triData)
    {
        int currentVerticeCount = vertices.Count;

        vertices.AddRange(triData.vertices);
        normals.AddRange(triData.normals);
        uvs.AddRange(triData.uvs);

        if(subMeshIndices.Count < triData.subMeshIndex + 1)
        {
            for(int i = subMeshIndices.Count; i < triData.subMeshIndex + 1; ++i)
            {
                subMeshIndices.Add(new List<int>());
            }
        }

        for (int i = 0; i < 3; ++i) 
        {
            subMeshIndices[triData.subMeshIndex].Add(currentVerticeCount + i);
        }
    }

    public Mesh GetGeneratedMesh()
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uvs);

        mesh.subMeshCount = subMeshIndices.Count;
        for(int i = 0; i < subMeshIndices.Count; ++i)
        {
            mesh.SetTriangles(subMeshIndices[i], i);
        }

        return mesh;
    }
}

public class TriangleData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector2> uvs = new List<Vector2>();
    public int subMeshIndex = 0;

    public TriangleData()
    {
    }

    public TriangleData(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int subMeshIndex)
    {
        this.vertices.AddRange(vertices);
        this.normals.AddRange(normals);
        this.uvs.AddRange(uvs);
        this.subMeshIndex = subMeshIndex;
    }

    public static TriangleData GetTriangleData(Mesh originalMesh, int triIndex1, int triIndex2, int triIndex3, int subMeshIndex)
    {
        Vector3[] vertices =
        {
            originalMesh.vertices[triIndex1],
            originalMesh.vertices[triIndex2],
            originalMesh.vertices[triIndex3]
        };

        Vector3[] normals =
        {
            originalMesh.normals[triIndex1],
            originalMesh.normals[triIndex2],
            originalMesh.normals[triIndex3]
        };

        Vector2[] uvs =
        {
            originalMesh.uv[triIndex1],
            originalMesh.uv[triIndex2],
            originalMesh.uv[triIndex3]
        };

        return new TriangleData(vertices, normals, uvs, subMeshIndex);
    }
}

public struct Vertex
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