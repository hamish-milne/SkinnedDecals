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
	public class DecalFixedObject : DecalObjectBase
	{
		private static readonly string[] modes = {FixedSingle, Fixed4, Fixed8};

		public override string[] RequiredModes => modes;

		public override Renderer Renderer => MeshRenderer;

		public override Bounds Bounds => MeshRenderer.bounds;

		private Mesh mesh;
		private MeshRenderer meshRenderer;

		public override Mesh Mesh =>
			mesh != null ? mesh : (mesh = GetComponent<MeshFilter>().sharedMesh);

		public MeshRenderer MeshRenderer =>
			meshRenderer != null ? meshRenderer : (meshRenderer = GetComponent<MeshRenderer>());

		public override bool ScreenSpace => false;

		[Serializable]
		protected class FixedChannel : DecalInstance
		{
			[SerializeField] protected DecalFixedObject obj;
			[SerializeField] protected Material material;
			[SerializeField] protected int submesh;
			protected MaterialPropertyBlock properties;
			[SerializeField] protected List<Matrix4x4> matrices = new List<Matrix4x4>(8);

			public override DecalObject DecalObject => obj;

			public override bool HasMultipleDecals => matrices.Count > 1;

			protected int GetMaxMatrices()
			{
				return DecalMaterial.IsModeSupported(Fixed8) ? 8 : 4;
			}

			public virtual bool TryAddMatrix(DecalObjectBase obj, DecalMaterial decalMaterial, int submesh, Matrix4x4 matrix)
			{
				if (DecalMaterial != decalMaterial || this.submesh != submesh || matrices.Count >= GetMaxMatrices())
					return false;
				matrices.Add(matrix);
				return true;
			}

			public virtual bool RefreshMatrices(bool force)
			{
				bool doRefresh = properties == null || material == null || force;
				if (!doRefresh) return false;
				if(properties == null)
					properties = new MaterialPropertyBlock();
				else
					properties.Clear();
				var ret = UpdateMaterial();
				if (matrices.Count == 1)
				{
					properties.SetMatrix(ProjectorSingle, matrices[0]);
				}
				else
				{
					var dummyMatrix = Matrix4x4.TRS(Vector3.one*float.NegativeInfinity, Quaternion.identity, Vector3.one);
					var shaderPropertyCount = matrices.Count > 4 ? 8 : 4;
					properties.SetMatrixArray(ProjectorMulti,
						matrices.Concat(Enumerable.Repeat(dummyMatrix, shaderPropertyCount-matrices.Count)).ToArray());
					// For Unity < 5.4 (I think)
					/*for (int i = 0; i < shaderPropertyCount; i++)
						properties.SetMatrix(ProjectorMulti + i,
							i < matrices.Count ? matrices[i] : dummyMatrix);*/
				}
				return ret;
			}

			public virtual bool UpdateMaterial()
			{
				var newMaterial = DecalMaterial.GetMaterial(matrices.Count > 4 ? Fixed8 :
					(matrices.Count > 1 ? Fixed4 : FixedSingle));
				var ret = newMaterial != material;
				material = newMaterial;
				return ret;
			}

			public FixedChannel(DecalFixedObject obj, DecalMaterial decal, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decal = decal;
				matrices.Add(matrix);
			}

			public MeshData GetMeshData()
			{
				return new MeshData
				{
					material = material,
					materialPropertyBlock = properties,
					submesh = submesh,
					matrix = Matrix4x4.identity
				};
			}

			public FixedChannel() { }
		}

		[SerializeField]
		protected List<FixedChannel> instances = new List<FixedChannel>();

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			var matrix = projector.worldToLocalMatrix * transform.localToWorldMatrix;
			var ret = instances.FirstOrDefault(obj => obj.TryAddMatrix(this, decal, submesh, matrix));
			if (ret == null)
			{
				instances.Add(ret = new FixedChannel(this, decal, matrix));
				ret.UpdateMaterial();
				ClearData();
			}
			if(ret.RefreshMatrices(true))
				ClearData();
			return ret;
		}

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if((action & (RefreshAction.MaterialPropertiesChanged | RefreshAction.ChangeInstanceMaterial)) != 0)
				foreach (var obj in instances)
					if(obj.UpdateMaterial())
						ClearData();
		}

		protected virtual MeshData[] GetMeshData()
		{
			if (instances.Count == 0 || !enabled)
				return null;
			return instances
				.Where(obj => obj.Enabled)
				.Select(obj =>
			{
				obj.RefreshMatrices(false);
				var data = obj.GetMeshData();
				data.mesh = Mesh;
				data.transform = MeshRenderer.transform;
				return data;
			}).ToArray();
		}

		protected override void GetDeferredData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			rendererData = null;
			meshData = GetMeshData();
		}

		protected override void GetForwardData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			rendererData = null;
			meshData = GetMeshData();
		}
	}
}
