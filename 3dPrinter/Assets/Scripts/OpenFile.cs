using UnityEngine;
using SFB;
using Dummiesman;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class OpenFile : MonoBehaviour
{
    [HideInInspector]
    public GameObject model;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public void OnClickOpen()
    {
        string[] path = StandaloneFileBrowser.OpenFilePanel("Open File", "", "obj", false);
        if (path.Length > 0)
        {
            StartCoroutine(OutputRoutineOpen(new System.Uri(path[0]).AbsoluteUri));
        }
    }
    private IEnumerator OutputRoutineOpen(string url)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("WWW ERROR: " + www.error);
        }
        else
        {
            MemoryStream textStream = new MemoryStream(Encoding.UTF8.GetBytes(www.downloadHandler.text));
            if (model != null)
            {
                Destroy(model);
            }
            model = new OBJLoader().Load(textStream);
            model.transform.localScale = new Vector3(-1, 1, 1); 
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
