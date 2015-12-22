using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkinnedDecals
{
	[AttributeUsage(AttributeTargets.Class)]
	public class DecalModeAttribute : Attribute
	{
	}

	public class DecalManager : MonoBehaviour
	{
		private static DecalMode[] modes;

		[SerializeField]
		protected bool allowExpensiveModes;

		[SerializeField]
		protected List<Camera> cameras = new List<Camera>();

		public bool AllowExpensiveModes => allowExpensiveModes;

		public List<Camera> Cameras => cameras;
		
		protected static void RebuildModeList()
		{
			var list = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => a.GetTypes())
				.Where(t => t.IsSubclassOf(typeof(DecalMode)))
				.Where(t => Attribute.IsDefined(t, typeof(DecalModeAttribute)))
				.Select(t => (DecalMode)Activator.CreateInstance(t))
				.ToArray();
			Array.Sort(list, (a, b) => a.Order.CompareTo(b.Order));
			modes = list;
		}

		static DecalManager()
		{
			try
			{
				RebuildModeList();
			} catch(Exception e)
			{
				Debug.LogException(e);
				Debug.LogError("Error when building decal mode list.");
			}
		}

		public virtual DecalInstance CreateDecal(Camera camera, DecalProjector projector, Renderer renderer)
		{
			if (camera == null)
				throw new ArgumentNullException(nameof(camera));
			return modes?.Select(m => m.Create(projector, camera, renderer)).FirstOrDefault();
		}

		public Mesh cube;

		public static Mesh GetCubeMesh()
		{
			return FindObjectOfType<DecalManager>().cube;
		}
	}
}
