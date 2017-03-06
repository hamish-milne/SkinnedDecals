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
	[RequireComponent(typeof(MeshRenderer))]
	[RendererType(typeof(MeshRenderer))]
	public class DecalFixedObject : DecalObjectBase
	{
		private static readonly string[] modes = {FixedSingle, Fixed4, Fixed8};

		public override string[] RequiredModes => modes;

		public override Renderer Renderer => MeshRenderer;

		public override Bounds? Bounds => MeshRenderer.bounds;

		private Mesh mesh;
		private MeshRenderer meshRenderer;

		public override Mesh Mesh =>
			mesh != null ? mesh : (mesh = GetComponent<MeshFilter>().sharedMesh);

		public MeshRenderer MeshRenderer =>
			meshRenderer != null ? meshRenderer : (meshRenderer = GetComponent<MeshRenderer>());

		public override bool ScreenSpace => false;

		[SerializeField]
		protected List<FixedInstance> instances = new List<FixedInstance>();

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

		protected class FixedDraw : IDecalDraw
		{
			public virtual bool Enabled => true;

			public const int MaxInstances = 8;
			public int Submesh { get; set; }
			public DecalMaterial DecalMaterial { get; set; }

			public List<FixedInstance> Instances { get; } = new List<FixedInstance>();
			public DecalObject DecalObject { get; }

			private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
			private Material mat;
			private readonly List<Matrix4x4> matrixList = new List<Matrix4x4>();

			public void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh, ref Renderer renderer, ref int submesh, ref Material material, ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix, List<KeyValuePair<string, ComputeBuffer>> buffers)
			{
				mesh = DecalObject.Mesh;
				submesh = Submesh;
				material = mat;
				propertyBlock = block;
				matrix = DecalObject.transform.localToWorldMatrix;
			}

			public void UpdateMaterial()
			{
				var mode = FixedSingle;
				if (Instances.Count > 1)
					mode = Fixed4;
				if (Instances.Count > 4)
					mode = Fixed8;
				mat = DecalMaterial?.GetMaterial(mode);
				block.Clear();
				if (mode == FixedSingle)
					block.SetMatrix(ProjectorSingle, Instances[0].matrix.inverse);
				else
				{
					matrixList.Clear();
					foreach(var o in Instances)
						matrixList.Add(o.matrix.inverse);
					block.SetMatrixArray(ProjectorMulti, matrixList);
				}
			}

			public FixedDraw(DecalObject obj)
			{
				DecalObject = obj;
			}
		}

		private readonly List<FixedDraw> drawGroups = new List<FixedDraw>();

		public override IEnumerable<IDecalDraw> GetDrawsUncached()
		{
			foreach(var g in drawGroups)
				g.Instances.Clear();
			foreach (var o in instances)
			{
				if (!o.Enabled || o.DecalMaterial == null) continue;
				FixedDraw group = null;
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var g in drawGroups)
				{
					if (g.Submesh != o.submesh ||
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
