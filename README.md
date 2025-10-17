[![NuGet](https://img.shields.io/nuget/v/nrs-gltf.svg)](https://www.nuget.org/packages/nrs-gltf/)
[![Build & Publish Beta](https://github.com/Nursia3D/GltfUtility/actions/workflows/build-and-publish-beta.yml/badge.svg)](https://github.com/Nursia3D/GltfUtility/actions/workflows/build-and-publish-beta.yml)
[![Chat](https://img.shields.io/discord/628186029488340992.svg)](https://discord.gg/ZeHxhCY)

### GltfUtility
Command line utility to manipulate Gltf/Glb files. 

It's only purpose is providing an easy way of adding mikktspace tangents to GLTF/GLB.

As the only way of doing that - I've found - is through Blender. But it requires too much effort. Especially when you have to deal with many models.

### Installation
`dotnet tool install --global nrs-gltf`

### Update
`dotnet tool update --global nrs-gltf`

### Usage
`nrs-gltf <inputFile> [outputFile] [-t] [-u]`

Both inputFile and outputFile should have either gltf or glb extension.

Flag `-t` generates mikktspace tangents.

Flag `-u` unwinds indices.

Example usage: `nrs-gltf FlightHelmet.gltf FlightHelmet.glb -t`

### Credits
* [MikkTSpaceSharp](https://github.com/rds1983/MikkTSpaceSharp)
* [glTFLoader](https://github.com/KhronosGroup/glTF-CSharp-Loader/tree/main/glTFLoader)

