using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DecalSystem
{
	public static class Util
	{
		public static IEnumerable<Type> GetAllTypes()
		{
			foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try
				{
					types = a.GetExportedTypes();
				}
				catch
				{
					continue;
				}
				foreach (var t in types)
				{
					yield return t;
				}
			}
		}

		public static Dictionary<TKey, TValue> ToDictionaryPermissive<TSource, TKey, TValue>(
			this IEnumerable<TSource> source,
			Func<TSource, TKey> keySelector,
			Func<TSource, TValue> valueSelector)
		{
			var ret = new Dictionary<TKey, TValue>();
			foreach (var e in source)
			{
				var key = keySelector(e);
				if (key == null) continue;
				ret[key] = valueSelector(e);
			}
			return ret;
		}

		public static T GetDelegate<T>(Type type, string method, bool isStatic) where T : class
		{
			var binding = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
			return (T)(object)Delegate.CreateDelegate(typeof(T), type.GetMethod(method, binding), false);
		}

		private static void Nop<T>(this T o) { }

		private static readonly Func<UnityEngine.Object, bool> isNativeObjectAlive =
			GetDelegate<Func<UnityEngine.Object, bool>>(typeof(UnityEngine.Object), "IsNativeObjectAlive", true);	

		public static bool Exists(this UnityEngine.Object obj)
		{
			// ReSharper disable once RedundantCast.0
			if ((object) obj == null) return false;
			if(isNativeObjectAlive != null)
				return isNativeObjectAlive(obj);
			try
			{
				// ReSharper disable once UnusedVariable
				var t = obj.hideFlags;
			}
			catch
			{
				return false;
			}
			return true;
		}

		private static readonly Action<Plane[], Matrix4x4> extractPlanes =
			GetDelegate<Action<Plane[], Matrix4x4>>(typeof(GeometryUtility), "Internal_ExtractPlanes", true);

		public static void ExtractPlanes(Plane[] planes, Camera camera)
		{
			if (extractPlanes != null)
				extractPlanes(planes, camera.projectionMatrix * camera.worldToCameraMatrix);
			else
				GeometryUtility.CalculateFrustumPlanes(camera).CopyTo(planes, 0);
		}

		/// <summary>
		/// Gets the world AABB for a unit cube in the given transform. This is faster than the matrix method
		/// </summary>
		/// <param name="transform"></param>
		/// <returns></returns>
		public static Bounds UnitBounds(Transform transform)
		{
			var pos = transform.position;
			var d1 = transform.TransformPoint(1, 1, 1) - pos;
			var ext = new Vector3(
				Math.Abs(Vector3.Dot(d1, new Vector3(1, 0, 0))),
				Math.Abs(Vector3.Dot(d1, new Vector3(0, 1, 0))),
				Math.Abs(Vector3.Dot(d1, new Vector3(0, 0, 1))));
			return new Bounds(transform.position, ext * 2);
		}

		/// <summary>
		/// Gets the world AABB for a unit cube in the given transform matrix. This is slower than the transform method
		/// </summary>
		/// <param name="localToWorld"></param>
		/// <returns></returns>
		public static Bounds UnitBounds(Matrix4x4 localToWorld)
		{
			var pos = (Vector3) localToWorld.GetColumn(3);
			var d1 = (Vector3)(localToWorld * Vector4.one) - pos;
			var ext = new Vector3(
				Math.Abs(Vector3.Dot(d1, new Vector3(1, 0, 0))),
				Math.Abs(Vector3.Dot(d1, new Vector3(0, 1, 0))),
				Math.Abs(Vector3.Dot(d1, new Vector3(0, 0, 1))));
			return new Bounds(pos, ext * 2);
		}
	}
}
