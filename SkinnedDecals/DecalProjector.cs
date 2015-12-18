using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkinnedDecals
{
	public abstract class DecalInstance : IDisposable
	{
		public virtual void Dispose()
		{
		}

		public abstract Color Color { get; set; }
	}

	public class DecalProjector : MonoBehaviour
	{
		protected class DecalCommand : IDisposable
		{
			private readonly Camera camera;
			protected CommandBuffer command;
			protected CameraEvent cameraEvent;
			protected Material renderMaterial;
			protected Texture2D albedo;

			public Color Color
			{
				get { return renderMaterial.GetColor("_Color"); }
				set { renderMaterial.SetColor("_Color", value); }
			}

			public DecalCommand(Camera camera, Renderer renderer, int submesh, DecalTextureSet decal, string shaderSuffix)
			{
				if(camera == null)
					throw new ArgumentNullException(nameof(camera));
				if (renderer == null)
					throw new ArgumentNullException(nameof(renderer));
				this.camera = camera;
				string shaderName;
				if (camera.actualRenderingPath == RenderingPath.DeferredShading)
				{
					cameraEvent = CameraEvent.AfterGBuffer;
					shaderName = "Decals/Deferred/" + shaderSuffix;
				}
				else
				{
					cameraEvent = CameraEvent.AfterForwardOpaque;
					albedo = renderer.sharedMaterials[submesh].mainTexture as Texture2D;
					shaderName = "Decals/Forward/" + shaderSuffix;
				}
				renderMaterial = new Material(Shader.Find(shaderName));

				Texture2D texture, normal, roughness;
				decal.GetTextures(out texture, out normal, out roughness);

				renderMaterial.SetTexture("_MainTex", texture);
				renderMaterial.SetTexture("_BodyAlbedo", albedo);
			}

			public virtual void Dispose()
			{
				if (camera != null && command != null)
					camera.RemoveCommandBuffer(cameraEvent, command);
				Destroy(renderMaterial);
			}
		}

		protected class DecalCommandSkinned : DecalCommand
		{
			private readonly ComputeBuffer buffer1;
			private readonly ComputeBuffer buffer2;

			public DecalCommandSkinned(
				Camera camera, Renderer renderer, int submesh,
				DecalTextureSet decal, Vector3[] data1, Vector3[] data2,
				int offset1, int offset2) : base(camera, renderer, submesh, decal, "Skinned")
			{
				if(data1 == null)
					data1 = new Vector3[0];
				if(data2 == null)
					data2 = new Vector3[0];

				buffer1 = new ComputeBuffer(data1.Length, sizeof(float) * 3);
				buffer2 = new ComputeBuffer(data2.Length, sizeof(float) * 3);
				buffer1.SetData(data1);
				buffer2.SetData(data2);
				renderMaterial.SetBuffer("_DecalUV1", buffer1);
				renderMaterial.SetBuffer("_DecalUV2", buffer2);
				renderMaterial.SetInt("_VertexOffset1", offset1);
				renderMaterial.SetInt("_VertexOffset2", offset2);

				command = new CommandBuffer { name = "SkinnedDecal" };
				command.DrawRenderer(renderer, renderMaterial, submesh);
				camera.AddCommandBuffer(cameraEvent, command);
			}

			public override void Dispose()
			{
				base.Dispose();
				buffer1?.Dispose();
				buffer2?.Dispose();
			}
		}

		protected class DecalCommandStatic : DecalCommand
		{
			public DecalCommandStatic(
				Camera camera, Renderer renderer, int submesh, DecalTextureSet decal,
				Matrix4x4 projectorMatrix) : base(camera, renderer, submesh, decal, "Static")
			{
				renderMaterial.SetMatrix("_Object2Projector", projectorMatrix);

				command = new CommandBuffer { name = "StaticDecal" };
				command.DrawRenderer(renderer, renderMaterial, submesh);
				camera.AddCommandBuffer(cameraEvent, command);
			}
		}

		class DefaultDecalInstance : DecalInstance
		{
			private readonly DecalCommand[] commands;

			public override Color Color
			{
				get { return commands[0].Color; }
				set
				{
					foreach (var c in commands)
						if (c != null)
							c.Color = value;
				}
			}

			public DefaultDecalInstance(params DecalCommand[] commands)
			{
				if(commands == null)
					throw new ArgumentNullException(nameof(commands));
				if(commands.Length == 0)
					throw new ArgumentNullException(nameof(commands), "Array is empty");
				this.commands = commands;
			}

			public override void Dispose()
			{
				foreach(var c in commands)
					c?.Dispose();
			}
		}

		[SerializeField] protected Texture2D decalTex;
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] new protected Camera camera;
		[SerializeField] protected Transform testPoint;
		private Renderer current;

		private readonly List<DecalInstance> instances = new List<DecalInstance>(); 

		static Mesh GetTempMesh()
		{
			return new Mesh();
		}

		private bool isOpenGl;

		protected virtual void Awake()
		{
			isOpenGl = SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL", StringComparison.Ordinal);
		}

		Vector3 GetUv(Vector3 w)
		{
			var u = transform.InverseTransformPoint(w);
			return new Vector3(u.x + 0.5f, isOpenGl ? 0.5f - u.y : 0.5f + u.y, 1);
		}

		protected DecalCommand ProjectSubmesh(Camera camera, Renderer renderer, Mesh mesh,
			DecalTextureSet decal, Vector3[] verts, Vector3[] uvData, Vector4[] planes, int submesh)
		{
			Profiler.BeginSample("Calculate UVs");
			for (int i = 0; i < uvData.Length; i++)
				// 10 is an arbitrary value such that x>1 or x<-1
				// -1 is an arbitrary negative value
				uvData[i] = new Vector3(10, 10, -1);

			var tris = mesh.GetTriangles(submesh);

			for (int i = 0; i < tris.Length; i += 3)
			{
				// Vertex IDs for this triangle
				var t1 = tris[i];
				var t2 = tris[i + 1];
				var t3 = tris[i + 2];

				// World vertices
				var w1 = verts[t1];
				var w2 = verts[t2];
				var w3 = verts[t3];

				// Check that this triangle intersects the projection box
				//     by checking that for each of the box's 8 planes, at least
				//     one vertex is on the side facing the centre of the box
				var check = true;
				// Keep this as a loop for speed
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var plane in planes)
				{
					if (plane.x * w1.x + plane.y * w1.y + plane.z * w1.z + plane.w > 0 ||
						plane.x * w2.x + plane.y * w2.y + plane.z * w2.z + plane.w > 0 ||
						plane.x * w3.x + plane.y * w3.y + plane.z * w3.z + plane.w > 0)
						continue;
					check = false;
					break;
				}
				if (!check)
					continue;

				// Compute the UV data at each vertex
				uvData[t1] = GetUv(w1);
				uvData[t2] = GetUv(w2);
				uvData[t3] = GetUv(w3);
			}
			Profiler.EndSample();

			Profiler.BeginSample("Optimize buffers");
			// Cut off space at beginning and end
			var minUv = 0;
			try
			{
				while (uvData[minUv].z <= 0)
					minUv++;
			}
			catch (IndexOutOfRangeException)
			{
				Debug.LogWarning("DecalProjector: No triangles intersect projection box", this);
				return null;
			}
			var maxUv = uvData.Length;
			while (uvData[maxUv - 1].z <= 0)
				maxUv--;

			// Find longest string of empty space in the remainder
			int maxSpaceStart = -1, maxSpaceLength = -1;
			int spaceStart = -1, spaceLength = -1;
			for (int i = minUv; i < maxUv; i++)
			{
				if (uvData[i].z < 0)
				{
					spaceStart = spaceStart < 0 ? i : spaceStart;
					spaceLength++;
				}
				else
				{
					if (spaceLength > maxSpaceLength)
					{
						maxSpaceStart = spaceStart;
						maxSpaceLength = spaceLength;
					}
					spaceStart = -1;
					spaceLength = -1;
				}
			}
			if (maxSpaceStart < 0)
			{
				maxSpaceStart = maxUv;
				maxSpaceLength = 0;
			}
			Profiler.EndSample();

			Profiler.BeginSample("Create command");
			// Create the two buffer arrays
			var data1 = new Vector3[maxSpaceStart - minUv];
			for (int i = 0; i < data1.Length; i++)
				data1[i] = uvData[i + minUv];
			var data2Offset = (maxSpaceStart + maxSpaceLength);
			var data2 = new Vector3[maxUv - data2Offset];
			for (int i = 0; i < data2.Length; i++)
				data2[i] = uvData[i + data2Offset];

			var ret = new DecalCommandSkinned(camera,
				renderer, submesh, decal, data1, data2, minUv, data2Offset);
			Profiler.EndSample();
			return ret;
		}

		static Vector4 GetPlane(Vector3 va, Vector3 vb, Vector3 vc)
		{
			var ab = vb - va;
			var ac = vc - va;
			var cross = Vector3.Cross(ab, ac);
			var d = -(cross.x * va.x + cross.y * va.y + cross.z * va.z);
			return new Vector4(cross.x, cross.y, cross.z, d);
		}

		protected void ProjectSkinned(SkinnedMeshRenderer renderer)
		{
			Profiler.BeginSample("Initialization");

			var mesh = GetTempMesh();
			renderer.BakeMesh(mesh);

			var e = new[]
			{
				new Vector3(+1, +1, +1),
				new Vector3(+1, +1, -1),
				new Vector3(+1, -1, +1),
				new Vector3(-1, +1, +1),
				new Vector3(+1, -1, -1),
				new Vector3(-1, -1, +1),
				new Vector3(-1, +1, -1),
				new Vector3(-1, -1, -1),
			};
			for (int i = 0; i < e.Length; i++)
				e[i] = transform.TransformPoint(e[i] / 2);
			var planes = new[]
			{
				GetPlane(e[1], e[6], e[4]),
				GetPlane(e[1], e[4], e[0]),
				GetPlane(e[1], e[0], e[6]),
				GetPlane(e[5], e[3], e[2]),
				GetPlane(e[5], e[7], e[3]),
				GetPlane(e[5], e[2], e[7]),
			};

			Profiler.EndSample();
			Profiler.BeginSample("Getting data");

			var verts = mesh.vertices;

			for (int i = 0; i < verts.Length; i++)
				verts[i] = current.transform.TransformPoint(verts[i]);

			var uvData = new Vector3[verts.Length];

			Profiler.EndSample();

			var commands = new DecalCommand[mesh.subMeshCount];
			for (int i = 0; i < commands.Length; i++)
			{
				Profiler.BeginSample("Submesh " + i);
				commands[i] = ProjectSubmesh(camera, current, mesh, decal, verts, uvData, planes, i);
				Profiler.EndSample();
			}

			instances.Add(new DefaultDecalInstance(commands));
		}

		protected void ProjectSkinnedEmulated(SkinnedMeshRenderer renderer)
		{
			var mesh = renderer.sharedMesh;

			var minDistance = float.PositiveInfinity;
			Transform shortestBone = null;
			foreach (var bone in renderer.bones)
			{
				var dist = Vector3.Distance(bone.position, transform.position);
				if (dist < minDistance)
				{
					minDistance = dist;
					shortestBone = bone;
				}
			}
			if (shortestBone == null)
				shortestBone = renderer.rootBone ?? renderer.transform;
			var matrix = transform.worldToLocalMatrix*shortestBone.transform.localToWorldMatrix;

			var commands = new DecalCommand[mesh.subMeshCount];
			for (int i = 0; i < commands.Length; i++)
			{
				commands[i] = new DecalCommandStatic(camera, renderer, i, decal, matrix);
			}
		}

		protected void ProjectStatic(MeshRenderer renderer)
		{
			var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
			if (mesh == null)
			{
				Debug.LogWarning($"DecalProjector: Renderer {renderer} has no mesh", this);
				return;
			}
			var commands = new DecalCommand[mesh.subMeshCount];
			var matrix = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
			for (int i = 0; i < commands.Length; i++)
			{
				commands[i] = new DecalCommandStatic(camera, renderer, i, decal, matrix);
			}
			instances.Add(new DefaultDecalInstance(commands));
		}

		public void Project()
		{
			decal = ScriptableObject.CreateInstance<DecalTextureSet>();
			decal.albedo = decalTex;

			var thisBounds = new Bounds(transform.position, transform.lossyScale);
			var renderers = FindObjectsOfType<Renderer>().Where(r => r.bounds.Intersects(thisBounds));
			current = renderers.FirstOrDefault();

			var meshRenderer = current as SkinnedMeshRenderer;
			if (meshRenderer != null)
			{
				ProjectSkinned(meshRenderer);
			}
			else if(current is MeshRenderer)
			{
				ProjectStatic((MeshRenderer)current);
			}
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = new Color(0, 1, 1, 0.3f);
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
		}

		protected virtual void OnDestroy()
		{
			foreach(var o in instances)
				o?.Dispose();
		}

		// TEST
		protected virtual void Update()
		{
			if (Input.GetKeyDown(KeyCode.A))
				Project();
		}
	}
}
