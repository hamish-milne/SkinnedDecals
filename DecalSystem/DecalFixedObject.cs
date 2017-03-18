using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
	/// <summary>
	/// Renders decals on a fixed <c>MeshRenderer</c>
	/// </summary>
	[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
	[RendererType(typeof(MeshRenderer))]
	public class DecalFixedObject : DecalObjectBase
	{

		public override Renderer Renderer => MeshRenderer;
		public override Bounds Bounds => MeshRenderer.bounds;
		public override Mesh Mesh => MeshFilter.sharedMesh;

		public MeshFilter MeshFilter { get; private set; }
		public MeshRenderer MeshRenderer { get; private set; }

		protected override void OnEnable()
		{
			base.OnEnable();
			MeshFilter = GetComponent<MeshFilter>();
			MeshRenderer = GetComponent<MeshRenderer>();
		}

		/// <summary>
		/// The list of instances
		/// </summary>
		[SerializeField]
		protected List<FixedInstance> instances = new List<FixedInstance>();

		/// <summary>
		/// A decal instance with a local matrix and submesh index
		/// </summary>
		[Serializable]
		protected class FixedInstance : DecalInstance
		{
			[NonSerialized]
			public DecalFixedObject obj;
			public Matrix4x4 matrix;
			public int submesh;

			public override bool Enabled
			{
				get { return base.Enabled; }
				set { base.Enabled = value; obj.ClearData(); }
			}

			public override DecalMaterial DecalMaterial
			{
				get { return base.DecalMaterial; }
				set { base.DecalMaterial = value; obj.ClearData(); }
			}

			public override DecalObject DecalObject => obj;

			public FixedInstance(DecalFixedObject obj, DecalMaterial decalMaterial, int submesh, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decalMaterial = decalMaterial;
				this.matrix = matrix;
				this.submesh = submesh;
			}
		}

		/// <summary>
		/// A group of Fixed decal instances. The shader supports 1, 4 or 8 decals drawn at once
		/// </summary>
		/// <remarks>
		/// This uses a property block to provide the array of matrices to the shader
		/// </remarks>
		protected class FixedDraw : IDecalDraw
		{
			public virtual bool Enabled => true;

			public const int MaxInstances = 8;
			public int Submesh { get; set; }
			public DecalMaterial DecalMaterial { get; set; }

			public List<FixedInstance> Instances { get; } = new List<FixedInstance>();
			public DecalObject DecalObject { get; }
			
			private Material mat;
			private readonly List<Matrix4x4> matrixList = new List<Matrix4x4>();

			public void GetDrawCommand(DecalCamera dcam, ref Mesh mesh, ref Renderer renderer,
				ref int submesh, ref Material material, ref Matrix4x4 matrix)
			{
				mesh = DecalObject.Mesh;
				submesh = Submesh;
				material = mat;
				matrix = DecalObject.transform.localToWorldMatrix;
			}

			/// <summary>
			/// Updates the material instance and property block
			/// </summary>
			public void UpdateMaterial()
			{
				var mode = FixedSingle;
				if (Instances.Count > 1)
					mode = Fixed4;
				if (Instances.Count > 4)
					mode = Fixed8;
				mat = DecalMaterial?.GetMaterial(mode);
				foreach(var o in Instances)
					matrixList.Add(o.matrix.inverse);
			}

			public void AddShaderProperties(IShaderProperties shaderProperties)
			{
				if(matrixList.Count == 0)
					shaderProperties.Add(ProjectorSingle, matrixList[0]);
				else
					shaderProperties.Add(ProjectorMulti, matrixList);
			}

			public FixedDraw(DecalObject obj)
			{
				DecalObject = obj;
			}
		}

		// Cached list of draw groups
		private readonly List<FixedDraw> drawGroups = new List<FixedDraw>();

		public override IEnumerable<IDecalDraw> GetDrawsUncached()
		{
			foreach(var g in drawGroups)
				g.Instances.Clear();
			// Groups instances together in FixedDraws
			foreach (var o in instances)
			{
				if (!o.Enabled || o.DecalMaterial == null) continue;
				FixedDraw group = null;
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var g in drawGroups)
				{
					if (!g.DecalMaterial.AllowMerge() ||
						g.Submesh != o.submesh ||
						g.DecalMaterial != o.DecalMaterial ||
						g.Instances.Count >= FixedDraw.MaxInstances)
						continue;
					group = g;
					break;
				}
				if(group == null)
					drawGroups.Add(group = new FixedDraw(this) {Submesh = o.submesh, DecalMaterial = o.DecalMaterial});
				group.Instances.Add(o);
			}
			drawGroups.RemoveAll(o => o.Instances.Count == 0);
			foreach(var g in drawGroups)
				g.UpdateMaterial();
			return drawGroups.Cast<IDecalDraw>();
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			var matrix = transform.worldToLocalMatrix * projector.localToWorldMatrix;
			var ret = new FixedInstance(this, decal, submesh, matrix);
			instances.Add(ret);
			return ret;
		}

		public override int Count => instances.Count;
		public override DecalInstance GetDecal(int index)
		{
			return instances[index];
		}

		public override bool RemoveDecal(DecalInstance instance)
		{
			return instances.Remove(instance as FixedInstance);
		}

		public override void UpdateBackRefs()
		{
			foreach (var o in instances)
				o.obj = this;
		}
	}
}
