using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SkinnedDecals
{
	public class DecalData : ScriptableObject
	{
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] protected string transformParent;
		[SerializeField] protected Matrix4x4 objectToProjector;
		[SerializeField] protected int vertexOffset;
		[SerializeField] protected Vector3[] uvData;
	}
}
