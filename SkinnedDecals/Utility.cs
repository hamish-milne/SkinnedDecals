﻿using UnityEngine;
using System;
using System.Collections.Generic;

namespace SkinnedDecals
{
	public static class Utility
	{
		public static void SetTextureKeyword(this Material material, string property,
			string keyword, Texture texture)
		{
			material.SetTexture(property, texture);
			if(texture)
				material.EnableKeyword(keyword);
			else
				material.DisableKeyword(keyword);
		}

		public static T GetOrAdd<T>(this Component obj) where T : Component
		{
			var ret = obj.GetComponent<T>();
			// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
			if (ret == null)
				ret = obj.gameObject.AddComponent<T>();
			return ret;
		}

		public static T GetOrAddInParent<T>(this Component obj) where T : Component
		{
			var ret = obj.GetComponentInParent<T>();
			// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
			if (ret == null)
				ret = obj.gameObject.AddComponent<T>();
			return ret;
		}
		
		public static bool CheckList<T>(this List<T> list, T item, ListMode mode)
		{
			if(list == null)
				throw new ArgumentNullException(nameof(list));
			switch(mode)
			{
				case ListMode.None:
					return true;
				case ListMode.Blacklist:
					return !list.Contains(item);
				case ListMode.Whitelist:
					return list.Contains(item);
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
			}
		}
	}
}