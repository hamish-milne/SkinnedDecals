using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Like <c>Material</c>, but for decals. It can switch between shaders depending on the
	/// required mode, and caches material instances.
	/// </summary>
	/// <remarks>
	/// See <c>DecalMaterialStandard</c> for guidance on how to inherit from this class
	/// </remarks>
	public abstract class DecalMaterial : ScriptableObject
	{
		// The following code generates methods that update Materials and MaterialPropertyBlocks
		#region PropertyActions cache

		class PropertyActions
		{
			public Action<DecalMaterial, Material> materialAction;
			public Action<DecalMaterial, MaterialPropertyBlock> propertyBlockAction;
		}

		private static readonly Dictionary<Type, string> methodNames
			= new Dictionary<Type, string>
		{
			{typeof (Color),     nameof(Material.SetColor)},
			{typeof (float),     nameof(Material.SetFloat)},
			{typeof (int),       nameof(Material.SetFloat)},
			{typeof (Matrix4x4), nameof(Material.SetMatrix)},
			{typeof (Texture),   nameof(Material.SetTexture)},
			{typeof (Texture2D), nameof(Material.SetTexture)},
			{typeof (Texture3D), nameof(Material.SetTexture)},
			{typeof (Vector4),   nameof(Material.SetVector)},
		};

		private static readonly Dictionary<Type, PropertyActions[]> propertyActions
			= new Dictionary<Type, PropertyActions[]>();

		private static readonly object[] paramArray = new object[2];

		private static void InvokeSetMethod(MethodInfo setter, object target, object p1, object p2)
		{
			paramArray[0] = p1;
			paramArray[1] = p2;
			setter.Invoke(target, paramArray);
		}

		private static Action<DecalMaterial, T> GetRefreshMethod<T>(FieldInfo field, string name)
		{
			var setter = typeof(T)
				.GetMethods()
				.First(m => m.Name == methodNames[field.FieldType]
				&& m.GetParameters()[0].ParameterType == typeof(string));
			return (dm, m) =>
			{
				var value = field.GetValue(dm);
				if (value == null || value is UnityEngine.Object && value.Equals(null))
					return;
				InvokeSetMethod(setter, m, name, value);
			};
		}

		private static void GetFields(Type type, out FieldInfo[] fields, out MaterialPropertyAttribute[] attrs)
		{
			fields = type
				.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(fi => methodNames.ContainsKey(fi.FieldType)).ToArray();
			attrs = fields
				.Select(fi => Attribute.GetCustomAttribute(fi, typeof(MaterialPropertyAttribute)))
				.Cast<MaterialPropertyAttribute>()
				.ToArray();
		}

		private static PropertyActions[] GetPropertyActions(Type type)
		{
			if (propertyActions.TryGetValue(type, out PropertyActions[] ret))
				return ret;
			GetFields(type, out FieldInfo[] fields, out MaterialPropertyAttribute[] attrs);
			var list = new List<PropertyActions>(fields.Length);
			for (int i = 0; i < fields.Length; i++)
			{
				if(attrs[i] == null) continue;
				list.Add(new PropertyActions
				{
					materialAction = GetRefreshMethod<Material>(fields[i], attrs[i].PropertyName),
					propertyBlockAction = GetRefreshMethod<MaterialPropertyBlock>(fields[i], attrs[i].PropertyName)
				});
			}
			ret = list.ToArray();
			propertyActions.Add(type, ret);
			return ret;
		}

		private Dictionary<string, string> propertyMap;

		/// <summary>
		/// Gets the backing field for the given property name on this instance
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public virtual string GetFieldForProperty(string propertyName)
		{
			if (propertyMap == null)
			{
				GetFields(GetType(), out FieldInfo[] fields, out MaterialPropertyAttribute[] attrs);
				propertyMap = Enumerable.Range(0, fields.Length)
					.ToDictionary(i => attrs[i].PropertyName, i => fields[i].Name);
			}
			propertyMap.TryGetValue(propertyName, out string fieldName);
			return fieldName;
		}

		#endregion

		/// <summary>
		/// Apply this to fields to use them as material properties
		/// </summary>
		[AttributeUsage(AttributeTargets.Field)]
		public class MaterialPropertyAttribute : Attribute
		{
			/// <summary>
			/// The material property name
			/// </summary>
			public string PropertyName { get; set; }

			public MaterialPropertyAttribute(string propertyName)
			{
				PropertyName = propertyName;
			}
		}

		/// <summary>
		/// A property block set to the current properties in this material
		/// </summary>
		public MaterialPropertyBlock Properties { get; private set; }

		protected virtual void OnEnable()
		{
			Refresh();
		}

		/// <summary>
		/// Notifies the material that one of its properties changed
		/// </summary>
		public virtual void Refresh()
		{
			if (Properties == null)
				Properties = new MaterialPropertyBlock();
			else
				Properties.Clear();
			CopyTo(Properties);
			if (defaultMaterial != null)
			{
				SetKeywords(defaultMaterial);
				CopyTo(defaultMaterial);
			}
			foreach (var mat in materialCache.Values.Where(mat => mat != null))
			{
				SetKeywords(mat);
				CopyTo(mat);
			}
		}

		/// <summary>
		/// Applies this material's properties to another property block, without clearing
		/// </summary>
		/// <param name="properties"></param>
		public virtual void CopyTo(MaterialPropertyBlock properties)
		{
			foreach (var prop in GetPropertyActions(GetType()))
				prop.propertyBlockAction(this, properties);
		}

		/// <summary>
		/// Applies this material's properties to a <c>Material</c> instance
		/// </summary>
		/// <param name="material"></param>
		public virtual void CopyTo(Material material)
		{
			foreach (var prop in GetPropertyActions(GetType()))
				prop.materialAction(this, material);
		}

		[Serializable]
		protected struct MaterialCacheKey : IEquatable<MaterialCacheKey>
		{
			public string modeKeyword;
			public string propertyName;
			public int value;

			// Explicitly define comparison methods to make dictionary usage faster
			public bool Equals(MaterialCacheKey other)
			{
				return other.modeKeyword == modeKeyword
				       && other.propertyName == propertyName
				       && other.value == value;
			}

			public override int GetHashCode()
			{
				// ReSharper disable NonReadonlyMemberInGetHashCode
				return (modeKeyword?.GetHashCode() ?? 0)*23 ^ (propertyName?.GetHashCode() ?? 0)*17 ^ value;
				// ReSharper restore NonReadonlyMemberInGetHashCode
			}
		}

		[Serializable]
		protected struct MaterialCacheEntry
		{
			public MaterialCacheKey key;
			public Material value;
		}

		// Keeping a persistent cache allows material identity to persist through environment re-loading,
		// and through multiple sessions if the material instance is saved to disk
		protected List<MaterialCacheEntry> persistentCache = new List<MaterialCacheEntry>();

		private Material PersistentCacheGet(MaterialCacheKey key)
		{
			foreach(var pair in persistentCache)
				if (pair.key.Equals(key) && pair.value != null)
					return pair.value;
			return null;
		}

		private void PersistentCacheSet(MaterialCacheKey key, Material value)
		{
			for(int i = 0; i < persistentCache.Count; i++)
				if (persistentCache[i].key.Equals(key))
				{
					persistentCache[i] = new MaterialCacheEntry { key = key, value = value };
					return;
				}
			// Cleanup null entries
			persistentCache = persistentCache.Where(pair => pair.value != null).ToList();
			persistentCache.Add(new MaterialCacheEntry { key = key, value = value });
		}

		private readonly Dictionary<MaterialCacheKey, Material> materialCache
			= new Dictionary<MaterialCacheKey, Material>();

		/// <summary>
		/// Stores the default (no mode keyword) material here, so the reference can be preserved.
		/// </summary>
		/// <remarks>The default material is used to render baked decals, and so needs to
		/// be saved as an asset. This field allows us to reference that asset and reduce
		/// the object count slightly.</remarks>
		[SerializeField]
		protected Material defaultMaterial;

		/// <summary>
		/// Creates a new <c>Material</c> that can be used to render the decal
		/// </summary>
		/// <param name="modeString">A keyword that generally defines how the decal position
		/// is read. This needs to be agreed between the decal renderer and shader code.</param>
		/// <returns>A new material with the correct shader and keywords set, or <c>null</c>
		/// if the given mode is not supported.</returns>
		public virtual Material CreateMaterial(string modeString)
		{
			var shader = GetShaderForMode(modeString);
			if(shader == null)
				throw new Exception($"Mode '{modeString}' not supported!");
			var mat = new Material(shader) {name = name + (modeString == "" ? "Default" : modeString)};
			mat.EnableKeyword(modeString);
			SetKeywords(mat);
			CopyTo(mat);
			return mat;
		}

		/// <summary>
		/// Returns a cached <c>Material</c> that can be used to render the decal.
		/// This overload also caches based on a provided integer property; this is useful for culling, blend modes etc.
		/// </summary>
		/// <param name="modeString">A keyword that generally defines how the decal position
		/// is read. This needs to be agreed between the decal renderer and shader code.</param>
		/// <param name="propertyName">The integer property name.</param>
		/// <param name="value">The integer property value</param>
		/// <returns>A cached material with the correct shader and keywords set, or <c>null</c>
		/// if the given mode is not supported.</returns>
		public virtual Material GetMaterial(string modeString, string propertyName, int value)
		{
			if (string.IsNullOrEmpty(modeString))
				return defaultMaterial != null ? defaultMaterial : (defaultMaterial
					= CreateMaterial(""));
			if (string.IsNullOrEmpty(propertyName))
			{
				propertyName = null;
				value = 0;
			}
			var key = new MaterialCacheKey {modeKeyword = modeString, propertyName = propertyName, value = value};
			materialCache.TryGetValue(key, out Material mat);
			if (mat != null && !mat.IsKeywordEnabled(modeString))
			{
				Debug.LogError($"Decal material keyword was modified! Expected {modeString}", mat);
				mat = null;
			}
			if (mat == null)
			{
				mat = PersistentCacheGet(key);
				if (mat != null)
					materialCache[key] = mat;
			}
			if (mat == null)
			{
				materialCache[key] = mat = CreateMaterial(modeString);
				if(propertyName != null)
					mat.SetInt(propertyName, value);
				PersistentCacheSet(key, mat);
			}
			return mat;
		}

		/// <summary>
		/// Returns a cached <c>Material</c> that can be used to render the decal.
		/// </summary>
		/// <param name="modeString">A keyword that generally defines how the decal position
		/// is read. This needs to be agreed between the decal renderer and shader code.</param>
		/// <returns>A cached material with the correct shader and keywords set, or <c>null</c>
		/// if the given mode is not supported.</returns>
		public Material GetMaterial(string modeString)
		{
			return GetMaterial(modeString, null, default(int));
		}

		/// <summary>
		/// Gets the keywords needed to display the decal with the current properties
		/// </summary>
		/// <param name="addKeyword">Called to add a keyword</param>
		/// <param name="removeKeyword">Called to remove a keyword</param>
		public abstract void SetKeywords(Action<string> addKeyword, Action<string> removeKeyword);

		/// <summary>
		/// Sets the required keywords on a material instance
		/// </summary>
		/// <param name="m"></param>
		public void SetKeywords(Material m)
		{
			SetKeywords(m.EnableKeyword, m.DisableKeyword);
		}

		/// <summary>
		/// Checks whether the given drawing mode is supported for the current platform
		/// </summary>
		/// <remarks>This is usually called by decal objects looking for the most efficient
		/// way to generate the required geometry.</remarks>
		/// <param name="mode"></param>
		/// <returns></returns>
		public virtual bool IsModeSupported(string mode)
		{
			return GetShaderForMode(mode)?.isSupported ?? false;
		}

		/// <summary>
		/// This allows the decal to provide a modified material for specific render paths.
		/// The modified instance should be cached and updated by the derived class.
		/// </summary>
		/// <param name="m"></param>
		/// <param name="rp"></param>
		/// <returns></returns>
		public virtual Material ModifyMaterial(Material m, RenderingPath rp)
		{
			return m;
		}

		public abstract Shader GetShaderForMode(string mode);

		/// <summary>
		/// For some rendering paths (i.e. deferred), we can run specific shader passes without requiring
		/// properties set per-pass. This function retrieves those pass indices, in order, or <c>null</c>
		/// if this isn't known (i.e. for forward)
		/// </summary>
		/// <param name="renderingPath"></param>
		/// <returns></returns>
		public virtual int[] GetKnownPasses(RenderingPath renderingPath)
		{
			return null;
		}

		/// <summary>
		/// Checks whether the given material instance requires a camera depth texture to function
		/// </summary>
		/// <param name="mat"></param>
		/// <returns></returns>
		public abstract bool RequiresDepthTexture(Material mat);

		/// <summary>
		/// Check whether we can merge multiple instances of this decal into a single draw call
		/// </summary>
		/// <returns></returns>
		public abstract bool AllowMerge();
	}
}
