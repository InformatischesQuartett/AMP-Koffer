// Support for DirectX and OpenGL native texture updating, from Unity 4.0 upwards
#if UNITY_4_6 ||UNITY_4_5 || UNITY_4_4 || UNITY_4_3 || UNITY_4_2 || UNITY_4_1 || UNITY_4_0_1 || UNITY_4_0
	#define AVPRO_UNITY_4_X
#endif

using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2012-2014 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

[AddComponentMenu("AVPro Live Camera/Manager (required)")]
public class AVProLiveCameraManager : MonoBehaviour
{
	private static AVProLiveCameraManager _instance;

	public enum ConversionMethod
	{
		Unknown,
		Unity4,
		Unity35_OpenGL,
		Unity34_OpenGL,
		UnityScript,
	}

	public bool _supportHotSwapping;
	public bool _supportInternalFormatConversion;

	// Format conversion
	public Shader _shaderBGRA32;
	public Shader _shaderMONO8;
	public Shader _shaderYUY2;
	public Shader _shaderUYVY;
	public Shader _shaderYVYU;
	public Shader _shaderHDYC;
	public Shader _shaderI420;
	public Shader _shaderYV12;
	public Shader _shaderDeinterlace;

	private bool _isInitialised;
	private ConversionMethod _conversionMethod = ConversionMethod.Unknown;
	private List<AVProLiveCameraDevice> _devices;
	
	//-------------------------------------------------------------------------

	public static AVProLiveCameraManager Instance  
	{
		get
		{
			if (_instance != null)
				return _instance;
			
			Debug.LogError("[AVProLiveCamera] Trying to use component before it has started or after it has been destroyed.");
			
			return null;
		}
	}

	public ConversionMethod TextureConversionMethod
	{
		get { return _conversionMethod; }
	}
		
	public int NumDevices
	{
		get { if (_devices != null) return _devices.Count; return 0; }
	}

	//-------------------------------------------------------------------------
	
	void Start()
	{
		if (!_isInitialised)
		{
			_instance = this;
			Init();
		}
	}
	
	void OnDestroy()
	{
		Deinit();
	}
		

#if UNITY_EDITOR
	[ContextMenu("Copy Plugin DLLs")]
	private void CopyPluginDLLs()
	{
		AVProLiveCameraCopyPluginWizard.DisplayCopyDialog();
	}
#endif

	protected bool Init()
	{
		try
		{
#if UNITY_3_5
			if (AVProLiveCameraPlugin.Init(true, _supportInternalFormatConversion))
#else
			if (AVProLiveCameraPlugin.Init(false, _supportInternalFormatConversion))
#endif
			{
				Debug.Log("[AVProLiveCamera] version " + AVProLiveCameraPlugin.GetPluginVersion().ToString("F2") + " initialised");
			}
			else
			{
				Debug.LogError("[AVProLiveCamera] failed to initialise.");
				this.enabled = false;
				Deinit();
				return false;
			}
		}
		catch (System.DllNotFoundException e)
		{
			Debug.Log("[AVProLiveCamera] Unity couldn't find the DLL, did you move the 'Plugins' folder to the root of your project?");
#if UNITY_EDITOR
			AVProLiveCameraCopyPluginWizard.DisplayCopyDialog();
#endif			
			throw e;
		}

		GetConversionMethod();
		EnumDevices();

		_isInitialised = true;

		return _isInitialised;
	}


	private void GetConversionMethod()
	{
		bool swapRedBlue = false;

		_conversionMethod = ConversionMethod.UnityScript;

#if AVPRO_UNITY_4_X		
		_conversionMethod = ConversionMethod.Unity4;
		if (SystemInfo.graphicsDeviceVersion.StartsWith("Direct3D 11"))
			swapRedBlue = true;

#elif UNITY_3_5 || UNITY3_4
		if (SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL"))
		{
#if UNITY_3_4
			_conversionMethod = ConversionMethod.Unity34_OpenGL;
#elif UNITY_3_5
			_conversionMethod = ConversionMethod.Unity35_OpenGL;
#endif
		}
		else
		{
			swapRedBlue = true;
		}
#else
		_conversionMethod = ConversionMethod.UnityScript;
		swapRedBlue = true;
#endif

		if (swapRedBlue)
		{
			Shader.DisableKeyword("SWAP_RED_BLUE_OFF");
			Shader.EnableKeyword("SWAP_RED_BLUE_ON");
		}
		else
		{
			Shader.DisableKeyword("SWAP_RED_BLUE_ON");
			Shader.EnableKeyword("SWAP_RED_BLUE_OFF");
		}
	}

	void Update()
	{
		if (_supportHotSwapping)
		{
			if (AVProLiveCameraPlugin.UpdateDevicesConnected())
			{
				// Add any new devices
				AddNewDevices();				
			}
		}
	}
	
#if AVPRO_UNITY_4_X || UNITY_3_5
	private int _lastFrameCount;
	void OnRenderObject()
	{
		if (_lastFrameCount != Time.frameCount)
		{
			_lastFrameCount = Time.frameCount;
			
			if (_conversionMethod == ConversionMethod.Unity4 || 
				_conversionMethod == ConversionMethod.Unity35_OpenGL)
			{
				GL.IssuePluginEvent(AVProLiveCameraPlugin.PluginID | (int)AVProLiveCameraPlugin.PluginEvent.UpdateAllTextures);		
			}
		}
	}
#endif
	
	private void AddNewDevices()
	{
		bool isDeviceAdded = false;
		
		int numDevices = AVProLiveCameraPlugin.GetNumDevices();
		for (int i = 0; i < numDevices; i++)
		{
			string deviceGUID;
			if (!AVProLiveCameraPlugin.GetDeviceGUID(i, out deviceGUID))
				continue;
			
			AVProLiveCameraDevice device = FindDeviceWithGUID(deviceGUID);
			if (device == null)
			{
				string deviceName;
				if (!AVProLiveCameraPlugin.GetDeviceName(i, out deviceName))
					continue;
				
				int numModes = AVProLiveCameraPlugin.GetNumModes(i);
				if (numModes > 0)
				{
					device = new AVProLiveCameraDevice(deviceName.ToString(), deviceGUID.ToString(), i);
					_devices.Add(device);
					isDeviceAdded = true;
				}
			}
		}
		
		if (isDeviceAdded)
		{
			this.SendMessage("NewDeviceAdded", null, SendMessageOptions.DontRequireReceiver);
		}
	}
	
	private AVProLiveCameraDevice FindDeviceWithGUID(string guid)
	{
		AVProLiveCameraDevice result = null;
		
		foreach (AVProLiveCameraDevice device in _devices)
		{
			if (device.GUID == guid)
			{
				result = device;
				break;
			}
		}
		
		return result;
	}

	private void EnumDevices()
	{
		ClearDevices();
		_devices = new List<AVProLiveCameraDevice>(8);
		int numDevices = AVProLiveCameraPlugin.GetNumDevices();
		for (int i = 0; i < numDevices; i++)
		{
			string deviceName;
			if (!AVProLiveCameraPlugin.GetDeviceName(i, out deviceName))
				continue;

			string deviceGUID;
			if (!AVProLiveCameraPlugin.GetDeviceGUID(i, out deviceGUID))
				continue;
			
			int numModes = AVProLiveCameraPlugin.GetNumModes(i);
			if (numModes > 0)
			{
				AVProLiveCameraDevice device = new AVProLiveCameraDevice(deviceName.ToString(), deviceGUID.ToString(), i);
				_devices.Add(device);
			}
		}		
	}
	
	private void ClearDevices()
	{
		if (_devices != null)
		{
			for (int i = 0; i < _devices.Count; i++)
			{
				_devices[i].Close();
				_devices[i].Dispose();
			}
			_devices.Clear();
			_devices = null;
		}		
	}
	
	public void Deinit()
	{
		ClearDevices();
		_instance = null;
		_isInitialised = false;

		AVProLiveCameraPlugin.Deinit();
	}

	public Shader GetDeinterlaceShader()
	{
		return _shaderDeinterlace;
	}

	public Shader GetPixelConversionShader(AVProLiveCameraPlugin.VideoFrameFormat format)
	{
		Shader result = null;
		switch (format)
		{
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_422_YUY2:
			result = _shaderYUY2;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_422_UYVY:
			result = _shaderUYVY;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_422_YVYU:
			result = _shaderYVYU;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_422_HDYC:
			result = _shaderHDYC;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.RAW_BGRA32:
			result= _shaderBGRA32;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.RAW_MONO8:
			result= _shaderMONO8;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_420_PLANAR_I420:
			result= _shaderI420;
			break;
		case AVProLiveCameraPlugin.VideoFrameFormat.YUV_420_PLANAR_YV12:
			result= _shaderYV12;
			break;			
		default:
			Debug.LogError("[AVProLiveCamera] Unknown video format '" + format);
			break;
		}
		return result;
	}
	
	public AVProLiveCameraDevice GetDevice(int index)
	{
		AVProLiveCameraDevice result = null;
		
		if (index >= 0 && index < _devices.Count)
			result = _devices[index];
		
		return result;
	}
	
	public AVProLiveCameraDevice GetDevice(string name)
	{
		AVProLiveCameraDevice result = null;
		int numDevices = NumDevices;
		for (int i = 0; i < numDevices; i++)
		{
			AVProLiveCameraDevice device = GetDevice(i);
			if (device.Name == name)
			{
				result = device;
				break;
			}
		}
		return result;
	}
}