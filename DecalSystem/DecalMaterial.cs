using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Like <c>Material</c>, but for decals. It can switch between shaders depending on the
	/// required mode.
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
			{typeof (Color), "SetColor"},
			{typeof (float), "SetFloat"},
			{typeof (Matrix4x4), "SetMatrix"},
			{typeof (Texture), "SetTexture"},
			{typeof (Texture2D), "SetTexture"},
			{typeof (Texture3D), "SetTexture"},
			{typeof (Vector4), "SetVector"},
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

		static Action<DecalMaterial, T> GetRefreshMethod<T>(FieldInfo field, string name)
		{
			var setter = typeof(T)
				.GetMethods()
				.First(m => m.Name == methodNames[field.FieldType]
				&& m.GetParameters()[0].ParameterType == typeof(string));
			return (dm, m) =>
			{
				var value = field.GetValue(dm);
				if (value is UnityEngine.Object && value.Equals(null))
					return;
				InvokeSetMethod(setter, m, name, value);
			};
		}

		static void GetFields(Type type, out FieldInfo[] fields, out MaterialPropertyAttribute[] attrs)
		{
			fields = type
				.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(fi => methodNames.ContainsKey(fi.FieldType)).ToArray();
			attrs = fields
				.Select(fi => Attribute.GetCustomAttribute(fi, typeof(MaterialPropertyAttribute)))
				.Cast<MaterialPropertyAttribute>()
				.ToArray();
		}

		static PropertyActions[] GetPropertyActions(Type type)
		{
			PropertyActions[] ret;
			if (propertyActions.TryGetValue(type, out ret))
				return ret;
			FieldInfo[] fields;
			MaterialPropertyAttribute[] attrs;
			GetFields(type, out fields, out attrs);
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

		public string GetFieldForProperty(string propertyName)
		{
			if (propertyMap == null)
			{
				FieldInfo[] fields;
				MaterialPropertyAttribute[] attrs;
				GetFields(GetType(), out fields, out attrs);
				propertyMap = Enumerable.Range(0, fields.Length)
					.ToDictionary(i => attrs[i].PropertyName, i => fields[i].Name);
			}
			string fieldName;
			propertyMap.TryGetValue(propertyName, out fieldName);
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
		}

		/// <summary>
		/// Applies this material's properties to another property block
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

		// TODO: Rework material cache

		/// <summary>
		/// Returns a cached <c>Material</c> that can be used to render the decal
		/// </summary>
		/// <param name="modeString">A keyword that generally defines how the decal position
		/// is read. This needs to be agreed between the decal renderer and shader code.</param>
		/// <returns>A cached material with the correct shader and keywords set, or <c>null</c>
		/// if the given mode is not supported. It does *not* have any decal-specific properties
		/// set; use <c>Properties</c> or instantiate the material and use <c>CopyTo</c>.</returns>
		public abstract Material GetMaterial(string modeString);

		/// <summary>
		/// Gets the keywords needed to display the decal with the current properties
		/// </summary>
		/// <param name="addKeyword">Called to add a keyword</param>
		/// <param name="removeKeyword">Called to remove a keyword</param>
		public abstract void SetKeywords(Action<string> addKeyword, Action<string> removeKeyword);

		public List<string> GetKeywords()
		{
			var list = new List<string>();
			SetKeywords(list.Add, kw => list.Remove(kw));
			return list;
		}

		public void SetKeywords(Material m)
		{
			SetKeywords(m.EnableKeyword, m.DisableKeyword);
		}

		private static readonly Dictionary<string, Material> materialCache
			= new Dictionary<string, Material>();

		/// <summary>
		/// Gets a cached material with the given shader and keywords
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="keywords"></param>
		/// <returns></returns>
		protected static Material GetMaterial(Shader shader, string[] keywords)
		{
			if(shader == null)
				throw new ArgumentNullException(nameof(shader));
			if(keywords == null)
				throw new ArgumentNullException(nameof(keywords));
			Array.Sort(keywords);
			var key = shader.name + "+" + string.Join("|", keywords);
			Material ret;
			materialCache.TryGetValue(key, out ret);
			if (ret != null) return ret;
			ret = new Material(shader);
			foreach(var kw in keywords)
				ret.EnableKeyword(kw);
			materialCache[key] = ret;
			return ret;
		}

		public abstract Shader GetShaderForMode(string mode);
	}
}
