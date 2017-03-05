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
			set { decalMaterial = value; NotifyDataChanged(); }
		}

		private readonly IDecalDraw[] drawArray = new IDecalDraw[1];
		private readonly DecalInstanceSingle single;

		public DecalSingle()
		{
			single = new DecalInstanceSingle(this);
			drawArray[0] = single;
		}

		public override int Count => 1;
		public override DecalInstance GetDecal(int index)
		{
			if(index < 0 || index >= 1)
				throw new ArgumentNullException(nameof(index));
			return single;
		}

		public override bool RemoveDecal(DecalInstance instance)
		{
			throw new NotSupportedException();
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			throw new NotSupportedException();
		}

		protected virtual void OnDrawGizmosSelected()
		{
			DecalProjector.DrawGizmo(transform);
		}

		protected class DecalInstanceSingle : DecalInstanceBase
		{
			private readonly DecalSingle obj;

			public override DecalObject DecalObject => obj;

			public override Matrix4x4? LocalMatrix => null;

			public override string ModeString => ShaderKeywords.ScreenSpace;

			public override DecalMaterial DecalMaterial
			{
				get { return obj.decalMaterial; }
				set { obj.decalMaterial = value; }
			}

			public override void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh,
				ref Renderer renderer, ref int submesh, ref Material material,
				ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix)
			{
				base.GetDrawCommand(renderPath, ref mesh, ref renderer, ref submesh, ref material, ref propertyBlock, ref matrix);
				mesh = DecalScreenSpaceObject.CubeMesh;
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

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if ((action & RefreshAction.EnableDisable) != 0)
				NotifyDataChanged();
		}

		public override IDecalDraw[] GetDecalDraws()
		{
			return enabled ? drawArray : null;
		}
	}
}
