using UnityEngine;
using SFB;
using Dummiesman;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Collections;

public class OpenFile : MonoBehaviour
{
    [HideInInspector]
    public GameObject model;
    //private SaveSTLtoOBJ stlConverter;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //stlConverter = new SaveSTLtoOBJ();
    }
    //public void onClickOpen()
    //{
    //    _ = OnClickOpenAsync();
    //}
    public void /*async Task*/ OnClickOpenAsync()
    {
        Debug.Log("runs");
        string[] path = StandaloneFileBrowser.OpenFilePanel("Open File", "", "obj", false);
        if (path.Length > 0)
        {
            //StartCoroutine(LoadAndConvertSTL(path[0]));
            StartCoroutine(LoadOBJ(new System.Uri(path[0]).AbsoluteUri));
            //await LoadOBJ(path[0]);
        }
        //    StartCoroutine(OutputRoutineOpen(new System.Uri(path[0]).AbsoluteUri));
        //}
    }

    private /*async Task*/ IEnumerator WaterTightness()
    {
        Debug.Log("this ran");
        MeshFilter modelFilter = model.GetComponentInChildren<MeshFilter>();
        Debug.Log(model.name);
        if (modelFilter == null)
        {
            Debug.LogError("No mesh filter found");
            /*return*/ yield break;
        }
        Mesh modelmesh = modelFilter.mesh;
        Vector3[] vertices = modelmesh.vertices;
        int[] triangles = modelmesh.triangles;
        Dictionary<(Vector3, Vector3), int> edgeCount = new Dictionary<(Vector3, Vector3), int>();
        Task<bool> task = Task.Run(() =>
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];

                AddEdge(edgeCount, v1, v2);
                AddEdge(edgeCount, v2, v3);
                AddEdge(edgeCount, v3, v1);
            }
            foreach (var edge in edgeCount)
            {
                if (edge.Value == 1)
                {
                    return false;
                }
            }
            return true;
        });
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.Result)
        {
            Debug.Log("model is watertight");
        }
        else
        {
            Debug.LogError("model is not watertight");
        }
    }

    private void AddEdge(Dictionary<(Vector3, Vector3), int> edgeCount, Vector3 v1, Vector3 v2)
    {
        var edge = (v1, v2);
        var reversedEdge = (v2, v1);
        if(edgeCount.ContainsKey(reversedEdge))
        {
            edgeCount[reversedEdge]++;
        }
        else if (edgeCount.ContainsKey(edge))
        {
            edgeCount[edge]++;
        }
        else
        {
            edgeCount[edge] = 1;
        }
    }

    //private IEnumerator LoadAndConvertSTL(string filePath)
    //{
    //    if (!File.Exists(filePath))
    //    {
    //        Debug.LogError("File does not exist: " + filePath);
    //        yield break;
    //    }

    //    byte[] fileData = File.ReadAllBytes(filePath);
    //    Mesh mesh = ParseSTL(fileData);
    //    Mesh testMesh = ParseSTL(File.ReadAllBytes(filePath));
    //    Debug.Log($"Vertices: {testMesh.vertexCount}, Triangles: {testMesh.triangles.Length}");

    //    if (mesh == null)
    //    {
    //        Debug.LogError("Failed to parse STL file.");
    //        yield break;
    //    }

    //    // Save as OBJ for easier loading
    //    Task<string> conversionTask = stlConverter.ConvertMeshToOBJAsync(mesh);
    //    yield return new WaitUntil(() => conversionTask.IsCompleted);
    //    string objContent = conversionTask.Result;
    //    if (objContent == null)
    //    {
    //        Debug.LogError("OBJ conversion failed.");
    //        yield break;
    //    }
    //    string objPath = Path.ChangeExtension(filePath, ".obj");
    //    yield return StartCoroutine(SaveOBJOnMainThread(objPath, objContent));
    //    // Load OBJ
    //    StartCoroutine(LoadOBJ(objPath));
    //}
    //private IEnumerator SaveOBJOnMainThread(string path, string content)
    //{
    //    yield return new WaitForEndOfFrame(); // Ensure this runs on the main thread
    //    Debug.Log("This runs");
    //    File.WriteAllText(path, content, Encoding.UTF8);
    //    Debug.Log($"OBJ File Saved: {path}");
    //}
    private IEnumerator /*async Task*/ LoadOBJ(string objPath)
    {
        Debug.Log("starting to load");
        string directoryPath = Path.GetDirectoryName(objPath);

        UnityWebRequest www = UnityWebRequest.Get(objPath);
        /*await*/ yield return www.SendWebRequest();
        Debug.Log("Test Request Status: " + www.result);

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to load OBJ: " + www.error);
            yield break /*return*/;
        }

        MemoryStream textStream = new MemoryStream(Encoding.UTF8.GetBytes(www.downloadHandler.text));


        if (model != null)
        {
            Destroy(model);
        }

        model = /*await Task.Run(() => */new OBJLoader().Load(textStream)/*)*/;
        //bool isComplete = false;
        //_ = WaterTightness().ContinueWith(_ => isComplete = true);
        //yield return new WaitUntil(() => isComplete);
        yield return StartCoroutine(WaterTightness());
        Shader customShader = Shader.Find("Universal Render Pipeline/Lit"); // Replace with your custom shader name
        if (customShader == null)
        {
            Debug.LogError("Shader not found!");
            yield break /*return*/;
        }

        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            foreach (Material mat in renderer.materials)
            {
                mat.shader = customShader;
            }
            model.transform.localScale = new Vector3(-1, 1, 1);
        }
    }
    public async void ExportToSTLAsync(string filePath)
    {
        if (model == null)
        {
            Debug.LogError("Target object is null!");
            return;
        }

        MeshFilter meshFilter = model.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
        {
            Debug.LogError("No MeshFilter found on the target object!");
            return;
        }
        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;  // Copy vertices
        int[] triangles = mesh.triangles;

        Debug.Log("Starting STL Export...");
        await Task.Run(() => WriteSTLFile(vertices, triangles, filePath));
        Debug.Log($"STL file saved to: {filePath}");
    }
    private void WriteSTLFile(Vector3[] vertices, int[] triangles, string filePath)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("solid UnityExport");

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 v1 = vertices[triangles[i]];
                    Vector3 v2 = vertices[triangles[i + 1]];
                    Vector3 v3 = vertices[triangles[i + 2]];

                    Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                    writer.WriteLine($"facet normal {normal.x} {normal.y} {normal.z}");
                    writer.WriteLine("outer loop");
                    writer.WriteLine($"vertex {v1.x} {v1.y} {v1.z}");
                    writer.WriteLine($"vertex {v2.x} {v2.y} {v2.z}");
                    writer.WriteLine($"vertex {v3.x} {v3.y} {v3.z}");
                    writer.WriteLine("endloop");
                    writer.WriteLine("endfacet");
                }

                writer.WriteLine("endsolid UnityExport");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"STL export failed: {ex.Message}");
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            string path = Path.Combine(Application.persistentDataPath, "exported_model.stl");
            ExportToSTLAsync(path);
            Debug.Log($"Exporting STL in background... Path: {path}");
        }
    }
    //private Mesh ParseSTL(byte[] fileData)
    //{
    //    using (MemoryStream stream = new MemoryStream(fileData))
    //    using (BinaryReader reader = new BinaryReader(stream))
    //    {
    //        string header = Encoding.UTF8.GetString(reader.ReadBytes(5));

    //        if (header.StartsWith("solid"))
    //        {
    //            Debug.Log("Detected ASCII STL file.");
    //            return ParseAsciiSTL(fileData);
    //        }
    //        else
    //        {
    //            Debug.Log("Detected Binary STL file.");
    //            return ParseBinarySTL(fileData);
    //        }
    //    }
    //}

    //private Mesh ParseBinarySTL(byte[] fileData)
    //{
    //    using (MemoryStream stream = new MemoryStream(fileData))
    //    using (BinaryReader reader = new BinaryReader(stream))
    //    {
    //        reader.ReadBytes(80); // Skip header
    //        int triangleCount = reader.ReadInt32();

    //        if (triangleCount <= 0)
    //        {
    //            Debug.LogError("Invalid STL file.");
    //            return null;
    //        }

    //        List<Vector3> vertices = new List<Vector3>();
    //        List<int> triangles = new List<int>();

    //        for (int i = 0; i < triangleCount; i++)
    //        {
    //            reader.ReadBytes(12); // Skip normal

    //            for (int j = 0; j < 3; j++)
    //            {
    //                float x = reader.ReadSingle();
    //                float y = reader.ReadSingle();
    //                float z = reader.ReadSingle();
    //                vertices.Add(new Vector3(x, y, z));
    //                triangles.Add(vertices.Count - 1);
    //            }

    //            reader.ReadUInt16(); // Skip attribute byte count
    //        }

    //        Mesh mesh = new Mesh
    //        {
    //            vertices = vertices.ToArray(),
    //            triangles = triangles.ToArray()
    //        };
    //        mesh.RecalculateNormals();
    //        Debug.Log("done parsing");
    //        return mesh;
    //    }
    //}

    //private Mesh ParseAsciiSTL(byte[] fileData)
    //{
    //    string stlText = Encoding.UTF8.GetString(fileData);
    //    StringReader reader = new StringReader(stlText);

    //    List<Vector3> vertices = new List<Vector3>();
    //    List<int> triangles = new List<int>();

    //    string line;
    //    while ((line = reader.ReadLine()) != null)
    //    {
    //        line = line.Trim();
    //        if (line.StartsWith("vertex"))
    //        {
    //            string[] parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
    //            float x = float.Parse(parts[1]);
    //            float y = float.Parse(parts[2]);
    //            float z = float.Parse(parts[3]);

    //            vertices.Add(new Vector3(x, y, z));
    //            triangles.Add(vertices.Count - 1);
    //        }
    //    }

    //    Mesh mesh = new Mesh
    //    {
    //        vertices = vertices.ToArray(),
    //        triangles = triangles.ToArray()
    //    };
    //    mesh.RecalculateNormals();
    //    return mesh;
    //}
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
