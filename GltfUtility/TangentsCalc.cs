using MikkTSpaceSharp;
using System;
using System.Numerics;
using static MikkTSpaceSharp.MikkTSpace;

namespace GltfUtility
{

	public static class TangentsCalc
	{
		public static SVec2 ToSVec2(this Vector2 v) => new SVec2(v.X, v.Y);
		public static SVec3 ToSVec3(this Vector3 v) => new SVec3(v.X, v.Y, v.Z);

		public static Vector4[] Calculate(Vector3[] positions, Vector3[] normals, Vector2[] uvs, uint[] indices)
		{
			if (positions == null)
			{
				throw new ArgumentNullException(nameof(positions));
			}

			if (normals == null)
			{
				throw new ArgumentNullException(nameof(normals));
			}

			if (uvs == null)
			{
				throw new ArgumentNullException(nameof(uvs));
			}

			if (positions.Length != normals.Length)
			{
				throw new ArgumentException($"Inconsistent sizes: positions.Length = {positions.Length}, normals.Length = {normals.Length}");
			}

			if (positions.Length != uvs.Length)
			{
				throw new ArgumentException($"Inconsistent sizes: positions.Length = {positions.Length}, uvs.Length = {uvs.Length}");
			}

			var result = new Vector4[positions.Length];
			Func<int, int, uint> indexCalc = (face, vertex) => indices[face * 3 + vertex];
			var ctx = new SMikkTSpaceContext
			{
				m_getNumFaces = () => indices.Length / 3,
				m_getNumVerticesOfFace = face => 3,
				m_getPosition = (face, vertex) => positions[indexCalc(face, vertex)].ToSVec3(),
				m_getNormal = (face, vertex) => normals[indexCalc(face, vertex)].ToSVec3(),
				m_getTexCoord = (face, vertex) => uvs[indexCalc(face, vertex)].ToSVec2(),
				m_setTSpaceBasic = (SVec3 tangent, float orient, int face, int vertex) =>
				{
					var idx = indexCalc(face, vertex);
					result[idx] = new Vector4(tangent.x, tangent.y, tangent.z, orient);
				}
			};

			var r = genTangSpaceDefault(ctx);
			if (!r)
			{
				throw new Exception("Tangents generation failed");
			}

			return result;
		}
	}
}
