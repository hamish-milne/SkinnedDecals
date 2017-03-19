using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Collects decal commands to render them, and provides other shared functionality
	/// </summary>
	[ExecuteInEditMode]
	public class DecalManager : MonoBehaviour
	{
		/// <summary>
		/// Whether to draw decals on the scene camera
		/// </summary>
		[SerializeField] protected bool renderSceneCamera = true;

		/// <summary>
		/// The currently enabled manager instance
		/// </summary>
		public static DecalManager Current { get; private set; }

		/// <summary>
		/// Finds a Manager somewhere in the scene, enabling a disabled one, or creating a new one if needed
		/// </summary>
		/// <returns>A <c>DecalManager</c> component</returns>
		public static DecalManager GetOrCreate()
		{
			if (Current == null)
			{
				Current = FindObjectOfType<DecalManager>();
				if (Current.Exists())
					Current.enabled = true;
			}
			if (Current == null)
				Current = new GameObject("DecalManager").AddComponent<DecalManager>();
			return Current;
		}

		private static Camera sceneCamera;

		/// <summary>
		/// The scene camera, if any
		/// </summary>
		public static Camera SceneCamera
		{
			get
			{
				if (Application.isEditor && sceneCamera == null)
					sceneCamera = Resources.FindObjectsOfTypeAll<Camera>()
						.FirstOrDefault(c => !c.enabled &&
						c.gameObject.activeInHierarchy && c.name == "SceneCamera");
				return sceneCamera;
			}
		}


		// All the cameras we're rendering to
		private Camera[] cameraArray;
		private DecalCamera[] decalCameraArray;

		// Retrieves the camera list, taking into account renderSceneCamera and minimising allocations
		public void CheckCameras()
		{
			if (renderSceneCamera && SceneCamera != null)
			{
				if (cameraArray == null || cameraArray.Length != Camera.allCamerasCount + 1)
					cameraArray = new Camera[Camera.allCamerasCount + 1];
				Camera.GetAllCameras(cameraArray);
				cameraArray[Camera.allCamerasCount] = SceneCamera;
			}
			else
			{
				if (cameraArray == null || cameraArray.Length != Camera.allCamerasCount)
					cameraArray = Camera.allCameras;
				else
					Camera.GetAllCameras(cameraArray);
			}
			if(decalCameraArray == null || decalCameraArray.Length != cameraArray.Length)
				decalCameraArray = new DecalCamera[cameraArray.Length];
			for (int i = 0; i < cameraArray.Length; i++)
				decalCameraArray[i] = GetDecalCamera(cameraArray[i]);
		}

		/// <summary>
		/// Checks whether assigning a renderer to be drawn (as opposed to a mesh) will work for all cameras
		/// </summary>
		/// <param name="material"></param>
		/// <returns><c>true</c> if it will work, <c>false</c> if not</returns>
		public virtual bool CanDrawRenderers(DecalMaterial material)
		{
			if (decalCameraArray == null) return true;
			foreach(var c in decalCameraArray)
				if (c != null && c.Camera != sceneCamera && !c.CanDrawRenderers(material))
					return false;
			return true;
		}

		/// <summary>
		/// Sets up a camera for decal rendering (may be called multiple times)
		/// </summary>
		/// <param name="cam"></param>
		protected virtual DecalCamera GetDecalCamera(Camera cam)
		{
			DecalCamera ret = cam.GetComponent<DecalCamera>();
			if (ret.Exists())
				ret.enabled = true;
			else
				ret = cam.gameObject.AddComponent<DecalCamera>();
			return ret;
		}

		protected virtual void Update()
		{
			CheckCameras();
		}

		protected virtual void OnEnable()
		{
			if (Current != null && Current != this)
				Debug.LogWarning($"DecalSystem: Multiple managers active in scene ({Current}, {this})", this);
			Current = this;
		}

		protected virtual void OnDisable()
		{
			if (Current == this)
				Current = null;
			cameraArray = null;
		}
	}
}
