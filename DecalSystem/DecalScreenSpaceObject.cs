﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	public class DecalScreenSpaceObject : DecalObjectBase
	{
		private static readonly string[] modes = {ShaderKeywords.ScreenSpace};

		public override string[] RequiredModes => modes;

		[Serializable]
		protected class Instance : DecalInstance
		{
			[SerializeField, HideInInspector] protected DecalScreenSpaceObject obj;
			public Matrix4x4 matrix;

			public override DecalObject DecalObject => obj;

			public Instance(DecalScreenSpaceObject obj, DecalMaterial decal, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decalMaterial = decal;
				this.matrix = matrix;
			}
		}

		[SerializeField]
		protected List<Instance> instances = new List<Instance>();

		[SerializeField]
		protected Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

		public override bool ScreenSpace => true;

		public override Bounds Bounds => bounds;

		public override Renderer Renderer => null;

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			// TODO: Make relative to object
			var ret = new Instance(this, decal, projector.localToWorldMatrix);
			instances.Add(ret);
			ClearData();
			return ret;
		}

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if ((action & (RefreshAction.ChangeInstanceMaterial
				| RefreshAction.MaterialPropertiesChanged)) != 0)
				ClearData();
		}

		private static Mesh cubeMesh;

		public static Mesh CubeMesh
		{
			get
			{
				if (cubeMesh != null) return cubeMesh;
				cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
				if(cubeMesh == null)
					throw new Exception("Cube mesh not found!");
				return cubeMesh;
			}
		}

		public override Mesh Mesh => CubeMesh;

		protected override bool RequireDepthTexture => true;

		private void Cleanup()
		{
			instances.RemoveAll(obj => obj.DecalMaterial == null);
		}

		protected virtual MeshData[] GetMeshData()
		{
			Cleanup();
			return instances
				.Where(obj => obj.Enabled)
				.Select(obj => new MeshData
			{
				instance = obj,
				material = obj.DecalMaterial.GetMaterial(ShaderKeywords.ScreenSpace),
				matrix = obj.matrix,
				mesh = Mesh
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
