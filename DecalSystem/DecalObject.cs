using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	public class UsePropertyAttribute : PropertyAttribute
	{
	}

	/// <summary>
	/// Represents a mesh item that will be rendered with <c>Graphics.DrawMesh</c>
	/// </summary>
	/// <remarks>
	/// <c>DrawMesh</c> will automatically cull objects, calculate lighting in any rendering path,
	/// and can have a <c>MaterialPropertyBlock</c> applied.
	/// </remarks>
	public struct MeshData
	{
		/// <summary>
		/// The mesh to draw
		/// </summary>
		public Mesh mesh;

		/// <summary>
		/// The submesh index
		/// </summary>
		public int submesh;

		/// <summary>
		/// The base transform to draw the mesh on.
		/// Uses the <c>localToWorldMatrix</c>, or the identity if this field is <c>null</c>
		/// </summary>
		public Transform transform;

		/// <summary>
		/// An extra matrix that is applied after the transform
		/// </summary>
		public Matrix4x4 matrix;

		/// <summary>
		/// The material to use
		/// </summary>
		public Material material;

		/// <summary>
		/// Extra material properties
		/// </summary>
		public MaterialPropertyBlock materialPropertyBlock;
	}

	/// <summary>
	/// Represents a renderer that is typically drawn with a <c>CommandBuffer</c>
	/// </summary>
	/// <remarks>
	/// Drawing renderers in a <c>CommandBuffer</c> is much more limited than <c>DrawMesh</c>,
	/// but is significantly more efficient for <c>SkinnedMeshRenderers</c>, as it avoids the need
	/// to manually skin the mesh.
	/// </remarks>
	public struct RendererData
	{
		/// <summary>
		/// The renderer to draw
		/// </summary>
		public Renderer renderer;

		/// <summary>
		/// The submesh index
		/// </summary>
		public int submesh;

		/// <summary>
		/// The material to use
		/// </summary>
		public Material material;

		/// <summary>
		/// The material pass to use (-1 for all passes)
		/// </summary>
		public int pass;
	}

	/// <summary>
	/// Defines all the draw commands that a <c>DecalObject</c> requires
	/// </summary>
	public class RenderPathData
	{
		/// <summary>
		/// The list of <c>MeshData</c>. Can be <c>null</c>
		/// </summary>
		public MeshData[] MeshData { get; }

		/// <summary>
		/// The list of <c>RendererData</c>. Can be <c>null</c>
		/// </summary>
		public RendererData[] RendererData { get; }

		/// <summary>
		/// Whether the <c>DecalObject</c> requires the camera to render a depth texture.
		/// </summary>
		public bool RequiresDepthTexture { get; }

		public RenderPathData(MeshData[] meshData, RendererData[] rendererData, bool requiresDepthTexture)
		{
			MeshData = meshData;
			RendererData = rendererData;
			RequiresDepthTexture = requiresDepthTexture;
		}
	}

	/// <summary>
	/// Defines the action(s) that triggered a Refresh of the given object
	/// </summary>
	[Flags]
	public enum RefreshAction
	{
		/// <summary>
		/// Nothing.
		/// </summary>
		None = 0,

		/// <summary>
		/// A <c>DecalInstance</c> was enabled/disabled
		/// </summary>
		EnableDisable = 1 << 0,

		/// <summary>
		/// A <c>DecalInstance</c>'s <c>DecalMaterial</c> was changed
		/// </summary>
		ChangeInstanceMaterial = 1 << 1,

		/// <summary>
		/// A <c>DecalMaterial</c>'s properties were changed
		/// </summary>
		MaterialPropertiesChanged = 1 << 2,

		/// <summary>
		/// The list of active scene cameras and/or rendering paths were changed
		/// </summary>
		CamerasChanged = 1 << 3,

		/// <summary>
		/// The properties of the attached renderer changed. This may cause decal instances to be wiped
		/// </summary>
		RendererChanged = 1 << 4,
	}

	/// TODO: Separate from DecalChannel
	/// <summary>
	/// Represents one (or more) decal instances
	/// </summary>
	[Serializable]
	public abstract class DecalInstance
	{
		[SerializeField, UseProperty] protected bool enabled = true;
		[SerializeField, UseProperty] protected DecalMaterial decal;

		/// <summary>
		/// Whether to draw this decal or not
		/// </summary>
		public virtual bool Enabled
		{
			get { return enabled; }
			set { enabled = value; DecalObject.Refresh(RefreshAction.EnableDisable); }
		}

		/// <summary>
		/// Whether this <c>DecalInstance</c> controls a single decal,
		/// or a full 'channel'
		/// </summary>
		public virtual bool HasMultipleDecals => false;

		/// <summary>
		/// The parent <c>DecalObject</c>
		/// </summary>
		public abstract DecalObject DecalObject { get; }

		/// <summary>
		/// The <c>DecalMaterial</c> to use
		/// </summary>
		public virtual DecalMaterial DecalMaterial
		{
			get { return decal; }
			set { decal = value; DecalObject.Refresh(RefreshAction.ChangeInstanceMaterial); }
		}
	}

	/// <summary>
	/// An object that can receive decals.
	/// </summary>
	public abstract class DecalObject : MonoBehaviour
	{
		public abstract string[] RequiredModes { get; }

		private static List<DecalObject> activeObjects;
		private static ReadOnlyCollection<DecalObject> activeObjectsReadonly;

		private static void GetActiveObjects()
		{
			if (activeObjects != null) return;
			activeObjects = FindObjectsOfType<DecalObject>().Where(obj => obj.enabled).ToList();
			activeObjectsReadonly = new ReadOnlyCollection<DecalObject>(activeObjects);
		}

		/// <summary>
		/// The list of currently enabled <c>DecalObject</c>s
		/// </summary>
		public static IList<DecalObject> ActiveObjects
		{
			get
			{
				GetActiveObjects();
				return activeObjectsReadonly;
			}
		} 

		/// <summary>
		/// Called when an object's <c>RenderPathData</c> changed
		/// </summary>
		public static event Action<DecalObject> DataChanged;

		protected void NotifyDataChanged()
		{
			DataChanged?.Invoke(this);
		}

		/// <summary>
		/// The world-space bounds of the object
		/// </summary>
		public abstract Bounds Bounds { get; }

		/// <summary>
		/// The renderer the object is attached to. Can be <c>null</c> for screen space decals.
		/// </summary>
		public abstract Renderer Renderer { get; }

		/// <summary>
		/// The renderer's shared materials, if any.
		/// </summary>
		public virtual Material[] Materials => Renderer?.sharedMaterials;

		/// <summary>
		/// The mesh drawn by the object. For geometry-based objects this is the normal shared
		/// mesh, but for screen space objects will usually be a cube or quad.
		/// </summary>
		public abstract Mesh Mesh { get; }

		/// <summary>
		/// Returns the exact geometry this frame. This will differ from <c>Mesh</c> for skinned objects.
		/// </summary>
		/// <returns></returns>
		public virtual Mesh GetCurrentMesh()
		{
			return Mesh;
		}

		/// <summary>
		/// Whether the object draws decals in screen space. Screen space decals don't care about
		/// geometry and thus can't be filtered by material or have baked static decals.
		/// </summary>
		public abstract bool ScreenSpace { get; }

		/// <summary>
		/// Gets the collection of draw commands for the given rendering path
		/// </summary>
		/// <param name="path"></param>
		/// <returns>The rendering data, cached at this level, or <c>null</c></returns>
		public abstract RenderPathData GetRenderPathData(RenderingPath path);

		/// <summary>
		/// Adds a new decal to this instance
		/// </summary>
		/// <param name="projector">The projection transform</param>
		/// <param name="decal">The decal material</param>
		/// <param name="submesh">The submesh index</param>
		/// <returns>The instance created or added to, or <c>null</c> if the box didn't intersect any geometry</returns>
		public abstract DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh);

		/// <summary>
		/// Whether to cull the object in code.
		/// </summary>
		/// <remarks>
		/// If this is <c>true</c>, the object won't be rendered unless <c>DecalManager.RenderObject</c>
		/// is called for each required camera.
		/// </remarks>
		public abstract bool UseManualCulling { get; }

		/// <summary>
		/// Refreshes the object
		/// </summary>
		/// <param name="action">The action that triggered the refresh</param>
		/// <remarks>
		/// This function will only recalculate data necessary for the given
		/// triggering actions.
		/// </remarks>
		public virtual void Refresh(RefreshAction action)
		{
		}

		/// <summary>
		/// Creates a new renderer to draw additional decals, setting it up as
		/// necessary.
		/// </summary>
		/// <param name="target">The <c>GameObject</c> to add the component to</param>
		/// <param name="mesh">The mesh to draw</param>
		/// <returns>The newly created renderer</returns>
		public virtual Renderer CreateRenderer(GameObject target, Mesh mesh)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Refreshes all active objects
		/// </summary>
		/// <param name="action">The triggering action</param>
		/// <param name="predicate">Filters the objects to refresh. Disable with <c>null</c></param>
		public static void RefreshAll(RefreshAction action, Predicate<DecalObject> predicate = null)
		{
			GetActiveObjects();
			for (int i = 0, count = activeObjects.Count; i < count; i++)
			{
				var obj = activeObjects[i];
				if(predicate == null || predicate(obj))
					obj.Refresh(action);
			}
		}

		/// <summary>
		/// Called when an object becomes enabled/disabled
		/// </summary>
		public static event Action<DecalObject, bool> ObjectChangeState;

		protected virtual void OnEnable()
		{
			GetActiveObjects();
			if (!activeObjects.Contains(this))
			{
				activeObjects.Add(this);
				ObjectChangeState?.Invoke(this, true);
			}
		}

		protected virtual void OnDisable()
		{
			if (activeObjects != null)
			{
				if(activeObjects.Remove(this))
					ObjectChangeState?.Invoke(this, false);
			}
		}
	}

	/// <summary>
	/// A base class for all the standard <c>DecalObject</c> types
	/// </summary>
	/// <remarks>
	/// While <c>DecalObject</c> provides the broadest extensibility, <c>DecalObjectBase</c>
	/// provides common functionality for common use cases.
	/// </remarks>
	public abstract class DecalObjectBase : DecalObject
	{
		/// <summary>
		/// Whether the object requires a camera depth texture
		/// </summary>
		protected virtual bool RequireDepthTexture => false;

		/// <summary>
		/// Whether to require <c>DecalManager.RenderObject</c> to be called for this object
		/// </summary>
		public override bool UseManualCulling => false;

		/// <summary>
		/// Gets the data for the forward path
		/// </summary>
		/// <param name="meshData"></param>
		/// <param name="rendererData"></param>
		protected abstract void GetForwardData(out MeshData[] meshData, out RendererData[] rendererData);

		/// <summary>
		/// Gets the data for the deferred path
		/// </summary>
		/// <param name="meshData"></param>
		/// <param name="rendererData"></param>
		protected abstract void GetDeferredData(out MeshData[] meshData, out RendererData[] rendererData);

		private RenderPathData deferredData, forwardData;

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if((action & RefreshAction.EnableDisable) != 0)
				ClearData();
		}

		/// <summary>
		/// Clears the cached data
		/// </summary>
		protected void ClearData()
		{
			deferredData = null;
			forwardData = null;
			NotifyDataChanged();
		}

		public override RenderPathData GetRenderPathData(RenderingPath path)
		{
			if (!enabled)
				return null;
			MeshData[] meshData;
			RendererData[] rendererData;
			switch (path)
			{
				case RenderingPath.DeferredShading:
					if (deferredData == null)
					{
						GetDeferredData(out meshData, out rendererData);
						deferredData = new RenderPathData(meshData, rendererData, RequireDepthTexture);
					}
					return deferredData;
				case RenderingPath.Forward:
					if (forwardData == null)
					{
						GetForwardData(out meshData, out rendererData);
						forwardData = new RenderPathData(meshData, rendererData, RequireDepthTexture);
					}
					return forwardData;
			}
			return null;
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			if(projector == null)
				throw new ArgumentNullException(nameof(projector));
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			if(ScreenSpace ? submesh >= 0 : (submesh < 0 || submesh >= Mesh.subMeshCount))
				throw new ArgumentOutOfRangeException(nameof(submesh));
			return null;
		}
	}
}
