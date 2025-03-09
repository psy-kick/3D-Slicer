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
using System.Linq;
using System.Threading;

public class OpenFile : MonoBehaviour
{
    [HideInInspector]
    public GameObject model;
    private MeshFilter modelfilter;
    [SerializeField]
    private float LayerHeight = 10f;
    private Vector3[] vertices;
    private int[] triangles;
    private float minY, maxY;
    List<(Vector3, Vector3, Vector3)> triangleVertices;
    List<List<Vector3>> slices = new List<List<Vector3>>();
    public Material lineMaterial;
    private CancellationTokenSource cancellationTokenSource;
    public Material sliceMaterial;

    public ComputeShader VoxelCompute;
    //private SaveSTLtoOBJ stlConverter;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public async Task PrecomputeModel()
    {
        Debug.Log("precompute");
        if (model == null)
        {
            Debug.LogError("model not loaded yet");
            return;
        }
        MeshFilter modelFilter = model.GetComponentInChildren<MeshFilter>();
        Mesh mesh = modelFilter.mesh;
        if (mesh == null)
        {
            Debug.LogError("Mesh not found in model!");
            return;
        }

        vertices = mesh.vertices;
        triangles = mesh.triangles;

        if (vertices == null || triangles == null || triangles.Length == 0)
        {
            Debug.LogError("Mesh data is invalid! Vertices or triangles are null/empty.");
            return;
        }

        minY = mesh.bounds.min.y;
        maxY = mesh.bounds.max.y;

        Debug.Log($"Starting Task.Run() with {triangles.Length / 3} triangles.");

        triangleVertices = await Task.Run(() =>
        {
            Debug.Log("tri");
            List<(Vector3, Vector3, Vector3)> tempTriangleVertices = new List<(Vector3, Vector3, Vector3)>(triangles.Length / 3);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                //Vector3 v1 = vertices[triangles[i]];
                //Vector3 v2 = vertices[triangles[i + 1]];
                //Vector3 v3 = vertices[triangles[i + 2]];

                tempTriangleVertices.Add((vertices[triangles[i]],
                                      vertices[triangles[i + 1]],
                                      vertices[triangles[i + 2]]));
            }
            return tempTriangleVertices;
        });
        //Debug.Log("Model computed");
        //triangleVertices = new List<(Vector3, Vector3, Vector3)>(triangles.Length / 3);
        //for (int i = 0; i < triangles.Length; i += 3)
        //{
        //    triangleVertices.Add((vertices[triangles[i]],
        //                          vertices[triangles[i + 1]],
        //                          vertices[triangles[i + 2]]));
        //}
        Debug.Log($"Model computed with {triangleVertices.Count} triangles.");
    }
    public void OnClickOpenAsync()
    {
        Debug.Log("runs");
        string[] path = StandaloneFileBrowser.OpenFilePanel("Open File", "", "obj", false);
        if (path.Length > 0)
        {
            StartCoroutine(LoadOBJ(new System.Uri(path[0]).AbsoluteUri));
        }
    }

    private IEnumerator WaterTightness()
    {
        Debug.Log("this ran");
        MeshFilter modelFilter = model.GetComponentInChildren<MeshFilter>();
        Debug.Log(model.name);
        if (modelFilter == null)
        {
            Debug.LogError("No mesh filter found");
            /*return*/ yield break;
        }
        //Mesh modelmesh = modelFilter.mesh;
        //Vector3[] vertices = modelmesh.vertices;
        //int[] triangles = modelmesh.triangles;
        Dictionary<(Vector3, Vector3), int> edgeCount = new Dictionary<(Vector3, Vector3), int>();
        Task<bool> task = Task.Run(() =>
        {
            //for (int i = 0; i < triangles.Length; i += 3)
            //{
            //    Vector3 v1 = vertices[triangles[i]];
            //    Vector3 v2 = vertices[triangles[i + 1]];
            //    Vector3 v3 = vertices[triangles[i + 2]];

            //    AddEdge(edgeCount, v1, v2);
            //    AddEdge(edgeCount, v2, v3);
            //    AddEdge(edgeCount, v3, v1);
            //}
            foreach (var (v1,v2,v3) in triangleVertices)
            {
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

    private IEnumerator LoadOBJ(string objPath)
    {
        if (objPath.StartsWith("file:///"))
        {
            objPath = new Uri(objPath).LocalPath;
        }
        if (!File.Exists(objPath))
        {
            Debug.LogError("File does not exist: " + objPath);
            yield break;
        }
        string directoryPath = Path.GetDirectoryName(objPath);
        Debug.Log("Loading file from: " + objPath);
        UnityWebRequest www = UnityWebRequest.Get(objPath);
        yield return www.SendWebRequest();
        Debug.Log("Test Request Status: " + www.result);

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to load OBJ: " + www.error);
            yield break;
        }

        MemoryStream textStream = new MemoryStream(Encoding.UTF8.GetBytes(www.downloadHandler.text));


        if (model != null)
        {
            Destroy(model);
        }

        model = new OBJLoader().Load(textStream);

        Voxalizer voxalizer = gameObject.AddComponent<Voxalizer>();
        voxalizer.VoxelCompute = VoxelCompute;
        voxalizer.Voxalize(model);

        //Task precomputeTask = PrecomputeModel();
        //yield return new WaitUntil(() => precomputeTask.IsCompleted);

        Mesh mesh = model.GetComponentInChildren<MeshFilter>().mesh;

        //yield return StartCoroutine(WaterTightness());
        Shader customShader = Shader.Find("Universal Render Pipeline/Lit");
        if (customShader == null)
        {
            Debug.LogError("Shader not found!");
            yield break;
        }

        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            foreach (Material mat in renderer.materials)
            {
                mat.shader = customShader;
            }
            model.transform.localScale = Vector3.one; // Reset scale
            model.transform.rotation = Quaternion.identity; // Reset rotation
            model.transform.position = Vector3.zero; // Reset position

        }
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            filter.mesh.RecalculateNormals();
            filter.mesh.RecalculateBounds();
            filter.mesh.RecalculateTangents();
            filter.mesh.Optimize();
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
        Vector3[] vertices = mesh.vertices;
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
    public void OnSliceButtonClick()
    {
        //StartCoroutine(SliceMeshAsync(minY, maxY));
        StartCoroutine(WaitForSlicing());
        //Debug.Log(slicedLayers.Count);
    }
    private IEnumerator WaitForSlicing()
    {
        Task slicingTask = SliceMeshAsync(minY, maxY);
        while (!slicingTask.IsCompleted) yield return null;

        Debug.Log("Slicing is finished, proceeding...");
    }
    public async Task SliceMeshAsync(float minY, float maxY)
    {
        Debug.Log("Slicing");
        slices.Clear();
        float minYVal = triangleVertices.Min(t => Mathf.Min(t.Item1.y, t.Item2.y, t.Item3.y));
        float maxYVal = triangleVertices.Max(t => Mathf.Max(t.Item1.y, t.Item2.y, t.Item3.y));
        Debug.Log($"Calculated minY: {minYVal}, maxY: {maxYVal}");
        await Task.Run(() =>
        {
            Debug.Log("Inside Task.Run, slicing...");
            Debug.Log($"Height range: minY = {minY}, maxY = {maxY}, LayerHeight = {LayerHeight}");
            Debug.Log($"Total Iterations: {(int)((maxY - minY) / LayerHeight)}");
            for (float height = minY; height <= maxY; height += LayerHeight)
            {
                List<Vector3> slicepoints = new List<Vector3>();
                foreach (var (v1, v2, v3) in triangleVertices)
                {
                    Debug.Log($"Checking Triangle: V1={v1}, V2={v2}, V3={v3} at height {height}");
                    Vector3? p1 = IntersectEdges(v1, v2, height);
                    Vector3? p2 = IntersectEdges(v2, v3, height);
                    Vector3? p3 = IntersectEdges(v3, v1, height);

                    if (p1.HasValue)
                    {
                        Debug.Log($"Intersection Found: {p1.Value} at height {height}");
                        slicepoints.Add(p1.Value);
                    }
                    if (p2.HasValue)
                    {
                        Debug.Log($"Intersection Found: {p2.Value} at height {height}");
                        slicepoints.Add(p2.Value);
                    }
                    if (p3.HasValue)
                    {
                        Debug.Log($"Intersection Found: {p3.Value} at height {height}");
                        slicepoints.Add(p3.Value);
                    }
                }
                if (slicepoints.Count > 0)
                {
                    lock (slices)
                    {
                        slices.Add(slicepoints);
                    }
                    //slices.Add(slicepoints);
                }
            }
            Debug.Log(slices.Count);
        });

        Debug.Log(slices.Count);
        CreateLineRenderers();
    }
    private void CreateLineRenderers()
    {
        // Remove old line renderers before drawing new ones
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        foreach (var slice in slices)
        {
            if (slice.Count < 2) continue; // Skip if there are not enough points to connect

            GameObject lineObj = new GameObject("SliceLine");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();

            lr.positionCount = slice.Count;
            lr.startWidth = 0.01f;
            lr.endWidth = 0.01f;
            lr.material = lineMaterial; // Assign a default material
            lr.startColor = Color.yellow;
            lr.endColor = Color.red;
            lr.useWorldSpace = true;
            lr.SetPositions(slice.ToArray());
        }
    }

    private Vector3? IntersectEdges(Vector3 v1, Vector3 v2, float height, float tolerance = 1e-6f)
    {
        Debug.Log($"Checking edge: {v1} -> {v2} for intersection at height {height}");

        // Check if the edge is near the slicing plane within the given tolerance
        if (Mathf.Abs(v1.y - v2.y) < tolerance)
        {
            // If the edge is almost parallel to the slicing plane (horizontal), no intersection occurs
            return null;
        }

        // Check if the two vertices are on opposite sides of the slicing plane
        if ((v1.y > height && v2.y < height) || (v1.y < height && v2.y > height))
        {
            // Linear interpolation to find the intersection point on the edge
            float t = (height - v1.y) / (v2.y - v1.y);

            // Calculate the intersection point using interpolation
            Vector3 intersection = Vector3.Lerp(v1, v2, t);

            // Log for debugging
            Debug.Log($"Intersection found: {intersection}");

            return intersection;
        }

        // No intersection if the edge is completely above or below the slicing plane
        return null;
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
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
