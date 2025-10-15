using glTFLoader;
using glTFLoader.Schema;
using GltfUtility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using static glTFLoader.Schema.Accessor;
using Buffer = glTFLoader.Schema.Buffer;

namespace DigitalRise
{
	internal class GltfProcessor
	{
		private Options _options;
		private Gltf _gltf;
		private readonly Dictionary<int, byte[]> _bufferCache = new Dictionary<int, byte[]>();

		private static void Log(string message) => Console.WriteLine(message);
		private byte[] FileResolver(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				// glb
				using (var stream = File.OpenRead(_options.InputFile))
				{
					return Interface.LoadBinaryBuffer(stream);
				}
			}

			var folder = Path.GetDirectoryName(_options.InputFile);
			using (var stream = File.OpenRead(Path.Combine(folder, path)))
			{
				return stream.ToBytes();
			}
		}

		private byte[] GetBuffer(int index)
		{
			byte[] result;
			if (_bufferCache.TryGetValue(index, out result))
			{
				return result;
			}

			result = _gltf.LoadBinaryBuffer(index, path => FileResolver(path));
			_bufferCache[index] = result;

			return result;
		}

		private ArraySegment<byte> GetAccessorData(int accessorIndex)
		{
			var accessor = _gltf.Accessors[accessorIndex];
			if (accessor.BufferView == null)
			{
				throw new NotSupportedException("Accessors without buffer index arent supported");
			}

			var bufferView = _gltf.BufferViews[accessor.BufferView.Value];
			var buffer = GetBuffer(bufferView.Buffer);

			var size = accessor.Type.GetComponentCount() * accessor.ComponentType.GetComponentSize();
			return new ArraySegment<byte>(buffer, bufferView.ByteOffset + accessor.ByteOffset, accessor.Count * size);
		}

		private T[] GetAccessorAs<T>(int accessorIndex)
		{
			var bytes = GetAccessorData(accessorIndex);

			var count = bytes.Count / Marshal.SizeOf(typeof(T));
			var result = new T[count];

			GCHandle handle = GCHandle.Alloc(result, GCHandleType.Pinned);
			try
			{
				IntPtr pointer = handle.AddrOfPinnedObject();
				Marshal.Copy(bytes.Array, bytes.Offset, pointer, bytes.Count);
			}
			finally
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}

			return result;
		}

		private uint[] GetIndices(MeshPrimitive primitive)
		{
			var indexAccessor = _gltf.Accessors[primitive.Indices.Value];
			if (indexAccessor.Type != TypeEnum.SCALAR)
			{
				throw new NotSupportedException("Only scalar index buffer are supported");
			}

			if (indexAccessor.ComponentType != ComponentTypeEnum.SHORT &&
				indexAccessor.ComponentType != ComponentTypeEnum.UNSIGNED_SHORT &&
				indexAccessor.ComponentType != ComponentTypeEnum.UNSIGNED_INT)
			{
				throw new NotSupportedException($"Index of type {indexAccessor.ComponentType} isn't supported");
			}

			var indices = new List<uint>();
			if (indexAccessor.ComponentType == ComponentTypeEnum.SHORT)
			{
				var data = GetAccessorAs<short>(primitive.Indices.Value);
				for (var i = 0; i < data.Length; ++i)
				{
					indices.Add((uint)data[i]);
				}
			}
			else if (indexAccessor.ComponentType == ComponentTypeEnum.UNSIGNED_SHORT)
			{
				var data = GetAccessorAs<ushort>(primitive.Indices.Value);
				for (var i = 0; i < data.Length; ++i)
				{
					indices.Add(data[i]);
				}
			}
			else
			{
				var data = GetAccessorAs<uint>(primitive.Indices.Value);
				for (var i = 0; i < data.Length; ++i)
				{
					indices.Add((uint)data[i]);
				}
			}

			return indices.ToArray();
		}

		private void GenerateTangentFrames()
		{
			var accessorsToUpdate = new HashSet<int>();
			var dataToAdd = new List<Tuple<MeshPrimitive, Vector4[]>>();

			foreach (var gltfMesh in _gltf.Meshes)
			{
				var meshName = gltfMesh.Name ?? "(unnamed)";

				for (var primitiveIndex = 0; primitiveIndex < gltfMesh.Primitives.Length; primitiveIndex++)
				{
					var primitive = gltfMesh.Primitives[primitiveIndex];
					var hasPositions = primitive.HasAttribute("POSITION");
					var hasNormals = primitive.HasAttribute("NORMAL");
					var hasTexCoords = primitive.HasAttribute("TEXCOORD_");

					if (!hasPositions)
					{
						Log($"Warning: could not generate tangents for mesh {meshName} primitive {primitiveIndex} since it lacks positions channel");
						continue;
					}

					if (!hasNormals)
					{
						Log($"Warning: could not generate tangents for mesh {meshName} primitive {primitiveIndex} since it lacks normals channel");
						continue;
					}

					if (!hasTexCoords)
					{
						Log($"Warning: could not generate tangents for mesh {meshName} primitive {primitiveIndex} since it lacks uvs channel");
						continue;
					}

					var positions = GetAccessorAs<Vector3>(primitive.FindAttribute("POSITION"));
					var uvs = GetAccessorAs<Vector2>(primitive.FindAttribute("TEXCOORD_"));
					var normals = GetAccessorAs<Vector3>(primitive.FindAttribute("NORMAL"));
					var indices = GetIndices(primitive);

					var tangents = TangentsCalc.Calculate(positions, normals, uvs, indices);
					dataToAdd.Add(new Tuple<MeshPrimitive, Vector4[]>(primitive, tangents));

					if (primitive.HasAttribute("TANGENT"))
					{
						Log($"Warning: Mesh {meshName} primitive {primitiveIndex} has tangents channel already. It will be overriden");
						accessorsToUpdate.Add(primitive.Attributes["TANGENT"]);
					}
				}
			}

			// Now reconstruct the buffer
			using (var ms = new MemoryStream())
			{
				// We must preserve the accessors' order
				var newBuffersViews = new List<BufferView>();
				var newAccessors = new List<Accessor>();
				for (var i = 0; i < _gltf.Accessors.Length; ++i)
				{
					var accessor = _gltf.Accessors[i];
					if (!accessorsToUpdate.Contains(i))
					{
						var start = (int)ms.Position;

						// Write data
						var data = GetAccessorData(i);
						ms.Write(data.Array, data.Offset, data.Count);

						// Create new buffer view
						var bufferView = new BufferView
						{
							ByteOffset = start,
							ByteLength = data.Count
						};
						newBuffersViews.Add(bufferView);

						// Update accessor
						// Now each accessor has its own buffer view, so all byte offsets should be zeroed
						accessor.ByteOffset = 0;
						accessor.BufferView = newBuffersViews.Count - 1;
					}

					newAccessors.Add(accessor);
				}

				// Write new data
				foreach (var d in dataToAdd)
				{
					var start = (int)ms.Position;
					ms.WriteData(d.Item2);

					// Create new buffer view
					var bufferView = new BufferView
					{
						ByteOffset = start,
						ByteLength = (int)ms.Position - start
					};
					newBuffersViews.Add(bufferView);

					Accessor accessor;
					var primitive = d.Item1;
					if (primitive.HasAttribute("TANGENT"))
					{
						// Update existing accessor
						accessor = newAccessors[primitive.Attributes["TANGENT"]];
					}
					else
					{
						// Create new one
						accessor = new Accessor();
						newAccessors.Add(accessor);
						primitive.Attributes["TANGENT"] = newAccessors.Count - 1;
					}

					accessor.ByteOffset = 0;
					accessor.ComponentType = ComponentTypeEnum.FLOAT;
					accessor.Type = TypeEnum.VEC4;
					accessor.Count = d.Item2.Length;
					accessor.BufferView = newBuffersViews.Count - 1;
				}

				_gltf.BufferViews = newBuffersViews.ToArray();
				_gltf.Accessors = newAccessors.ToArray();

				_bufferCache[0] = ms.ToArray();
				_gltf.Buffers[0].ByteLength = _bufferCache[0].Length;

				// Make sure there's only one buffer
				_gltf.Buffers = new Buffer[] { _gltf.Buffers[0] };
			}
		}

		private void UnwindIndices()
		{
			foreach (var gltfMesh in _gltf.Meshes)
			{
				foreach (var primitive in gltfMesh.Primitives)
				{
					if (primitive.Indices == null)
					{
						throw new NotSupportedException("Meshes without indices arent supported");
					}

					var indexAccessor = _gltf.Accessors[primitive.Indices.Value];
					if (indexAccessor.Type != Accessor.TypeEnum.SCALAR)
					{
						throw new NotSupportedException("Only scalar index buffer are supported");
					}

					if (indexAccessor.ComponentType != Accessor.ComponentTypeEnum.SHORT &&
						indexAccessor.ComponentType != Accessor.ComponentTypeEnum.UNSIGNED_SHORT &&
						indexAccessor.ComponentType != Accessor.ComponentTypeEnum.UNSIGNED_INT)
					{
						throw new NotSupportedException($"Index of type {indexAccessor.ComponentType} isn't supported");
					}

					// Flip winding
					var indexData = GetAccessorData(primitive.Indices.Value);
					if (indexAccessor.ComponentType == ComponentTypeEnum.UNSIGNED_SHORT)
					{
						var data = new ushort[indexData.Count / 2];
						System.Buffer.BlockCopy(indexData.Array, indexData.Offset, data, 0, indexData.Count);

						for (var i = 0; i < data.Length / 3; i++)
						{
							var temp = data[i * 3];
							data[i * 3] = data[i * 3 + 2];
							data[i * 3 + 2] = temp;
						}

						System.Buffer.BlockCopy(data, 0, indexData.Array, indexData.Offset, indexData.Count);
					}
					else if (indexAccessor.ComponentType == ComponentTypeEnum.SHORT)
					{
						var data = new short[indexData.Count / 2];
						System.Buffer.BlockCopy(indexData.Array, indexData.Offset, data, 0, indexData.Count);

						for (var i = 0; i < data.Length / 3; i++)
						{
							var temp = data[i * 3];
							data[i * 3] = data[i * 3 + 2];
							data[i * 3 + 2] = temp;
						}

						System.Buffer.BlockCopy(data, 0, indexData.Array, indexData.Offset, indexData.Count);
					}
					else
					{
						var data = new uint[indexData.Count / 4];
						System.Buffer.BlockCopy(indexData.Array, indexData.Offset, data, 0, indexData.Count);

						for (var i = 0; i < data.Length / 3; i++)
						{
							var temp = data[i * 3];
							data[i * 3] = data[i * 3 + 2];
							data[i * 3 + 2] = temp;
						}

						System.Buffer.BlockCopy(data, 0, indexData.Array, indexData.Offset, indexData.Count);
					}
				}
			}
		}

		public Gltf Process(Options options)
		{
			_bufferCache.Clear();
			_options = options;

			if (string.IsNullOrEmpty(_options.OutputFile))
			{
				_options.OutputFile = _options.InputFile;
			}

			Log($"Loading model {options.InputFile}...");
			using (var stream = File.OpenRead(_options.InputFile))
			{
				_gltf = Interface.LoadModel(stream);
			}

			if (_options.Tangent)
			{
				Log("Generating tangents...");
				GenerateTangentFrames();
			}

			if (_options.Unwind)
			{
				Log("Unwinding indices...");
				UnwindIndices();
			}

			var outputExt = Path.GetExtension(options.OutputFile).ToLower();
			if (outputExt == ".glb")
			{
				// Load all buffers and erase their uris(required for SaveBinaryModel to work)
				for (var i = 0; i < _gltf.Buffers.Length; ++i)
				{
					GetBuffer(i);
					_gltf.Buffers[i].Uri = null;
				}

				Interface.SaveBinaryModel(_gltf, GetBuffer(0), _options.OutputFile);
			}
			else
			{
				var outputFolder = Path.GetDirectoryName(options.OutputFile);
				var outputName = Path.GetFileNameWithoutExtension(options.OutputFile);
				var nameChanged = Path.GetFileNameWithoutExtension(options.InputFile) != outputName;

				for (var i = 0; i < _gltf.Buffers.Length; ++i)
				{
					var b = _gltf.Buffers[i];

					if (nameChanged)
					{
						// Change name of the binary
						b.Uri = $"{outputName}.bin";
					}

					var buffer = GetBuffer(i);

					var fullUri = Path.Combine(outputFolder, b.Uri);
					File.WriteAllBytes(fullUri, buffer);
					Log($"Wrote {fullUri}");
				}

				Interface.SaveModel(_gltf, _options.OutputFile);
			}

			Log($"Wrote {_options.OutputFile}");

			return _gltf;
		}
	}
}