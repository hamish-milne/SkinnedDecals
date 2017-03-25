using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Applied to a variable, this causes the editor to use use a property of the same name as the data source.
	/// Works at the type root and for <c>DecalInstance</c>s
	/// </summary>
	public class UsePropertyAttribute : PropertyAttribute
	{
	}

	/// <summary>
	/// Indicates that the below <c>DecalObject</c> should be created by default for the component type given
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class RendererTypeAttribute : Attribute
	{
		public Type Type { get; set; }

		public RendererTypeAttribute(Type type)
		{
			Type = type;
		}
	}

	/// <summary>
	/// An object that can receive decals.
	/// </summary>
	[ExecuteInEditMode]
	public abstract class DecalObject : MonoBehaviour
	{

		// Maps component types to their default renderer
		private static readonly Dictionary<Type, Type> rendererTypeMap = Util.GetAllTypes()
			.Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DecalObject)))
			.ToDictionaryPermissive(
				t => ((RendererTypeAttribute) Attribute.GetCustomAttribute(t, typeof(RendererTypeAttribute)))?.Type,
				t => t);

		/// <summary>
		/// Gets or creates a <c>DecalObject</c> for the given object
		/// </summary>
		/// <param name="obj"></param>
		/// <returns>Always a <c>DecalObject</c> component of the given object</returns>
		public static DecalObject GetOrCreate(GameObject obj)
		{
			var ret = obj.GetComponent<DecalObject>();
			if (ret != null) return ret;
			foreach (var c in obj.GetComponents(typeof(Component)))
			{
				if (rendererTypeMap.TryGetValue(c.GetType(), out var doType))
					return (DecalObject)obj.AddComponent(doType);
			}
			return (DecalObject) obj.AddComponent(rendererTypeMap[typeof(object)]);
		}

		// Global list of active objects, so we can easily access them in the camera
		private static List<DecalObject> activeObjects;
		private static ReadOnlyCollection<DecalObject> activeObjectsReadonly;

		private static void GetActiveObjects()
		{
			if (Application.isPlaying && activeObjects != null) return;
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
		/// The world-space bounds of the object
		/// </summary>
		public abstract Bounds Bounds { get; }

		/// <summary>
		/// If <c>false</c>, <c>AddDecal</c> and <c>RemoveDecal</c> are not supported
		/// </summary>
		public abstract bool CanAddDecals { get; }

		/// <summary>
		/// The renderer the object is attached to. Can be <c>null</c> for screen space decals.
		/// </summary>
		public abstract Renderer Renderer { get; }

		/// <summary>
		/// The renderer's shared materials, if any.
		/// </summary>
		public virtual Material[] Materials => Renderer?.sharedMaterials;

		/// <summary>
		/// If true, we need to manually test this object's bounds with the camera's frustum.
		/// This isn't the cheapest operation; ideally call <c>DecalCamera.RenderObject</c>
		/// </summary>
		public virtual bool ManualCulling => false;

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
		/// The number of decal instances, enabled or otherwise
		/// </summary>
		public abstract int Count { get; }

		/// <summary>
		/// Gets the decal instance at the given index
		/// </summary>
		/// <param name="index">A value between 0 and <c>Count</c></param>
		/// <returns>A non-null <c>DecalInstance</c></returns>
		public abstract DecalInstance GetDecal(int index);

		/// <summary>
		/// Removes the decal at the given index
		/// </summary>
		/// <param name="index">A value between 0 and <c>Count</c></param>
		public void RemoveDecal(int index)
		{
			RemoveDecal(GetDecal(index));
		}

		/// <summary>
		/// Removes the given decal
		/// </summary>
		/// <param name="instance">A decal instance, or <c>null</c></param>
		/// <returns>True if the decal was present and is now removed, otherwise false</returns>
		public abstract bool RemoveDecal(DecalInstance instance);

		/// <summary>
		/// Gets the collection of draw commands for the given rendering path
		/// </summary>
		/// <returns>The array of draw commands, or <c>null</c></returns>
		/// <remarks>This will never be called if the object is disabled</remarks>
		public abstract IDecalDraw[] GetDecalDraws();

		/// <summary>
		/// Adds a new decal to this instance
		/// </summary>
		/// <param name="projector">The projection transform</param>
		/// <param name="decal">The decal material</param>
		/// <param name="submesh">The submesh index</param>
		/// <param name="maxNormal"></param>
		/// <returns>The instance created or added to, or <c>null</c> if the box didn't intersect any geometry</returns>
		public abstract DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh, float maxNormal);

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
		/// Clears data for all active objects
		/// </summary>
		/// <param name="predicate">Filters the objects to refresh. Disable with <c>null</c></param>
		public static void RefreshAll(Predicate<DecalObject> predicate = null)
		{
			GetActiveObjects();
			for (int i = 0, count = activeObjects.Count; i < count; i++)
			{
				var obj = activeObjects[i];
				if(predicate == null || predicate(obj))
					obj.ClearData();
			}
		}

		/// <summary>
		/// Clears any cached data
		/// </summary>
		public abstract void ClearData();

		/// <summary>
		/// Called when an object becomes enabled/disabled
		/// </summary>
		public static event Action<DecalObject, bool> ObjectChangeState;

		protected virtual void OnEnable()
		{
			if (!Application.isPlaying)
				ObjectChangeState?.Invoke(this, false);
			else
			{
				GetActiveObjects();
				if (!activeObjects.Contains(this))
				{
					activeObjects.Add(this);
					ObjectChangeState?.Invoke(this, true);
				}
			}
		}

		protected virtual void OnDisable()
		{
			if(!Application.isPlaying)
				ObjectChangeState?.Invoke(this, false);
			else if (activeObjects != null)
			{
				if(activeObjects.Remove(this))
					ObjectChangeState?.Invoke(this, false);
			}
		}

		/// <summary>
		/// Updates non-serialized back-references between instances and the object
		/// </summary>
		public abstract void UpdateBackRefs();
	}

	/// <summary>
	/// A base class for all the standard <c>DecalObject</c> types
	/// </summary>
	/// <remarks>
	/// While <c>DecalObject</c> provides the broadest extensibility, <c>DecalObjectBase</c>
	/// provides common functionality for common use cases. In particular, the result of <c>GetDecalDraws</c>
	/// is cached, and automatic culling is implemented.
	/// </remarks>
	public abstract class DecalObjectBase : DecalObject
	{
		public override bool ScreenSpace => false;

		public override void ClearData()
		{
			drawArray = null;
		}

		private IDecalDraw[] drawArray;

		public override IDecalDraw[] GetDecalDraws()
		{
			if(Application.isEditor)
				UpdateBackRefs();
			return drawArray ?? (drawArray = GetDrawsUncached().ToArray());
		}

		public virtual IEnumerable<IDecalDraw> GetDrawsUncached()
		{
			return Enumerable.Range(0, Count)
				.Select(GetDecal)
				.OfType<IDecalDraw>();
		}

		public override bool CanAddDecals => true;

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh, float maxNormal)
		{
			if(projector == null)
				throw new ArgumentNullException(nameof(projector));
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			if(ScreenSpace ? submesh >= 0 : (submesh < 0 || submesh >= Mesh.subMeshCount))
				throw new ArgumentOutOfRangeException(nameof(submesh));
			ClearData();
			return null;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			ClearData();
			UpdateBackRefs();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			ClearData();
		}

		protected virtual void OnWillRenderObject()
		{
			if (ManualCulling) return;
			var dcam = Camera.current.GetComponent<DecalCamera>();
			if (dcam != null)
				dcam.RenderObject(this);
		}
	}
}
