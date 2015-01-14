using UnityEngine;
using System.Collections;

public class WebcamScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
		WebCamDevice[] devices = WebCamTexture.devices;

		if (devices.Length > 0) {
			WebCamTexture myWebCamTex = new WebCamTexture();
			myWebCamTex.Play();

			renderer.material.mainTexture = myWebCamTex;
		}
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
