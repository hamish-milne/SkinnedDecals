using System;
using UnityEngine;

namespace DecalSystem
{
	public class DecalSingle : DecalObject
	{
		[SerializeField] protected DecalMaterial decalMaterial;

		public override bool ScreenSpace => true;
		public override Renderer Renderer => null;
		public override Mesh Mesh => DecalScreenSpaceObject.CubeMesh;
		public override Bounds? Bounds => null;
		public override Material[] Materials => null;
		public override string[] RequiredModes { get; } = {ShaderKeywords.ScreenSpace};
		public override bool UseManualCulling => false;

		public virtual DecalMaterial DecalMaterial
		{
			get { return decalMaterial; }
			set { decalMaterial = value; } // TODO: Notify data changed?
		}

		private readonly MeshData[] meshData = new MeshData[1];
		private readonly DecalInstanceSingle instance;

		public DecalSingle()
		{
			instance = new DecalInstanceSingle(this);
		}

		protected class DecalInstanceSingle : DecalInstance
		{
			private readonly DecalSingle obj;

			public override DecalObject DecalObject => obj;

			public override DecalMaterial DecalMaterial
			{
				get { return obj.decalMaterial; }
				set { obj.decalMaterial = value; }
			}

			public override bool Enabled
			{
				get { return obj.enabled; }
				set { obj.enabled = value; }
			}

			public DecalInstanceSingle(DecalSingle obj)
			{
				this.obj = obj;
			}
		}

		public override MeshData[] GetRenderPathData(RenderingPath path)
		{
			if (DecalMaterial == null) return null;
			if(Application.isEditor)
			meshData[0] = new MeshData
			{
				instance = instance,
				material = DecalMaterial.GetMaterial(ShaderKeywords.ScreenSpace),
				transform = transform,
				mesh = Mesh
			};
			return meshData;
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			throw new NotSupportedException();
		}

		protected virtual void OnDrawGizmosSelected()
		{
			DecalProjector.DrawGizmo(transform);
		}
	}
}
