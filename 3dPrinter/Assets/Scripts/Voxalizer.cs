using System;
using System.Linq;
using UnityEngine;

public class Voxalizer : MonoBehaviour
{
    public ComputeShader VoxelCompute;
    public int Resolution = 99;
    private ComputeBuffer VertexBuffer, TriangleBuffer, VoxelBuffer;
    private int GridsizeX, GridsizeY, GridsizeZ;
    private Vector3 MinBounds, MaxBounds;
    private float Voxelsize;
    private int[] Voxeldata;
    private int TriangleCount;

    public void Voxalize(GameObject model)
    {
        Mesh mesh = model.GetComponentInChildren<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        MinBounds = vertices.Aggregate(Vector3.Min);
        MaxBounds = vertices.Aggregate(Vector3.Max);

        float largestDim = Mathf.Max(MaxBounds.x - MinBounds.x, MaxBounds.y - MinBounds.y, MaxBounds.z - MinBounds.z);
        Voxelsize = largestDim / Resolution;

        TriangleCount = triangles.Length / 3;

        GridsizeX = Mathf.CeilToInt((MaxBounds.x - MinBounds.x) / Voxelsize);
        GridsizeY = Mathf.CeilToInt((MaxBounds.y - MinBounds.y) / Voxelsize);
        GridsizeZ = Mathf.CeilToInt((MaxBounds.z - MinBounds.z) / Voxelsize);

        VertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        VertexBuffer.SetData(vertices);

        TriangleBuffer = new ComputeBuffer(triangles.Length / 3, sizeof(int) * 3);
        TriangleBuffer.SetData(
            Enumerable.Range(0, triangles.Length / 3)
            .Select(i => new Vector3Int(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2])).ToArray()
        );

        VoxelBuffer = new ComputeBuffer(GridsizeX * GridsizeY * GridsizeZ, sizeof(int));

        int kernel = VoxelCompute.FindKernel("VoxelizeMesh");
        VoxelCompute.SetBuffer(kernel, "Vertices", VertexBuffer);
        VoxelCompute.SetBuffer(kernel, "Triangles", TriangleBuffer);
        VoxelCompute.SetBuffer(kernel, "VoxelGrid", VoxelBuffer);
        VoxelCompute.SetInt("GridsizeX", GridsizeX);
        VoxelCompute.SetInt("GridsizeY", GridsizeY);
        VoxelCompute.SetInt("GridsizeZ", GridsizeZ);
        VoxelCompute.SetVector("MinBounds", MinBounds);
        VoxelCompute.SetFloat("Voxelsize", Voxelsize);
        VoxelCompute.SetInt("TriangleCount", TriangleCount);

        VoxelCompute.Dispatch(kernel, Mathf.CeilToInt(GridsizeX / 8f), Mathf.CeilToInt(GridsizeY / 8f), Mathf.CeilToInt(GridsizeZ / 8f));

        Voxeldata = new int[GridsizeX * GridsizeY * GridsizeZ];
        VoxelBuffer.GetData(Voxeldata);

        VertexBuffer.Release();
        TriangleBuffer.Release();
        VoxelBuffer.Release();

        CreateVisualization();
    }

    private void CreateVisualization()
    {
        for(int x = 0; x < GridsizeX; x++)
        {
            for(int y = 0; y < GridsizeY; y++)
            {
                for(int z = 0; z < GridsizeZ; z++)
                {
                    int index = z * GridsizeX * GridsizeY + y * GridsizeX + x;
                    if (Voxeldata[index] == 1)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = MinBounds + new Vector3(x, y, z) * Voxelsize;
                        cube.transform.localScale = Vector3.one * Voxelsize;
                        cube.GetComponent<Renderer>().material.color = Color.blue;
                    }
                }
            }
        }
    }
}
