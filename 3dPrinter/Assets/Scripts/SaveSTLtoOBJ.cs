using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.IO;

public class SaveSTLtoOBJ
{
    Vector3[] vertices;  //  Copy vertex data
    int[] triangles;
    Vector3[] normals;
    public async Task<string> ConvertMeshToOBJAsync(Mesh mesh)
    {
        // Copy mesh data **on the main thread**
        vertices = mesh.vertices;   //  Copy vertex data
        triangles = mesh.triangles;     //  Copy triangle data

        return await Task.Run(() =>
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Converted from STL to OBJ");

                Dictionary<Vector3, int> vertexIndexMap = new Dictionary<Vector3, int>();
                List<Vector3> uniqueVertices = new List<Vector3>();
                int index = 1;

                normals = mesh.normals.Length > 0 ? mesh.normals : CalculateNormals(mesh);
                //  Process copied vertices (Safe in background thread)
                foreach (Vector3 v in vertices)
                {
                    if (!vertexIndexMap.ContainsKey(v))
                    {
                        vertexIndexMap[v] = index++;
                        uniqueVertices.Add(v);
                    }
                }

                foreach (Vector3 v in uniqueVertices)
                {
                    sb.AppendLine($"v {v.x} {v.y} {v.z}");
                }

                //  Process copied triangles (Safe in background thread)
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int v1 = triangles[i] + 1;
                    int v2 = triangles[i + 1] + 1;
                    int v3 = triangles[i + 2] + 1;
                    sb.AppendLine($"f {v1}//{v1} {v3}//{v3} {v2}//{v2}");
                }

                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to convert STL to OBJ: {ex.Message}");
                return null;
            }
        });
    }
    private Vector3[] CalculateNormals(Mesh mesh)
    {
        normals = new Vector3[mesh.vertexCount];
        triangles = mesh.triangles;
        vertices = mesh.vertices;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int index0 = triangles[i];
            int index1 = triangles[i + 1];
            int index2 = triangles[i + 2];

            Vector3 v0 = vertices[index0];
            Vector3 v1 = vertices[index1];
            Vector3 v2 = vertices[index2];

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            normals[index0] += normal;
            normals[index1] += normal;
            normals[index2] += normal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].normalized;
        }

        return normals;
    }
}
