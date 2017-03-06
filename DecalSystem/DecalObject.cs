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

	public class RefreshOnChangeAttribute : PropertyAttribute
	{
		public RefreshAction RefreshAction { get; set; }

		public RefreshOnChangeAttribute(RefreshAction refreshAction)
		{
			RefreshAction = refreshAction;
		}
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
		public DecalInstance instance;

		/// <summary>
		/// The mesh to draw
		/// </summary>
		public Mesh mesh;

		public Renderer renderer;

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
		public Matrix4x4? matrix;

		/// <summary>
		/// The material to use
		/// </summary>
		public Material material;

		/// <summary>
		/// Extra material properties
		/// </summary>
		public MaterialPropertyBlock materialPropertyBlock;

		public Matrix4x4? GetFinalMatrix()
		{
			Matrix4x4 m;
			if (transform != null && matrix != null)
				m = transform.localToWorldMatrix * matrix.Value;
			else if (transform != null)
				m = transform.localToWorldMatrix;
			else if (matrix != null)
				m = matrix.Value;
			else return null;
			return m;
		}
	}

	public interface IDecalDraw
	{
		bool Enabled { get; }
		DecalObject DecalObject { get; }
		DecalMaterial DecalMaterial { get; }
		void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh, ref Renderer renderer, ref int submesh, ref Material material, ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix, List<KeyValuePair<string, ComputeBuffer>> buffers);
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
	
	/// <summary>
	/// Represents one (or more) decal instances
	/// </summary>
	[Serializable]
	public abstract class DecalInstance
	{
		[SerializeField, UseProperty]
		protected bool enabled = true;
		[SerializeField, UseProperty]
		protected DecalMaterial decalMaterial;

		/// <summary>
		/// Whether to draw this decal or not
		/// </summary>
		public virtual bool Enabled
		{
			get { return enabled; }
			set { enabled = value; }
		}

		/// <summary>
		/// The parent <c>DecalObject</c>
		/// </summary>
		public abstract DecalObject DecalObject { get; }

		/// <summary>
		/// The <c>DecalMaterial</c> to use
		/// </summary>
		public virtual DecalMaterial DecalMaterial
		{
			get { return decalMaterial; }
			set { decalMaterial = value; }
		}
	}

	[Serializable]
	public abstract class DecalInstanceBase : DecalInstance, IDecalDraw
	{
		public abstract Matrix4x4? LocalMatrix { get; }
		
		public abstract string ModeString { get; }

		protected Matrix4x4 DefaultMatrix()
		{
			Matrix4x4 m;
			var transform = DecalObject?.transform;
			var matrix = LocalMatrix;
			if (transform != null && matrix != null)
				m = transform.localToWorldMatrix * matrix.Value;
			else if (transform != null)
				m = transform.localToWorldMatrix;
			else if (matrix != null)
				m = matrix.Value;
			else
				throw new Exception("No matrix or transform defined");
			return m;
		}

		public virtual void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh, ref Renderer renderer, ref int submesh, ref Material material, ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix, List<KeyValuePair<string, ComputeBuffer>> buffers)
		{
			matrix = DefaultMatrix();
			material = DecalMaterial?.GetMaterial(ModeString);
		}
	}

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

		private static readonly Dictionary<Type, Type> rendererTypeMap = Util.GetAllTypes()
			.Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DecalObject)))
			.ToDictionaryPermissive(
				t => ((RendererTypeAttribute) Attribute.GetCustomAttribute(t, typeof(RendererTypeAttribute)))?.Type,
				t => t);

		public static DecalObject GetOrCreate(GameObject obj)
		{
			var ret = obj.GetComponent<DecalObject>();
			if (ret != null) return ret;
			foreach (var c in obj.GetComponents(typeof(Component)))
			{
				Type doType;
				if (rendererTypeMap.TryGetValue(c.GetType(), out doType))
					return (DecalObject) obj.AddComponent(doType);
			}
			return (DecalObject) obj.AddComponent(rendererTypeMap[typeof(object)]);
		}


		public abstract string[] RequiredModes { get; }

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
		/// The world-space bounds of the object. If this is <c>null</c>, new decals cannot be added
		/// </summary>
		public abstract Bounds? Bounds { get; }

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

		public abstract int Count { get; }

		public abstract DecalInstance GetDecal(int index);

		public virtual bool RemoveDecal(int index)
		{
			return RemoveDecal(GetDecal(index));
		}

		public abstract bool RemoveDecal(DecalInstance instance);

		/// <summary>
		/// Gets the collection of draw commands for the given rendering path
		/// </summary>
		/// <returns>The rendering data, cached at this level, or <c>null</c></returns>
		public abstract IDecalDraw[] GetDecalDraws();

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

		public abstract void UpdateBackRefs();
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
		/// Whether to require <c>DecalManager.RenderObject</c> to be called for this object
		/// </summary>
		public override bool UseManualCulling => false;

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

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
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
	}
}
