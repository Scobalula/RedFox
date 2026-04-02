![Banner](assets/banner.png)

RedFox is a collection of utilities and libraries for C# / .NET that I use across my projects. It covers compression, I/O, 3D scene graphs and skeletal animation, 2D image processing with pure C# codecs, cryptography, game asset extraction, and more.

RedFox is ultimately a personal project that I also use as a testing ground for learning and experimenting. You may find code with conflicting design choices, highly verbose exposed internals, and a few wheels reinvented along the way — that's by design, not by accident. Where I'm testing the waters with patterns, features, or just seeing how things work under the hood.

---

## Namespace Overview

### RedFox

Core utilities including high-performance byte-pattern scanning with SIMD (SSE2/AVX2) acceleration and sliding-window chunked I/O for searching binary data with wildcard support.

### RedFox.Compression

Compression abstractions and codec implementations:

- **Deflate** — ZLIB/Deflate via native `miniz`.
- **LZ4** — Extremely fast compression via native `liblz4`.
- **ZStandard** — ZStandard compression via native `libzstd`.

All codecs share a common `CompressionCodec` base with `Compress`/`Decompress` over `Span<byte>`.

### RedFox.Cryptography

Hashing implementations:

- **MurMur3** — Pure C# MurMurHash3 with 32-bit and 128-bit variants.

### RedFox.Graphics2D

A full 2D image pipeline modelled after DirectXTex's `ScratchImage`, with an `Image` container holding all mip levels, array slices, and depth slices in a contiguous buffer. Includes:

- **Primitives** — Pixel format enumerations (~100+ DXGI-based formats) and format metadata.
- **Codecs** — Per-pixel encode/decode to/from `Vector4` with SIMD-accelerated batch operations.
- **BC** — Pure C# block compression (BC1–BC7, BC6H HDR) with quality and fast encoding paths.
- **Vulkan.BC** — GPU-accelerated BC encode/decode via Vulkan compute shaders (SPIR-V).
- **Format Translators** — Readers and writers for BMP, PNG (pure C#), JPEG, OpenEXR, TGA, TIFF, DDS, KTX, and IWI.

### RedFox.Graphics3D

A 3D scene graph, skeletal animation system, and model interchange layer:

- **Core** — `Scene` with hierarchical `SceneNode` tree, `Skeleton`, `Mesh`, `Model`, `Material`, `Camera`, `Light`, packed vector types, and a full animation system with multi-layer blending, blend shapes, and IK/constraint solvers.
- **IO** — `SceneTranslator` base class with format auto-detection and translation context.
- **Format Translators** — glTF 2.0 (.gltf/.glb), Kaydara FBX (.fbx), Wavefront OBJ (.obj), Biovision Hierarchy (.bvh), Maya ASCII (.ma), MD5Mesh/MD5Anim, Valve SMD, SEAnim, and SEModel.
- **OpenGL** — Scene renderer built on Silk.NET with mesh upload, shader management, camera controls, and animation playback.
- **Preview** — Standalone model preview window for quick visual inspection.

### RedFox.IO

I/O utilities including `SpanReader` (ref struct for typed reads from byte spans), `BinaryReader`/`BinaryWriter`/`Stream` extensions, `TextTokenReader`, and a `VirtualFileSystem` with in-memory directory trees.

- **ProcessMemory** — Cross-platform (Windows + Linux) process memory reading and writing with `ProcessReader`/`ProcessWriter`, process discovery, and module resolution.

### RedFox.GameExtraction

A game asset extraction framework with a plugin architecture (`IAssetSourceReader` / `IAssetHandler`) coordinated through an `AssetManager`, backed by the virtual file system from `RedFox.IO`.

- **UI** — Avalonia-based desktop application with asset browsing, hex viewer, text editor, and preview capabilities.

### RedFox.Zenith

Software licensing with hardware-bound verification, including GumRoad and TPM 2.0-based license verifiers.

---

## AI Disclosure

I use AI as a tool to assist with writing code throughout this library — sometimes in small doses, sometimes in large batches. It's part of how I learn, experiment, and iterate. I believe AI is a powerful tool when used intentionally, and I have a personal interest in improvements to local LLMs that don't depend on massive data centers.

If AI-generated or AI-assisted code doesn't sit well with you, this library probably isn't for you — and that's perfectly fine. No hard feelings.

---

## Contributing

Issues and pull requests are very welcome! Whether it's a bug report, a feature idea, a new format you'd like to see supported, or a fix for something I broke — I'd love to hear from you. This is a project I build for myself and share in the hope it's useful, so community input is always appreciated.

---

## Credits & Third-Party Libraries

RedFox itself is MIT licensed, however it uses and bundles third-party libraries that carry their own licenses. **It is your responsibility to verify the license terms of any third-party library before use.**

| Library | Purpose | License |
|---|---|---|
| [Silk.NET](https://github.com/dotnet/Silk.NET) | Vulkan, OpenGL, OpenGLES, Windowing, Input bindings | MIT |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Cross-platform UI framework | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM pattern support | MIT |
| [Microsoft.TSS](https://github.com/microsoft/TSS.MSR) | TPM 2.0 TSS for hardware license binding | MIT |
| [xUnit](https://github.com/xunit/xunit) | Unit testing | Apache 2.0 |
| [miniz](https://github.com/richgel999/miniz) | ZLIB/Deflate compression (native) | MIT |
| [LZ4](https://github.com/lz4/lz4) | Extremely fast compression (native, bundled via submodule) | BSD 2-Clause |
| [libzstd](https://github.com/facebook/zstd) | ZStandard compression (native) | BSD 3-Clause |
| [Cast.NET](https://github.com/Scobalula/Cast.NET) | Cast container format reader/writer (bundled via submodule) | MIT |

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

*Third-party libraries included in or referenced by this project are subject to their respective licenses. Please review them individually.*
