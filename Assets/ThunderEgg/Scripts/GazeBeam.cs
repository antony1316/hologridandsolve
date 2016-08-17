using HoloToolkit.Unity;
using UnityEngine;
using System.Collections;

public class GazeBeam : MonoBehaviour {

    private LineRenderer gazeLineRend;

    // Use this for initialization
    void Start ()
    {
        gazeLineRend = Camera.main.gameObject.AddComponent<LineRenderer>();
        gazeLineRend.SetVertexCount(2);
        gazeLineRend.SetWidth(0.01f, 0.01f);
        gazeLineRend.SetPosition(0, Camera.main.transform.position);
        gazeLineRend.SetPosition(1, Camera.main.transform.position);
    }
	
	// Update is called once per frame
	void Update ()
    {
        gazeLineRend.SetPosition(0, Camera.main.transform.position + Vector3.down * 0.5f);
        gazeLineRend.SetPosition(1, GazeManager.Instance.Position);
    }
}
