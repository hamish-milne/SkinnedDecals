using System;
using System.Collections.Generic;
using System.Reflection;

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

		private static readonly Func<UnityEngine.Object, bool> isNativeObjectAlive =
			(Func < UnityEngine.Object, bool>)
			Delegate.CreateDelegate(typeof(Func<UnityEngine.Object, bool>),
				typeof(UnityEngine.Object)
					.GetMethod("IsNativeObjectAlive", BindingFlags.NonPublic | BindingFlags.Static), true);
			

		public static bool Exists(this UnityEngine.Object obj)
		{
			// ReSharper disable once RedundantCast.0
			if ((object) obj == null) return false;
			return isNativeObjectAlive(obj);
		}
	}
}
