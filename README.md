![Banner](assets/banner.png)

RedFox is my collection of utilities, libraries, and classes I share across all my projects, it covers compression, 3D graphics (scenes, animations, asset reading/writing, etc.), 2D graphics (converters, pure C# readers/writers), IO, and more. It ultimately replaces my old PhilLibX and Borks libraries to merge them into 1 unified set of projects.

RedFox is, at it's core, a personal project that is also used for experimenting and leanring. You'll likely find a lot of code in here with weird design choices, mixed quality, and wheels re-invented. A lot of the classes are publicly exposed due to its intended use case.

## Namespace Overview

### RedFox

Core utilities covering math, etc. or misc. code that does not fit into any particular project at the moment.

### RedFox.Compression

Compression abstractions and implementations (including wrappers around populat codecs) such as LZ4, ZStandard, and MiniZ. All codecs share a unified class `CompressionCodec`.

### RedFox.Cryptography

Encryption and Cryptographic operations such as hashing, curently only covers a pure C# implementation of MurMur3.

### RedFox.Graphics2D

A full 2D image pipeline inspired by Microsoft's DirectXTex library, providing methods for reading/writing images via translators, converting between formats (including a GPU backed BC decoder/encoder), and more to come.

### RedFox.Graphics3D

A full 3D scene graph with contains for meshes, animations, skeletons, and more. Contains translators for various formats and utilities for working with the scene.

It also includes an OpenGL renderer that can be used in Avalonia and supports meshes and skeletal animation playback (heavy WIP).

### RedFox.IO

I/O helpers and extensions including virtual file system classes, SpanReader for high-performant reading from spans, along with inter-process reader/writers, text parsers, and more.

### RedFox.GameExtraction

A game extraction framework along with an Avalonia based UI for parsing game packages/processes in a shared method (heavy WIP).

### RedFox.Zenith

Software licensing with hardware-bound verification, including GumRoad and TPM 2.0-based license verifiers.

## AI Disclosure

I use AI a lot to assist with building apps, tools, and code. I know a lot of people have mixed feelings on AI but I think it's an insanely powerful tool when used correctly and definitely has aided me with building projects quicker around my already weeks with my actual job and life. Personally I'm very interested in AI models that don't rely on large data centers and I'm always keeping an eye on the local LLM space for productivity.

If AI-generated or AI-assisted code doesn't sit well with you, then I don't recommend using my tools or code.

## Contributing

I'm always open to PRs and or issues if you have them to submit! Unfortunately due to limited time, bug reports and issues are prioritised to Patreon/Supporters of my apps and critical issues that affect a wider user-base. Even if you can't afford to financially support my projects, please still feel free to open an issue with suggestions, reports, etc. and I'll do my best to look after it!

## Credits & Third-Party Libraries

RedFox itself is MIT licensed, however it uses and bundles third-party libraries that carry their own licenses. **It is your responsibility to verify the license terms of any third-party library before use.**

Please keep in mind some of these libraries aren't directly used in the project, but were ported, used as reference, or served as inspiration for underlying code.

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
| [DirectXTex](https://github.com/Microsoft/DirectXTex) | Served as inspiration for Graphics2D | MIT |
| [DirectXMath](https://github.com/Microsoft/DirectXMath) | Packed vector utilities ported into Graphics3D | MIT |
| [DirectXMesh](https://github.com/Microsoft/DirectXMesh) | Mesh tools/optimizers ported into Graphics3D | MIT |

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

*Third-party libraries included in or referenced by this project are subject to their respective licenses. Please review them individually.*
